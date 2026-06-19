using System.Collections.Generic;
using System.Linq;
using BetterRimworlds.Utilities;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
internal static class AtlantisRisingResearchComplete
{
    private const string AtlantisRisingDefName = "AtlantisRising";
    private const int GateRoomSize = 15;
    private const float RichSoilRadius = 15.5f;
    private const int DiningPlatformWidth = 17;
    private const int DiningPlatformHeight = 10;
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
            $"and a southern dining platform has been prepared with {placedTables} of {TableTargetCount} tables placed.",
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

        placedTables = PlaceDiningFurniture(map, platformRect);
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
        int minZ = gateRoomRect.minZ - DiningPlatformHeight - 2;
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

    private static int PlaceDiningFurniture(Map map, CellRect platformRect)
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

        foreach (IntVec3 pos in GetPreferredTablePositions(platformRect))
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

    private static int CountExistingTables(Map map, CellRect platformRect, ThingDef tableDef)
    {
        return map.listerThings.ThingsOfDef(tableDef)
            .Where(thing => platformRect.Contains(thing.Position))
            .Count();
    }

    private static IEnumerable<IntVec3> GetPreferredTablePositions(CellRect platformRect)
    {
        IntVec3 center = platformRect.CenterCell;

        yield return new IntVec3(center.x - 4, 0, center.z + 2);
        yield return new IntVec3(center.x + 4, 0, center.z + 2);
        yield return new IntVec3(center.x - 4, 0, center.z - 3);
        yield return new IntVec3(center.x + 4, 0, center.z - 3);
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

    private static Thing SpawnClaimedThing(Map map, ThingDef def, IntVec3 cell, ThingDef stuff, Rot4 rotation)
    {
        Thing thing = ThingMaker.MakeThing(def, stuff);
        Thing spawned = GenSpawn.Spawn(thing, cell, map, rotation, WipeMode.Vanish);

        if (spawned?.def.CanHaveFaction == true)
        {
            spawned.SetFaction(Faction.OfPlayer);
        }

        return spawned;
    }
}
