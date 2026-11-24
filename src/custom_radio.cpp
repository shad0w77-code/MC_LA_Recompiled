// Local MP3 playback, pause-menu commands and a non-interactive track toast.
#include "custom_radio.h"
#include "generated/default/mcla_init.h"
#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/logging.h>
#include <rex/runtime.h>
#include <rex/ppc.h>
#include <rex/system/function_dispatcher.h>
#include <rex/ui/imgui_dialog.h>
#include <rex/ui/keybinds.h>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstring>
#include <cwctype>
#include <deque>
#include <filesystem>
#include <random>
#include <mutex>
#include <string>
#include <sstream>
#include <array>
#include <thread>
#include <vector>
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <mmsystem.h>
#include <pdh.h>
#include <pdhmsg.h>
#pragma comment(lib,"pdh.lib")
#endif

REXCVAR_DEFINE_STRING(mcla_hud_style,"full","MCLA","FPS display format").allowed({"full","simple"});
REXCVAR_DEFINE_BOOL(mcla_radio_mp3,false,"MCLA","Local MP3: F9 pause; F10 next; Shift+F10 previous");
REXCVAR_DEFINE_STRING(mcla_radio_folder,"radio","MCLA","MP3 folder, relative to executable or absolute");
REXCVAR_DEFINE_INT32(mcla_radio_volume,70,"MCLA","Local MP3 volume, percent").range(0,100);
REXCVAR_DEFINE_BOOL(mcla_radio_paused,false,"MCLA","Pause local MP3 radio (session only)");
REXCVAR_DEFINE_BOOL(mcla_radio_mute_game,true,"MCLA","Mute native music while a local track is playing; preserve voice and effects");
REXCVAR_DEFINE_BOOL(mcla_radio_pause_unfocused,true,"MCLA","Pause MP3 radio while unfocused");
void MclaNoteMenuSettingChanged(const char* option);

