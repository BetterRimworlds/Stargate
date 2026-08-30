using System.Collections.Generic;
using System.Linq;
using BetterRimworlds.Utilities;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
internal static class AtlantisRisingResearchComplete
{
    private const string AtlantisRisingDefName = "AtlantisRising";
    private const int GateRoomSize = 15;
    private const float RichSoilRadius = 15.5f;
    // The dining spire is 22x15, double-sided like the Gate Room's 2-thick
    // ancient walls (inner wall ring + outer wall ring), so the walled
    // footprint comes out 24x17.
    private const int DiningPlatformWidth = 20;
    private const int DiningPlatformHeight = 15;
    // The dining platform sits six rows further out than the original layout.
    // In this codebase's Atlantis orientation the minZ side is treated as
    // "south" (see EnsureSouthAccess and the "south wall" bench logic), so this
    // is the "six rows further south" move; the walkway grows to match.
    private const int DiningPlatformSouthExtraRows = 6;
    private const int DiningPlatformWalkwayGap = 2; // original 2-row walkway gap.
    private const int TableTargetCount = 4;

    public static void Postfix(ResearchProjectDef __0)
    {
        if (__0?.defName != AtlantisRisingDefName)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        int updatedMaps = 0;
        int placedTables = 0;

        foreach (Map map in Find.Maps)
        {
            if (!TryApplyAtlantisRising(map, out int mapTablesPlaced))
            {
                continue;
            }

            updatedMaps++;
            placedTables += mapTablesPlaced;
        }

        if (updatedMaps <= 0)
        {
            Log.Warning(
                "BetterRimworlds.Stargate: AtlantisRising finished, but no player home map " +
                "with a Stargate was found to update."
            );
            return;
        }

        Find.LetterStack.ReceiveLetter(
            "Atlantis Rising",
            "The Atlantis platform has risen around the Stargate. Rich soil now surrounds the base, " +
            "and a southern dining spire now rises beyond the walkway, walled in limestone and roofed, " +
            $"with power conduits, Luminescent Limestone architectural lighting, {placedTables} of {TableTargetCount} tables, " +
            "and steel shelves stocked with packaged survival meals.",
            LetterDefOf.PositiveEvent
        );
    }

    private static bool TryApplyAtlantisRising(Map map, out int placedTables)
    {
        placedTables = 0;

        if (map == null || !map.IsPlayerHome)
        {
            return false;
        }

        Building_Stargate stargate = GetPlayerStargate(map);
        if (stargate == null)
        {
            return false;
        }

        CellRect gateRoomRect = GetGateRoomRect(stargate.Position);
        CellRect platformRect = GetDiningPlatformRect(gateRoomRect);
        CellRect walkwayRect = GetWalkwayRect(gateRoomRect, platformRect);

        ApplyRichSoil(map, stargate.Position, gateRoomRect);
        EnsureSouthAccess(map, gateRoomRect);
        ApplyConcreteFloor(map, platformRect);
        ApplyConcreteFloor(map, walkwayRect);
        ClaimHomeArea(map, platformRect);
        ClaimHomeArea(map, walkwayRect);

        // The dining spire: a double-walled perimeter (Gate Room style) with
        // in-wall power-conduit rings, a door on the walkway side, and a full
        // Luminescent Limestone interior wall ring for architectural lighting
        // (falling back to standing lamps if the def is missing).
        BuildDiningRoomShell(map, platformRect, gateRoomRect.CenterCell.x, walkwayRect);
        if (LuminescentWallsUtility.GetWallDef() == null)
        {
            PlaceDiningRoomLamps(map, platformRect);
        }

        // Roof the spire (inner + outer wall rings) and the covered bridge, so
        // the walkway from the Gate Room to the spire is fully enclosed. The
        // interior is at most 6 cells from a wall, so the roof needs no columns.
        BlueprintSpawner spawner = new BlueprintSpawner(map);
        spawner.PlaceRoof(platformRect, outerBand: 1);
        spawner.PlaceRoof(walkwayRect, outerBand: 0);

        placedTables = PlaceDiningFurniture(map, platformRect, gateRoomRect.CenterCell.x);
        PlaceDiningRoomShelves(map, platformRect, gateRoomRect.CenterCell.x);
        return true;
    }

