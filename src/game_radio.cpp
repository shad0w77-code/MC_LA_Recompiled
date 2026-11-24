// The MP3 folder as songs of the game's own radio. Ported from LARecomp's
// src/mc_engine/mp3custom/mp3custom.cpp (the host-player route; the reverse engineering is theirs).
//
// Every .mp3 in the radio folder (mcla_radio_folder, subfolders included) becomes a row of
// mcMusicManager: it is listed on the ALL station and on its style's station (its ID3 genre, or its
// subfolder), named "song - artist" from its tags; the radio menu and the D-pad select it like a
// shipped song, the shuffle counts it and the HUD shows its name. The game can only play the
// cues banked in music.rpf, so a custom row's audio is played on the host (game_radio_player.cpp) and
// the guest is kept from starting a cue of its own for it. Hook addresses: config/game_radio.toml.
//
// mcMusicManager layout (sub_821F0600 / sub_821EF950 / sub_821EED18):
//   +24          "the RAGE path is not playing" flag; set while a custom row plays, so that the
//                per-frame update (sub_821F0838) doesn't restart the song every frame
//   +32          float, music volume
//   +48          current song index within the current station
//   +52          current station
//   +68          current song entry
//   +88 + 8*g    atArray<songEntry*> of station g: ptr, u16 count, u16 capacity
//   +160 + 24*g  shuffle table of station g; +4 is its song count
// A song entry is { music game object, hash code, char* name key }. The game object is read for four
// fields only (+10 genre, +11 hash unaligned, +19 name length, +20 name), so a guest allocation with
// those fields works; each custom track gets its own with an id the shipped songs can't have.

#include "generated/default/mcla_init.h"

#include "game_radio_player.h"

#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/logging.h>
#include <rex/ppc.h>
#include <rex/runtime.h>
#include <rex/system/function_dispatcher.h>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

REXCVAR_DECLARE(std::string, mcla_radio_folder);
REXCVAR_DECLARE(int32_t, mcla_radio_volume);

REXCVAR_DEFINE_BOOL(mcla_radio_in_game, true, "MCLA",
                    "The MP3 folder's songs join the game's own radio (ALL and CUSTOM PLAYLISTS). "
                    "While on, the separate MP3 player (F9/F10) stays off")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_BOOL(mcla_radio_announce, true, "MCLA",
                    "Show the song name on the HUD strip under the speedometer when the radio changes song");