namespace {
using Clock=std::chrono::steady_clock;
std::mutex state_mutex,audio_mutex;
std::deque<int> commands;
std::string status;
Clock::time_point toast_until{};
std::atomic<bool> playing{false};
// sub_82387B90 returns this static audManager instance. SetVolume is not
// necessarily called during boot, so waiting for that hook would miss music.
uint32_t audio_manager=0x8288E920;
bool music_owned=false;

void Toast(std::string text) {std::lock_guard lock(state_mutex);status=std::move(text);toast_until=Clock::now()+std::chrono::seconds(6);}
struct HudLoad {
 std::atomic<int> cpu{-1}, gpu{-1};
#ifdef _WIN32
 std::jthread worker{[this](std::stop_token stop) {
   PDH_HQUERY query{};
   PDH_HCOUNTER engine{};
   if (PdhOpenQueryW(nullptr,0,&query)==ERROR_SUCCESS)
     PdhAddEnglishCounterW(query, LR"(\GPU Engine(*)\Utilization Percentage)",0,&engine);
   const auto prefix=L"pid_"+std::to_wstring(GetCurrentProcessId())+L"_";
   uint64_t oldTotal=0,oldIdle=0;
   auto ticks=[](FILETIME t){return (uint64_t(t.dwHighDateTime)<<32)|t.dwLowDateTime;};
   while (!stop.stop_requested()) {
     if (rex::cvar::GetFlagByName("mcla_fps_hud")=="true") {
       FILETIME idle,kernel,user;
       if(GetSystemTimes(&idle,&kernel,&user)){
         auto total=ticks(kernel)+ticks(user), free=ticks(idle);
         if(oldTotal && total>oldTotal) cpu=int(std::clamp(100.0*(1.0-double(free-oldIdle)/(total-oldTotal)),0.0,100.0)+0.5);
         oldTotal=total; oldIdle=free;
       }
       int peak=-1;
       if(engine && PdhCollectQueryData(query)==ERROR_SUCCESS){
         DWORD bytes=0,count=0;
         if(PdhGetFormattedCounterArrayW(engine,PDH_FMT_DOUBLE,&bytes,&count,nullptr)==PDH_MORE_DATA){
           std::vector<BYTE> buffer(bytes);
           auto* items=reinterpret_cast<PDH_FMT_COUNTERVALUE_ITEM_W*>(buffer.data());
           if(PdhGetFormattedCounterArrayW(engine,PDH_FMT_DOUBLE,&bytes,&count,items)==ERROR_SUCCESS)
             for(DWORD i=0;i<count;++i){
               std::wstring_view name(items[i].szName);
               const auto& v=items[i].FmtValue;
               if(name.starts_with(prefix) && name.find(L"engtype_3D")!=name.npos && v.CStatus<=1)
                 peak=std::max(peak,int(std::clamp(v.doubleValue,0.0,100.0)+0.5));
             }
         }
       }
       gpu=peak;
     }
     std::this_thread::sleep_for(std::chrono::seconds(1));
   }
   if(query) PdhCloseQuery(query);
 }};
#endif
};

void DrawPerformanceHud(ImGuiIO& io,const std::string& stats){

 std::istringstream input(stats); std::array<std::string,8> v;
 for(auto& field:v) if(!std::getline(input,field))return;
 const float scale=std::clamp(io.DisplaySize.y/900.f,0.85f,1.8f);
 const float x=io.DisplaySize.x-256*scale,y=14*scale;
 auto pt=[&](float a,float b){return ImVec2(x+a*scale,y+b*scale);};
 auto* d=ImGui::GetForegroundDrawList(); auto* font=ImGui::GetFont();
 const auto hot=IM_COL32(255,87,44,255), white=IM_COL32(248,248,246,255), muted=IM_COL32(175,184,190,255);
 auto label=[&](float a,float b,float size,ImU32 color,const std::string& text){d->AddText(font,size*scale,pt(a,b),color,text.c_str());};
 if(REXCVAR_GET(mcla_hud_style)=="simple"){
   d->AddRectFilled(pt(128,0),pt(242,32),IM_COL32(12,15,19,237),5*scale);
   label(136,4,21,hot,v[6]+" FPS");
   return;
 }
 static HudLoad load;
 d->AddRectFilled(pt(0,0),pt(242,238),IM_COL32(12,15,19,237),6*scale);
 d->AddRectFilled(pt(0,3),pt(3,235),hot);
 label(12,8,13,muted,"DESEMPENHO   /   DX12");
 label(12,27,30,hot,v[6]);label(12,60,12,muted,"FPS TOTAL");
 label(132,32,22,white,v[7]+" ms");label(132,60,12,muted,"TEMPO DE QUADRO");
 auto meter=[&](float a,const char* name,int percent){
   label(a,82,14,muted,name);label(a+57,82,14,white,percent<0?"--":std::to_string(percent)+"%");
   d->AddRectFilled(pt(a,102),pt(a+100,105),IM_COL32(65,69,74,255));
   if(percent>0)d->AddRectFilled(pt(a,102),pt(a+percent,105),hot);
 };
 meter(12,"CPU",load.cpu.load());meter(132,"GPU 3D",load.gpu.load());
 auto row=[&](float b,const char* name,const std::string& value){label(12,b,13,muted,name);label(96,b,14,white,value);};
 row(117,"FSR",v[2]);row(137,"FRAME GEN",v[3]);row(157,"CENA",v[4]);row(177,"ALVO",v[5]);
 row(217,"BASE",v[0]+" FPS / "+v[1]+" ms");
 row(197,"TELA",std::to_string(int(io.DisplaySize.x*io.DisplayFramebufferScale.x))+" x "+std::to_string(int(io.DisplaySize.y*io.DisplayFramebufferScale.y)));
}
class RadioToast final:public rex::ui::ImGuiDialog {
public:
 explicit RadioToast(rex::ui::ImGuiDrawer* d):ImGuiDialog(d){}
 bool RepaintsContinuously() const override{return rex::cvar::GetFlagByName("mcla_fps_hud")=="true";}
protected:
 void OnDraw(ImGuiIO& io) override {
  if (RepaintsContinuously()) {
    const auto stats = rex::cvar::GetFlagByName("mcla_fps_hud_stats");
    if (!stats.empty()) DrawPerformanceHud(io,stats);
  }
  std::string text;{std::lock_guard lock(state_mutex);if(Clock::now()>=toast_until)return;text=status;}
  if(text.empty())return;
  float width=std::min(440.0f,io.DisplaySize.x-40.0f),size=18.0f;
  auto* font=ImGui::GetFont();auto extent=font->CalcTextSizeA(size,10000,width-24,text.c_str());
  auto* draw=ImGui::GetForegroundDrawList();ImVec2 origin(20,36);
  draw->AddRectFilled(origin,ImVec2(origin.x+width,origin.y+extent.y+24),IM_COL32(10,18,25,225),6);
  draw->AddText(font,size,ImVec2(origin.x+12,origin.y+12),IM_COL32(220,245,238,255),text.c_str(),nullptr,width-24);
 }
};
#ifdef _WIN32
std::jthread radio_thread;
bool Focused() {DWORD pid=0;HWND w=GetForegroundWindow();if(w)GetWindowThreadProcessId(w,&pid);return pid==GetCurrentProcessId();}
std::vector<std::filesystem::path> Tracks() {
 std::vector<std::filesystem::path> result;std::error_code error;
 auto utf8=REXCVAR_GET(mcla_radio_folder);
 auto folder=std::filesystem::path(std::u8string(utf8.begin(),utf8.end()));if(folder.empty())return result;
 if(folder.is_relative())folder=rex::filesystem::GetExecutableFolder()/folder;
 std::filesystem::create_directories(folder,error);
 // Every .mp3 in the folder and in its subfolders.
 for(std::filesystem::recursive_directory_iterator it(folder,std::filesystem::directory_options::skip_permission_denied,error),end;!error&&it!=end;it.increment(error)) {
  if(!it->is_regular_file(error))continue;
  auto ext=it->path().extension().wstring();std::transform(ext.begin(),ext.end(),ext.begin(),::towlower);
  if(ext==L".mp3")result.push_back(it->path());
 }
 std::sort(result.begin(),result.end());return result;
}
bool Send(const std::wstring& command,wchar_t* reply=nullptr,UINT length=0) {
 MCIERROR error=mciSendStringW(command.c_str(),reply,length,nullptr);
 if(error)REXLOG_WARN("MP3: MCI error {}",uint32_t(error));return error==0;
}
}
bool MclaGameRadioActive();  // src/game_radio.cpp
namespace {
void RadioLoop(std::stop_token stop) {
 std::vector<std::filesystem::path> tracks;size_t index=0;
 bool opened=false,paused=false,was_enabled=false;int last_volume=-1;std::string last_folder;
 auto close=[&] {if(opened)Send(L"close mcla_local_radio");opened=false;paused=false;};
 auto info=[&] {
  if(!opened)return;auto name=tracks[index].stem().u8string();
  Toast(std::string(paused?"Rádio MP3 — pausada\n":"Rádio MP3 — tocando\n")+
   std::to_string(index+1)+"/"+std::to_string(tracks.size())+"  "+std::string(name.begin(),name.end())+
   "\nVolume "+std::to_string(REXCVAR_GET(mcla_radio_volume))+"%  |  F9 pausa · F10 próxima");
 };
 auto play=[&](int direction) {
  close();size_t attempts=0;
  while(attempts++<tracks.size()) {
   auto filename=tracks[index].wstring();
   if(filename.find(L'"')==std::wstring::npos&&Send(L"open \""+filename+L"\" type mpegvideo alias mcla_local_radio")) {
    opened=true;last_volume=REXCVAR_GET(mcla_radio_volume);
    Send(L"setaudio mcla_local_radio volume to "+std::to_wstring(last_volume*10));
    if(Send(L"play mcla_local_radio")) {info();REXLOG_INFO("MP3: playing track {}/{}",index+1,tracks.size());return;}
    close();
   }
   index=direction<0?(index+tracks.size()-1)%tracks.size():(index+1)%tracks.size();
  }
  Toast(tracks.empty()?"Rádio MP3: coloque arquivos .mp3 na pasta configurada.":"Rádio MP3: nenhuma faixa pôde ser reproduzida. Confira os arquivos e o registro.");
 };
 try {
  while(!stop.stop_requested()) {
   // With the MP3s in the game radio (src/game_radio.cpp) this separate player stays off.
   bool enabled=REXCVAR_GET(mcla_radio_mp3)&&!MclaGameRadioActive();auto folder=REXCVAR_GET(mcla_radio_folder);
   std::deque<int> pending;{std::lock_guard lock(state_mutex);pending.swap(commands);}
   if(!enabled) {if(was_enabled){close();Toast("Rádio MP3 desligada");}was_enabled=false;}
   else {
    if(!was_enabled||folder!=last_folder||std::find(pending.begin(),pending.end(),3)!=pending.end()) {
     try {tracks=Tracks();}catch(const std::exception& e){tracks.clear();REXLOG_WARN("MP3 folder: {}",e.what());}
     // Starts on a random track, so that it is not always the same one; the next ones follow in order.
     index=tracks.empty()?0:[&]{std::mt19937 generator{std::random_device{}()};return std::uniform_int_distribution<size_t>(0,tracks.size()-1)(generator);}();play(1);last_folder=folder;
    }
    was_enabled=true;
    for(int command:pending) {
     if((command==1||command==2)&&!tracks.empty()) {index=command==1?(index+tracks.size()-1)%tracks.size():(index+1)%tracks.size();play(command==1?-1:1);}
     else if(command==4) {if(opened)info();else Toast("Rádio MP3: nenhuma faixa carregada. Use Reler pasta após adicionar músicas.");}
    }
    if(opened) {
     int volume=REXCVAR_GET(mcla_radio_volume);
     if(volume!=last_volume) {if(Send(L"setaudio mcla_local_radio volume to "+std::to_wstring(volume*10)))last_volume=volume;REXLOG_INFO("MP3: volume {}",volume);info();}
     bool want_pause=REXCVAR_GET(mcla_radio_paused)||(REXCVAR_GET(mcla_radio_pause_unfocused)&&!Focused());
     if(want_pause!=paused&&Send(want_pause?L"pause mcla_local_radio":L"resume mcla_local_radio")) {paused=want_pause;REXLOG_INFO("MP3: {}",paused?"paused":"resumed");info();}
     wchar_t mode[32]{};
     if(!paused&&Send(L"status mcla_local_radio mode",mode,32)&&std::wstring_view(mode)==L"stopped"&&!tracks.empty()) {index=(index+1)%tracks.size();play(1);}
    }
   }
   playing.store(opened && !paused);
   std::this_thread::sleep_for(std::chrono::milliseconds(50));
  }
 } catch(const std::exception& e) {REXLOG_ERROR("MP3: {}",e.what());Toast("Rádio MP3: erro ao ler a pasta. Desligue e confira o caminho.");}
 close();
 playing.store(false);
}
#endif
}