    private static Building_Stargate GetPlayerStargate(Map map)
    {
        ThingDef stargateDef = DefDatabase<ThingDef>.GetNamedSilentFail("Stargate");
        if (stargateDef == null)
        {
            return null;
        }

        return map.listerThings.ThingsOfDef(stargateDef)
            .OfType<Building_Stargate>()
            .FirstOrDefault(stargate => stargate.Faction == Faction.OfPlayer);
    }

    private static CellRect GetGateRoomRect(IntVec3 center)
    {
        int halfSize = GateRoomSize / 2;
        return new CellRect(center.x - halfSize, center.z - halfSize, GateRoomSize, GateRoomSize);
    }

    private static CellRect GetDiningPlatformRect(CellRect gateRoomRect)
    {
        int minX = gateRoomRect.CenterCell.x - (DiningPlatformWidth / 2);
        int minZ = gateRoomRect.minZ
                   - DiningPlatformHeight
                   - DiningPlatformWalkwayGap
                   - DiningPlatformSouthExtraRows;
        return new CellRect(minX, minZ, DiningPlatformWidth, DiningPlatformHeight);
    }

    private static CellRect GetWalkwayRect(CellRect gateRoomRect, CellRect platformRect)
    {
        int width = 3;
        int minX = gateRoomRect.CenterCell.x - 1;
        int minZ = platformRect.maxZ + 1;
        int height = gateRoomRect.minZ - minZ;

        if (height <= 0)
        {
            return CellRect.Empty;
        }

        return new CellRect(minX, minZ, width, height);
    }

    private static void BuildDiningRoomShell(Map map, CellRect platformRect, int doorX, CellRect walkwayRect)
    {
        ThingDef wallDef = ThingDefOf.Wall;
        ThingDef doorDef = ThingDefOf.Door;
        ThingDef plasteelDef = ThingDefOf.Plasteel;
        // The spire and bridge walls are limestone, per the Atlantis design.
        ThingDef limestoneDef = DefDatabase<ThingDef>.GetNamedSilentFail("BlocksLimestone") ?? plasteelDef;
        // Luminescent Limestone on every interior / corridor wall cell.
        ThingDef luminescentWallDef = LuminescentWallsUtility.GetWallDef();

        if (wallDef == null || doorDef == null || plasteelDef == null)
        {
            return;
        }

        // One door on the inner wall ring where the walkway meets it, aligned
        // with the walkway column; the outer wall ring keeps an open passage
        // there instead. Mirrors the Gate Room's 2-thick ancient wall look
        // (see ScenPart_StargateFacility.GenerateRoomStructure).
        IntVec3 doorCell = new IntVec3(doorX, 0, platformRect.maxZ);
        IntVec3 outerPassageCell = new IntVec3(doorX, 0, platformRect.maxZ + 1);
        CellRect outerWallRect = platformRect.ExpandedBy(1);

        // OUTER wall layer, keeping the walkway passage open. The passage cell
        // itself is cleared like the Gate Room's outer doorway so nothing can
        // silently seal the entrance. Atlantis uses Luminescent Limestone
        // for both wall rings.
        ClearCellForBuilding(map, outerPassageCell);

        foreach (IntVec3 cell in outerWallRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;
            if (cell == outerPassageCell) continue;

            ClearCellForBuilding(map, cell);
            if (luminescentWallDef != null)
            {
                SpawnClaimedThing(map, luminescentWallDef, cell, null, Rot4.North);
            }
            else
            {
                SpawnClaimedThing(map, wallDef, cell, limestoneDef, Rot4.North);
            }
        }

        // INNER wall layer, with the single door. The entire interior ring is
        // Luminescent Limestone so the dining room is lit by the architecture.
        foreach (IntVec3 cell in platformRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;

            ClearCellForBuilding(map, cell);

            if (cell == doorCell)
            {
                SpawnClaimedThing(map, doorDef, cell, limestoneDef, Rot4.North);
            }
            else if (luminescentWallDef != null)
            {
                SpawnClaimedThing(map, luminescentWallDef, cell, null, Rot4.North);
            }
            else
            {
                SpawnClaimedThing(map, wallDef, cell, limestoneDef, Rot4.North);
            }
        }

        // BRIDGE: walled sides along the full walkway, linking the spire to the
        // Gate Room. Existing walls are kept as-is (the walkway's outer rows
        // already belong to the spire's outer ring and the Gate Room's own
        // wall); the power line runs inside these walls (see below).
        // Bridge side walls are fully Luminescent Limestone so the corridor
        // is lit by the architecture end-to-end.
        for (int z = walkwayRect.minZ; z <= walkwayRect.maxZ; z++)
        {
            IntVec3[] sideCells =
            {
                new IntVec3(walkwayRect.minX, 0, z),
                new IntVec3(walkwayRect.maxX, 0, z)
            };

            foreach (IntVec3 side in sideCells)
            {
                if (!side.InBounds(map)) continue;
                if (HasAnyWall(map, side)) continue;

                ClearCellForBuilding(map, side);

                if (luminescentWallDef != null)
                {
                    SpawnClaimedThing(map, luminescentWallDef, side, null, Rot4.North);
                }
                else
                {
                    SpawnClaimedThing(map, wallDef, side, limestoneDef, Rot4.North);
                }
            }
        }

        PlaceDiningRoomPowerConduits(map, platformRect, walkwayRect, doorCell, outerPassageCell);
    }