namespace {

constexpr uint32_t kMusicManagerGlobal = 0x8286D048;
constexpr uint32_t kStringTableGlobal = 0x8286D7FC;
// The music slider of the audio settings (sub_82387B90() + 1244). mgr+32 only holds the real value
// once the audio options screen has been opened.
constexpr uint32_t kSettingsMusicVolume = 0x8288E920 + 1244;

constexpr uint32_t kGuestMallocFn = 0x82130528;  // sysMemAllocator::Allocate
constexpr uint32_t kArrayAppendFn = 0x8262E420;  // atArray::Append(this, grow)
constexpr uint32_t kGotoSongFn = 0x821F0600;     // mcMusicManager::GotoSong(idx)
constexpr uint32_t kPlayFn = 0x821F0108;         // mcMusicManager::Play(flags)
constexpr uint32_t kInfoStripFn = 0x82208930;    // (uiLogic, text, -1, -1, seconds)
constexpr uint32_t kUILogicGlobal = 0x8286D8D4;
constexpr uint32_t kStringLookupFn = 0x82218310;  // (table, key) -> char*
constexpr size_t kNotifyMax = 96;

constexpr uint32_t kMgrNoCue = 24;
constexpr uint32_t kMgrVolume = 32;
constexpr uint32_t kMgrSongIndex = 48;
constexpr uint32_t kMgrGenre = 52;
constexpr uint32_t kMgrCurrentSong = 68;
constexpr uint32_t kMgrGenreArrays = 88;
constexpr uint32_t kMgrShuffles = 160;
constexpr uint32_t kShuffleStride = 24;
constexpr uint32_t kShuffleCount = 4;

constexpr uint32_t kGenreAll = 7;       // MUSIC_GENRE_ALL
constexpr uint32_t kGenreOnTheFly = 8;  // MUSIC_GENRE_ON_THE_FLY, "Custom Playlists"
constexpr uint32_t kStationCount = 9;

constexpr uint32_t kEntrySize = 12;
constexpr uint32_t kObjSize = 64;
constexpr uint32_t kObjGenre = 10;
constexpr uint32_t kObjHash = 11;
constexpr uint32_t kObjNameLen = 19;
constexpr uint32_t kObjName = 20;
constexpr uint32_t kObjNameMax = kObjSize - kObjName - 1;

uint8_t* Membase() {
  auto* runtime = rex::Runtime::instance();
  return runtime ? runtime->virtual_membase() : nullptr;
}

bool IsGuestPtr(uint32_t ea) { return ea >= 0x82000000u && ea < 0xC0000000u; }

uint32_t Read32(uint32_t ea) {
  uint8_t* base = Membase();
  if (!base || !ea) return 0;
  const uint8_t* p = base + ea;
  return (uint32_t(p[0]) << 24) | (uint32_t(p[1]) << 16) | (uint32_t(p[2]) << 8) | p[3];
}

uint16_t Read16(uint32_t ea) {
  uint8_t* base = Membase();
  if (!base || !ea) return 0;
  return uint16_t((uint32_t(base[ea]) << 8) | base[ea + 1]);
}

void Write32(uint32_t ea, uint32_t v) {
  uint8_t* base = Membase();
  if (!base || !ea) return;
  base[ea] = uint8_t(v >> 24);
  base[ea + 1] = uint8_t(v >> 16);
  base[ea + 2] = uint8_t(v >> 8);
  base[ea + 3] = uint8_t(v);
}

float ReadFloat(uint32_t ea) {
  const uint32_t bits = Read32(ea);
  float out;
  std::memcpy(&out, &bits, sizeof out);
  return out;
}

PPCFunc* Guest(uint32_t address) {
  auto* runtime = rex::Runtime::instance();
  return runtime && runtime->function_dispatcher() ? runtime->function_dispatcher()->GetFunction(address)
                                                   : nullptr;
}

uint32_t Call1(uint32_t fn, uint32_t a0) {
  PPCFunc* f = Guest(fn);
  return f ? rex::ppc::GuestToHostFunction<uint32_t>(f, a0) : 0;
}

uint32_t Call2(uint32_t fn, uint32_t a0, uint32_t a1) {
  PPCFunc* f = Guest(fn);
  return f ? rex::ppc::GuestToHostFunction<uint32_t>(f, a0, a1) : 0;
}

std::string ReadString(uint32_t ea, size_t limit) {
  uint8_t* base = Membase();
  if (!base || ea < 0x1000u) return {};
  std::string out;
  for (size_t i = 0; i < limit && base[ea + i]; ++i) out.push_back(char(base[ea + i]));
  return out;
}

uint32_t AllocString(const std::string& text) {
  uint8_t* base = Membase();
  if (!base) return 0;
  const uint32_t length = uint32_t(text.size()) + 1;
  const uint32_t buffer = Call1(kGuestMallocFn, length);
  if (buffer) std::memcpy(base + buffer, text.c_str(), length);
  return buffer;
}

// atStringHash (sub_821C9790): one-at-a-time over the lower-cased string, '\' as '/', the character
// sign-extended as the guest does.
uint32_t HashString(const char* text) {
  uint32_t h = 0;
  for (const char* p = text; *p; ++p) {
    char c = *p;
    if (c >= 'A' && c <= 'Z') c += 32;
    else if (c == '\\') c = '/';
    h = (h + uint32_t(int32_t(c))) * 1025u;
    h ^= h >> 6;
  }
  h *= 9u;
  h ^= h >> 11;
  h *= 32769u;
  return h;
}

// The string table's map (sub_82218268): bucket array at +0, u16 bucket count at +4, nodes
// {hash, value, next}, pushed at the head of the bucket.
bool InsertString(uint32_t map, uint32_t hash, uint32_t value) {
  const uint32_t buckets = Read32(map);
  const uint16_t count = Read16(map + 4);
  if (!count || !buckets) return false;
  const uint32_t node = Call1(kGuestMallocFn, kEntrySize);
  if (!node) return false;
  const uint32_t bucket = hash % count;
  Write32(node, hash);
  Write32(node + 4, value);
  Write32(node + 8, Read32(buckets + bucket * 4));
  Write32(buckets + bucket * 4, node);
  return true;
}

struct Track {
  std::filesystem::path path;
  std::string title;   // "song - artist" (or the file name), as the radio list shows it
  uint32_t genre = 0;  // the station besides ALL (MUSIC_GENRE_*)
  uint32_t id = 0;     // the song's hash code, from its path in the folder: a playlist keeps it
  uint32_t entry = 0;
  uint32_t title_ea = 0;
};

std::vector<Track> g_tracks;
bool g_installed = false;
bool g_strings_registered = false;
bool g_player_ready = false;
int g_current = -1;  // index into g_tracks; -1 when a shipped song (or nothing) plays

// The game's fonts carry ASCII and the Latin-1 letters of its languages; an accented letter is
// written without its accent, anything else as a space, so the row always renders.
std::string RowTitle(const std::wstring& name) {
  static const wchar_t kFrom[] =
      L"ÀÁÂÃÄÅàáâãäåÈÉÊË"
      L"èéêëÌÍÎÏìíîïÒÓÔÕ"
      L"ÖòóôõöÙÚÛÜùúûüÇç"
      L"ÑñÝýÿ";
  static const char kTo[] = "AAAAAAaaaaaaEEEEeeeeIIIIiiiiOOOOOoooooUUUUuuuuCcNnYyy";
  std::string out;
  for (wchar_t c : name) {
    if (!c) break;
    if (c >= 0x20 && c < 0x7f) {
      out.push_back(char(c));
    } else if (const wchar_t* at = std::wcschr(kFrom, c)) {
      out.push_back(kTo[at - kFrom]);
    } else {
      out.push_back(' ');
    }
  }
  const size_t first = out.find_first_not_of(' ');
  out = first == std::string::npos ? std::string{} : out.substr(first, out.find_last_not_of(' ') - first + 1);
  if (out.size() > 100) out.resize(100);  // the string table copies into a 132-byte buffer
  // Upper case, as the game's own song names are written.
  std::transform(out.begin(), out.end(), out.begin(), [](char c) { return char(std::toupper(uint8_t(c))); });
  return out;
}

// --- ID3 tags ------------------------------------------------------------------

struct Tags {
  std::wstring title, artist, genre;
};

std::wstring Latin1(const uint8_t* p, size_t n) {
  std::wstring out;
  for (size_t i = 0; i < n && p[i]; ++i) out.push_back(wchar_t(p[i]));
  return out;
}

std::wstring Utf16(const uint8_t* p, size_t n, bool big_endian) {
  std::wstring out;
  for (size_t i = 0; i + 1 < n; i += 2) {
    const wchar_t c = big_endian ? wchar_t((p[i] << 8) | p[i + 1]) : wchar_t(p[i] | (p[i + 1] << 8));
    if (!c) break;
    out.push_back(c);
  }
  return out;
}

std::wstring Utf8(const uint8_t* p, size_t n) {
  size_t length = 0;
  while (length < n && p[length]) ++length;
  if (!length) return {};
  const int wide = MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(p), int(length), nullptr, 0);
  if (wide <= 0) return {};
  std::wstring out(size_t(wide), L'\0');
  MultiByteToWideChar(CP_UTF8, 0, reinterpret_cast<const char*>(p), int(length), out.data(), wide);
  return out;
}

