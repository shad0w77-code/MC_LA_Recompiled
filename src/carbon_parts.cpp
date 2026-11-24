// Carbon fibre on any body part. Ported from LARecomp's src/mc_engine/carbon_parts.cpp (the reverse
// engineering is theirs). The game draws carbon by forcing the "carbonfiberhood" technique group around a
// part, but only for slots 2 and 40 (the hood). The part draw loop, sub_8235D500, is hooked at the two
// places that test the slot (config/carbon_parts.toml) and decides from a bitmask kept in the car's own
// carbon byte (CustomData + 0x898, the "CarbonFiberHood" of the save): bit 0 is still the hood, so a save
// from a console stays valid. The mask belongs to the car and is also written into that car's record in
// the save. Roof parts make a convertible's top vanish (no carbon technique); trunks turn carbon only on
// the models whose shader has the technique.

#include "generated/default/mcla_init.h"

#include "carbon_parts.h"

#include <atomic>
#include <cstdint>
#include <cstring>

#include <rex/logging.h>
#include <rex/ppc.h>
#include <rex/runtime.h>

namespace {

// Forced technique group (grmShader) — sub_82187BE0 writes it, -1 = none.
constexpr uint32_t kForcedTechniqueGroup = 0x827D5C60;
// Technique group id of "carbonfiberhood", filled by sub_82377950 at boot.
constexpr uint32_t kCarbonGroupId = 0x827E7614;

// sub_8235D500 stack: var_5EC holds CustomData, arg_1F holds the caller's
// "carbon allowed in this pass" byte (a2).
constexpr uint32_t kSpCustomData = 0x64;
constexpr uint32_t kSpAllowCarbon = 0x66F;

constexpr uint32_t kCustomCarbonMask = 0x898;   // from CustomData
constexpr uint32_t kReflCarbonMask = 0x858;     // from CustomData + 0x40

// dword_8288DCF8 — the vehicle the local player is driving. sub_8235D500
// itself compares a1 against it to tell the hero car from traffic.
constexpr uint32_t kPlayerVehicle = 0x8288DCF8;

// LocalOptions save block (sub_82387B90 returns this static object).
constexpr uint32_t kLocalOptions = 0x8288E920;
constexpr uint32_t kLoOwnedCount = 1340;
constexpr uint32_t kLoCurrentIndex = 1344;
constexpr uint32_t kLoCarConfigs = 1360;
constexpr uint32_t kLoDirty = 254032;
constexpr uint32_t kCarConfigSize = 8176;
constexpr int kMaxOwnedCars = 30;
// CarConfig vehicle name, char[32]. sub_8238AB50 matches owned cars by it.
constexpr uint32_t kCarConfigName = 8109;
constexpr uint32_t kCarConfigNameLen = 32;

// Part group bits. Slot indices come from off_827E9608 (55 entries).
constexpr uint8_t kGrpHood = 0x01;    // hood, hood_wide
constexpr uint8_t kGrpTrunk = 0x02;   // trunk_swap, trunk_wide
constexpr uint8_t kGrpDoors = 0x04;   // door_l/r, door_l/r_wide
constexpr uint8_t kGrpRoof = 0x08;    // conv_top, conv_topup, conv_topdown
constexpr uint8_t kGrpBumpers = 0x10; // bumper_f, bumper_r
constexpr uint8_t kGrpBody = 0x20;    // fenders, skirts, widebody
constexpr uint8_t kGrpSpoiler = 0x40; // spoiler, spoiler_b, spoiler_wide
constexpr uint8_t kGrpExtras = 0x80;  // grill, intercooler, exhaust, bike shells

// slot -> group bit. 0 means the slot never receives carbon: lights, glass,
// interior, wheels-related and occluders keep their own shaders, and a part
// whose shader has no "carbonfiberhood" technique draws with zero passes
// (i.e. vanishes) if the group is forced on it.
//
// The roof slots (0, 1, 22) are a measured half-case: on a convertible the
// top has no carbon technique and vanishes instead of turning carbon. That is
// kept as an opt-in — most cars carry a fixed roof rather than a top, and a
// deliberately roofless build is a look some players want. Off by default.
//
// Trunks are shader-dependent per car: some models turn carbon, others keep
// their paint. Nothing to fix on our side — the technique simply is not in
// that car's trunk shader.
constexpr uint8_t kSlotGroup[55] = {
    /*  0 conv_top      */ kGrpRoof,
    /*  1 conv_topup    */ kGrpRoof,
    /*  2 hood          */ kGrpHood,
    /*  3 bumper_r      */ kGrpBumpers,
    /*  4 trunk_swap    */ kGrpTrunk,
    /*  5 intercooler   */ kGrpExtras,
    /*  6 bumper_f      */ kGrpBumpers,
    /*  7 fenders       */ kGrpBody,
    /*  8 skirts        */ kGrpBody,
    /*  9 spoiler       */ kGrpSpoiler,
    /* 10 spoiler_b     */ kGrpSpoiler,
    /* 11 headlight     */ 0,
    /* 12 taillight     */ 0,
    /* 13 taillight_b   */ 0,
    /* 14 frontgrill    */ kGrpExtras,
    /* 15 wheeliebar    */ 0,
    /* 16 blower        */ kGrpExtras,
    /* 17 bike_tail     */ kGrpExtras,
    /* 18 bike_tank     */ kGrpExtras,
    /* 19 bike_cowl     */ kGrpExtras,
    /* 20 door_l        */ kGrpDoors,
    /* 21 door_r        */ kGrpDoors,
    /* 22 conv_topdown  */ kGrpRoof,
    /* 23 exh_pipe      */ 0,
    /* 24 exh_side      */ 0,
    /* 25 armfl         */ 0,
    /* 26 armfr         */ 0,
    /* 27 armrl         */ 0,
    /* 28 armrr         */ 0,
    /* 29 shockfl       */ 0,
    /* 30 shockfr       */ 0,
    /* 31 shockrl       */ 0,
    /* 32 shockrr       */ 0,
    /* 33 guiderl       */ 0,
    /* 34 guiderr       */ 0,
    /* 35 axle          */ 0,
    /* 36 subbumf       */ 0,
    /* 37 subbumr       */ 0,
    /* 38 widebody      */ kGrpBody,
    /* 39 spoiler_wide  */ kGrpSpoiler,
    /* 40 hood_wide     */ kGrpHood,
    /* 41 door_l_wide   */ kGrpDoors,
    /* 42 door_r_wide   */ kGrpDoors,
    /* 43 trunk_wide    */ kGrpTrunk,
    /* 44 interior      */ 0,
    /* 45 seats_f       */ 0,
    /* 46 seats_p       */ 0,
    /* 47 door_l_int    */ 0,
    /* 48 door_r_int    */ 0,
    /* 49 stereo        */ 0,
    /* 50 stereo_l      */ 0,
    /* 51 stereo_r      */ 0,
    /* 52 steer_whl     */ 0,
    /* 53 post_gauge    */ 0,
    /* 54 occluder_     */ 0,
};

// The live config (CustomData) is one block the player keeps across cars: the
// pointer does not change on a car swap, the game copies the next car's
// config into it. The draw loop caches its address for the menus.
std::atomic<uint32_t> g_player_custom{0};
std::atomic<uint32_t> g_player_car{0};   // model data of the player's car, for the log

uint8_t* GetMembase() {
    auto* rt = rex::Runtime::instance();
    return rt ? rt->virtual_membase() : nullptr;
}

bool IsPlausibleGuestPtr(uint32_t ea) {
    return ea >= 0x1000 && ea < 0xC0000000;
}

uint32_t ReadGuestBE32(const uint8_t* base, uint32_t ea) {
    const uint8_t* p = base + ea;
    return (uint32_t(p[0]) << 24) | (uint32_t(p[1]) << 16) |
           (uint32_t(p[2]) << 8) | uint32_t(p[3]);
}

void WriteGuestBE32(uint8_t* base, uint32_t ea, uint32_t v) {
    uint8_t* p = base + ea;
    p[0] = uint8_t((v >> 24) & 0xFF);
    p[1] = uint8_t((v >> 16) & 0xFF);
    p[2] = uint8_t((v >> 8) & 0xFF);
    p[3] = uint8_t(v & 0xFF);
}

// CarConfig +8100: non-zero when the config belongs to an owned car.
// LeaveGarage (sub_826AA930) only commits the live config to the slot when it
// is set; a showroom or test car falls through to "not owned".
constexpr uint32_t kCarConfigOwned = 8100;

// Copy the mask into the save record of the car the live config belongs to:
// the current owned slot, and only when that slot really holds the same car.
// Same condition LeaveGarage uses, plus the name check, because right after a
// swap the current index already points at the next car while the live config
// still holds the previous one until it streams in (sub_822AAA18).
void StoreInOwnedRecord(uint8_t* base, uint32_t custom, uint8_t mask) {
    if (ReadGuestBE32(base, custom + kCarConfigOwned) == 0) return;

    const int32_t count =
        static_cast<int32_t>(ReadGuestBE32(base, kLocalOptions + kLoOwnedCount));
    const int32_t index =
        static_cast<int32_t>(ReadGuestBE32(base, kLocalOptions + kLoCurrentIndex));
    if (count <= 0 || count > kMaxOwnedCars || index < 0 || index >= count)
        return;

    const uint32_t record =
        kLocalOptions + kLoCarConfigs + kCarConfigSize * uint32_t(index);
    if (record == custom) return;  // the live config is the record itself

    const char* live_name =
        reinterpret_cast<const char*>(base + custom + kCarConfigName);
    const char* slot_name =
        reinterpret_cast<const char*>(base + record + kCarConfigName);
    if (std::strncmp(live_name, slot_name, kCarConfigNameLen) != 0) {
        REXLOG_INFO("[Carbon] slot {} holds another car, mask left in the live config",
                          index);
        return;
    }

    if (base[record + kCustomCarbonMask] != mask) {
        base[record + kCustomCarbonMask] = mask;
        base[kLocalOptions + kLoDirty] = 1;
    }
}

// Shared predicate for both draw-loop sites. veh = the vehicle being drawn
// (r31 in sub_8235D500).
bool SlotWantsCarbon(uint8_t* base, uint32_t sp, int slot, uint32_t veh) {
    if (slot < 0 || slot >= int(sizeof(kSlotGroup))) return false;
    if (!base[sp + kSpAllowCarbon]) return false;  // caller's a2

    const uint32_t custom = ReadGuestBE32(base, sp + kSpCustomData);
    if (!IsPlausibleGuestPtr(custom)) return false;

    // Remember the player's live config for the menus. The byte itself is
    // left alone: the game loads it from the car's own save record.
    if (veh && veh == ReadGuestBE32(base, kPlayerVehicle)) {
        g_player_custom.store(custom, std::memory_order_relaxed);

        const uint32_t holder = ReadGuestBE32(base, veh + 20);
        const uint32_t car = holder ? ReadGuestBE32(base, holder + 80) : 0;
        if (car && g_player_car.exchange(car, std::memory_order_relaxed) != car) {
            REXLOG_INFO("[Carbon] car 0x{:08X}, mask 0x{:02X}", car,
                              base[custom + kCustomCarbonMask]);
        }
    }

    const uint8_t mask = base[custom + kCustomCarbonMask];
    if (!mask) return false;

    return (kSlotGroup[slot] & mask) != 0;
}

}  // namespace

