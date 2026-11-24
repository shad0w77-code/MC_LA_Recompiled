using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using MclaBrFsrSetup;

static class LauncherStorageTests {
 static int count;
 static void Check(bool condition,string name) {if(!condition)throw new Exception(name);count++;Console.WriteLine("OK: "+name);}
 static void Reject(Action action,string name) {try{action();}catch(IOException){Check(true,name);return;}throw new Exception("Não rejeitou: "+name);}
 static MemoryStream Zip(params string[] names) {
  var stream=new MemoryStream();
  using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
   foreach(string name in names)using(var writer=new StreamWriter(zip.CreateEntry(name).Open()))writer.Write("restaurado");
  stream.Position=0;return stream;
 }
 [STAThread]
 static void Main() {
  string root=Path.Combine(Path.GetTempPath(),"mcla-storage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try {
   string data=Path.Combine(root,"userdata");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"career"),"original");
   using(var stream=Zip("profile/career"))using(var zip=new ZipArchive(stream)) {
    string safety=SaveBackup.Restore(zip,data,()=>false,null);
    Check(File.ReadAllText(Path.Combine(data,"profile/career"))=="restaurado","restaura conteúdo e estrutura");
    Check(File.ReadAllText(Path.Combine(safety,"career"))=="original","preserva dados anteriores");
   }
   foreach(string[] names in new[]{new[]{"../escape"},new[]{"/absolute"},new[]{"C:/escape"},new[]{"same","SAME"},new[]{"file","file/child"},new[]{"dir./bad"}}) {
    using(var stream=Zip(names))using(var zip=new ZipArchive(stream))Reject(()=>SaveBackup.Restore(zip,data,()=>false,null),"rejeita ZIP: "+String.Join(",",names));
   }
   using(var stream=Zip("replacement"))using(var zip=new ZipArchive(stream)) {
    Reject(()=>SaveBackup.Restore(zip,data,()=>true,null),"jogo aberto impede a troca");
    Check(File.Exists(Path.Combine(data,"profile/career")),"falha mantém save atual");
    Check(!Directory.GetDirectories(root,"*.import-*").Any(),"falha limpa staging");
   }
   using(var stream=Zip("new-save"))using(var zip=new ZipArchive(stream))
    Check(SaveBackup.Restore(zip,Path.Combine(root,"first"),()=>false,null)==null,"restaura sem dados anteriores");
   using(var stream=Zip())using(var zip=new ZipArchive(stream))Reject(()=>SaveBackup.Inspect(zip),"rejeita ZIP vazio");
   string backups=Path.Combine(root,"backups");Directory.CreateDirectory(backups);
   string manual=Path.Combine(backups,"MCLA-auto-manual.zip");File.WriteAllText(manual,"manual");
   Directory.CreateDirectory(Path.Combine(data,"cache"));File.WriteAllText(Path.Combine(data,"cache/shader"),"cache");
   for(int i=0;i<12;i++) {File.WriteAllText(Path.Combine(data,"generation"),i.ToString());SaveBackup.CreateAutomatic(data,backups,()=>false);}
   var automatic=Directory.GetFiles(backups,"MCLA-auto-*.zip").Where(p=>p!=manual).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
   Check(automatic.Length==10,"retenção mantém 10 backups automáticos");
   Check(File.ReadAllText(manual)=="manual","retenção preserva backup manual");
   using(var zip=ZipFile.OpenRead(automatic[0]))using(var reader=new StreamReader(zip.GetEntry("generation").Open()))Check(reader.ReadToEnd()=="2","retenção remove as cópias mais antigas");
   using(var zip=ZipFile.OpenRead(automatic[9])) {
    Check(zip.GetEntry("cache/shader")==null,"backup automático exclui cache");
    using(var reader=new StreamReader(zip.GetEntry("generation").Open()))Check(reader.ReadToEnd()=="11","último backup tem conteúdo atual");
    string restored=Path.Combine(root,"auto-restored");SaveBackup.Restore(zip,restored,()=>false,null);
    Check(File.ReadAllText(Path.Combine(restored,"generation"))=="11","backup automático pode ser restaurado");
   }
   Reject(()=>SaveBackup.CreateAutomatic(data,Path.Combine(data,"backups"),()=>false),"impede backups dentro dos dados");
   Reject(()=>SaveBackup.CreateAutomatic(data,backups,()=>true),"impede backup com jogo aberto");
   int calls=0;
   Reject(()=>SaveBackup.CreateAutomatic(data,backups,()=>++calls>1),"descarta backup se jogo abrir durante a cópia");
   Check(!Directory.GetFiles(backups,"*.partial").Any(),"falha limpa ZIP temporário");
   Check(SaveBackup.CreateAutomatic(Path.Combine(root,"absent"),backups,()=>false)==null,"primeiro uso sem saves não cria backup vazio");
   Check(SaveBackup.LatestRecoverable(backups,null)==automatic[9],"recuperação escolhe último backup legível");
   string corrupt=Path.Combine(backups,"MCLA-auto-99991231-235959-9999999-"+new String('a',32)+".zip");
   File.WriteAllText(corrupt,"ZIP interrompido");int skipped=0;
   Check(SaveBackup.LatestRecoverable(backups,s=>skipped++)==automatic[9]&&skipped==1,"recuperação pula cópia interrompida e informa no registro");
   Check(SaveBackup.LatestRecoverable(Path.Combine(root,"absent"),null)==null,"recuperação trata ausência de backups");
   string config="# jogador\nmcla_radio_mute_game = false\nresolution_scale = 2\nhdr_peak_nits = 950.000000\nreadback_resolve = 'full' # manual\nuser_data_root = \"D:/Meus # saves\" # comentário\n[custom]\nresolution_scale = 99\n";
   var read=LauncherSettings.Read(config);
   Check(read["resolution_scale"]=="2"&&read["readback_resolve"]=="full","lê valores raiz e string literal TOML");
   Check(read["user_data_root"]=="D:/Meus # saves","lê string com # e comentário final");
   Check(LauncherSettings.Equal(read["hdr_peak_nits"],"950"),"normaliza números do jogo sem perder precisão");
   var changes=new Dictionary<string,string>{{"resolution_scale","3"},{"mcla_gpu_backend","vulkan"}};
   string merged=LauncherSettings.Merge(config,changes,k=>k=="mcla_gpu_backend");
   var reread=LauncherSettings.Read(merged);
   Check(reread["resolution_scale"]=="3"&&reread["mcla_gpu_backend"]=="vulkan","configuração salva pode ser relida");
   Check(merged.Contains("[custom]\r\nresolution_scale = 99"),"preserva chaves homônimas em tabelas");
   Check(reread["hdr_peak_nits"]=="950.000000"&&reread["readback_resolve"]=="full","preserva opções não alteradas");
   Check(LauncherSettings.Label("FG","frame_generation").Contains("reiniciar"),"FG sinaliza reinício");
   Check(LauncherSettings.Label("Vinis","readback_resolve")=="Vinis","opção dinâmica não exige reinício");
   string install=Path.Combine(root,"installation");Directory.CreateDirectory(install);
   File.WriteAllText(Path.Combine(install,"mcla.exe"),"");
   File.WriteAllText(Path.Combine(install,".mcla-br-install.json"),"{\"identity\":\"mcla-la-recompiled\",\"phase\":\"ready\"}");
   string settings=Path.Combine(install,"mcla.toml");
   File.WriteAllText(settings,config.Replace("[custom]","frame_generation = \"fsr\"\nmcla_gpu_backend = \"vulkan\"\n[custom]"));
   string preferences=Path.Combine(root,"launcher.json");
   File.WriteAllText(preferences,new JavaScriptSerializer().Serialize(new Dictionary<string,string>{{"installation",install}}));
   var app=new System.Windows.Application();var window=ModernLauncher.Create(preferences);object controller=window.Tag;
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var selected=(Dictionary<string,string>)controller.GetType().GetMethod("Selected",flags).Invoke(controller,null);
   Check(selected["hdr_peak_nits"]=="950.000000"&&selected["readback_resolve"]=="full","interface preserva opções personalizadas");
   Check(selected["frame_generation"]=="fsr","carregamento Vulkan preserva preferência FG");
   // A game session changes a value after the configurator was opened.
   File.WriteAllText(settings,File.ReadAllText(settings).Replace("950.000000","1050.000000"));
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"resolution_scale","3"});
   controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
   var applied=LauncherSettings.Read(File.ReadAllText(settings));
   Check(applied["resolution_scale"]=="3","interface salva alteração escolhida");
   Check(applied["hdr_peak_nits"]=="1050.000000","interface preserva alteração posterior feita pelo jogo");
   Check(applied["frame_generation"]=="fsr"&&applied["readback_resolve"]=="full","interface não sobrescreve outras preferências");
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"resolution_scale","1080p"});
   var native1080=(Dictionary<string,string>)controller.GetType().GetMethod("Selected",flags).Invoke(controller,null);
   Check(native1080["resolution_scale"]=="1"&&native1080["mcla_render_resolution"]=="1080p"&&native1080["temporal_aa_quality"]=="native"&&native1080["mcla_anti_aliasing"]=="fsr","1080p serializes native mode at integer scale 1");
   var choices=(Dictionary<string,System.Windows.Controls.ComboBox>)controller.GetType().GetField("choices",flags).GetValue(controller);
   Check(!choices["temporal_aa_quality"].IsEnabled,"1080p constrains FSR quality to Native AA");
   controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
   var nativeSaved=LauncherSettings.Read(File.ReadAllText(settings));
   Check(nativeSaved["mcla_render_resolution"]=="1080p"&&nativeSaved["resolution_scale"]=="1","native mode round trips through TOML and launcher load");
   Check(File.ReadAllText(settings).Contains("mcla_render_resolution = \"1080p\""),"native render mode is a TOML string");
   foreach(string scale in new[]{"1","2","3"}) {
    controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"resolution_scale",scale});
    var scaled=(Dictionary<string,string>)controller.GetType().GetMethod("Selected",flags).Invoke(controller,null);
    Check(scaled["mcla_render_resolution"]=="scaled"&&scaled["resolution_scale"]==scale,"scaled resolution "+scale);
   }
   nativeSaved.Remove("mcla_render_resolution");nativeSaved["resolution_scale"]="2";nativeSaved["temporal_aa_quality"]="1080p";
   Check(LauncherSettings.Native1080(nativeSaved),"recognizes legacy fake 1080p");
   File.WriteAllText(settings,controller.GetType().GetMethod("Toml",flags).Invoke(controller,new object[]{nativeSaved}).ToString());
   controller.GetType().GetMethod("LoadSettings",flags).Invoke(controller,null);
   var legacy=(Dictionary<string,string>)controller.GetType().GetMethod("Selected",flags).Invoke(controller,null);
   Check(legacy["mcla_render_resolution"]=="1080p"&&legacy["resolution_scale"]=="1"&&legacy["temporal_aa_quality"]=="native","legacy UI selection normalizes to native1080");
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_time_hour","6.25"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_weather","fog"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_time_hold","false"});
   controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_radio_volume","35"});
   // This runtime-only preference has no launcher control; preserve its explicit config value.
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_radio_pause_unfocused","false"});
   controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
   var radio=LauncherSettings.Read(File.ReadAllText(settings));
   Check(radio["mcla_radio_volume"]=="35"&&radio["mcla_radio_mute_game"]=="false"&&radio["mcla_radio_pause_unfocused"]=="false","rádio preserva volume personalizado e preferências após gravar e reler");
   var world=LauncherSettings.Read(File.ReadAllText(settings));
   Check(world["mcla_time_hour"]=="6.25"&&world["mcla_weather"]=="fog"&&world["mcla_time_hold"]=="false","horário personalizado, clima e ciclo sobrevivem à releitura");
   using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("MclaSetup.graphics_profiles.json"))
   using(var reader=new StreamReader(resource)) {
    var profiles=new JavaScriptSerializer().Deserialize<Dictionary<string,Dictionary<string,string>>>(reader.ReadToEnd());
    foreach(string name in new[]{"Leve","Equilibrado","Native AA"}) {
     string id=name=="Leve"?"performance":name=="Native AA"?"native":"balanced";
     controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"resolution_scale","1080p"});
     controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
     controller.GetType().GetMethod("Preset",flags).Invoke(controller,new object[]{name});
     var profile=(Dictionary<string,string>)controller.GetType().GetMethod("Selected",flags).Invoke(controller,null);
     Check(profiles[id].All(p=>LauncherSettings.Equal(profile[p.Key],p.Value)),"interface aplica todos os valores do perfil "+id);
     Check(profile["mcla_graphics_profile"]==id,"interface grava identidade do perfil "+id);
     Check(profile["mcla_render_resolution"]=="scaled"&&profile["resolution_scale"]=="1","profile exits native1080: "+id);
     Check(choices["temporal_aa_quality"].IsEnabled,"profile unlocks FSR quality: "+id);
     Check(profile["frame_generation"]=="fsr","perfil preserva escolha explícita de FG "+id);
     Check(profile["mcla_weather"]=="fog"&&profile["mcla_time_hour"]=="6.25","perfil gráfico preserva horário e clima "+id);
     controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
     var persisted=LauncherSettings.Read(File.ReadAllText(settings));
     Check(persisted["mcla_render_resolution"]=="scaled","profile persists scaled mode: "+id);
     Check(profiles[id].All(p=>LauncherSettings.Equal(persisted[p.Key],p.Value)),"perfil sobrevive à gravação e releitura "+id);
    }
   }
   File.WriteAllText(settings,File.ReadAllText(settings).Replace("anisotropic_override = 4","anisotropic_override = 5").Replace("mcla_render_resolution = \"scaled\"","mcla_render_resolution = \"1080p\""));
   controller.GetType().GetMethod("Preset",flags).Invoke(controller,new object[]{"Native AA"});
   controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
   Check(LauncherSettings.Read(File.ReadAllText(settings))["anisotropic_override"]=="4","selecionar o mesmo perfil reaplica valores após edição externa");
   Check(LauncherSettings.Read(File.ReadAllText(settings))["mcla_render_resolution"]=="scaled","reapplying profile overrides externally changed native1080 mode");
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"low_latency","on"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_speed_units","kmh"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_time_realtime","true"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_vibration","false"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_left_stick_deadzone","15"});
   controller.GetType().GetMethod("Set",flags).Invoke(controller,new object[]{"mcla_radio_in_game","false"});
   controller.GetType().GetMethod("Apply",flags).Invoke(controller,null);
   var extra=LauncherSettings.Read(File.ReadAllText(settings));
   Check(extra["low_latency"]=="on"&&extra["mcla_speed_units"]=="kmh"&&extra["mcla_time_realtime"]=="true","novas opções de latência, km/h e relógio real persistem");
   Check(extra["mcla_vibration"]=="false"&&extra["mcla_left_stick_deadzone"]=="15"&&extra["mcla_radio_in_game"]=="false","novas opções de vibração, dead zone e rádio no jogo persistem");
   var banner=(System.Windows.UIElement)window.FindName("RemoteUpdateBanner");
   var sideVer=(System.Windows.Controls.TextBlock)window.FindName("SidebarVersion");
   Check(banner!=null&&banner.Visibility==System.Windows.Visibility.Collapsed,"banner de atualização remota inicia oculto");
   Check(sideVer!=null&&sideVer.Text=="v"+EmbeddedPayload.Version,"rodapé exibe versão atual do instalador");
   Check(UpdateChecker.IsNewer("v1.0.1","1.0.0")&&UpdateChecker.IsNewer("1.1","1.0.0"),"UpdateChecker reconhece versões mais novas");
   Check(!UpdateChecker.IsNewer("v1.0.0","1.0.0")&&!UpdateChecker.IsNewer("1.0.0","1.0.1"),"UpdateChecker ignora versões iguais ou antigas");
   Check(UpdateChecker.IsAllowedUrl("https://github.com/shad0w77-code/MC_LA_Recompiled/releases/latest")&&!UpdateChecker.IsAllowedUrl("http://github.com/evil")&&!UpdateChecker.IsAllowedUrl("https://evil.example/update.zip"),"UpdateChecker aceita apenas HTTPS nos domínios oficiais do GitHub");
   string releaseJson="{\"tag_name\":\"v1.0.1\",\"name\":\"v1.0.1\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/shad0w77-code/MC_LA_Recompiled/releases/tag/v1.0.1\",\"body\":\"### Melhorias\\n- Correção X\",\"assets\":[{\"name\":\"Midnight-Club-LA-Recompiled-Windows-x64.zip\",\"size\":1234,\"browser_download_url\":\"https://github.com/shad0w77-code/MC_LA_Recompiled/releases/download/v1.0.1/Midnight-Club-LA-Recompiled-Windows-x64.zip\"},{\"name\":\"SHA256SUMS.txt\",\"size\":100,\"browser_download_url\":\"https://github.com/shad0w77-code/MC_LA_Recompiled/releases/download/v1.0.1/SHA256SUMS.txt\"}]}";
   var parsed=UpdateChecker.ParseLatestReleaseJson(releaseJson,"1.0.0");
   Check(parsed!=null&&parsed.Version=="1.0.1"&&parsed.ZipDownloadUrl.EndsWith(".zip")&&parsed.ChecksumsDownloadUrl.EndsWith("SHA256SUMS.txt"),"ParseLatestReleaseJson extrai release, ZIP e SHA256SUMS");
   Check(UpdateChecker.ParseLatestReleaseJson(releaseJson.Replace("\"draft\":false","\"draft\":true"),"1.0.0")==null,"ParseLatestReleaseJson ignora draft");
   Check(UpdateChecker.ParseLatestReleaseJson(releaseJson.Replace("\"prerelease\":false","\"prerelease\":true"),"1.0.0")==null,"ParseLatestReleaseJson ignora prerelease");
   Check(UpdateChecker.ParseLatestReleaseJson(releaseJson,"1.0.1")==null,"ParseLatestReleaseJson ignora mesma versão");
   string updateDir=Path.Combine(root,"update-test");Directory.CreateDirectory(updateDir);
   string exePayloadPath=Path.Combine(updateDir,"payload.exe");File.WriteAllText(exePayloadPath,"stub-installer-v101");
   string exeHash=UpdateChecker.ComputeSha256(exePayloadPath);
   Check(UpdateChecker.ParseExpectedSha256(exeHash+"  "+UpdateChecker.ExpectedExeName+"\n",UpdateChecker.ExpectedExeName)==exeHash,"ParseExpectedSha256 lê hash corretamente");
   string goodZip=Path.Combine(updateDir,"good.zip");
   using(var z=ZipFile.Open(goodZip,ZipArchiveMode.Create)) {
    z.CreateEntryFromFile(exePayloadPath,UpdateChecker.ExpectedExeName);
    using(var sw=new StreamWriter(z.CreateEntry("SHA256SUMS.txt").Open()))sw.WriteLine(exeHash+"  "+UpdateChecker.ExpectedExeName);
   }
   string goodZipHash=UpdateChecker.ComputeSha256(goodZip);
   string extractedExe=UpdateChecker.ExtractUpdaterFromZip(goodZip,Path.Combine(updateDir,"out"),goodZipHash);
   Check(File.Exists(extractedExe)&&UpdateChecker.ComputeSha256(extractedExe)==exeHash,"ExtractUpdaterFromZip extrai e valida SHA-256 do ZIP e do executável");
   Reject(()=>UpdateChecker.ExtractUpdaterFromZip(goodZip,Path.Combine(updateDir,"badhash"),new String('0',64)),"ExtractUpdaterFromZip rejeita SHA-256 inválido do ZIP");
   string badInnerZip=Path.Combine(updateDir,"badinner.zip");
   using(var z=ZipFile.Open(badInnerZip,ZipArchiveMode.Create)) {
    z.CreateEntryFromFile(exePayloadPath,UpdateChecker.ExpectedExeName);
    using(var sw=new StreamWriter(z.CreateEntry("SHA256SUMS.txt").Open()))sw.WriteLine(new String('f',64)+"  "+UpdateChecker.ExpectedExeName);
   }
   Reject(()=>UpdateChecker.ExtractUpdaterFromZip(badInnerZip,Path.Combine(updateDir,"badinner-out"),null),"ExtractUpdaterFromZip rejeita SHA-256 inválido do executável interno");
   string evilZip=Path.Combine(updateDir,"evil.zip");
   using(var stream=Zip("../evil.exe"))using(var fs=File.Create(evilZip))stream.CopyTo(fs);
   Reject(()=>UpdateChecker.ExtractUpdaterFromZip(evilZip,Path.Combine(updateDir,"evil-out"),null),"ExtractUpdaterFromZip rejeita path traversal no ZIP");
   window.Close();app.Shutdown();
   Console.WriteLine(count+" verificações passaram.");
  }finally {Directory.Delete(root,true);}
 }
}
