#pragma once

#include <cstdint>

// Carbon fibre on any body part of the car being driven (src/carbon_parts.cpp). One bit per
// group, kept in the byte the save knows as CarbonFiberHood; bit 0 is the game's own (the hood).
enum CarbonGroup : uint8_t {
  kCarbonHood = 0x01,
  kCarbonTrunk = 0x02,
  kCarbonDoors = 0x04,
  kCarbonRoof = 0x08,  // drops a convertible's soft top
  kCarbonBumpers = 0x10,
  kCarbonBody = 0x20,  // fenders, skirts, wide body
  kCarbonSpoiler = 0x40,
  kCarbonExtras = 0x80,  // grill, intercooler, blower, bike bodywork
};

// False until the player's car has been drawn once.
bool CarbonHaveCar();
bool CarbonHasGroup(uint8_t group);
void CarbonToggleGroup(uint8_t group);