// A text frame: an encoding byte, then the text (ID3v2.4 may list several values, NUL-separated;
// the first one is kept).
std::wstring FrameText(const uint8_t* p, size_t n) {
  if (n < 2) return {};
  const uint8_t encoding = p[0];
  ++p;
  --n;
  switch (encoding) {
    case 1:
      if (n >= 2 && p[0] == 0xFE && p[1] == 0xFF) return Utf16(p + 2, n - 2, true);
      if (n >= 2 && p[0] == 0xFF && p[1] == 0xFE) return Utf16(p + 2, n - 2, false);
      return Utf16(p, n, false);
    case 2: return Utf16(p, n, true);
    case 3: return Utf8(p, n);
    default: return Latin1(p, n);
  }
}

uint32_t Syncsafe(const uint8_t* p) {
  return (uint32_t(p[0]) << 21) | (uint32_t(p[1]) << 14) | (uint32_t(p[2]) << 7) | p[3];
}

uint32_t Big32(const uint8_t* p) {
  return (uint32_t(p[0]) << 24) | (uint32_t(p[1]) << 16) | (uint32_t(p[2]) << 8) | p[3];
}

Tags ReadTags(const std::filesystem::path& path) {
  Tags tags;
  std::ifstream file(path, std::ios::binary);
  if (!file) return tags;
  uint8_t header[10] = {};
  if (file.read(reinterpret_cast<char*>(header), 10) && !std::memcmp(header, "ID3", 3) && header[3] >= 2 &&
      header[3] <= 4) {
    const uint8_t version = header[3];
    const uint32_t size = std::min<uint32_t>(Syncsafe(header + 6), 4u << 20);
    std::vector<uint8_t> data(size);
    file.read(reinterpret_cast<char*>(data.data()), size);
    data.resize(size_t(file.gcount()));
    size_t pos = 0;
    if ((header[5] & 0x40) && version >= 3 && data.size() >= 4) {  // an extended header
      pos = version == 4 ? Syncsafe(data.data()) : Big32(data.data()) + 4;
    }
    const size_t id_length = version == 2 ? 3 : 4;
    const size_t frame_header = version == 2 ? 6 : 10;
    while (pos + frame_header <= data.size() && data[pos]) {
      const uint8_t* f = data.data() + pos;
      const uint32_t frame_size = version == 2   ? (uint32_t(f[3]) << 16) | (uint32_t(f[4]) << 8) | f[5]
                                  : version == 4 ? Syncsafe(f + 4)
                                                 : Big32(f + 4);
      if (frame_size == 0 || pos + frame_header + frame_size > data.size()) break;
      const std::string id(reinterpret_cast<const char*>(f), id_length);
      const uint8_t* body = f + frame_header;
      if (id == "TIT2" || id == "TT2") tags.title = FrameText(body, frame_size);
      else if (id == "TPE1" || id == "TP1") tags.artist = FrameText(body, frame_size);
      else if (id == "TCON" || id == "TCO") tags.genre = FrameText(body, frame_size);
      pos += frame_header + frame_size;
    }
  }
  if (tags.title.empty() || tags.artist.empty()) {  // ID3v1, at the end of the file
    uint8_t v1[128] = {};
    file.clear();
    file.seekg(-128, std::ios::end);
    if (file.read(reinterpret_cast<char*>(v1), 128) && !std::memcmp(v1, "TAG", 3)) {
      if (tags.title.empty()) tags.title = Latin1(v1 + 3, 30);
      if (tags.artist.empty()) tags.artist = Latin1(v1 + 33, 30);
      if (tags.genre.empty() && v1[127] != 0xFF) tags.genre = L"(" + std::to_wstring(v1[127]) + L")";
    }
  }
  return tags;
}