    private static void PlaceDiningRoomPowerConduits(Map map, CellRect platformRect, CellRect walkwayRect, IntVec3 doorCell, IntVec3 outerPassageCell)
    {
        ThingDef conduitDef = ThingDefOf.PowerConduit;
        if (conduitDef == null) return;

        BlueprintSpawner spawner = new BlueprintSpawner(map);

        // In-wall conduit rings inside BOTH wall layers, mirroring the Gate
        // Room's "conduits inside the walls" pattern. The inner ring is
        // interrupted only at the door and the outer ring only at its passage;
        // both rings stay connected through the wall seam and the bridge.
        spawner.SpawnConduitRing(
            platformRect,
            conduitDef,
            claimForPlayer: true,
            skipCells: new[] { doorCell }
        );
        spawner.SpawnConduitRing(
            platformRect.ExpandedBy(1),
            conduitDef,
            claimForPlayer: true,
            skipCells: new[] { outerPassageCell }
        );

        // The bridge's power line runs inside the bridge walls (both side
        // columns), tying the dining rings into the Gate Room's perimeter grid.
        for (int z = walkwayRect.minZ; z <= walkwayRect.maxZ; z++)
        {
            spawner.SpawnConduitAt(new IntVec3(walkwayRect.minX, 0, z), conduitDef, claimForPlayer: true);
            spawner.SpawnConduitAt(new IntVec3(walkwayRect.maxX, 0, z), conduitDef, claimForPlayer: true);
        }
    }

    private static void PlaceDiningRoomLamps(Map map, CellRect platformRect)
    {
        ThingDef lampDef = ThingDefOf.StandingLamp;
        if (lampDef == null) return;

        CellRect interior = platformRect.ContractedBy(1);

        // Two lamps, centered along the room's length: one toward the left
        // (west) wall and one toward the right (east) wall. The west lamp sits
        // one cell further west to line up with the shifted table grid.
        IntVec3[] lampPositions =
        {
            new IntVec3(interior.minX + 4, 0, interior.CenterCell.z),
            new IntVec3(interior.maxX - 5, 0, interior.CenterCell.z)
        };

        foreach (IntVec3 pos in lampPositions)
        {
            if (!pos.InBounds(map)) continue;

            ClearCellForBuilding(map, pos);
            // StandingLamp is not madeFromStuff, so no stuff def is passed.
            SpawnClaimedThing(map, lampDef, pos, null, Rot4.North);
        }
    }

