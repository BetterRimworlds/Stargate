using System;
using System.Collections.Generic;
using System.Linq;
using BetterRimworlds.Utilities;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
internal static class LabOfJanus
{
    private const string SecretLabDefName = "SecretLab";
    // East-facing layout matches SecretLab's reference shell: 9x12 outer,
    // 7x10 interior, door on the west wall toward the gate room.
    private const int LabWidth = 9;
    private const int LabHeight = 12;

    private const int GateRoomSize = 15;

    // Fixed SW corner chosen on the original 90x90 Atlantis map: (61, 40),
    // with the gate at map centre (45, 45). Offset (+16, -5) is applied to
    // whatever the current map centre is (120x120 centre is (60, 60) → (76, 55)).
    private const int ReferenceCenterX = 90 / 2;
    private const int ReferenceCenterZ = 90 / 2;
    private const int ReferenceMinX = 61;
    private const int ReferenceMinZ = 40;
    private const int LabOffsetX = ReferenceMinX - ReferenceCenterX;
    private const int LabOffsetZ = ReferenceMinZ - ReferenceCenterZ;

    public static void Postfix(ResearchProjectDef __0)
    {
        if (__0?.defName != SecretLabDefName || Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        int placedLabs = 0;

        foreach (Map map in Find.Maps)
        {
            if (!TryAddSecretLab(map))
            {
                continue;
            }

            placedLabs++;
        }

        if (placedLabs == 0)
        {
            Log.Warning(
                "BetterRimworlds.Stargate: SecretLab finished, but no player-home map " +
                "with a Stargate was found for Janus's Lab."
            );
            return;
        }

        Find.LetterStack.ReceiveLetter(
            "Janus's Lab Revealed",
            "Janus's Lab has been revealed on the reserved site east of the Atlantis gate room. " +
            "Its entrance faces the main base, and the lab is now ready for use.",
            LetterDefOf.PositiveEvent
        );
    }

    private static bool TryAddSecretLab(Map map)
    {
        // Match AtlantisRising: a player-home map with a Stargate is enough.
        // TileInfo.WaterCovered is not reliable on every RimWorld version after
        // the tile API split, and aborting on it made the lab vanish entirely.
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
        CellRect labRect = GetSecretLabRect(map);

        if (!IsRectInsideMap(map, labRect))
        {
            Log.Warning(
                "BetterRimworlds.Stargate: Janus's Lab footprint was outside the map bounds at " +
                labRect + "."
            );
            return false;
        }

        if (HasSecretLab(map, labRect))
        {
            return false;
        }

        if (HasPlayerStructuresInLabFootprint(map, labRect))
        {
            Log.Warning(
                "BetterRimworlds.Stargate: Janus's Lab was not revealed because the reserved " +
                "site at " + labRect + " already contains player-owned structures."
            );
            return false;
        }

        try
        {
            // Reveal first so stockpile zones and fog-blocker updates can run.
            RevealRect(map, labRect.ExpandedBy(1));

            SecretLab.Generate(map, new SecretLab.Plan
            {
                RoomRect = labRect,
                Side = Rot4.East
            });

            BlueprintSpawner spawner = new BlueprintSpawner(map);
            spawner.PlaceRoof(labRect, outerBand: 0);
            ClaimFactionCapableThings(map, labRect);
            ClaimHomeArea(map, labRect);
            PlacePlatformConnection(map, gateRoomRect, labRect);

            // The lab shell is spawned without a faction, then claimed above, so
            // give its luminescent walls their hidden conduits now that they are
            // player-owned and the connection run exists.
            LuminescentWallPower.PowerAll(map);

            IntVec3 unfogRoot = SecretLab.DoorCellFor(labRect, Rot4.East) + IntVec3.East;
            if (unfogRoot.InBounds(map))
            {
                FloodFillerFog.FloodUnfog(unfogRoot, map);
            }

            Log.Message("BetterRimworlds.Stargate: Janus's Lab placed at " + labRect + ".");
            return true;
        }
        catch (Exception e)
        {
            Log.Error("BetterRimworlds.Stargate: Janus's Lab placement failed at " + labRect + ":\n" + e);
            return false;
        }
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

    private static CellRect GetSecretLabRect(Map map)
    {
        IntVec3 origin = map.Center + new IntVec3(LabOffsetX, 0, LabOffsetZ);
        return new CellRect(origin.x, origin.z, LabWidth, LabHeight);
    }

    private static bool IsRectInsideMap(Map map, CellRect rect)
    {
        return rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < map.Size.x && rect.maxZ < map.Size.z;
    }

    private static bool HasSecretLab(Map map, CellRect labRect)
    {
        IntVec3 doorCell = SecretLab.DoorCellFor(labRect, Rot4.East);
        if (!doorCell.InBounds(map))
        {
            return false;
        }

        bool hasDoor = map.thingGrid.ThingsListAt(doorCell)
            .Any(thing => thing.def == ThingDefOf.Door);
        if (!hasDoor)
        {
            return false;
        }

        return labRect.EdgeCells.Any(cell =>
            cell.InBounds(map) &&
            map.thingGrid.ThingsListAt(cell).Any(thing => IsLabWall(thing)));
    }

    private static bool IsLabWall(Thing thing)
    {
        if (thing?.def == null)
        {
            return false;
        }

        if (thing.def.defName == "BR_LuminescentPlasteelWall")
        {
            return true;
        }

        return thing.def == ThingDefOf.Wall && thing.Stuff == ThingDefOf.Plasteel;
    }

    private static bool HasPlayerStructuresInLabFootprint(Map map, CellRect labRect)
    {
        foreach (IntVec3 cell in labRect.Cells)
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            if (HasPlayerBlockingThing(map, cell))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPlayerBlockingThing(Map map, IntVec3 cell)
    {
        return map.thingGrid.ThingsListAt(cell).Any(thing =>
            !(thing is Pawn) &&
            (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item));
    }

    private static void PlacePlatformConnection(Map map, CellRect gateRoomRect, CellRect labRect)
    {
        ThingDef conduitDef = GetConnectionConduitDef();
        IntVec3 labDoor = SecretLab.DoorCellFor(labRect, Rot4.East);
        // The gate room's conduit ring is embedded in its inner wall layer
        // (gateRoomRect.EdgeCells), so the platform run has to start on the gate
        // room's OUTER wall cell (maxX + 1) to be cardinally adjacent to that
        // ring. Conduits share wall cells throughout this codebase (the gate
        // room and Secret Lab rings both rely on it), so this cell is the single
        // conduit that joins the lab run to the main base grid. Starting at
        // maxX + 2 left a one-cell gap and the lab stayed on its own network.
        int minX = gateRoomRect.maxX + 1;
        int maxX = labDoor.x - 1;
        int z = labRect.CenterCell.z;
        BlueprintSpawner spawner = new BlueprintSpawner(map);

        for (int x = minX; x <= maxX; x++)
        {
            IntVec3 cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map) || cell == labDoor)
            {
                continue;
            }

            if (!CanShareCellWithConduit(map, cell))
            {
                continue;
            }

            PrepareConnectionCell(map, cell);
            spawner.SpawnConduitAt(cell, conduitDef, claimForPlayer: true);
        }
    }

    /// <summary>
    /// Hidden conduits were added in RimWorld 1.5 and are the preferred link
    /// between the lab and the base: they are invisible, tougher, and immune to
    /// short circuits. Older supported versions (1.2-1.4) have no such def, so
    /// they fall back to the regular power conduit.
    /// </summary>
    private static ThingDef GetConnectionConduitDef()
    {
        return DefDatabase<ThingDef>.GetNamedSilentFail("HiddenConduit") ?? ThingDefOf.PowerConduit;
    }

    /// <summary>
    /// True when a connection conduit may occupy <paramref name="cell"/>: empty
    /// floor, an existing conduit, or a wall cell. Conduits coexist with walls —
    /// the gate-room and Secret Lab wall rings depend on it — so a wall must not
    /// block the run; any other pawn or building still does.
    /// </summary>
    private static bool CanShareCellWithConduit(Map map, IntVec3 cell)
    {
        foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
        {
            if (thing is Pawn)
            {
                return false;
            }

            if (thing.def.category != ThingCategory.Building)
            {
                continue;
            }

            if (IsConduitDef(thing.def) || IsConduitShareableWall(thing))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsConduitDef(ThingDef def)
    {
        return def == ThingDefOf.PowerConduit
            || def.defName == "HiddenConduit"
            || def.defName == "WaterproofConduit";
    }

    private static bool IsConduitShareableWall(Thing thing)
    {
        return thing?.def != null && LuminescentWallsUtility.IsAnyWall(thing.def);
    }

    /// <summary>
    /// Floors, roofs, and claims a platform cell for a lab connection conduit.
    /// Wall cells are left untouched: the conduit simply shares the existing
    /// base wall, so no terrain, roof, or home-area changes are made there.
    /// </summary>
    private static void PrepareConnectionCell(Map map, IntVec3 cell)
    {
        if (map.thingGrid.ThingsListAt(cell).Any(IsConduitShareableWall))
        {
            return;
        }

        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
        map.areaManager.Home[cell] = true;
        if (map.roofGrid.RoofAt(cell) == null)
        {
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
        }
    }

    private static void ClaimHomeArea(Map map, CellRect labRect)
    {
        foreach (IntVec3 cell in labRect.Cells)
        {
            if (cell.InBounds(map))
            {
                map.areaManager.Home[cell] = true;
            }
        }
    }

    private static void ClaimFactionCapableThings(Map map, CellRect labRect)
    {
        foreach (IntVec3 cell in labRect.Cells)
        {
            foreach (Thing thing in map.thingGrid.ThingsListAt(cell).ToList())
            {
                if (thing.def.CanHaveFaction)
                {
                    thing.SetFaction(Faction.OfPlayer);
                }
            }
        }
    }

    private static void RevealRect(Map map, CellRect rect)
    {
        foreach (IntVec3 cell in rect.Cells)
        {
            if (cell.InBounds(map))
            {
                map.fogGrid.Unfog(cell);
            }
        }
    }

}
