using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MclaBrFsrSetup {
 internal sealed class SaveArchiveInfo {
  internal readonly int Entries;
  internal readonly long Bytes;
  internal SaveArchiveInfo(int entries,long bytes) {Entries=entries;Bytes=bytes;}
 }

 // Filesystem operations are separate from the WPF dialog so they can be
 // exercised against disposable test directories before touching real saves.
 internal static class SaveBackup {
  const int MaxEntries=20000;
  const long MaxBytes=10L*1024*1024*1024;

  internal const int AutomaticCopies=10;

  static bool AutomaticName(string name) {
   return System.Text.RegularExpressions.Regex.IsMatch(name,@"^MCLA-auto-\d{8}-\d{6}-\d{7}-[0-9a-f]{32}\.zip$");
  }

  internal static string LatestRecoverable(string folder,Action<string> log) {
   if(!Directory.Exists(folder))return null;
   foreach(var file in new DirectoryInfo(folder).GetFiles("MCLA-auto-*.zip")
           .Where(f=>AutomaticName(f.Name)).OrderByDescending(f=>f.Name,StringComparer.Ordinal)) {
    try {
     using(var archive=ZipFile.OpenRead(file.FullName)) {
      Inspect(archive);byte[] buffer=new byte[65536];
      foreach(var entry in archive.Entries) {
       long bytes=0;
       using(var stream=entry.Open()) {int n;while((n=stream.Read(buffer,0,buffer.Length))>0) {
        bytes+=n;if(bytes>entry.Length)throw new InvalidDataException("Tamanho inválido.");
       }}
       if(bytes!=entry.Length)throw new InvalidDataException("Backup incompleto.");
      }
     }
     return file.FullName;
    } catch(InvalidDataException e) {if(log!=null)log("Backup ignorado: "+file.Name+": "+e.Message);}
      catch(IOException e) {if(log!=null)log("Backup indisponível: "+file.Name+": "+e.Message);}
   }
   return null;
  }

  internal static string CreateAutomatic(string source,string folder,Func<bool> gameRunning) {
   if(gameRunning())throw new IOException("Feche o jogo antes do backup automático.");
   if(!Directory.Exists(source))return null;
   string root=Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   string destination=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
   if((destination+Path.DirectorySeparatorChar).StartsWith(root,StringComparison.OrdinalIgnoreCase))
    throw new IOException("A pasta de backups deve ficar fora da pasta dos dados.");
   // Do not follow junctions into unrelated player data or into the backups.
   var pending=new Stack<string>();pending.Push(source);var files=new List<string>();long total=0;
   while(pending.Count>0) {
    string current=pending.Pop();
    if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
     throw new IOException("Os dados contêm um link ou junção: "+current);
    foreach(string entry in Directory.GetFileSystemEntries(current)) {
     if(String.Equals(Path.GetFullPath(entry),Path.Combine(root,"cache"),StringComparison.OrdinalIgnoreCase))continue;
     var attributes=File.GetAttributes(entry);
     if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Os dados contêm um link ou junção: "+entry);
     if((attributes&FileAttributes.Directory)!=0)pending.Push(entry);
     else {
      total+=new FileInfo(entry).Length;files.Add(entry);
      if(total>MaxBytes||files.Count>MaxEntries)throw new IOException("Os dados excedem o limite do backup automático.");
     }
    }
   }
   if(files.Count==0)return null;
   Directory.CreateDirectory(destination);
   string path=Path.Combine(destination,"MCLA-auto-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+Guid.NewGuid().ToString("N")+".zip");
   string temporary=path+".partial";
   try {
    using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Create))
     foreach(string file in files)archive.CreateEntryFromFile(file,Path.GetFullPath(file).Substring(root.Length).Replace('\\','/'),CompressionLevel.Optimal);
    using(var archive=ZipFile.OpenRead(temporary))Inspect(archive);
    if(gameRunning())throw new IOException("O jogo foi iniciado durante o backup. Tente novamente com ele fechado.");
    File.Move(temporary,path);
    // Only this feature's completed archives are eligible for retention.
    foreach(var old in new DirectoryInfo(destination).GetFiles("MCLA-auto-*.zip")
             .Where(f=>AutomaticName(f.Name))
             .OrderByDescending(f=>f.Name,StringComparer.Ordinal).Skip(AutomaticCopies))old.Delete();
    return path;
   } finally {if(File.Exists(temporary))File.Delete(temporary);}
  }

  internal static SaveArchiveInfo Inspect(ZipArchive archive) {
   if(archive.Entries.Count==0||archive.Entries.Count>MaxEntries)
    throw new IOException("O ZIP está vazio ou tem arquivos demais.");
   long total=0;
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var directories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var entry in archive.Entries) {
    string name=entry.FullName.Replace('\\','/');
    string clean=name.TrimEnd('/');
    string[] parts=clean.Split('/');
    if(String.IsNullOrEmpty(clean)||name.StartsWith("/")||name.Contains(":")||
       parts.Any(s=>s.Length==0||s=="."||s==".."||s.EndsWith(".")||s.EndsWith(" "))||
       !names.Add(clean))
     throw new IOException("O ZIP contém um caminho inválido ou duplicado: "+name);
    if(entry.FullName.EndsWith("/")||entry.FullName.EndsWith("\\")) {
     if(entry.Length!=0)throw new IOException("Diretório com dados inválidos: "+name);
     directories.Add(clean);
    } else files.Add(clean);
    if(entry.Length>MaxBytes-total)
     throw new IOException("O ZIP excede o limite de 10 GB de dados descompactados.");
    total+=entry.Length;
   }
   foreach(string name in names) {
    string ancestor=name;
    while(ancestor.LastIndexOf('/')>=0) {
     ancestor=ancestor.Substring(0,ancestor.LastIndexOf('/'));
     if(files.Contains(ancestor))
      throw new IOException("O ZIP contém arquivos que conflitam com pastas: "+ancestor);
    }
   }
   if(files.Overlaps(directories))
    throw new IOException("O ZIP contém nomes de arquivos e pastas conflitantes.");
   return new SaveArchiveInfo(archive.Entries.Count,total);
  }

  // The destination and staging directory share a parent, so the final swap
  // is a same-volume rename. Existing data is moved to a sibling directory
  // first and is never deleted by this function.
  internal static string Restore(ZipArchive archive,string destination,
                                 Func<bool> gameRunning,Action<string> log) {
   SaveArchiveInfo info=Inspect(archive);
   string target=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   string parent=Path.GetDirectoryName(target);
   if(String.IsNullOrEmpty(parent))throw new IOException("Pasta de dados inválida.");
   if(Directory.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)
    throw new IOException("A pasta de dados é um link ou junção. Configure uma pasta normal para restaurar.");
   string stage=target+".import-"+Guid.NewGuid().ToString("N");
   string safety=target+".antes-da-restauracao-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff");
   bool moved=false;
   try {
    Directory.CreateDirectory(parent);
    Directory.CreateDirectory(stage);
    string root=Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    long extracted=0;
    byte[] buffer=new byte[65536];
    foreach(var entry in archive.Entries) {
     string relative=entry.FullName.Replace('/','\\').TrimEnd('\\');
     string path=Path.GetFullPath(Path.Combine(stage,relative));
     if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))
      throw new IOException("Caminho fora da pasta de importação.");
     if(entry.FullName.EndsWith("/")||entry.FullName.EndsWith("\\")) {
      Directory.CreateDirectory(path);
      continue;
     }
     Directory.CreateDirectory(Path.GetDirectoryName(path));
     long bytes=0;
     using(var source=entry.Open())
     using(var output=File.Create(path)) {
      int n;
      while((n=source.Read(buffer,0,buffer.Length))>0) {
       bytes+=n;
       extracted+=n;
       if(bytes>entry.Length||extracted>info.Bytes)
        throw new IOException("O tamanho extraído do ZIP excede o tamanho declarado.");
       output.Write(buffer,0,n);
      }
     }
     if(bytes!=entry.Length)
      throw new IOException("O arquivo ZIP terminou antes do tamanho declarado: "+entry.FullName);
    }
    if(gameRunning())throw new IOException("O jogo foi iniciado durante a extração. Feche-o e tente novamente.");
    if(Directory.Exists(safety)||File.Exists(safety))
     throw new IOException("A pasta de segurança já existe. Tente novamente.");
    if(Directory.Exists(target)) {Directory.Move(target,safety);moved=true;}
    try {Directory.Move(stage,target);}
    catch(Exception swapError) {
     if(moved&&!Directory.Exists(target)) {
      try {Directory.Move(safety,target);}
      catch(Exception rollbackError) {
       throw new IOException("A troca falhou e o retorno também falhou. Os dados originais estão em "+safety,
                             new AggregateException(swapError,rollbackError));
      }
     }
     throw;
    }
    return moved?safety:null;
   } finally {
    if(Directory.Exists(stage)) {
     try {Directory.Delete(stage,true);}
     catch(Exception e) {if(log!=null)log("Limpeza da importação temporária: "+e.Message);}
    }
   }
  }
 }
}