void MclaRadioCommand(int command) {std::lock_guard lock(state_mutex);if(commands.size()<32)commands.push_back(command);}
std::string MclaRadioStatus() {std::lock_guard lock(state_mutex);return status;}
void MclaRadioInstallHotkeys() {
#ifdef _WIN32
 rex::ui::RegisterBind("bind_mcla_radio_pause","F9","MP3 pause; Ctrl+F9 volume down",[] {
  if(!REXCVAR_GET(mcla_radio_mp3))return;
  if(GetAsyncKeyState(VK_CONTROL)&0x8000) {rex::cvar::SetFlagByName("mcla_radio_volume",std::to_string(std::max(0,REXCVAR_GET(mcla_radio_volume)-5)));MclaNoteMenuSettingChanged("mcla_radio_volume");}
  else rex::cvar::SetFlagByName("mcla_radio_paused",REXCVAR_GET(mcla_radio_paused)?"false":"true");
 });
 rex::ui::RegisterBind("bind_mcla_radio_next","F10","MP3 next; Shift+F10 previous; Ctrl+F10 volume up",[] {
  if(!REXCVAR_GET(mcla_radio_mp3))return;
  if(GetAsyncKeyState(VK_CONTROL)&0x8000) {rex::cvar::SetFlagByName("mcla_radio_volume",std::to_string(std::min(100,REXCVAR_GET(mcla_radio_volume)+5)));MclaNoteMenuSettingChanged("mcla_radio_volume");}
  else MclaRadioCommand((GetAsyncKeyState(VK_SHIFT)&0x8000)?1:2);
 });
#endif
}
std::unique_ptr<rex::ui::ImGuiDialog> MclaRadioCreateOverlay(rex::ui::ImGuiDrawer* drawer) {return std::make_unique<RadioToast>(drawer);}
void MclaRadioStart() {
#ifdef _WIN32
 if(!radio_thread.joinable())radio_thread=std::jthread(RadioLoop);
#endif
}
void MclaRadioStop() {
#ifdef _WIN32
 if(radio_thread.joinable()){radio_thread.request_stop();radio_thread.join();}
 rex::ui::UnregisterBind("bind_mcla_radio_pause");rex::ui::UnregisterBind("bind_mcla_radio_next");
#endif
}