// The game's stations: 0 ECLECTIC, 1 ELECTRONIC, 2 HARD ROCK, 3 HIP HOP, 4 ROCK, 5 TECHNO, 6 WEST
// COAST RAP. The genre tag (or, without one, the subfolder's name) picks one by its words, an ID3v1
// number by the standard list. Anything else goes to ECLECTIC.
uint32_t Station(std::wstring text) {
  std::transform(text.begin(), text.end(), text.begin(), [](wchar_t c) { return wchar_t(std::towlower(c)); });
  if (text.size() > 2 && text[0] == L'(' && std::iswdigit(text[1])) {
    switch (_wtoi(text.c_str() + 1)) {
      case 7: case 15: case 16: case 42: case 61: return 3;              // hip-hop, rap, reggae, soul
      case 9: case 22: case 43: case 79: case 129: case 137: case 138: case 144: return 2;  // metal, punk
      case 1: case 17: case 20: case 40: case 47: case 56: case 81: case 94: return 4;      // rock
      case 18: case 31: case 34: case 35: case 66: case 76: case 127: return 5;            // techno, house
      case 26: case 30: case 39: case 51: case 52: case 54: case 115: return 1;            // electronic
      default: break;
    }
    const size_t close = text.find(L')');
    text = close == std::wstring::npos ? std::wstring{} : text.substr(close + 1);
  }
  auto has = [&](const wchar_t* word) { return text.find(word) != std::wstring::npos; };
  if (has(L"west coast") || has(L"g-funk") || has(L"gangsta")) return 6;
  if (has(L"hip") || has(L"rap") || has(L"trap") || has(L"r&b") || has(L"rnb") || has(L"funk") ||
      has(L"reggae") || has(L"soul")) return 3;
  if (has(L"metal") || has(L"hard") || has(L"punk") || has(L"grunge") || has(L"core")) return 2;
  if (has(L"techno") || has(L"house") || has(L"trance") || has(L"dubstep") || has(L"drum") || has(L"edm") ||
      has(L"club")) return 5;
  if (has(L"electr") || has(L"synth") || has(L"dance") || has(L"ambient") || has(L"chill") || has(L"lo-fi") ||
      has(L"lofi")) return 1;
  if (has(L"rock") || has(L"alternativ") || has(L"indie")) return 4;
  return 0;
}

