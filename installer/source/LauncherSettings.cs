using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace MclaBrFsrSetup {
 internal static class LauncherSettings {
  internal static string Label(string label,string key) {
   switch(key) {
    case "mcla_render_resolution":case "resolution_scale":case "mcla_gpu_backend":case "frame_generation":case "mcla_intro":
    case "mcla_race_shadows":case "mcla_fast_car_shadows":case "mcla_foliage_impostors":case "mcla_fullscreen_blur":
    case "mcla_radio_in_game":
     return label+" (requer reiniciar)";
    default:return label;
   }
  }
  internal static bool Native1080(Dictionary<string,string> values) {
   string mode,scale,quality,aa;
   return (values.TryGetValue("mcla_render_resolution",out mode)&&mode=="1080p") ||
    ((!values.ContainsKey("mcla_render_resolution")||mode=="scaled") &&
     values.TryGetValue("resolution_scale",out scale)&&scale=="2"&&
     values.TryGetValue("temporal_aa_quality",out quality)&&quality=="1080p"&&
     values.TryGetValue("mcla_anti_aliasing",out aa)&&aa=="fsr");
  }
  internal static void NormalizeRenderResolution(Dictionary<string,string> values) {
   bool native1080=values["resolution_scale"]=="1080p";
   values["mcla_render_resolution"]=native1080?"1080p":"scaled";
   if(native1080) {
    values["resolution_scale"]="1";
    values["mcla_anti_aliasing"]="fsr";
    values["temporal_aa_quality"]="native";
   } else if(values["temporal_aa_quality"]=="1080p")values["temporal_aa_quality"]="native";
  }
  internal static bool Equal(string left,string right) {
   double a,b;
   return left==right||(Double.TryParse(left,NumberStyles.Float,CultureInfo.InvariantCulture,out a)&&
    Double.TryParse(right,NumberStyles.Float,CultureInfo.InvariantCulture,out b)&&a==b);
  }
  internal static Dictionary<string,string> Read(string text) {
   var result=new Dictionary<string,string>();var json=new JavaScriptSerializer();
   foreach(string line in text.Split('\n')) {
    if(line.TrimStart().StartsWith("["))break;
    var m=Regex.Match(line,@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$");
    if(!m.Success)continue;
    string value=m.Groups[2].Value;
    try {
     if(value.StartsWith("\"")) {
      var quoted=Regex.Match(value,"^\"(?:[^\"\\\\]|\\\\.)*\"");
      if(!quoted.Success)continue;value=json.Deserialize<string>(quoted.Value);
     } else if(value.StartsWith("'")) {
      int end=value.IndexOf('\'',1);if(end<0)continue;value=value.Substring(1,end-1);
     } else value=value.Split('#')[0].Trim();
     result[m.Groups[1].Value]=value;
    }catch(ArgumentException) {}
   }
   return result;
  }
  // Replace only root keys; a table with the same key belongs to someone else.
  internal static string Merge(string original,Dictionary<string,string> changes,Func<string,bool> stringKey) {
   var text=new StringBuilder();bool root=true;var json=new JavaScriptSerializer();
   Action append=delegate {
    foreach(var pair in changes)text.AppendLine(pair.Key+" = "+(stringKey(pair.Key)?json.Serialize(pair.Value):pair.Value));
   };
   using(var reader=new StringReader(original)) {
    string line;
    while((line=reader.ReadLine())!=null) {
     if(root&&line.TrimStart().StartsWith("[")){append();root=false;}
     var m=Regex.Match(line,@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=");
     if(root&&m.Success&&changes.ContainsKey(m.Groups[1].Value))continue;
     text.AppendLine(line);
    }
   }
   if(root)append();return text.ToString();
  }
 }
}
