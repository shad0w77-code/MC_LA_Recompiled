using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MclaBrFsrSetup {
 static class ModernLauncher {
  public static Window Create(string preferencesPath=null) {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MclaSetup.Launcher.xaml")) {
    var window=(Window)XamlReader.Load(stream);window.Tag=new LauncherController(window,preferencesPath);return window;
   }
  }
 }
 sealed class LauncherController {
  readonly Window ui;
  readonly Dictionary<string,ComboBox> choices=new Dictionary<string,ComboBox>();
  readonly Dictionary<string,CheckBox> toggles=new Dictionary<string,CheckBox>();
  TextBox radioFolder;
  readonly JavaScriptSerializer json=new JavaScriptSerializer();
  readonly string preferences=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MCLA_LA_Recompiled","launcher.json");
  bool HasBundle {get{return Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("MclaSetup.offline.bundle.zip");}}
  string installed;
  Dictionary<string,string> firstRun;
  Dictionary<string,string> loadedSelection;
  ReleaseUpdateInfo remoteUpdate;
  bool working;
  bool loading;
  bool setupExpanded;
  string graphicsProfile="custom";
  bool profileRequested;
  Process worker;
  T Find<T>(string name) where T:class {return ui.FindName(name) as T;}
  string Folder {get{return Path.GetFullPath(Find<TextBox>("InstallPath").Text.Trim());}}
  string GameExe {get{return installed==null ? null : Path.Combine(installed,"mcla.exe");}}
  static bool Newer(string a,string b) {return UpdateChecker.IsNewer(a,b);}
  string InstalledRelease() {try{var m=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(installed,".mcla-br-install.json")));return m.ContainsKey("release")?Convert.ToString(m["release"]):"1.0.0";}catch{return "1.0.0";}}
  bool UpdateAvailable {get{return HasBundle&&installed!=null&&Newer(EmbeddedPayload.Version,InstalledRelease());}}

  public LauncherController(Window window,string preferencesPath=null) {
   if(preferencesPath!=null)preferences=preferencesPath;
   ui=window;
   ui.Icon=ImageResource("setup.ico");
   Find<Image>("TitleIcon").Source=ImageResource("setup.ico");
   Find<Image>("SideLogo").Source=ImageResource("logo.png");
   Find<Image>("HeroArt").Source=ImageResource("banner.png");
   Find<Image>("CreditsCover").Source=ImageResource("cover.png");
   Find<TextBlock>("TitleVersion").Text="v"+EmbeddedPayload.Version;
   Find<TextBlock>("SidebarVersion").Text="v"+EmbeddedPayload.Version;
   Find<TextBlock>("CreditsVersion").Text="v"+EmbeddedPayload.Version;

   Click("MinimizeButton",delegate{ui.WindowState=WindowState.Minimized;});
   Click("MaximizeButton",delegate{ui.WindowState=ui.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;});
   Click("CloseButton",delegate{ui.Close();});
   ui.StateChanged+=delegate{
    Find<Border>("RootFrame").Margin=ui.WindowState==WindowState.Maximized?new Thickness(7):new Thickness(0);
    Find<Button>("MaximizeButton").Content=ui.WindowState==WindowState.Maximized?"❐":"□";
   };

   string[] nav={"Home","Graphics","Controls","Radio","Saves","Logs","Credits"};
   foreach(string page in nav) {string id=page;Find<RadioButton>("Nav"+id).Checked+=delegate{Navigate(id);};}

   BuildGraphics();
   BuildControlsOptions();
   BuildRadioWorldOptions();
   Find<ComboBox>("ButtonPrompts").SelectedIndex=0;
   firstRun=Selected();

   Find<TextBox>("InstallPath").Text=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MCLA_LA_Recompiled");
   try {if(File.Exists(preferences)) {var saved=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(preferences));if(saved.ContainsKey("installation") && Owned(saved["installation"])) Find<TextBox>("InstallPath").Text=saved["installation"];}} catch(Exception e) {Log("Preferências: "+e.Message);}
   if(Owned(AppDomain.CurrentDomain.BaseDirectory)) Find<TextBox>("InstallPath").Text=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
   if(!HasBundle) {
    Find<FrameworkElement>("IsoLabel").Visibility=Find<FrameworkElement>("IsoRow").Visibility=Visibility.Collapsed;
    Find<Button>("PrepareButton").Visibility=Find<CheckBox>("DesktopShortcut").Visibility=Find<FrameworkElement>("TranslateCard").Visibility=Visibility.Collapsed;
    Find<TextBlock>("HomeInfo").Text="Use as abas para ajustar os gráficos, os controles e fazer backup dos seus dados. O atalho da Área de Trabalho abre o jogo diretamente.";
   }

   Find<TextBox>("InstallPath").LostKeyboardFocus+=delegate{Refresh(true);};
   Click("BrowseIso",delegate {var picker=new OpenFileDialog {Title="Sua ISO de Midnight Club: Los Angeles Complete Edition",Filter="Imagem de disco (*.iso)|*.iso",CheckFileExists=true};if(picker.ShowDialog(ui)==true) Find<TextBox>("IsoPath").Text=picker.FileName;});
   Click("BrowseInstall",delegate{ChooseFolder(!HasBundle);});
   Click("LocateButton",delegate{ChooseFolder(true);});
   Click("ReinstallToggle",delegate{setupExpanded=!setupExpanded;UpdateSetupVisibility();});
   Click("PlayButton",Play);
   Click("OpenGameFolder",delegate{OpenDirectory(installed??Folder);});
   Find<Button>("PrepareButton").Click+=async delegate {await Prepare();};
   Find<Button>("PrepareShadersButton").Click+=async delegate {await PrepareShaders();};
   Click("PresetLight",delegate{Preset("Leve");});
   Click("PresetBalanced",delegate{Preset("Equilibrado");});
   Click("PresetNative",delegate{Preset("Native AA");});
   Click("ApplyGraphics",Apply);
   Click("ApplyProfile",Apply);
   Click("ApplyRadioWorld",Apply);
   Click("RecoverAutoBackup",delegate{RestoreBackup(true);});
   Click("OpenSaves",delegate{OpenDirectory(SaveDirectory());});
   Click("OpenAutoBackups",delegate{if(installed==null)throw new IOException("Localize uma instalação primeiro.");string path=Path.Combine(installed,"save-backups");Directory.CreateDirectory(path);OpenDirectory(path);});
   Click("BackupSaves",Backup);
   Click("RestoreSaves",RestoreBackup);
   Click("OpenScreenshots",delegate{if(installed==null)throw new IOException("Localize uma instalação antes de abrir as capturas.");string folder=Path.Combine(installed,"screenshots");Directory.CreateDirectory(folder);OpenDirectory(folder);});
   Click("BrowseMods",ChooseModsFolder);
   Click("PlayModsButton",PlayWithMods);
   Click("CopyLogs",delegate{Clipboard.SetText(Find<TextBox>("LogText").Text);Status("Registro copiado.");});
   Click("OpenLogs",delegate{if(installed!=null) OpenDirectory(Path.Combine(installed,"logs"));});
   Click("OpenLicenses",delegate{if(installed!=null) OpenDirectory(Path.Combine(installed,"licenses"));else Status("Os créditos e as licenças acompanham a instalação do jogo.");});
   Click("GithubLinkButton",delegate{OpenUrl(UpdateChecker.RepoPageUrl);});
   Click("CreditsGithubButton",delegate{OpenUrl(UpdateChecker.RepoPageUrl);});
   Click("OpenReleaseButton",delegate{OpenUrl(remoteUpdate!=null&&!String.IsNullOrEmpty(remoteUpdate.HtmlUrl)?remoteUpdate.HtmlUrl:UpdateChecker.ReleasesPageUrl);});
   Find<Button>("DownloadUpdateButton").Click+=async delegate {await DownloadAndLaunchUpdate();};
   Find<Button>("CheckUpdatesButton").Click+=async delegate {await CheckRemoteUpdateAsync(true);};

   ui.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {if(working){e.Cancel=true;Status("Uma operação está em andamento. Aguarde o término para fechar.");}};
   Refresh(true);
   Log("Configurador v"+EmbeddedPayload.Version+" pronto. Use sua própria ISO ou localize uma instalação.");
   if(preferencesPath==null) CheckRemoteUpdateAsync(false);
  }

  BitmapSource ImageResource(string name) {
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("MclaSetup."+name)) {
    if(s==null) return null;
    var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=s;b.EndInit();b.Freeze();return b;
   }
  }
  void OpenUrl(string url) {if(!UpdateChecker.IsAllowedUrl(url))return;Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
  void Click(string id,Action action) {var b=Find<Button>(id);if(b!=null)b.Click+=delegate{try{action();}catch(Exception e){Error(e);}};}
  void Navigate(string id) {
   foreach(string p in new[]{"Home","Graphics","Controls","Radio","Saves","Logs","Credits"}) {
    var el=Find<FrameworkElement>("Page"+p);
    if(el!=null)el.Visibility=p==id?Visibility.Visible:Visibility.Collapsed;
   }
   AnimatePageEnter(id);
   if(id=="Saves") RefreshSaves();
  }
  void AnimatePageEnter(string id) {
   var page=Find<FrameworkElement>("Page"+id);
   var transform=Find<TranslateTransform>("Page"+id+"Transform");
   if(page==null||transform==null)return;
   var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
   var fade=new DoubleAnimation(0.0,1.0,TimeSpan.FromMilliseconds(180)) {EasingFunction=ease};
   var slide=new DoubleAnimation(10.0,0.0,TimeSpan.FromMilliseconds(180)) {EasingFunction=ease};
   page.BeginAnimation(UIElement.OpacityProperty,fade);
   transform.BeginAnimation(TranslateTransform.YProperty,slide);
  }
  void AnimateBannerEnter() {
   var banner=Find<FrameworkElement>("RemoteUpdateBanner");
   var transform=Find<TranslateTransform>("BannerTransform");
   if(banner==null||transform==null)return;
   var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
   banner.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0.0,1.0,TimeSpan.FromMilliseconds(260)){EasingFunction=ease});
   transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-12.0,0.0,TimeSpan.FromMilliseconds(260)){EasingFunction=ease});
  }
  void Go(string id) {Find<RadioButton>("Nav"+id).IsChecked=true;Navigate(id);}
  void Status(string text) {Find<TextBlock>("StatusText").Text=text;}
  void Log(string text) {
   if(String.IsNullOrEmpty(text))return;
   if(!ui.Dispatcher.CheckAccess()){ui.Dispatcher.BeginInvoke(new Action<string>(Log),text);return;}
   Find<TextBox>("LogText").AppendText(text+Environment.NewLine);
   Find<TextBox>("LogText").ScrollToEnd();
   if(text.StartsWith("[ETAPA")) Status(text);
  }
  void Error(Exception error) {Log("ERRO: "+error.Message);Status(error.Message);MessageBox.Show(ui,error.Message,"Midnight Club L.A. Recompiled",MessageBoxButton.OK,MessageBoxImage.Information);}
  bool Owned(string folder) {try {var marker=Path.Combine(folder,".mcla-br-install.json");if(!File.Exists(marker)||!File.Exists(Path.Combine(folder,"mcla.exe")))return false;var m=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(marker));return Convert.ToString(m["identity"])=="mcla-la-recompiled" && Convert.ToString(m["phase"])=="ready";}catch{return false;}}
  void ChooseFolder(bool mustExist) {using(var picker=new System.Windows.Forms.FolderBrowserDialog {ShowNewFolderButton=!mustExist,Description=mustExist?"Escolha a pasta que contém mcla.exe e a instalação preparada":"Escolha uma subpasta para o jogo"}) {if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;if(mustExist&&!Owned(picker.SelectedPath))throw new IOException("Essa pasta não é uma instalação pronta deste configurador. Selecione a pasta que contém mcla.exe.");Find<TextBox>("InstallPath").Text=picker.SelectedPath;Refresh(true);}}
  void ChooseModsFolder() {using(var picker=new System.Windows.Forms.FolderBrowserDialog {ShowNewFolderButton=false,Description="Selecione a pasta game modificada"}) {if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;Find<TextBox>("ModsPath").Text=picker.SelectedPath;}}

  void UpdateSetupVisibility() {
   bool update=UpdateAvailable;
   bool ready=installed!=null&&!update;
   Find<FrameworkElement>("ReadySummaryGrid").Visibility=ready?Visibility.Visible:Visibility.Collapsed;
   Find<FrameworkElement>("ReinstallToggle").Visibility=(ready&&HasBundle)?Visibility.Visible:Visibility.Collapsed;
   Find<FrameworkElement>("SetupPanel").Visibility=(!ready||setupExpanded||!HasBundle)?Visibility.Visible:Visibility.Collapsed;
   Find<TextBlock>("ReinstallChevron").Text=setupExpanded?"▲ Recolher":"▼ Mostrar";
  }

  void Refresh(bool read) {
   string folder;try{folder=Folder;}catch{folder="";}
   string previous=installed;installed=Owned(folder)?folder:null;
   if(installed==null && previous!=null) {
    loading=true;
    foreach(var pair in firstRun)Set(pair.Key,pair.Value);
    Set("window_size",firstRun["window_width"]+","+firstRun["window_height"]);
    Find<TextBox>("PlayerName").Text="User";
    Find<ComboBox>("ButtonPrompts").SelectedIndex=0;
    radioFolder.Text=firstRun["mcla_radio_folder"];
    loading=false;
    Find<TextBlock>("PresetLabel").Text="Perfil: Padrão do jogo";
   }
   Find<TextBlock>("InstallStatus").Text=installed==null?"Prepare seu jogo":"Tudo pronto";
   Find<TextBlock>("HomeActionHint").Text=installed==null?"Sua ISO será necessária apenas na preparação.":"Configurações e saves desta instalação reconhecidos.";
   ShowMode();
   Find<Button>("PlayButton").IsEnabled=installed!=null&&!working;
   Find<Button>("PrepareShadersButton").IsEnabled=installed!=null&&!working;
   Find<Button>("ApplyGraphics").Content=installed==null?"USAR ESTAS OPÇÕES NA PREPARAÇÃO":"SALVAR CONFIGURAÇÕES";
   Find<Button>("ApplyProfile").Content=installed==null?"USAR ESTE PERFIL NA PREPARAÇÃO":"SALVAR PERFIL E CONTROLES";
   Find<Button>("ApplyRadioWorld").Content=installed==null?"USAR NA PREPARAÇÃO":"SALVAR RÁDIO E MUNDO";
   if(installed!=null) {
    if(read)LoadSettings();
    SavePreferences();
    UpdateReadySummary();
    Status("Instalação reconhecida. Você já pode jogar.");
   } else {
    Find<TextBlock>("TitleStatusText").Text="Sem instalação";
    Find<Border>("TitleStatusBadge").Background=new SolidColorBrush(Color.FromRgb(26,31,43));
    Find<Border>("TitleStatusBadge").BorderBrush=new SolidColorBrush(Color.FromRgb(54,65,86));
    Find<TextBlock>("TitleStatusText").Foreground=new SolidColorBrush(Color.FromRgb(148,163,184));
   }
   RefreshHeroChips();
   UpdateSetupVisibility();
   RefreshSaves();
  }

  void UpdateReadySummary() {
   if(installed==null)return;
   Find<TextBlock>("SummaryPath").Text=installed;
   bool ptbr=File.Exists(Path.Combine(installed,"game",".mcla-ptbr.json"));
   Find<TextBlock>("SummaryPathDetail").Text=ptbr?"Tradução PT-BR ativa (fontes Õ/õ aplicadas)":"Idioma original em inglês";
   bool hasPack=File.Exists(Path.Combine(installed,"shaderpack","545407F8.shaderpack"));
   Find<TextBlock>("SummaryShaderStatus").Text=hasPack?"545407F8.shaderpack pronto":"Será gerado no início";
   Find<TextBlock>("TitleStatusText").Text=UpdateAvailable?"Atualização local pronta":"Instalação pronta";
   Find<Border>("TitleStatusBadge").Background=new SolidColorBrush(UpdateAvailable?Color.FromRgb(42,29,11):Color.FromRgb(22,41,33));
   Find<Border>("TitleStatusBadge").BorderBrush=new SolidColorBrush(UpdateAvailable?Color.FromRgb(156,101,20):Color.FromRgb(43,107,77));
   Find<TextBlock>("TitleStatusText").Foreground=new SolidColorBrush(UpdateAvailable?Color.FromRgb(251,191,36):Color.FromRgb(110,231,183));
  }

  void RefreshHeroChips() {
   var wrap=Find<WrapPanel>("HeroChips");
   if(wrap==null)return;
   wrap.Children.Clear();
   if(installed==null)return;
   string api=Value("mcla_gpu_backend");
   string apiLabel=api=="d3d12"?"DirectX 12":api=="vulkan"?"Vulkan":"API Automática";
   string res=Value("resolution_scale");
   string resLabel=res=="1080p"?"1080p Nativo":res=="2"?"1440p (2×)":res=="3"?"2160p (3×)":"720p (1×)";
   string aa=Value("mcla_anti_aliasing");
   string aaLabel=aa=="fsr"?"FSR ("+Value("temporal_aa_quality")+")":aa=="msaa"?"MSAA 2×":"Sem AA";
   string units=Value("mcla_speed_units")=="kmh"?"km/h":"mph";
   bool ptbr=File.Exists(Path.Combine(installed,"game",".mcla-ptbr.json"));
   var items=new List<string> {apiLabel,resLabel,aaLabel,ptbr?"PT-BR · "+units:"EN · "+units};
   if(toggles.ContainsKey("frame_generation")&&toggles["frame_generation"].IsChecked==true)items.Add("FG 4.0.2");
   if(choices.ContainsKey("low_latency")&&Value("low_latency")=="on")items.Add("Baixa latência");
   foreach(string text in items) {
    var b=new Border {Background=new SolidColorBrush(Color.FromArgb(150,10,12,16)),BorderBrush=new SolidColorBrush(Color.FromArgb(65,255,255,255)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(8,3,8,3),Margin=new Thickness(0,0,6,4)};
    b.Child=new TextBlock {Text=text,FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(226,232,240)),FontWeight=FontWeights.SemiBold};
    wrap.Children.Add(b);
   }
  }

  void ShowMode() {
   if(!HasBundle)return;
   bool update=UpdateAvailable;
   Find<TextBlock>("InstallStatus").Text=update?"Atualização disponível":installed==null?"Prepare seu jogo":"Tudo pronto";
   Find<TextBlock>("HomeActionHint").Text=update?"v"+InstalledRelease()+" → v"+EmbeddedPayload.Version+". Seus saves e configurações serão mantidos; a ISO não é necessária.":installed==null?"Sua ISO será necessária apenas na preparação.":"Configurações e saves desta instalação reconhecidos.";
   Find<Button>("PrepareButton").Content=update?"ATUALIZAR PARA A v"+EmbeddedPayload.Version:installed!=null?"REINSTALAR COM A ISO":"PREPARAR MEU JOGO";
   var show=update?Visibility.Collapsed:Visibility.Visible;
   Find<FrameworkElement>("IsoLabel").Visibility=Find<FrameworkElement>("IsoRow").Visibility=Find<FrameworkElement>("TranslateCard").Visibility=Find<CheckBox>("DesktopShortcut").Visibility=show;
   if(installed!=null&&!update)Find<CheckBox>("TranslateGame").IsChecked=File.Exists(Path.Combine(installed,"game",".mcla-ptbr.json"));
   Find<TextBlock>("HomeInfo").Text=update?"Esta atualização troca os arquivos do jogo e acrescenta as traduções novas sobre as que você já tem. Seus saves e o mcla.toml não são alterados; uma cópia dos saves e dos arquivos anteriores fica na pasta backups.":"Use sua própria ISO de Midnight Club: Los Angeles Complete Edition para Xbox 360.";
  }

  void SavePreferences() {if(installed==null)return;Directory.CreateDirectory(Path.GetDirectoryName(preferences));File.WriteAllText(preferences,json.Serialize(new Dictionary<string,string>{{"installation",installed}}),Encoding.UTF8);}

  StackPanel Group(string containerName,string title,bool amber) {
   var border=new Border {Style=(Style)ui.Resources["Card"],Margin=new Thickness(0,0,0,16)};
   var stack=new StackPanel();
   stack.Children.Add(new TextBlock {Text=title,FontWeight=FontWeights.Bold,FontSize=12,Foreground=(Brush)ui.Resources[amber?"AccentAmber":"Accent"],Margin=new Thickness(0,0,0,14)});
   border.Child=stack;
   Find<StackPanel>(containerName).Children.Add(border);
   return stack;
  }
  StackPanel Group(string title) {return Group("GraphicsGroups",title,false);}

  void Choice(StackPanel parent,string label,string key,string[] labels,string[] values,int selected) {
   label=LauncherSettings.Label(label,key);
   var row=new Grid {Margin=new Thickness(0,0,0,12)};
   row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
   row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1.15,GridUnitType.Star)});
   row.Children.Add(new TextBlock {Text=label,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,16,0),Foreground=new SolidColorBrush(Color.FromRgb(226,232,240))});
   var c=new ComboBox {MinWidth=200};
   for(int i=0;i<labels.Length;i++)c.Items.Add(new ComboBoxItem {Content=labels[i],Tag=values[i]});
   c.SelectedIndex=selected;
   System.Windows.Automation.AutomationProperties.SetName(c,label);
   Grid.SetColumn(c,1);row.Children.Add(c);parent.Children.Add(row);
   choices[key]=c;
   c.SelectionChanged+=delegate{if(!loading){graphicsProfile="custom";Find<TextBlock>("PresetLabel").Text="Perfil: Personalizado";}};
  }
  void Toggle(StackPanel parent,string label,string key,bool initial) {
   label=LauncherSettings.Label(label,key);
   var check=new CheckBox {Content=label,IsChecked=initial,Margin=new Thickness(0,0,0,15)};
   parent.Children.Add(check);
   toggles[key]=check;
   check.Checked+=delegate{if(!loading){graphicsProfile="custom";Find<TextBlock>("PresetLabel").Text="Perfil: Personalizado";}};
   check.Unchecked+=delegate{if(!loading){graphicsProfile="custom";Find<TextBlock>("PresetLabel").Text="Perfil: Personalizado";}};
  }

  void BuildControlsOptions() {
   loading=true;
   var controls=Group("ControlsGroups","TECLADO E MOUSE (MnK)",false);
   Toggle(controls,"Jogar com teclado", "mnk_mode",true);
   Toggle(controls,"Controlar a câmera com o mouse", "mnk_mouse",true);
   Choice(controls,"Sensibilidade do mouse","mnk_sensitivity",new[]{"0,25×","0,50×","0,75×","1,00×","1,50×","2,00×","3,00×"},new[]{"0.25","0.5","0.75","1.0","1.5","2.0","3.0"},3);
   controls.Children.Add(new TextBlock {Text="Dirigir: W acelera, S freia/ré, A/D esterçam. Setas: menus. Mouse ou I/J/K/L: câmera. Espaço: confirmar/freio de mão; Backspace: voltar; Enter: iniciar/pausar. E/R/Q/F e Tab executam as funções adicionais do controle.",Style=(Style)ui.Resources["Hint"],FontSize=11,Margin=new Thickness(0,2,0,0)});

   var pad=Group("ControlsGroups","GAMEPAD (VIBRAÇÃO E DEAD ZONES)",true);
   Toggle(pad,"Vibração dos controles","mcla_vibration",true);
   Choice(pad,"Dead zone do analógico esquerdo","mcla_left_stick_deadzone",new[]{"Desligada","5%","10%","15%","20%","25%","30%"},new[]{"0","5","10","15","20","25","30"},0);
   Choice(pad,"Dead zone do analógico direito","mcla_right_stick_deadzone",new[]{"Desligada","5%","10%","15%","20%","25%","30%"},new[]{"0","5","10","15","20","25","30"},0);

   var access=Group("ControlsGroups","ACESSIBILIDADE E ATALHOS",false);
   Choice(access,"Abertura do jogo","mcla_intro",new[]{"Normal","Rápida","Pular logos"},new[]{"normal","fast","skip"},0);
   Toggle(access,"Captura de tela em PNG com F12", "mcla_screenshot_hotkey",true);
   Toggle(access,"Ocultar ponteiro do mouse durante o jogo", "mcla_hide_cursor",true);
   access.Children.Add(new TextBlock {Text="As capturas são salvas na pasta screenshots do jogo, com data e hora no nome do arquivo.",Style=(Style)ui.Resources["Hint"],FontSize=11});
   loading=false;
  }

  void BuildRadioWorldOptions() {
   loading=true;
   var radio=Group("RadioWorldGroups","RÁDIO MP3",false);
   Toggle(radio,"Ativar Rádio MP3 local", "mcla_radio_mp3",false);
   Toggle(radio,"Inserir MP3s nas estações do jogo (ALL / Gênero)", "mcla_radio_in_game",true);
   Toggle(radio,"Mostrar \"TOCANDO: música\" no HUD", "mcla_radio_announce",true);
   Choice(radio,"Volume MP3","mcla_radio_volume",new[]{"0%","10%","20%","30%","40%","50%","60%","70%","80%","90%","100%"},new[]{"0","10","20","30","40","50","60","70","80","90","100"},7);
   Toggle(radio,"Pausar rádio ao sair da janela (Alt+Tab)","mcla_radio_pause_unfocused",true);
   radio.Children.Add(new TextBlock {Text="Pasta das músicas (.mp3 e subpastas)",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,2,0,8)});
   radioFolder=new TextBox {Text="radio",MinWidth=320,MaxWidth=580,HorizontalAlignment=HorizontalAlignment.Left};
   radio.Children.Add(radioFolder);
   radioFolder.TextChanged+=delegate{if(!loading){graphicsProfile="custom";Find<TextBlock>("PresetLabel").Text="Perfil: Personalizado";}};
   radio.Children.Add(new TextBlock {Text="Com 'Inserir MP3s nas estações' ligado, as faixas entram na estação ALL e no gênero da tag ID3 ou subpasta. Desligado, usa o player separado (F9 pausa, F10/Shift+F10 troca, Ctrl+F9/F10 ajusta volume).",Style=(Style)ui.Resources["Hint"],FontSize=11,Margin=new Thickness(0,10,0,0)});

   var world=Group("RadioWorldGroups","MUNDO, VELOCÍMETRO E CÂMERA LIVRE",true);
   Choice(world,"Unidades de velocidade e radar","mcla_speed_units",new[]{"mph (milhas por hora)","km/h (quilômetros por hora)"},new[]{"mph","kmh"},0);
   Toggle(world,"Relógio Real (hora do mundo segue o PC)","mcla_time_realtime",false);
   Choice(world,"Velocidade do ciclo dia/noite","mcla_time_speed",new[]{"0× (parado)","0,5×","1× (normal)","2×","5×","10×"},new[]{"0","0.5","1","2","5","10"},2);
   Choice(world,"Horário fixo","mcla_time_hour",new[]{"Automático","00:00","03:00","06:00","09:00","12:00","15:00","18:00","21:00"},new[]{"-1","0","3","6","9","12","15","18","21"},0);
   Toggle(world,"Fixar horário escolhido","mcla_time_hold",true);
   Choice(world,"Clima","mcla_weather",new[]{"Automático","Céu limpo","Nublado","Tempestade","Neblina"},new[]{"auto","clear","cloudy","storm","fog"},0);
   Choice(world,"Velocidade da câmera livre","mcla_free_camera_speed",new[]{"5","10","20","40","80"},new[]{"5","10","20","40","80"},2);
   Choice(world,"Rotação da câmera livre","mcla_free_camera_sensitivity",new[]{"0,5","1","1,5","2","3"},new[]{"0.5","1","1.5","2","3"},2);
   world.Children.Add(new TextBlock {Text="No jogo: F8 liga/desliga a câmera livre. Com Num Lock ligado, use 8/2/4/6 do teclado numérico para mover e 9/3 para subir/descer. I/J/K/L ou arrastar com botão direito gira a câmera; Shift acelera e Ctrl desacelera.",Style=(Style)ui.Resources["Hint"],FontSize=11});
   loading=false;
  }

  void BuildGraphics() {
   loading=true;
   var g=Group("TELA E APRESENTAÇÃO");
   Choice(g,"Modo de exibição","fullscreen",new[]{"Tela cheia (resolução do monitor)","Janela"},new[]{"true","false"},0);
   Choice(g,"Tamanho da janela","window_size",new[]{"Automático","1280 × 720","1920 × 1080","2560 × 1440","3440 × 1440","3840 × 2160"},new[]{"0,0","1280,720","1920,1080","2560,1440","3440,1440","3840,2160"},0);
   Choice(g,"Proporção","mcla_aspect_ratio",new[]{"Automático","16:9","21:9"},new[]{"auto","16:9","43:18"},0);
   Choice(g,"Resolução de renderização","resolution_scale",new[]{"720p", "1080p", "1440p", "2160p"},new[]{"1","1080p","2","3"},0);
   Choice(g,"API gráfica","mcla_gpu_backend",new[]{"Automático","DirectX 12","Vulkan"},new[]{"any","d3d12","vulkan"},0);
   Choice(g,"Sincronização vertical","vsync",new[]{"Não","Sim"},new[]{"false","true"},1);
   Choice(g,"Limite de FPS","mcla_fps",new[]{"Sem limite","30","40","45","50","60","90","120","144"},new[]{"0","30","40","45","50","60","90","120","144"},1);
   Choice(g,"HDR","hdr",new[]{"Desligado","Automático","Ligado"},new[]{"off","auto","on"},1);
   Choice(g,"Brilho máximo HDR","hdr_peak_nits",new[]{"Automático","400 nits","600 nits","1.000 nits","1.500 nits","2.000 nits","4.000 nits"},new[]{"0","400","600","1000","1500","2000","4000"},0);
   Choice(g,"Branco de referência HDR","hdr_paper_white_nits",new[]{"Automático","80 nits","120 nits","160 nits","200 nits","240 nits","300 nits","400 nits","480 nits"},new[]{"0","80","120","160","200","240","300","400","480"},0);
   Choice(g,"Intensidade dos realces HDR","mcla_hdr_highlights",new[]{"Normal","Brilhante","Mais brilhante","Máxima"},new[]{"1","1.5","2","3"},2);
   Choice(g,"Gama de exibição","mcla_display_gamma",new[]{"Monitor de PC","TV"},new[]{"pc","tv"},1);
   Choice(g,"Atualização do HUD","mcla_ui_hud_rate",new[]{"A cada quadro","30 FPS","60 FPS","120 FPS"},new[]{"0","30","60","120"},2);

   g=Group("RENDERIZAÇÃO E TEXTURAS");
   Choice(g,"Filtro anisotrópico","anisotropic_override",new[]{"Padrão do jogo","Desligado","1×","2×","4×","8×","16×"},new[]{"-1","0","1","2","3","4","5"},4);
   Choice(g,"Vinil dos veículos","readback_resolve",new[]{"Ativado (padrão)","Desativado (mais desempenho)","Ativado (por cache)","Ativado (síncrono, mais lento)"},new[]{"fast","none","some","full"},0);
   g.Children.Add(new TextBlock {Text="O filtro anisotrópico melhora a nitidez de texturas vistas em ângulo. Desativar vinis pode ocultar adesivos nos veículos carregados depois da alteração.",Style=(Style)ui.Resources["Hint"],FontSize=11});

   g=Group("AMD FIDELITYFX E BAIXA LATÊNCIA");
   Choice(g,"Antisserrilhado","mcla_anti_aliasing",new[]{"FSR (versão depende da API/GPU)","MSAA 2× (padrão do jogo)","Desligado (mais serrilhados)"},new[]{"fsr","msaa","off"},1);
   Choice(g,"Modo FSR","temporal_aa_quality",new[]{"Native AA — menos serrilhados","Qualidade — recomendado","Equilibrado — mais FPS","Desempenho — menos detalhes"},new[]{"native","quality","balanced","performance"},1);
   Choice(g,"Nitidez FSR","temporal_aa_sharpness",new[]{"Desligada","0,2","0,4","0,6","0,8","1,0"},new[]{"0","0.2","0.4","0.6","0.8","1.0"},1);
   Choice(g,"Baixa latência (Anti-Lag 2 / NVIDIA Reflex)","low_latency",new[]{"Automático (só Reflex)","Ligado (inclui Anti-Lag 2)","Desligado"},new[]{"auto","on","off"},0);
   Toggle(g,"FRAME GENERATION 4.0.2","frame_generation",false);
   Toggle(g,"Smooth UI (interpola menus/HUD entre passos de 30 Hz)","mcla_ui_interpolate",true);
   g.Children.Add(new TextBlock {Text="Comece com FSR Qualidade e nitidez 0,2. A geração de quadros exige reinício. No Vulkan experimental, selecione FSR no antisserrilhado e desligue o HDR.",Style=(Style)ui.Resources["Hint"],FontSize=11});

   g=Group("SOMBRAS E EFEITOS");
   foreach(var p in new[]{new[]{"Sombras","mcla_shadows"},new[]{"Sombras nas corridas","mcla_race_shadows"},new[]{"Sombras da vegetação","mcla_foliage_shadows"},new[]{"Árvores distantes","mcla_foliage_impostors"}})Toggle(g,p[0],p[1],true);
   foreach(var p in new[]{new[]{"Sombras simples dos carros","mcla_fast_car_shadows"},new[]{"Desfoque de movimento","mcla_motion_blur"},new[]{"Profundidade de campo","mcla_depth_of_field"},new[]{"Desfoque de tela","mcla_fullscreen_blur"}})Toggle(g,p[0],p[1],p[1]!="mcla_fast_car_shadows");

   g=Group("CIDADE E TRÁFEGO");
   foreach(var p in new[]{new[]{"Detalhe da cidade","mcla_city_lod"},new[]{"Detalhe do tráfego","mcla_traffic_lod"},new[]{"Pedestres","mcla_pedestrians"},new[]{"Carros estacionados","mcla_parked_cars"}})
    Choice(g,p[0],p[1],p[1]=="mcla_pedestrians"||p[1]=="mcla_parked_cars"?new[]{"100%","75%","50%","25%","Nenhum"}:new[]{"100%","75%","50%","25%"},p[1]=="mcla_pedestrians"||p[1]=="mcla_parked_cars"?new[]{"1.0","0.75","0.5","0.25","0"}:new[]{"1.0","0.75","0.5","0.25"},0);
   Choice(g,"Distância de tráfego","mcla_traffic_distance",new[]{"Curta (250)","Média (325)","Original (400)"},new[]{"250","325","400"},2);

   g=Group("RENDERIZAÇÃO AVANÇADA");
   Toggle(g,"Otimização de espera da GPU", "mcla_fence_yield",true);
   Toggle(g,"Renderizar em bloco único (se suportado)", "mcla_single_tile",true);
   g.Children.Add(new TextBlock {Text="O bloco único depende da implementação de EDRAM do dispositivo. Quando indisponível, o jogo mantém seu caminho de renderização normal.",Style=(Style)ui.Resources["Hint"],FontSize=11});

   choices["resolution_scale"].SelectionChanged+=delegate{
    if(loading)return;
    if(Value("resolution_scale")=="1080p"){Set("mcla_anti_aliasing","fsr");Set("temporal_aa_quality","native");}
    else if(Value("temporal_aa_quality")=="1080p")Set("temporal_aa_quality","native");
    UpdateAvailability();
   };
   choices["mcla_gpu_backend"].SelectionChanged+=delegate{UpdateAvailability();};
   choices["hdr"].SelectionChanged+=delegate{UpdateAvailability();};
   choices["mcla_anti_aliasing"].SelectionChanged+=delegate{UpdateAvailability();};
   UpdateAvailability();
   loading=false;
  }

  string Value(string key) {var item=choices[key].SelectedItem as ComboBoxItem;return item==null?"":Convert.ToString(item.Tag);}
  void UpdateAvailability() {
   // Reading a file must never clear a saved value because the machine/API
   // cannot use it in this session. The runtime decides actual availability.
   bool native1080=Value("resolution_scale")=="1080p";
   if(native1080) {Set("mcla_anti_aliasing","fsr");Set("temporal_aa_quality","native");}
   bool fsr=Value("mcla_anti_aliasing")=="fsr";
   choices["mcla_anti_aliasing"].IsEnabled=!native1080;
   toggles["frame_generation"].IsEnabled=Value("mcla_gpu_backend")!="vulkan" ||
       (fsr && Value("hdr")=="off" && installed!=null && File.Exists(Path.Combine(installed,"amd_fidelityfx_framegeneration_vk.dll")));
   choices["temporal_aa_quality"].IsEnabled=fsr&&!native1080;
   choices["temporal_aa_sharpness"].IsEnabled=fsr;
  }
  void Set(string key,string value) {
   if(key=="mcla_graphics_profile"){graphicsProfile=value;return;}
   if(choices.ContainsKey(key)) {
    foreach(ComboBoxItem item in choices[key].Items)
     if(LauncherSettings.Equal(Convert.ToString(item.Tag),value)){choices[key].SelectedItem=item;return;}
    var custom=new ComboBoxItem {Content="Personalizado ("+value+")",Tag=value};
    choices[key].Items.Add(custom);choices[key].SelectedItem=custom;return;
   }
   if(toggles.ContainsKey(key))toggles[key].IsChecked=key=="frame_generation"?value=="fsr":value=="true";
  }
  void Preset(string name) {
   string id=name=="Leve"?"performance":name=="Native AA"?"native":"balanced";
   loading=true;try{foreach(var pair in GraphicsProfiles()[id])Set(pair.Key,pair.Value);}finally{loading=false;}
   graphicsProfile=id;profileRequested=true;UpdateAvailability();Find<TextBlock>("PresetLabel").Text="Perfil: "+name;
   Status("Perfil selecionado em 720p base. FSR Qualidade preserva detalhes; Native AA prioriza serrilhados. Salve para aplicar. Itens marcados exigem reinício.");
  }
  Dictionary<string,Dictionary<string,string>> GraphicsProfiles() {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MclaSetup.graphics_profiles.json"))
   using(var reader=new StreamReader(stream)) {
    return json.Deserialize<Dictionary<string,Dictionary<string,string>>>(reader.ReadToEnd());
   }
  }
  bool StringKey(string key) {return new[]{"mcla_render_resolution","mcla_weather","mcla_graphics_profile","mcla_gpu_backend","mcla_aspect_ratio","mcla_anti_aliasing","temporal_aa_quality","frame_generation","hdr","mcla_display_gamma","readback_resolve","mcla_intro","mcla_player_name","mcla_button_prompts","mcla_radio_folder","low_latency","mcla_speed_units"}.Contains(key);}
  Dictionary<string,string> Selected() {
   string name=Find<TextBox>("PlayerName").Text.Trim();if(!Regex.IsMatch(name,@"^[A-Za-z0-9_ ]{1,15}$")) {Go("Controls");throw new IOException("Escolha um nome de até 15 letras sem acento, números, espaço ou sublinhado.");}
   var values=new Dictionary<string,string>{{"mcla_graphics_profile",graphicsProfile}};
   foreach(var pair in choices) {
    string v=Value(pair.Key);
    if(pair.Key=="window_size"){var size=v.Split(',');values["window_width"]=size[0];values["window_height"]=size[1];}
    else values[pair.Key]=v;
   }
   LauncherSettings.NormalizeRenderResolution(values);
   foreach(var pair in toggles)values[pair.Key]=pair.Key=="frame_generation"?(pair.Value.IsChecked==true?"fsr":"off"):(pair.Value.IsChecked==true?"true":"false");
   values["mcla_radio_folder"]=String.IsNullOrWhiteSpace(radioFolder.Text)?"radio":radioFolder.Text.Trim();
   values["mcla_player_name"]=name;
   values["mcla_button_prompts"]=Convert.ToString(((ComboBoxItem)Find<ComboBox>("ButtonPrompts").SelectedItem).Tag);
   return values;
  }
  string Toml(Dictionary<string,string> values) {var text=new StringBuilder();foreach(var p in values)text.AppendLine(p.Key+" = "+(StringKey(p.Key)?json.Serialize(p.Value):p.Value));return text.ToString();}
  Dictionary<string,string> ReadConfig() {
   string path=installed==null?null:Path.Combine(installed,"mcla.toml");
   return LauncherSettings.Read(path!=null&&File.Exists(path)?File.ReadAllText(path):"");
  }
  void LoadSettings() {
   loading=true;
   foreach(var pair in firstRun)Set(pair.Key,pair.Value);
   Set("window_size",firstRun["window_width"]+","+firstRun["window_height"]);
   radioFolder.Text=firstRun["mcla_radio_folder"];
   Find<TextBox>("PlayerName").Text=firstRun["mcla_player_name"];
   Find<ComboBox>("ButtonPrompts").SelectedIndex=0;
   var values=ReadConfig();
   foreach(var pair in values)Set(pair.Key,pair.Value);
   if(LauncherSettings.Native1080(values))Set("resolution_scale","1080p");
   if(values.ContainsKey("window_width")&&values.ContainsKey("window_height"))Set("window_size",values["window_width"]+","+values["window_height"]);
   if(values.ContainsKey("mcla_radio_folder"))radioFolder.Text=values["mcla_radio_folder"];
   if(values.ContainsKey("mcla_player_name"))Find<TextBox>("PlayerName").Text=values["mcla_player_name"];
   if(values.ContainsKey("mcla_button_prompts"))foreach(ComboBoxItem i in Find<ComboBox>("ButtonPrompts").Items)if(Convert.ToString(i.Tag)==values["mcla_button_prompts"])Find<ComboBox>("ButtonPrompts").SelectedItem=i;
   UpdateAvailability();
   loading=false;
   loadedSelection=Selected();
   profileRequested=false;
   Find<TextBlock>("PresetLabel").Text="Perfil: Configurações da instalação";
   RefreshHeroChips();
  }
  bool GameRunning() {if(installed==null)return false;foreach(var p in Process.GetProcessesByName("mcla")) {try{if(String.Equals(p.MainModule.FileName,GameExe,StringComparison.OrdinalIgnoreCase))return true;}catch{}finally{p.Dispose();}}return false;}
  void Apply() {
   var values=Selected();
   if(installed==null){Status("Opções selecionadas. Elas serão aplicadas ao preparar o jogo.");return;}
   if(GameRunning())throw new IOException("Feche o jogo antes de salvar as configurações no configurador.");
   var changes=values.Where(p=>loadedSelection==null||!loadedSelection.ContainsKey(p.Key)||
    !LauncherSettings.Equal(loadedSelection[p.Key],p.Value)).ToDictionary(p=>p.Key,p=>p.Value);
   if(profileRequested&&graphicsProfile!="custom") {
    changes["mcla_graphics_profile"]=graphicsProfile;
    foreach(var pair in GraphicsProfiles()[graphicsProfile])changes[pair.Key]=pair.Value;
    // Profiles select integer scales, including Native AA at 720p base.
    changes["mcla_render_resolution"]="scaled";
   }
   if(changes.Count==0){LoadSettings();Status("Nenhuma alteração. Configurações relidas do jogo.");return;}
   string path=Path.Combine(installed,"mcla.toml");string old=File.Exists(path)?File.ReadAllText(path):"";
   string fresh=path+".new";
   File.WriteAllText(fresh,LauncherSettings.Merge(old,changes,StringKey),new UTF8Encoding(false));
   if(File.Exists(path)) {
    string backups=Path.Combine(installed,"config-backups");Directory.CreateDirectory(backups);
    File.Replace(fresh,path,Path.Combine(backups,DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff")+".toml"));
   }else File.Move(fresh,path);
   LoadSettings();Status("Configurações salvas. Valem na próxima abertura do jogo.");Log("Configurações salvas em "+path);
  }
  void Play() {
   if(installed==null||!Owned(installed))throw new IOException("Localize uma instalação pronta antes de jogar.");
   if(GameRunning()){Status("O jogo já está aberto.");return;}
   string backup=SaveBackup.CreateAutomatic(SaveDirectory(),Path.Combine(installed,"save-backups"),GameRunning);
   if(backup!=null)Log("Backup automático (últimas "+SaveBackup.AutomaticCopies+" cópias): "+backup);
   if(GameRunning()){Status("O jogo já está aberto.");return;}
   Process.Start(new ProcessStartInfo(GameExe) {WorkingDirectory=installed,UseShellExecute=true});
   Status("Jogo iniciado. "+(backup==null?"Ainda não há dados para backup.":"Backup automático concluído."));
  }
  void PlayWithMods() {
   if(installed==null||!Owned(installed))throw new IOException("Localize uma instalação pronta antes de iniciar com mods.");
   if(GameRunning())throw new IOException("Feche o jogo antes de iniciar a sessão com mods.");
   string modsDir=Find<TextBox>("ModsPath").Text.Trim();
   if(String.IsNullOrWhiteSpace(modsDir)||!Directory.Exists(modsDir))throw new IOException("Selecione uma pasta existente contendo os arquivos modificados do jogo.");
   string fullMods=Path.GetFullPath(modsDir);
   if(!File.Exists(Path.Combine(fullMods,"default.xex"))&&!File.Exists(Path.Combine(fullMods,"xarchive_cache.rpf"))) {
    throw new IOException("A pasta selecionada não parece conter os dados do jogo (default.xex / xarchive_cache.rpf).");
   }
   string modsUserdata=Path.Combine(installed,"userdata-mods");
   Directory.CreateDirectory(modsUserdata);
   string args="--game_data_root="+Quote(fullMods)+" --user_data_root="+Quote(modsUserdata);
   Process.Start(new ProcessStartInfo(GameExe,args) {WorkingDirectory=installed,UseShellExecute=true});
   Log("Iniciando sessão isolada com mods ("+fullMods+" -> "+modsUserdata+").");
   Status("Jogo iniciado com mods (saves separados em userdata-mods).");
  }
  async Task PrepareShaders() {
   var button=Find<Button>("PrepareShadersButton");
   try {
    if(installed==null||!Owned(installed))throw new IOException("Localize uma instalação pronta antes de preparar os shaders.");
    if(GameRunning()){Status("Feche o jogo antes de preparar os shaders.");return;}
    button.IsEnabled=false;Find<Button>("PlayButton").IsEnabled=false;working=true;Go("Logs");
    Log("Preparando os shaders a partir dos arquivos do seu jogo…");Status("Preparando os shaders… pode levar de 15 segundos a alguns minutos.");
    var start=new ProcessStartInfo(GameExe,"--mcla_prepare_shaders=true") {WorkingDirectory=installed,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    int code=-1;
    using(var process=new Process {StartInfo=start}) {
     process.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null&&e.Data.IndexOf("shader",StringComparison.OrdinalIgnoreCase)>=0)Log(e.Data);};
     process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
     await Task.Run(delegate{process.WaitForExit();});code=process.ExitCode;
    }
    string result=code==0?"Shaders preparados. A primeira partida abre sem compilar.":code==2?"O pacote de shaders não está nesta instalação. Atualize o jogo pelo instalador.":code==3?"O pacote de shaders está danificado. Atualize o jogo pelo instalador.":code==4?"O pacote de shaders é de outros arquivos do jogo.":code==5?"O pacote de shaders é de outra versão do armazenamento. Atualize o jogo pelo instalador.":code==6?"Não foi possível gravar na pasta dos shaders.":code==7?"A pasta dos shaders está em uso por outro processo.":"A preparação dos shaders terminou com o código "+code+". Veja a aba Registro.";
    Log(result);Status(result);
   } catch(Exception e) {Log("Preparar shaders: "+e.Message);Status(e.Message);}
   finally {working=false;button.IsEnabled=true;Find<Button>("PlayButton").IsEnabled=installed!=null;}
  }
  void OpenDirectory(string path) {if(path==null||!Directory.Exists(path))throw new IOException("Essa pasta ainda não existe. Inicie ou prepare o jogo primeiro.");Process.Start("explorer.exe",Quote(path));}
  string SaveDirectory() {
   if(installed==null)return null;var values=ReadConfig();
   string path=values.ContainsKey("user_data_root")?values["user_data_root"]:"";
   if(String.IsNullOrWhiteSpace(path))return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"mcla");
   return Path.IsPathRooted(path)?path:Path.Combine(installed,path);
  }
  void RefreshSaves() {try {string path=SaveDirectory();Find<TextBlock>("SavePath").Text=path??"Prepare ou localize uma instalação.";if(path!=null&&Directory.Exists(path)){var files=new DirectoryInfo(path).GetFiles("*",SearchOption.AllDirectories);Find<TextBlock>("SaveSummary").Text=files.Length+" arquivos · "+(files.Sum(f=>f.Length)/1048576.0).ToString("0.0",CultureInfo.CurrentCulture)+" MB";}else Find<TextBlock>("SaveSummary").Text="Os dados serão criados ao iniciar e salvar o jogo.";}catch(Exception e){Find<TextBlock>("SaveSummary").Text=e.Message;}}
  void Backup() {if(GameRunning())throw new IOException("Feche o jogo antes de exportar o backup.");string path=SaveDirectory();if(path==null||!Directory.Exists(path))throw new IOException("Ainda não há dados de usuário para exportar.");var picker=new SaveFileDialog {Title="Exportar backup dos dados",Filter="Arquivo ZIP (*.zip)|*.zip",FileName="MCLA-save-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};if(picker.ShowDialog(ui)!=true)return;string dest=Path.GetFullPath(picker.FileName);string prefix=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(dest.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Salve o backup fora da pasta dos dados.");ZipFile.CreateFromDirectory(path,dest,CompressionLevel.Optimal,false);Status("Backup exportado: "+dest);}
  void RestoreBackup() {RestoreBackup(false);}
  void RestoreBackup(bool automatic) {
   if(installed==null)throw new IOException("Localize uma instalação antes de restaurar os dados.");
   if(GameRunning())throw new IOException("Feche o jogo antes de restaurar um backup.");
   var picker=new OpenFileDialog {Title="Restaurar um backup exportado pelo MCLA",Filter="Backup ZIP (*.zip)|*.zip",CheckFileExists=true};
   if(automatic) {
    string folder=Path.Combine(installed,"save-backups");
    string latest=SaveBackup.LatestRecoverable(folder,Log);
    if(latest==null)throw new IOException("Não há um backup automático recuperável. Inicie pelo botão JOGAR para criar cópias antes de cada sessão.");
    picker.InitialDirectory=folder;picker.FileName=latest;picker.Title="Recuperar progresso — escolha uma cópia automática";
   }
   if(picker.ShowDialog(ui)!=true)return;
   string target=Path.GetFullPath(SaveDirectory()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   if(String.IsNullOrEmpty(Path.GetDirectoryName(target)))throw new IOException("Pasta de dados inválida.");
   string zipPath=Path.GetFullPath(picker.FileName);
   if(zipPath.StartsWith(target+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
    throw new IOException("Selecione um ZIP fora da pasta que será restaurada.");
   using(var archive=ZipFile.OpenRead(zipPath)) {
    var info=SaveBackup.Inspect(archive);
    string question="Restaurar "+info.Entries+" entradas ("+(info.Bytes/1048576.0).ToString("0.0",CultureInfo.CurrentCulture)+" MB)?\n\nOs dados atuais serão guardados em uma pasta de segurança antes da troca. O jogo deve continuar fechado.";
    if(MessageBox.Show(ui,question,"Confirmar restauração",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
    string safety=SaveBackup.Restore(archive,target,GameRunning,Log);
    RefreshSaves();Status("Backup restaurado. Cópia anterior: "+(safety??"não havia dados anteriores"));
    Log("Backup restaurado de "+zipPath+" para "+target);
    if(safety!=null)Log("Dados anteriores preservados em "+safety);
   }
  }
  static string Quote(string value) {return "\""+value.Replace("\"","")+"\"";}

  async Task CheckRemoteUpdateAsync(bool userInitiated) {
   var btn=Find<Button>("CheckUpdatesButton");
   if(userInitiated) {btn.IsEnabled=false;Status("Verificando atualizações no GitHub...");}
   try {
    var info=await UpdateChecker.CheckForUpdatesAsync(EmbeddedPayload.Version,Log);
    if(info!=null) {
     remoteUpdate=info;
     Find<TextBlock>("RemoteUpdateTitle").Text="Nova versão disponível: v"+info.Version+" (atual: v"+EmbeddedPayload.Version+")";
     Find<TextBlock>("RemoteUpdateSummary").Text=String.IsNullOrWhiteSpace(info.NotesSummary)?"Seus saves e configurações serão mantidos; a ISO não é necessária.":info.NotesSummary;
     Find<Button>("DownloadUpdateButton").Visibility=!String.IsNullOrEmpty(info.ZipDownloadUrl)?Visibility.Visible:Visibility.Collapsed;
     var banner=Find<FrameworkElement>("RemoteUpdateBanner");
     if(banner.Visibility!=Visibility.Visible) {banner.Visibility=Visibility.Visible;AnimateBannerEnter();}
     Log("Nova release disponível no GitHub: v"+info.Version+" ("+info.HtmlUrl+")");
     Status("Nova versão disponível no GitHub: v"+info.Version);
    } else if(userInitiated) {
     Status("Você já está na versão mais recente (v"+EmbeddedPayload.Version+").");
     Log("Nenhuma versão mais nova encontrada no GitHub (atual: v"+EmbeddedPayload.Version+").");
    }
   } finally {
    if(userInitiated) btn.IsEnabled=true;
   }
  }

  async Task DownloadAndLaunchUpdate() {
   if(working||remoteUpdate==null)return;
   if(String.IsNullOrEmpty(remoteUpdate.ZipDownloadUrl)) {OpenUrl(remoteUpdate.HtmlUrl);return;}
   if(GameRunning())throw new IOException("Feche o jogo antes de baixar e aplicar a atualização.");
   SavePreferences();
   working=true;
   var dlBtn=Find<Button>("DownloadUpdateButton");
   var prepBtn=Find<Button>("PrepareButton");
   var playBtn=Find<Button>("PlayButton");
   var nav=Find<StackPanel>("Navigation");
   var bar=Find<ProgressBar>("WorkProgress");
   dlBtn.IsEnabled=false;prepBtn.IsEnabled=false;playBtn.IsEnabled=false;nav.IsEnabled=false;
   bar.IsIndeterminate=false;bar.Value=0;
   try {
    Status("Baixando atualização v"+remoteUpdate.Version+"...");
    string newExe=await UpdateChecker.DownloadAndStageUpdateAsync(remoteUpdate,EmbeddedPayload.Version,delegate(int pct) {
     ui.Dispatcher.BeginInvoke(new Action(delegate {
      bar.Value=pct;
      Status("Baixando atualização v"+remoteUpdate.Version+"... "+pct+"%");
     }));
    },Log);
    Log("Atualização pronta em "+newExe+". Abrindo o novo instalador...");
    Process.Start(new ProcessStartInfo(newExe) {WorkingDirectory=Path.GetDirectoryName(newExe),UseShellExecute=true});
    working=false;
    ui.Close();
   } catch(Exception e) {
    working=false;dlBtn.IsEnabled=true;prepBtn.IsEnabled=true;nav.IsEnabled=true;playBtn.IsEnabled=installed!=null;bar.Value=0;
    Error(e);
   }
  }

  async Task Prepare() {
   string script=null;
   try {
    if(working)return;if(!HasBundle)throw new IOException("Use o instalador completo para preparar uma ISO.");bool update=UpdateAvailable;var selected=update?null:Selected();bool translate=Find<CheckBox>("TranslateGame").IsChecked==true;string target=Folder;string iso=update?"":Find<TextBox>("IsoPath").Text.Trim();if(!update&&(!File.Exists(iso)||!String.Equals(Path.GetExtension(iso),".iso",StringComparison.OrdinalIgnoreCase)))throw new IOException("Selecione uma ISO existente pelo botão Procurar ISO.");
    string self=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);if(String.Equals(target.TrimEnd(Path.DirectorySeparatorChar),self,StringComparison.OrdinalIgnoreCase))throw new IOException("Escolha uma subpasta para o jogo. O destino não pode ser a própria pasta do instalador.");if(GameRunning())throw new IOException("Feche o jogo antes de preparar ou atualizar essa instalação.");
    Go("Logs");Log(update?"Iniciando a atualização…":"Iniciando preparação do jogo…");working=true;Find<Button>("PrepareButton").IsEnabled=false;Find<Button>("PlayButton").IsEnabled=false;Find<StackPanel>("Navigation").IsEnabled=false;Find<TextBox>("InstallPath").IsEnabled=Find<TextBox>("IsoPath").IsEnabled=false;Find<Button>("BrowseInstall").IsEnabled=Find<Button>("BrowseIso").IsEnabled=false;Find<ProgressBar>("WorkProgress").IsIndeterminate=true;Status("Verificando e extraindo o pacote interno…");
    string bundle=await Task.Run(delegate{return EmbeddedPayload.Extract(null);});string scratch=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MCLA_LA_Recompiled","Setup");Directory.CreateDirectory(scratch);script=Path.Combine(scratch,"bootstrap-"+Guid.NewGuid().ToString("N")+".ps1");using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("MclaSetup.bootstrap.ps1"))using(var output=File.Create(script))input.CopyTo(output);
    string common=" -InstallDirectory "+Quote(target)+" -InstallerDirectory "+Quote(self)+" -BundleDirectory "+Quote(bundle);string mode;if(update)mode=" -Update";else mode=" -Iso "+Quote(Path.GetFullPath(iso))+" -GraphicsSettings "+Quote(Convert.ToBase64String(Encoding.UTF8.GetBytes(Toml(selected))))+" -Translate "+(translate?"1":"0");var start=new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(script)+common+mode) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};worker=new Process {StartInfo=start};worker.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){Log(e.Data);};worker.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){Log(e.Data);};worker.Start();worker.BeginOutputReadLine();worker.BeginErrorReadLine();await Task.Run(delegate{worker.WaitForExit();});if(worker.ExitCode!=0||!Owned(target))throw new IOException("A preparação não terminou. Veja as mensagens na aba Registro.");string launcherTarget=Path.Combine(target,"Midnight Club L.A. Recompiled.exe");string launcherSource=Assembly.GetExecutingAssembly().Location;if(!String.Equals(Path.GetFullPath(launcherSource),Path.GetFullPath(launcherTarget),StringComparison.OrdinalIgnoreCase)){File.Copy(launcherSource,launcherTarget,true);Log("Instalador e configurador disponíveis no mesmo executável: "+launcherTarget);}installed=target;SavePreferences();if(Find<CheckBox>("DesktopShortcut").IsChecked==true){try{GameShortcut.Create(target);Log("Atalho direto criado na Área de Trabalho.");}catch(Exception e){Log("Jogo pronto; não foi possível criar o atalho: "+e.Message);}}Refresh(true);Status(update?"Atualizado. Seus saves e configurações foram mantidos.":"Tudo pronto. Seu jogo já pode ser iniciado.");
   }catch(Exception e){Error(e);Go("Logs");}
   finally {working=false;Find<Button>("PrepareButton").IsEnabled=true;Find<StackPanel>("Navigation").IsEnabled=true;Find<TextBox>("InstallPath").IsEnabled=Find<TextBox>("IsoPath").IsEnabled=true;Find<Button>("BrowseInstall").IsEnabled=Find<Button>("BrowseIso").IsEnabled=true;Find<ProgressBar>("WorkProgress").IsIndeterminate=false;Find<ProgressBar>("WorkProgress").Value=installed!=null?100:0;Find<Button>("PlayButton").IsEnabled=installed!=null;if(worker!=null){worker.Dispose();worker=null;}if(script!=null&&File.Exists(script))File.Delete(script);try{string directory=Path.GetDirectoryName(preferences);Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"last-install.log"),Find<TextBox>("LogText").Text);}catch{}}
  }
 }
}