uint32_t PathHash(const std::wstring& text) {
  uint32_t h = 2166136261u;
  for (wchar_t c : text) {
    h ^= uint32_t(std::towlower(c));
    h *= 16777619u;
  }
  return h;
}

void CollectTracks() {
  const std::string utf8 = REXCVAR_GET(mcla_radio_folder);
  std::filesystem::path folder(std::u8string(utf8.begin(), utf8.end()));
  if (folder.empty()) return;
  if (folder.is_relative()) folder = rex::filesystem::GetExecutableFolder() / folder;
  std::error_code error;
  for (std::filesystem::recursive_directory_iterator
           it(folder, std::filesystem::directory_options::skip_permission_denied, error),
       end;
       !error && it != end; it.increment(error)) {
    if (!it->is_regular_file(error)) continue;
    std::wstring ext = it->path().extension().wstring();
    std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t c) { return wchar_t(std::towlower(c)); });
    if (ext != L".mp3") continue;
    const Tags tags = ReadTags(it->path());
    std::string title = RowTitle(tags.title);
    const std::string artist = RowTitle(tags.artist);
    if (title.empty()) title = RowTitle(it->path().stem().wstring());
    if (title.empty()) continue;
    if (!artist.empty() && title.find(artist) == std::string::npos) title += " - " + artist;
    Track track;
    track.path = it->path();
    track.title = title.substr(0, 100);
    std::error_code relative_error;
    std::wstring style = tags.genre;
    if (style.empty()) {
      const auto parent = std::filesystem::relative(it->path().parent_path(), folder, relative_error);
      if (!relative_error && parent != L".") style = parent.wstring();
    }
    track.genre = Station(style);
    const auto relative = std::filesystem::relative(it->path(), folder, relative_error);
    track.id = 0xC0000000u | (PathHash(relative_error ? it->path().wstring() : relative.wstring()) & 0x3FFFFFFFu);
    g_tracks.push_back(std::move(track));
  }
  std::sort(g_tracks.begin(), g_tracks.end(), [](const Track& a, const Track& b) { return a.title < b.title; });
  // Two files with the same row text would share a string-table key: number the repeats.
  for (size_t i = 1, repeat = 1; i < g_tracks.size(); ++i) {
    if (g_tracks[i].title == g_tracks[i - repeat].title) {
      g_tracks[i].title += " " + std::to_string(++repeat);
    } else {
      repeat = 1;
    }
  }
}