    private static void ClearCellForBuilding(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return;

        List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();

        foreach (Thing thing in things)
        {
            if (thing is Pawn) continue;

            // Same as ScenPart_StargateFacility.ClearCellForBuilding: clear
            // walls, doors, geysers, buildings and plants, but preserve loose
            // player items on the freshly-concreted platform.
            if (thing.def == ThingDefOf.Wall ||
                thing.def == ThingDefOf.Door ||
                thing.def == ThingDefOf.SteamGeyser ||
                thing.def == ThingDefOf.ChunkSlagSteel ||
                thing.def == ThingDefOf.Filth_RubbleBuilding ||
                thing.def.category == ThingCategory.Building ||
                thing.def.category == ThingCategory.Plant)
            {
                thing.Destroy();
            }
        }
    }

    private static void ApplyRichSoil(Map map, IntVec3 center, CellRect gateRoomRect)
    {
#if RIMWORLD12
        TerrainDef richSoil = DefDatabase<TerrainDef>.GetNamed("SoilRich");
#else
        TerrainDef richSoil = TerrainDefOf.SoilRich;
#endif

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, RichSoilRadius, true))
        {
            if (!cell.InBounds(map)) continue;
            if (gateRoomRect.Contains(cell)) continue;

            map.terrainGrid.SetTerrain(cell, richSoil);
        }
    }

    private static void EnsureSouthAccess(Map map, CellRect gateRoomRect)
    {
        CellRect outerWallRect = gateRoomRect.ExpandedBy(1);
        IntVec3 innerDoorCell = new IntVec3(gateRoomRect.CenterCell.x, 0, gateRoomRect.minZ);
        IntVec3 outerPassageCell = new IntVec3(outerWallRect.CenterCell.x, 0, outerWallRect.minZ);

        ClearPassageCell(map, innerDoorCell);
        ClearPassageCell(map, outerPassageCell);

        if (!HasThingDef(map, innerDoorCell, ThingDefOf.Door))
        {
            // Seated in the south wall, so Building_Door.DoorPreDraw() re-derives
            // this door's facing as north every frame — the rotation argument
            // below is inert. Keep the door in a horizontal wall run or the
            // facing flips to east. See GetAtlantisEntranceSide.
            SpawnClaimedThing(map, ThingDefOf.Door, innerDoorCell, ThingDefOf.Plasteel, Rot4.North);
        }
    }

    private static void ApplyConcreteFloor(Map map, CellRect rect)
    {
        if (rect == CellRect.Empty)
        {
            return;
        }

        foreach (IntVec3 cell in rect.Cells)
        {
            if (!cell.InBounds(map)) continue;

            // Preserve existing player structures; only clear naturally spawned clutter.
            ClearNaturalClutter(map, cell);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
        }
    }

    private static void ClearPassageCell(Map map, IntVec3 cell)
    {
        List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();

        foreach (Thing thing in things)
        {
            if (thing is Pawn) continue;

            if (thing.def.category == ThingCategory.Building ||
                thing.def.category == ThingCategory.Plant ||
                thing.def.category == ThingCategory.Item ||
                thing.def.destroyable)
            {
                thing.Destroy();
            }
        }

        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
    }

    private static void ClearNaturalClutter(Map map, IntVec3 cell)
    {
        List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();

        foreach (Thing thing in things)
        {
            if (thing is Pawn) continue;

            if (thing.def.category == ThingCategory.Plant ||
                thing.def == ThingDefOf.ChunkSlagSteel ||
                thing.def == ThingDefOf.Filth_RubbleBuilding)
            {
                thing.Destroy();
            }
        }
    }

    private static void ClaimHomeArea(Map map, CellRect rect)
    {
        if (rect == CellRect.Empty)
        {
            return;
        }

        foreach (IntVec3 cell in rect.Cells)
        {
            if (!cell.InBounds(map)) continue;
            map.areaManager.Home[cell] = true;
        }
    }

    private static int PlaceDiningFurniture(Map map, CellRect platformRect, int centerX)
    {
        ThingDef tableDef = DefDatabase<ThingDef>.GetNamedSilentFail("Table2x4c");
        ThingDef chairDef = DefDatabase<ThingDef>.GetNamedSilentFail("DiningChair");
        ThingDef plasteelDef = ThingDefOf.Plasteel;

        if (tableDef == null || chairDef == null || plasteelDef == null)
        {
            Log.Warning(
                "BetterRimworlds.Stargate: AtlantisRising could not place dining furniture " +
                "because Table2x4c, DiningChair, or Plasteel was missing."
            );
            return 0;
        }

        int existingTables = CountExistingTables(map, platformRect, tableDef);
        if (existingTables >= TableTargetCount)
        {
            return 0;
        }

        int placedTables = 0;
        BlueprintSpawner spawner = new BlueprintSpawner(map);

        foreach (IntVec3 pos in GetPreferredTablePositions(platformRect, centerX))
        {
            if (existingTables + placedTables >= TableTargetCount)
            {
                break;
            }

            if (!TryPlaceTable(map, platformRect, spawner, tableDef, chairDef, plasteelDef, pos))
            {
                continue;
            }

            placedTables++;
        }

        if (existingTables + placedTables < TableTargetCount)
        {
            foreach (IntVec3 pos in platformRect.Cells)
            {
                if (existingTables + placedTables >= TableTargetCount)
                {
                    break;
                }

                if (!TryPlaceTable(map, platformRect, spawner, tableDef, chairDef, plasteelDef, pos))
                {
                    continue;
                }

                placedTables++;
            }
        }

        return placedTables;
    }

