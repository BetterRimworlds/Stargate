# Stargate Scenario & Map Generation

This directory contains the map generation and scenario setup code for the BetterRimworlds Stargate mod.

## File Organization

### Core Scenario

| File | Purpose | Lines |
|------|---------|-------|
| `ScenPart_StargateFacility.cs` | Entry point, orchestration, fog handling, scenario messages | ~85 |
| `ScenPart_StargateFacility.Structure.cs` | Room construction, walls, floors, Stargate placement, equipment, casket, roof | ~140 |
| `ScenPart_StargateFacility.Power.cs` | Power conduit network generation | ~60 |
| `ScenPart_StargateFacility.Details.cs` | Lighting, debris, survival meals, home area, colonist spawning | ~95 |
| `ScenPart_StargateFacility.Clearing.cs` | Footprint clearing, cell preparation for building | ~75 |
| `ScenPart_StargateFacility.Helpers.cs` | `PlaceClaimed`, tile description, rotation utilities | ~110 |

**Total:** ~566 lines across 6 focused files (was 766 in single file)

### Destination Map Generation

| File | Purpose | Lines |
|------|---------|-------|
| `StargateDestinationMapGen.cs` | Entry point, underlay terrain, dispatch to generators | ~50 |
| `StargateDestinationMapGen.Ocean.cs` | Atlantis-style water surroundings | ~60 |
| `StargateDestinationMapGen.Cavern.cs` | Tok'ra-style mountain base with caverns, flora, ore | ~170 |

**Total:** ~280 lines across 3 focused files

## Design Patterns

### Partial Classes

All scenario and map generation classes use C# `partial` to split implementation across multiple files while maintaining:

- Shared private constants (`RoomSize`, `WooshRadius`)
- Access to private helpers across files
- Single cohesive class semantics at runtime
- File-per-domain organization for AI/token efficiency

### Generation Pipeline

```
ScenPart_StargateFacility.GenerateIntoMap()
    │
    ├── ClearFacilityFootprint()     // Remove obstructions
    ├── StargateDestinationMapGen.Apply()  // Tile-specific terrain (Ocean/Impassable)
    │
    ├── GenerateRoomStructure()      // Walls, floors, door
    ├── PlaceRoof()                  // Constructed roof over facility
    ├── PlacePowerConduits()         // Power network
    ├── PlaceStargate()              // Main building
    ├── PlaceSupportEquipment()      // Vanometric cell, ZPM, DHD, casket
    ├── PlaceSurvivalMeals()         // Starting food
    ├── AddFacilityDetails()         // Lamps, debris
    ├── ClaimHomeArea()              // Home zone for colonist AI
    └── SpawnColonists()             // Player pawns (minus guardian)
    
    [Deferred]
    └── ShowScenarioMessage()        // UI after map ready
```

### Tile Types

| Description | Generator | Theme |
|-------------|-----------|-------|
| `"Ocean"` | `GenerateOceanSurroundings()` | Atlantis — shallow water apron, deep ocean beyond |
| `"Impassable"` | `GenerateImpassableSurroundings()` | Tok'ra — carved mountain base, caverns, glowstools, ore veins |
| (default) | Vanilla terrain | Surface facility with natural surroundings |

## Key Implementation Notes

### Room Size Contract

`RoomSize = 15` must match exactly between:
- `ScenPart_StargateFacility.RoomSize`
- `StargateDestinationMapGen.RoomSize`

The room is always centered on `map.Center` with this fixed size.

### Facility Underlay

Rich soil is placed under the entire facility footprint before any floors. This enables:
- **Ocean:** Removing concrete reveals farmable soil (Atlantis gardens)
- **Impassable:** Underground growing under artificial light

Sequence: `SetTerrain(SoilRich)` → `SetTerrain(Concrete)` → removal restores soil.

### Fog of War Handling

The deferred `LongEventHandler.ExecuteWhenFinished()` block:
1. Forces `MapDrawer` section allocation (prevents NPEs with RocketMan)
2. Refogs entire map
3. Flood-unfogs from walkable seed cell inside room
4. Redraws fog and things
5. Shows scenario message dialog

### Cavern Generation (Impassable)

| Phase | Action |
|-------|--------|
| 1 | Fill map with solid rock + thick roof (preserve facility area) |
| 2 | Carve 5-9 small caverns (6-14 cells each) with organic shapes |
| 3 | Plant glowstool/agarilux/bryolux in carved cells |
| 4 | Scatter rich ore deposits across all rock |
| 5 | Place guaranteed ore veins on cavern walls (~25% conversion) |

### The Guardian

First pawn in `startingAndOptionalPawns` is placed in a cryptosleep casket. This pawn:
- Does not spawn normally with other colonists
- Is preserved for narrative/story purposes
- Awakens when casket is opened

## Version Compatibility

Preprocessor directives handle RimWorld version differences:

| Directive | Versions | Purpose |
|-----------|----------|---------|
| `RIMWORLD12` | 1.2 | `DefDatabase<TerrainDef>.GetNamed("SoilRich")` vs `TerrainDefOf.SoilRich` |
| `RIMWORLD15 \|\| RIMWORLD16` | 1.5+ | `MapMeshFlagDefOf` vs `MapMeshFlag`, `FogGrid.Refog()` vs manual array |

## Dependencies

- **Royalty DLC:** `VanometricPowerCell`, `Plant_Agarilux`, `Plant_Bryolux` (graceful fallback if absent)
- **Modded:** `ArchotechZPM`, `StargateDHD` (graceful fallback if absent)
- **Core:** All other ThingDefs are vanilla

## Adding New Tile Types

1. Add case in `DescribeTile()` (Helpers file)
2. Create `StargateDestinationMapGen.YourType.cs`
3. Add dispatch in `StargateDestinationMapGen.Apply()`

Example:
```csharp
case "YourType":
    GenerateYourTypeSurroundings(map);
    break;
```

Maintain the pattern: file-per-type, all `partial` in same class, shared constants from main file.
