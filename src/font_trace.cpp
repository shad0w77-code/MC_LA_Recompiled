// Opt-in, read-only diagnostics for the mission summary title.
#include "generated/default/mcla_init.h"
#include <rex/cvar.h>
#include <rex/logging.h>
#include <mutex>
#include <set>
#include <string>
#include <tuple>

REXCVAR_DEFINE_BOOL(mcla_font_trace, false, "MCLA", "Trace mission summary font mappings");

namespace {
void Trace(uint8_t* base, uint32_t text, uint32_t font, const char* stage) {
  if (!REXCVAR_GET(mcla_font_trace) || !text || !font) return;
  // Only inspect the fixed ASCII prefix before reading the rest of the string.
  constexpr char prefix[] = "RESUMO DAS MISS";
  for (unsigned i = 0; i < sizeof(prefix) - 1; ++i)
    if (base[text + i] != prefix[i]) return;
  std::string title, bytes;
  constexpr char hex[] = "0123456789abcdef";
  for (unsigned i = 0; i < 96 && base[text + i]; ++i) {
    auto b = base[text + i];
    title += char(b);
    bytes += hex[b >> 4]; bytes += hex[b & 15]; bytes += ' ';
  }
  const auto count = REX_LOAD_U16(font + 160);
  const auto codes = REX_LOAD_U32(font + 16);
  static std::mutex mutex;
  static std::set<std::tuple<uint32_t, uint32_t, std::string, std::string>> seen;
  std::lock_guard lock(mutex);
  if (seen.size() >= 64 || !seen.emplace(font, codes, title, stage).second) return;
  std::string name;
  for (unsigned i = 0; i < 64 && base[font + 196 + i]; ++i)
    name += char(base[font + 196 + i]);
  REXLOG_INFO("FONT_TRACE {} title='{}' bytes={} font={:08X} name='{}' count={} codes={:08X}",
              stage, title, bytes, font, name, count, codes);
  if (!codes || count > 4096) return;
  for (uint16_t code : {uint16_t(0x4f), uint16_t(0xd5), uint16_t(0xf5)}) {
    int index = -1;
    for (unsigned i = 0; i < count; ++i)
      if (REX_LOAD_U16(codes + i * 2) == code) { index = int(i); break; }
    REXLOG_INFO("FONT_TRACE U+{:04X} index={}", code, index);
    if (index < 0) continue;
    for (unsigned slot = 0; slot < 3; ++slot) {
      const auto atlas = REX_LOAD_U32(font + 164 + slot * 4);
      if (!atlas) continue;
      const auto glyphCount = REX_LOAD_U16(atlas + 10);
      const auto indexes = REX_LOAD_U32(atlas + 12);
      const auto textures = REX_LOAD_U32(atlas);
      const auto textureCount = REX_LOAD_U16(atlas + 20);
      if (unsigned(index) >= glyphCount) continue;
      const auto textureIndex = indexes ? base[indexes + index] : 0;
      const auto texture = textures && textureIndex < textureCount
          ? REX_LOAD_U32(textures + textureIndex * 4) : 0;
      REXLOG_INFO("FONT_TRACE atlas_slot={} atlas={:08X} size={} glyphs={} texture_index={} texture_count={} texture={:08X}",
                  slot, atlas, REX_LOAD_U16(atlas + 8), glyphCount, textureIndex, textureCount, texture);
    }
  }
}
}

extern "C" REX_FUNC(sub_826041F0) {
  Trace(base, ctx.r3.u32, ctx.r4.u32, "measure");
  __imp__sub_826041F0(ctx, base);
}

extern "C" REX_FUNC(sub_82604548) {
  Trace(base, ctx.r4.u32, ctx.r5.u32, "render");
  __imp__sub_82604548(ctx, base);
}
