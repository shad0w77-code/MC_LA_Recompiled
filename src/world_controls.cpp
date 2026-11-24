// Local world/camera controls. Guest entry points verified against default.xex.
// Camera manager/gate RE reference: mzzvxm/LARecomp, hooks/camera.cpp.
// This implementation uses keyboard input without capturing/recentering the
// system cursor, so Alt+Tab cannot leave a confined or captured mouse behind.
#include "generated/default/mcla_init.h"
#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <ctime>
#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/runtime.h>
#include <rex/ui/window.h>
#if REX_PLATFORM_WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

REXCVAR_DEFINE_BOOL(mcla_free_camera, false, "MCLA", "Free camera: F8 toggle, numpad 8/2/4/6 move, 9/3 height, IJKL or right mouse drag to look, Shift faster, Ctrl slower");
REXCVAR_DEFINE_DOUBLE(mcla_free_camera_speed, 20.0, "MCLA", "Free camera speed in world units/second").range(1.0, 200.0);
REXCVAR_DEFINE_DOUBLE(mcla_free_camera_sensitivity, 1.5, "MCLA", "Free camera look speed, radians/second").range(0.2, 5.0);
REXCVAR_DEFINE_DOUBLE(mcla_time_hour, -1.0, "MCLA", "World hour 0..23.75; -1 restores automatic time").range(-1.0, 23.75);
REXCVAR_DEFINE_BOOL(mcla_time_realtime, false, "MCLA", "The world hour follows this PC's clock");
REXCVAR_DEFINE_DOUBLE(mcla_time_speed, 1.0, "MCLA", "Speed of the day and night cycle (1 = as the game runs it, 0 stops it)").range(0.0, 20.0);
REXCVAR_DEFINE_BOOL(mcla_time_hold, true, "MCLA", "Hold the selected hour; off resumes the day/night cycle");
REXCVAR_DEFINE_STRING(mcla_weather, "auto", "MCLA", "World weather override").allowed({"auto", "clear", "cloudy", "storm", "fog"});

namespace {
uint8_t* Base() {auto* r=rex::Runtime::instance();return r?r->virtual_membase():nullptr;}
bool Pointer(uint32_t p) {return p>=0x10000 && p<=0xDFFF0000 && !(p&3);}
uint32_t Word(uint8_t* b,uint32_t a) {return uint32_t(b[a])<<24|uint32_t(b[a+1])<<16|uint32_t(b[a+2])<<8|b[a+3];}
void Word(uint8_t* b,uint32_t a,uint32_t v) {b[a]=uint8_t(v>>24);b[a+1]=uint8_t(v>>16);b[a+2]=uint8_t(v>>8);b[a+3]=uint8_t(v);}
float Number(uint8_t* b,uint32_t a) {uint32_t v=Word(b,a);float f;std::memcpy(&f,&v,4);return f;}
void Number(uint8_t* b,uint32_t a,float f) {uint32_t v;std::memcpy(&v,&f,4);Word(b,a,v);}
bool Focused() {
#if REX_PLATFORM_WIN32
 auto* r=rex::Runtime::instance();auto* w=r?r->display_window():nullptr;
 return w && GetForegroundWindow()==static_cast<HWND>(w->GetNativeWindowHandle());
#else
 return false;
#endif
}
bool Key(int key) {
#if REX_PLATFORM_WIN32
 return (GetAsyncKeyState(key)&0x8000)!=0;
#else
 return false;
#endif
}
uint32_t camera_manager=0,old_index=0,old_input=0;
uint8_t old_update=0;
float old_blend=0;
auto previous_tick=std::chrono::steady_clock::now();
bool previous_f8=false;
void ReleaseCamera(uint8_t* base) {
 if(!camera_manager)return;
 Word(base,camera_manager,old_index);
 base[camera_manager+856]=old_update;
 Number(base,camera_manager+860,old_blend);
 uint32_t camera=Word(base,camera_manager+8);
 if(Pointer(camera))Word(base,camera+0x68,old_input);
 REXLOG_INFO("world controls: free camera released");camera_manager=0;
}
}