// Native Options passes music as category 1 to SetVolume (0x8238C090).
// Gain lives at manager+4*(310+category); +0x3E050 marks settings dirty.
void mcla_radio_music_volume(PPCRegister& r3,PPCRegister& r4,PPCRegister& f1) {
 if(r4.u32!=1)return;
 std::lock_guard lock(audio_mutex);audio_manager=r3.u32;

}
void MclaRadioTick() {
 auto* runtime=rex::Runtime::instance();auto* base=runtime?runtime->virtual_membase():nullptr;if(!base)return;
 std::lock_guard lock(audio_mutex);if(audio_manager<0x10000||audio_manager>0xDFFC0000)return;
 const bool mute=playing.load();
 static auto last_apply=Clock::time_point{};
 const auto now=Clock::now();
 if(mute==music_owned && (!mute || now-last_apply<std::chrono::milliseconds(250)))return;
 // The native audio menu applies each category to the live mixer after
 // setting the options. Category 1 uses the second handle in its table.
 if(!REX_LOAD_U32(0x828D8138))return;
 const auto handle=REX_LOAD_U32(0x8286CED4);
 auto* apply=runtime->function_dispatcher()->GetFunction(0x82547CB0);
 if(!apply || !rex::runtime::ThreadState::Get())return;
 uint32_t bits=REX_LOAD_U32(audio_manager+1244);
 float volume;
 std::memcpy(&volume,&bits,sizeof(volume));
 if(!std::isfinite(volume))return;
 rex::ppc::GuestToHostFunction<void>(apply,handle,mute?0.0f:std::clamp(volume,0.0f,1.0f));
 last_apply=now;
 if(mute!=music_owned)REXLOG_INFO("MP3: live music mixer {}",mute?"muted":"restored");
 music_owned=mute;
}