uint32_t GenreArray(uint32_t mgr, uint32_t genre) { return mgr + kMgrGenreArrays + 8 * genre; }

bool AppendToGenre(uint32_t mgr, uint32_t genre, uint32_t entry) {
  const uint32_t slot = Call2(kArrayAppendFn, GenreArray(mgr, genre), 16);
  if (!IsGuestPtr(slot)) return false;
  Write32(slot, entry);
  return true;
}

void InstallTracks(uint32_t mgr) {
  if (g_installed || !IsGuestPtr(mgr)) return;
  g_installed = true;
  if (!REXCVAR_GET(mcla_radio_in_game)) return;
  CollectTracks();
  if (g_tracks.empty()) {
    REXLOG_INFO("game radio: no .mp3 in the radio folder");
    return;
  }
  g_player_ready = mcla_game_radio::PlayerInit();
  if (!g_player_ready) {
    REXLOG_WARN("game radio: the host player isn't available; the MP3s stay out of the radio");
    g_tracks.clear();
    return;
  }
  if (!IsGuestPtr(Read32(GenreArray(mgr, kGenreAll))) || !Read16(GenreArray(mgr, kGenreAll) + 4)) {
    REXLOG_WARN("game radio: the ALL station is empty at install time");
    g_tracks.clear();
    return;
  }
  uint8_t* base = Membase();
  size_t added = 0;
  for (size_t i = 0; i < g_tracks.size(); ++i) {
    Track& track = g_tracks[i];
    const uint32_t title = AllocString(track.title);
    const uint32_t entry = Call1(kGuestMallocFn, kEntrySize);
    const uint32_t object = Call1(kGuestMallocFn, kObjSize);
    if (!IsGuestPtr(title) || !IsGuestPtr(entry) || !IsGuestPtr(object) || !base) {
      REXLOG_WARN("game radio: guest allocation failed at '{}'", track.title);
      break;
    }
    const uint32_t id = track.id;  // shipped songs have other ids
    std::memset(base + object, 0, kObjSize);
    base[object + kObjGenre] = uint8_t(track.genre);
    Write32(object + kObjHash, id);
    std::string name = track.title.substr(0, kObjNameMax);
    base[object + kObjNameLen] = uint8_t(name.size());
    std::memcpy(base + object + kObjName, name.data(), name.size());
    Write32(entry, object);
    Write32(entry + 4, id);
    Write32(entry + 8, title);
    track.entry = entry;
    track.title_ea = title;
    // Its style's station and ALL; CUSTOM PLAYLISTS is the player's own list, filled from the menu.
    if (!AppendToGenre(mgr, kGenreAll, entry) || !AppendToGenre(mgr, track.genre, entry)) {
      REXLOG_WARN("game radio: couldn't add '{}' to the stations", track.title);
      track.entry = 0;
      break;
    }
    ++added;
  }
  uint32_t per_station[7] = {};
  for (const Track& track : g_tracks) {
    if (track.entry && track.genre < 7) ++per_station[track.genre];
  }
  REXLOG_INFO("game radio: {} MP3 song(s) added to ALL; by station: eclectic {}, electronic {}, hard rock {}, "
              "hip hop {}, rock {}, techno {}, west coast rap {}",
              added, per_station[0], per_station[1], per_station[2], per_station[3], per_station[4], per_station[5],
              per_station[6]);
}