bool mcla_free_camera_gate() {
#if REX_PLATFORM_WIN32
 bool down=Focused()&&Key(VK_F8);
 if(down&&!previous_f8)rex::cvar::SetFlagByName("mcla_free_camera",REXCVAR_GET(mcla_free_camera)?"false":"true");
 previous_f8=down;
#endif
 // One last manager tick is necessary to restore its original state.
 return REXCVAR_GET(mcla_free_camera)||camera_manager!=0;
}

void mcla_free_camera_update(PPCRegister& r3, PPCRegister& r1) {
 auto* base=Base();uint32_t manager=r3.u32;
 if(!base||!Pointer(manager))return;
 if(!REXCVAR_GET(mcla_free_camera)) {ReleaseCamera(base);return;}
 uint32_t camera=Word(base,manager+8);
 if(!Pointer(camera))return;
 // A new scene can replace/free the old manager. Never write back through a
 // pointer retained from that scene; only restore while its owner is live.
 if(camera_manager && camera_manager!=manager)camera_manager=0;
 if(!camera_manager) {
  camera_manager=manager;old_index=Word(base,manager);old_update=base[manager+856];old_blend=Number(base,manager+860);
  old_input=Word(base,camera+0x68);
  // The camera system has already assembled the live camera matrix at
  // stack+96. Start at that pose instead of the developer camera's old origin.
  if(Pointer(r1.u32)) {
   float x=Number(base,r1.u32+144),y=Number(base,r1.u32+148),z=Number(base,r1.u32+152);
   float fx=Number(base,r1.u32+128),fy=Number(base,r1.u32+132),fz=Number(base,r1.u32+136);
   if(std::isfinite(x)&&std::isfinite(y)&&std::isfinite(z)&&
      std::isfinite(fx)&&std::isfinite(fy)&&std::isfinite(fz)) {
    Number(base,camera+0x50,x);Number(base,camera+0x54,y);Number(base,camera+0x58,z);
    Number(base,camera+0x3C,std::atan2(fx,fz));Number(base,camera+0x40,std::asin(std::clamp(fy,-1.0f,1.0f)));
   }
  }
  previous_tick=std::chrono::steady_clock::now();
  REXLOG_INFO("world controls: free camera active; F8 returns to driving (manager 0x{:08X})",manager);
 }
 Word(base,manager,0);base[manager+856]=1;Number(base,manager+860,1.0f);Word(base,camera+0x68,0);
 auto now=std::chrono::steady_clock::now();
 float dt=std::clamp(std::chrono::duration<float>(now-previous_tick).count(),0.0f,0.05f);previous_tick=now;
 static bool mouse_drag=false;
 if(!Focused()) {mouse_drag=false;return;}
 float yaw=Number(base,camera+0x3C),pitch=Number(base,camera+0x40);
 float x=Number(base,camera+0x50),y=Number(base,camera+0x54),z=Number(base,camera+0x58);
 if(!std::isfinite(yaw)||!std::isfinite(pitch)||!std::isfinite(x)||!std::isfinite(y)||!std::isfinite(z))return;
 float sensitivity=float(REXCVAR_GET(mcla_free_camera_sensitivity));
 yaw+=(float(Key('J'))-float(Key('L')))*sensitivity*dt;
 pitch=std::clamp(pitch+(float(Key('K'))-float(Key('I')))*sensitivity*dt,-1.5f,1.5f);
#if REX_PLATFORM_WIN32
 static POINT previous_mouse{};POINT mouse{};
 if(Key(VK_RBUTTON)&&GetCursorPos(&mouse)) {
  if(mouse_drag) {yaw-=float(mouse.x-previous_mouse.x)*0.0025f;pitch+=float(mouse.y-previous_mouse.y)*0.0025f;}
  previous_mouse=mouse;mouse_drag=true;
 } else mouse_drag=false;
 pitch=std::clamp(pitch,-1.5f,1.5f);
#endif
 Number(base,camera+0x38,0);Number(base,camera+0x3C,yaw);Number(base,camera+0x40,pitch);
 float forward=0,side=0,up=0;
#if REX_PLATFORM_WIN32
 forward=float(Key(VK_NUMPAD2))-float(Key(VK_NUMPAD8));
 side=float(Key(VK_NUMPAD6))-float(Key(VK_NUMPAD4));
 up=float(Key(VK_NUMPAD9))-float(Key(VK_NUMPAD3));
#endif
 float norm=std::max(1.0f,std::sqrt(forward*forward+side*side+up*up));
 float speed=float(REXCVAR_GET(mcla_free_camera_speed));
#if REX_PLATFORM_WIN32
 if(Key(VK_SHIFT))speed*=4;if(Key(VK_CONTROL))speed*=0.25f;
#endif
 float step=speed*dt/norm;
 Number(base,camera+0x50,x+(std::cos(pitch)*std::sin(yaw)*forward+std::cos(yaw)*side)*step);
 Number(base,camera+0x54,y+(std::sin(pitch)*forward+up)*step);
 Number(base,camera+0x58,z+(std::cos(pitch)*std::cos(yaw)*forward-std::sin(yaw)*side)*step);
}