bool CarbonHaveCar() {
  return g_player_custom.load(std::memory_order_relaxed) != 0 && GetMembase() != nullptr;
}

static uint8_t CarbonGetMask() {
  uint8_t* base = GetMembase();
  const uint32_t custom = g_player_custom.load(std::memory_order_relaxed);
  if (!base || !IsPlausibleGuestPtr(custom)) return 0;
  return base[custom + kCustomCarbonMask];
}

static void CarbonSetMask(uint8_t mask) {
  uint8_t* base = GetMembase();
  const uint32_t custom = g_player_custom.load(std::memory_order_relaxed);
  if (!base || !IsPlausibleGuestPtr(custom)) return;
  base[custom + kCustomCarbonMask] = mask;  // the live config, for the renderer
  StoreInOwnedRecord(base, custom, mask);   // and the car's record, for the save
  REXLOG_INFO("carbon: mask 0x{:02X}", mask);
}

bool CarbonHasGroup(uint8_t group) { return (CarbonGetMask() & group) != 0; }

void CarbonToggleGroup(uint8_t group) { CarbonSetMask(uint8_t(CarbonGetMask() ^ group)); }

// 0x8235DED0: replaces `slot == 2 || slot == 40` and the flag test; r27 = the part slot, r31 = the
// vehicle. True skips the original block (to 0x8235DF04).
bool mcla_carbon_part_push(PPCRegister& r1, PPCRegister& r27, PPCRegister& r31) {
  uint8_t* base = GetMembase();
  if (!base) return true;
  const uint32_t sp = static_cast<uint32_t>(r1.u64);
  const int slot = static_cast<int>(static_cast<int32_t>(r27.u64));
  if (SlotWantsCarbon(base, sp, slot, static_cast<uint32_t>(r31.u64))) {
    WriteGuestBE32(base, kForcedTechniqueGroup, ReadGuestBE32(base, kCarbonGroupId));
  }
  return true;
}

// 0x8235E038: the same test after the part, which puts the forced group back to -1 (to 0x8235E06C).
bool mcla_carbon_part_pop(PPCRegister& r1, PPCRegister& r27, PPCRegister& r31) {
  uint8_t* base = GetMembase();
  if (!base) return true;
  const uint32_t sp = static_cast<uint32_t>(r1.u64);
  const int slot = static_cast<int>(static_cast<int32_t>(r27.u64));
  if (SlotWantsCarbon(base, sp, slot, static_cast<uint32_t>(r31.u64))) {
    WriteGuestBE32(base, kForcedTechniqueGroup, 0xFFFFFFFFu);
  }
  return true;
}