// A row's text goes through the string table; a key it doesn't have renders as "!!key!!". The table
// is not there yet when the manager is built, so this is retried from the frame tick.
void RegisterStrings() {
  if (g_strings_registered || g_tracks.empty()) return;
  const uint32_t table = Read32(kStringTableGlobal);
  if (!IsGuestPtr(table)) return;
  size_t done = 0;
  for (const Track& track : g_tracks) {
    if (track.title_ea && InsertString(table + 16, HashString(track.title.c_str()), track.title_ea)) ++done;
  }
  if (done) {
    g_strings_registered = true;
    REXLOG_INFO("game radio: {} song name(s) in the string table", done);
  }
}

// The game's music slider (squared: XAudio2 is linear, the game's mixer a taper), times the MP3
// volume of the radio menu.
float MusicVolume(uint32_t mgr) {
  float v = ReadFloat(kSettingsMusicVolume);
  if (!(v >= 0.0f && v <= 1.0f)) v = ReadFloat(mgr + kMgrVolume);
  if (!(v >= 0.0f && v <= 1.0f)) v = 1.0f;
  const float trim = std::clamp(float(REXCVAR_GET(mcla_radio_volume)) / 100.0f, 0.0f, 1.0f);
  return v * v * trim;
}

int TrackForEntry(uint32_t entry) {
  if (!entry) return -1;
  for (size_t i = 0; i < g_tracks.size(); ++i) {
    if (g_tracks[i].entry == entry) return int(i);
  }
  return -1;
}

uint32_t g_notify_buffer = 0;
uint32_t g_notified_key = 0;
bool g_notify_primed = false;

void Announce(uint32_t key) {
  if (!REXCVAR_GET(mcla_radio_announce) || !IsGuestPtr(key) || key == g_notified_key) return;
  if (!g_notify_primed) {  // the song chosen at start-up: nobody is looking at the HUD yet
    g_notify_primed = true;
    g_notified_key = key;
    return;
  }
  const uint32_t ui = Read32(kUILogicGlobal);
  if (!IsGuestPtr(ui)) return;
  const uint32_t table = Read32(kStringTableGlobal);
  uint32_t text = IsGuestPtr(table) ? Call2(kStringLookupFn, table, key) : 0;
  if (!IsGuestPtr(text)) text = key;
  std::string title = ReadString(text, kNotifyMax);
  if (title.empty()) return;
  std::string line = "TOCANDO: " + title;
  if (line.size() > kNotifyMax) line.resize(kNotifyMax);
  if (!g_notify_buffer) {
    g_notify_buffer = Call1(kGuestMallocFn, uint32_t(kNotifyMax) + 1);
    if (!IsGuestPtr(g_notify_buffer)) {
      g_notify_buffer = 0;
      return;
    }
  }
  uint8_t* base = Membase();
  if (!base) return;
  std::memcpy(base + g_notify_buffer, line.c_str(), line.size() + 1);
  g_notified_key = key;
  if (PPCFunc* strip = Guest(kInfoStripFn)) {
    rex::ppc::GuestToHostFunction<uint32_t>(strip, ui, g_notify_buffer, uint32_t(-1), uint32_t(-1), 8.0f);
  }
}

}  // namespace

bool MclaGameRadioActive() { return REXCVAR_GET(mcla_radio_in_game); }

// Per frame (src/frame_timing.cpp): song names, volume, and the next song when an MP3 ends.
void MclaGameRadioTick() {
  if (g_tracks.empty()) return;
  const uint32_t mgr = Read32(kMusicManagerGlobal);
  if (!IsGuestPtr(mgr)) return;
  RegisterStrings();
  mcla_game_radio::PlayerSetVolume(MusicVolume(mgr));
  if (g_current < 0 || !mcla_game_radio::PlayerTrackEnded()) return;
  const uint32_t genre = Read32(mgr + kMgrGenre);
  const uint16_t count = genre < kStationCount ? Read16(GenreArray(mgr, genre) + 4) : 0;
  g_current = -1;
  if (count) {
    Call2(kGotoSongFn, mgr, (Read32(mgr + kMgrSongIndex) + 1) % count);
    Call2(kPlayFn, mgr, 0);
  } else {
    mcla_game_radio::PlayerStop();
  }
}