void mcla_world_update(PPCRegister& r3) {
 auto* base=Base();uint32_t manager=r3.u32;if(!base||!Pointer(manager))return;
 static uint32_t last_manager=0;
 static bool time_owned=false,weather_owned=false,last_realtime=false;
 static uint8_t saved_time_auto=1,saved_weather_auto=1;
 static float last_hour=-1;
 static std::string last_weather;
 if(manager!=last_manager) {last_manager=manager;time_owned=false;last_realtime=false;last_hour=-1;}
 float hour=float(REXCVAR_GET(mcla_time_hour));bool hold=REXCVAR_GET(mcla_time_hold);
 bool realtime=REXCVAR_GET(mcla_time_realtime);
 if(realtime) {
  std::time_t now=std::time(nullptr);std::tm local{};
#if REX_PLATFORM_WIN32
  localtime_s(&local,&now);
#else
  localtime_r(&now,&local);
#endif
  hour=float(local.tm_hour)+float(local.tm_min)/60.0f+float(local.tm_sec)/3600.0f;hold=true;
 }
 if(hour>=0) {
  if(!time_owned) {saved_time_auto=base[manager+14545];time_owned=true;}
  if(hour!=last_hour || hold) {
   // Cancel a pending scripted warp and keep all three lighting buffers in sync.
   for(uint32_t offset:{14532u,14536u,14540u})Number(base,manager+offset,hour);
   base[manager+14552]=0;
  }
  base[manager+14545]=hold?0:saved_time_auto;
  if(realtime) {
   if(!last_realtime)REXLOG_INFO("world controls: real clock active (hour {:.2f})",hour);
  } else if(hour!=last_hour || last_realtime) {
   REXLOG_INFO("world controls: hour {} (hold {})",hour,hold);
  }
  last_realtime=realtime;
  last_hour=hour;
 } else {
  last_realtime=false;
  if(time_owned) {base[manager+14545]=saved_time_auto;time_owned=false;last_hour=-1;REXLOG_INFO("world controls: automatic time restored");}
  // Rescale the game's own cycle: measure how far it moved since the last frame and add the
  // missing share (speed 0 removes the step, so the day stops).
  static float previous=-1;
  const double speed=REXCVAR_GET(mcla_time_speed);
  float now_hour=Number(base,manager+14540);
  if(speed!=1.0 && previous>=0) {
   float delta=now_hour-previous;if(delta<0)delta+=24.0f;
   if(delta>0 && delta<1.0f) {
    float advanced=now_hour+float(delta*(speed-1.0));
    while(advanced>=24.0f)advanced-=24.0f;while(advanced<0)advanced+=24.0f;
    Number(base,manager+14540,advanced);now_hour=advanced;
   }
  }
  previous=now_hour;
 }
 const auto weather=REXCVAR_GET(mcla_weather);
 if(weather!="auto") {
  if(!weather_owned) {saved_weather_auto=base[0x8287E138];weather_owned=true;}
  int index=weather=="clear"?0:weather=="cloudy"?1:weather=="storm"?2:3;
  base[0x8287E138]=0;Word(base,0x8287E134,index);
  // Both render buffers plus the game's published blend weights.
  for(uint32_t buffer=0;buffer<3;++buffer)
   for(uint32_t i=0;i<4;++i)Number(base,0x827E1B10+buffer*16+i*4,i==uint32_t(index)?1.0f:0.0f);
  if(weather!=last_weather)REXLOG_INFO("world controls: weather {}",weather);
 } else if(weather_owned) {base[0x8287E138]=saved_weather_auto;weather_owned=false;REXLOG_INFO("world controls: automatic weather restored");}
 last_weather=weather;
}