private static void PlaceDiningRoomShelves(Map map, CellRect platformRect, int centerX)
    {
        ThingDef shelfDef = DefDatabase<ThingDef>.GetNamedSilentFail("Shelf");
        ThingDef mealDef = ThingDefOf.MealSurvivalPack;
        ThingDef steelDef = ThingDefOf.Steel;

        if (shelfDef == null || mealDef == null || steelDef == null)
        {
            Log.Warning(
                "BetterRimworlds.Stargate: AtlantisRising could not place dining shelves " +
                "because Shelf, MealSurvivalPack, or Steel was missing."
            );
            return;
        }

        // Packaged survival meals on steel shelves lining the south wall
        // (opposite the walkway door). The 1.4+ shelves support 3 stacked
        // items per storage cell; 1.2/1.3 shelves only support 1 stack per
        // cell. Each stack is capped at mealDef.stackLimit, so we spawn
        // multiple stacks per cell instead of one oversized stack.
        #if RIMWORLD12 || RIMWORLD13
        int shelfCount = 8;
        int mealsPerShelf = 20;
        int stacksPerCell = 1;
        #else
        int shelfCount = 4;
        int mealsPerShelf = 60;
        int stacksPerCell = 3;
        #endif

        int maxStackSize = mealDef.stackLimit;

        CellRect interior = platformRect.ContractedBy(1);

        // Shelves run lengthwise along the south interior wall (Rot4.North, so
        // the occupied width is shelfDef.size.x per shelf), centered on the gate.
        Rot4 shelfRot = Rot4.North;
        int shelfWidth = shelfDef.size.x;
        int startX = interior.CenterCell.x - ((shelfCount * shelfWidth) / 2);
        int rowZ = interior.minZ;

        for (int i = 0; i < shelfCount; i++)
        {
            IntVec3 pos = new IntVec3(startX + (i * shelfWidth), 0, rowZ);
            CellRect shelfRect = GenAdj.OccupiedRect(pos, shelfRot, shelfDef.size);

            if (!BlueprintSpawner.RectFullyInside(interior, shelfRect))
            {
                continue;
            }

            foreach (IntVec3 cell in shelfRect.Cells)
            {
                ClearCellForBuilding(map, cell);
            }

            SpawnClaimedThing(map, shelfDef, pos, steelDef, shelfRot);

            // Distribute this shelf's meal count across its cells, and
            // within each cell across up to `stacksPerCell` separate
            // stacks, so no single stack exceeds mealDef.stackLimit.
            int remaining = mealsPerShelf;

            foreach (IntVec3 cell in shelfRect.Cells)
            {
                for (int s = 0; s < stacksPerCell && remaining > 0; s++)
                {
                    int stackCount = Mathf.Min(maxStackSize, remaining);
                    remaining -= stackCount;

                    Thing meals = ThingMaker.MakeThing(mealDef);
                    meals.stackCount = stackCount;
                    GenSpawn.Spawn(meals, cell, map, WipeMode.Vanish);
                }
            }

            if (remaining > 0)
            {
                Log.Warning(
                    $"BetterRimworlds.Stargate: AtlantisRising dining shelf could only place " +
                    $"{mealsPerShelf - remaining}/{mealsPerShelf} meals " +
                    $"(shelfRect.Area={shelfRect.Area}, stacksPerCell={stacksPerCell}, maxStackSize={maxStackSize})."
                );
            }
        }
    }

    private static int CountExistingTables(Map map, CellRect platformRect, ThingDef tableDef)
    {
        return map.listerThings.ThingsOfDef(tableDef)
            .Where(thing => platformRect.Contains(thing.Position))
            .Count();
    }

    private static IEnumerable<IntVec3> GetPreferredTablePositions(CellRect platformRect, int centerX)
    {
        // The 2x4 tables sit in a 2x2 grid inside the walled interior, pulled
        // one column west of the gate center: two columns offset one cell to
        // the west, one row near the north interior wall and one row two rows
        // off the south interior wall (the south row sits one column further
        // north so the walkway-side chairs clear the wall). Chairs follow the
        // tables automatically (see PlaceTableChairs).
        // GenAdj.OccupiedRect centers the footprint on the given position: for
        // Table2x4c (2,4) at Rot4.East, a table centered at z occupies rows
        // z-1..z and columns x-2..x+1 (verified against RimWorld 1.6).
        CellRect interior = platformRect.ContractedBy(1);

        yield return new IntVec3(centerX - 5, 0, interior.minZ + 3);
        yield return new IntVec3(centerX + 3, 0, interior.minZ + 3);
        yield return new IntVec3(centerX - 5, 0, interior.maxZ - 2);
        yield return new IntVec3(centerX + 3, 0, interior.maxZ - 2);
    }

    private static bool TryPlaceTable(
        Map map,
        CellRect platformRect,
        BlueprintSpawner spawner,
        ThingDef tableDef,
        ThingDef chairDef,
        ThingDef plasteelDef,
        IntVec3 pos)
    {
        Rot4 tableRot = Rot4.East;
        CellRect tableRect = GenAdj.OccupiedRect(pos, tableRot, tableDef.size);

        if (!BlueprintSpawner.RectFullyInside(platformRect, tableRect))
        {
            return false;
        }

        if (!CellsStandableAndClear(map, tableRect.Cells))
        {
            return false;
        }

        Thing table = spawner.SpawnFixed(
            tableDef,
            pos,
            tableRot,
            platformRect,
            plasteelDef,
            claimForPlayer: true
        );
        if (table == null)
        {
            return false;
        }

        PlaceTableChairs(map, platformRect, spawner, chairDef, plasteelDef, pos, tableDef);
        return true;
    }

    private static void PlaceTableChairs(
        Map map,
        CellRect platformRect,
        BlueprintSpawner spawner,
        ThingDef chairDef,
        ThingDef plasteelDef,
        IntVec3 tablePos,
        ThingDef tableDef)
    {
        Rot4 tableRot = Rot4.East;
        CellRect tableRect = GenAdj.OccupiedRect(tablePos, tableRot, tableDef.size);

        foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(tablePos, tableRot, tableDef.size))
        {
            bool outsideX = cell.x < tableRect.minX || cell.x > tableRect.maxX;
            bool outsideZ = cell.z < tableRect.minZ || cell.z > tableRect.maxZ;

            if (outsideX && outsideZ)
            {
                continue;
            }

            if (!cell.InBounds(map) || !platformRect.Contains(cell))
            {
                continue;
            }

            if (!cell.Standable(map))
            {
                continue;
            }

            if (HasBlockingThing(map, cell))
            {
                continue;
            }

            Rot4 chairRot = GetChairRotation(cell, tableRect);
            if (!chairRot.IsValid)
            {
                continue;
            }

            Thing chair = spawner.SpawnFixed(
                chairDef,
                cell,
                chairRot,
                platformRect,
                plasteelDef,
                claimForPlayer: true
            );
            if (chair == null)
            {
                continue;
            }
        }
    }

    private static Rot4 GetChairRotation(IntVec3 cell, CellRect tableRect)
    {
        if (cell.z < tableRect.minZ) return Rot4.North;
        if (cell.z > tableRect.maxZ) return Rot4.South;
        if (cell.x < tableRect.minX) return Rot4.East;
        if (cell.x > tableRect.maxX) return Rot4.West;

        return Rot4.Invalid;
    }

    private static bool CellsStandableAndClear(Map map, IEnumerable<IntVec3> cells)
    {
        foreach (IntVec3 cell in cells)
        {
            if (!cell.InBounds(map))
            {
                return false;
            }

            if (!cell.Standable(map))
            {
                return false;
            }

            if (HasBlockingThing(map, cell))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBlockingThing(Map map, IntVec3 cell)
    {
        List<Thing> things = map.thingGrid.ThingsListAt(cell);

        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];

            if (thing is Pawn)
            {
                continue;
            }

            if (thing.def.category == ThingCategory.Building)
            {
                return true;
            }

            if (thing.def.category == ThingCategory.Item && thing.def.EverHaulable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasThingDef(Map map, IntVec3 cell, ThingDef def)
    {
        List<Thing> things = map.thingGrid.ThingsListAt(cell);

        for (int i = 0; i < things.Count; i++)
        {
            if (things[i].def == def)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyWall(Map map, IntVec3 cell)
    {
        List<Thing> things = map.thingGrid.ThingsListAt(cell);

        for (int i = 0; i < things.Count; i++)
        {
            if (LuminescentWallsUtility.IsAnyWall(things[i].def))
            {
                return true;
            }
        }

        return false;
    }

    private static Thing SpawnClaimedThing(Map map, ThingDef def, IntVec3 cell, ThingDef stuff, Rot4 rotation)
    {
        // Only pass stuff for defs that actually use it — ThingMaker logs an
        // error and nulls the stuff for non-madeFromStuff defs like StandingLamp.
        // Mirrors BlueprintSpawner.SpawnFixed's actualStuff handling.
        ThingDef actualStuff = stuff;
        if (def != null && !def.MadeFromStuff)
        {
            actualStuff = null;
        }

        Thing thing = ThingMaker.MakeThing(def, actualStuff);
        Thing spawned = GenSpawn.Spawn(thing, cell, map, rotation, WipeMode.Vanish);

        if (spawned?.def.CanHaveFaction == true)
        {
            spawned.SetFaction(Faction.OfPlayer);
        }

        return spawned;
    }
}