// 0x821EFF4C, the end of the mcMusicManager constructor: both AddSong passes have run, the shuffle
// tables are about to be built.
void mcla_game_radio_install(PPCRegister& r31) { InstallTracks(r31.u32); }

// 0x821EFB5C in StartCurrentSong, the call that starts the current song's cue. True skips it.
bool mcla_game_radio_start(PPCRegister& r31) {
  const uint32_t mgr = r31.u32;
  const int index = TrackForEntry(Read32(mgr + kMgrCurrentSong));
  if (index < 0) {
    if (g_current >= 0) {
      g_current = -1;
      mcla_game_radio::PlayerStop();
    }
    Write32(mgr + kMgrNoCue, 0);
    return false;
  }
  Write32(mgr + kMgrNoCue, 1);
  if (g_current == index && mcla_game_radio::PlayerIsPlaying()) {
    mcla_game_radio::PlayerPause(false);  // a resume, or the manager asking again
    return true;
  }
  g_current = index;
  mcla_game_radio::PlayerSetVolume(MusicVolume(mgr));
  mcla_game_radio::PlayerPlayFile(g_tracks[size_t(index)].path.wstring());
  REXLOG_INFO("game radio: playing '{}'", g_tracks[size_t(index)].title);
  // The D-pad steps the station without GotoSong, and the game's own name on the HUD waits for a
  // cue a custom row never has: show the name here (Announce skips one already on the strip).
  Announce(g_tracks[size_t(index)].title_ea);
  return true;
}

// 0x821F0600, GotoSong: every song change. A shipped song after a custom one takes the cue path again.
void mcla_game_radio_select(PPCRegister& r3) {
  if (IsGuestPtr(r3.u32)) Write32(r3.u32 + kMgrNoCue, 0);
}

// 0x821F06F0, the end of GotoSong: r4 is the new song's name key.
void mcla_game_radio_announce(PPCRegister& r4) { Announce(r4.u32); }

// 0x821EF1F8, mcMusicManager::Stop: every teardown of the radio (the garage among them).
void mcla_game_radio_stop(PPCRegister& r3) {
  if (g_current < 0) return;
  g_current = -1;
  mcla_game_radio::PlayerStop();
  if (IsGuestPtr(r3.u32)) Write32(r3.u32 + kMgrNoCue, 0);
}

// 0x821EF158, mcMusicManager::Pause. Resumed through StartCurrentSong.
void mcla_game_radio_pause(PPCRegister& r3) {
  (void)r3;
  if (g_current >= 0) mcla_game_radio::PlayerPause(true);
}

// mcMusicManager::SetGenre(this, station). A save's station or playlist can name songs this machine
// doesn't have (other MP3s, a changed folder), and SetGenre stops the game on a station with no songs.
// Such a station plays ALL instead (or any station with songs); the save keeps its own setting.
extern "C" REX_FUNC(sub_821EEC68) {
  const uint32_t mgr = ctx.r3.u32;
  const int32_t station = int32_t(ctx.r4.u32);
  if (IsGuestPtr(mgr)) {
    auto songs = [&](uint32_t s) { return int32_t(Read32(mgr + kMgrShuffles + kShuffleStride * s + kShuffleCount)); };
    const bool valid = station >= 0 && station < int32_t(kStationCount);
    if (!valid || songs(uint32_t(station)) <= 0) {
      uint32_t stand_in = songs(kGenreAll) > 0 ? kGenreAll : kStationCount;
      for (uint32_t s = 0; stand_in == kStationCount && s < kStationCount; ++s) {
        if (songs(s) > 0) stand_in = s;
      }
      if (stand_in < kStationCount) {
        REXLOG_WARN("game radio: station {} has no songs here; playing station {} instead", station, stand_in);
        ctx.r4.u64 = stand_in;
      }
    }
  }
  __imp__sub_821EEC68(ctx, base);
}
