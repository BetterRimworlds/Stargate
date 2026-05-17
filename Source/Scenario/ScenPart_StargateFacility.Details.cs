// ==== Source/Scenario/ScenPart_StargateFacility.Details.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility
{
    private void AddFacilityDetails(Map map, CellRect roomRect, IntVec3 center)
    {
        IntVec3[] lampPositions = new[]
        {
            new IntVec3(roomRect.minX + 3, 0, roomRect.minZ + 3),
            new IntVec3(roomRect.maxX - 3, 0, roomRect.minZ + 3),
            new IntVec3(roomRect.minX + 3, 0, roomRect.maxZ - 3),
            new IntVec3(roomRect.maxX - 3, 0, roomRect.maxZ - 3)
        };

        foreach (IntVec3 pos in lampPositions)
        {
            if (!pos.InBounds(map)) continue;
            if (pos.DistanceTo(center) <= WooshRadius) continue;

            ClearCellForBuilding(map, pos);
            PlaceClaimed(map, ThingDefOf.StandingLamp, pos);
        }

        // Ancient debris - strictly outside the woosh radius.
        // Debris stays unclaimed/unfactioned by design (it's loose junk).
        for (int i = 0; i < 12; i++)
        {
            IntVec3 debrisPos = CellFinder.RandomClosewalkCellNear(roomRect.CenterCell, map, 6);

            if (!debrisPos.InBounds(map)) continue;
            if (debrisPos.DistanceTo(center) <= WooshRadius) continue;
            if (!roomRect.Contains(debrisPos)) continue;
            if (!debrisPos.Walkable(map)) continue;

            ThingDef debrisDef = Rand.Bool ? ThingDefOf.ChunkSlagSteel : ThingDefOf.Filth_RubbleBuilding;
            Thing debris = ThingMaker.MakeThing(debrisDef);
            GenPlace.TryPlaceThing(debris, debrisPos, map, ThingPlaceMode.Near);
        }
    }

    private void PlaceDiningArea(Map map, CellRect roomRect, IntVec3 center)
    {
        ThingDef tableDef = DefDatabase<ThingDef>.GetNamedSilentFail("Table2x4c");
        if (tableDef == null) { Log.Warning("[Stargate] Could not place dining area: missing Table2x4c."); return; }

        CellRect interior = new CellRect(roomRect.minX + 1, roomRect.minZ + 1, roomRect.Width - 2, roomRect.Height - 2);
        CellRect zpmRect = GetZpmRect(roomRect);
        CellRect lightRect = GetLightRect(roomRect);
        bool useNorthWall = _entranceSide != Rot4.North;

        IntVec3 tablePos = FindBestTablePos(interior, tableDef, zpmRect, lightRect, center, useNorthWall);
        if (!tablePos.IsValid) { Log.Warning("[Stargate] Could not find a valid dining table position."); return; }

        SpawnTable(map, tablePos, tableDef);
        SpawnChairs(map, tablePos, tableDef, interior, zpmRect, lightRect);
        PlaceSurvivalMeals(map, interior, tablePos, tableDef, useNorthWall);
    }

    private CellRect GetZpmRect(CellRect roomRect)
    {
        IntVec3 zpmPos = new IntVec3(roomRect.minX + 2, 0, roomRect.maxZ - 2);
        ThingDef zpmDef = DefDatabase<ThingDef>.GetNamedSilentFail("ArchotechZPM");
        return zpmDef != null ? GenAdj.OccupiedRect(zpmPos, Rot4.North, zpmDef.size) : new CellRect(zpmPos.x, zpmPos.z, 1, 1);
    }

    private CellRect GetLightRect(CellRect roomRect)
    {
        int centerX = (roomRect.minX + roomRect.maxX) / 2;
        int centerZ = (roomRect.minZ + roomRect.maxZ) / 2;
    
        return new CellRect(centerX, centerZ, 1, 1);
    }

    private IntVec3 FindBestTablePos(CellRect interior, ThingDef tableDef, CellRect zpmRect, CellRect lightRect, IntVec3 center, bool useNorthWall)
    {
        IntVec3 bestPos = IntVec3.Invalid;
        int bestScore = int.MaxValue;
        Rot4 tableRot = Rot4.East;

        foreach (IntVec3 candidate in interior.Cells)
        {
            CellRect occupied = GenAdj.OccupiedRect(candidate, tableRot, tableDef.size);
            if (!IsValidPlacement(occupied, interior, zpmRect, lightRect, center, useNorthWall)) continue;

            int score = Mathf.Abs(occupied.minX - (useNorthWall ? zpmRect.maxX + 1 : zpmRect.minX));
            if (score < bestScore) { bestScore = score; bestPos = candidate; }
        }

        return bestPos;
    }

    private bool IsValidPlacement(CellRect occupied, CellRect interior, CellRect zpmRect, CellRect lightRect, IntVec3 center, bool useNorthWall)
    {
        bool withinInterior = interior.Contains(new IntVec3(occupied.minX, 0, occupied.minZ)) &&
                              interior.Contains(new IntVec3(occupied.maxX, 0, occupied.maxZ));
        if (!withinInterior) return false;

        bool flushWithWall = useNorthWall ? occupied.maxZ == interior.maxZ : occupied.minZ == interior.minZ;
        if (!flushWithWall) return false;

        if (occupied.Cells.Any(c => c.DistanceTo(center) <= WooshRadius)) return false;
        if (occupied.Overlaps(zpmRect)) return false;
        if (occupied.Overlaps(lightRect)) return false;

        return true;
    }

    private void SpawnTable(Map map, IntVec3 tablePos, ThingDef tableDef)
    {
        CellRect tableRect = GenAdj.OccupiedRect(tablePos, Rot4.East, tableDef.size);
        foreach (IntVec3 cell in tableRect.Cells) ClearCellForBuilding(map, cell);

        Thing table = ThingMaker.MakeThing(tableDef, ThingDefOf.Steel);
        table.SetFactionDirect(Faction.OfPlayer);
        GenSpawn.Spawn(table, tablePos, map, Rot4.East);
    }

    private void SpawnChairs(Map map, IntVec3 tablePos, ThingDef tableDef, CellRect interior, CellRect zpmRect, CellRect lightRect)
    {
        ThingDef chairDef = DefDatabase<ThingDef>.GetNamedSilentFail("DiningChair");
        if (chairDef == null) return;

        CellRect tableRect = GenAdj.OccupiedRect(tablePos, Rot4.East, tableDef.size);

        foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(tablePos, Rot4.East, tableDef.size))
        {
            // Skip cells that are diagonally adjacent to the table's actual rectangle.
            // This removes the corner chairs, including the far-left and far-right
            // chairs on the bottom row.
            bool outsideX = cell.x < tableRect.minX || cell.x > tableRect.maxX;
            bool outsideZ = cell.z < tableRect.minZ || cell.z > tableRect.maxZ;

            if (outsideX && outsideZ)
                continue;

            Rot4 chairRot = Rot4.Invalid;

            if (cell.z < tableRect.minZ)
                chairRot = Rot4.North;
            else if (cell.z > tableRect.maxZ)
                chairRot = Rot4.South;
            else if (cell.x < tableRect.minX)
                chairRot = Rot4.East;
            else if (cell.x > tableRect.maxX)
                chairRot = Rot4.West;

            if (!chairRot.IsValid) continue;

            if (!interior.Contains(cell)) continue;

            if (zpmRect.Contains(cell) || lightRect.Contains(cell)) continue;

            ClearCellForBuilding(map, cell);

            Thing chair = ThingMaker.MakeThing(chairDef, ThingDefOf.Steel);
            chair.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(chair, cell, map, chairRot);
        }
    }
    
    // Register the gate room as Home so colonists clean, haul, and repair inside it.
    // Without this, properly-claimed buildings still don't get baseline base behavior
    // because the area manager doesn't consider the cells "ours" yet.
    private void ClaimHomeArea(Map map, CellRect roomRect)
    {
        foreach (IntVec3 cell in roomRect.Cells)
        {
            if (!cell.InBounds(map)) continue;
            map.areaManager.Home[cell] = true;
        }
    }

    private void SpawnColonists(Map map, IntVec3 center)
    {
        if (Find.GameInitData?.startingAndOptionalPawns == null) return;

        IntVec3 spawnSpot = CellFinder.RandomClosewalkCellNear(center + new IntVec3(0, 0, 3), map, 3);

        foreach (Pawn pawn in Find.GameInitData.startingAndOptionalPawns)
        {
            if (pawn == null) continue;
            if (pawn == _guardianPawn) continue; // Same reference, not a second lookup
            if (pawn.Spawned) continue;

            GenPlace.TryPlaceThing(pawn, spawnSpot, map, ThingPlaceMode.Near);
        }
    }
    
    private void PlaceSurvivalMeals(Map map, CellRect interior, IntVec3 tablePos, ThingDef tableDef, bool useNorthWall)
    {
        ThingDef shelfDef = DefDatabase<ThingDef>.GetNamedSilentFail("Shelf");
        ThingDef mealDef = ThingDefOf.MealSurvivalPack;

        Rot4 shelfRot = Rot4.East;
        CellRect tableRect = GenAdj.OccupiedRect(tablePos, Rot4.East, tableDef.size);

        IntVec3 shelfPos = FindSurvivalMealShelfPos(interior, shelfDef, shelfRot, useNorthWall);

        if (!shelfPos.IsValid)
        {
            Log.Warning("[Stargate] Could not find a valid shelf position for survival meals.");
            return;
        }

        CellRect shelfRect = GenAdj.OccupiedRect(shelfPos, shelfRot, shelfDef.size);

        foreach (IntVec3 cell in shelfRect.Cells)
            ClearCellForBuilding(map, cell);

        Thing shelf = ThingMaker.MakeThing(shelfDef, ThingDefOf.Steel);
        shelf.SetFactionDirect(Faction.OfPlayer);
        GenSpawn.Spawn(shelf, shelfPos, map, shelfRot);

        int placed = 0;

        foreach (IntVec3 cell in shelfRect.Cells)
        {
            if (placed >= 2) break;

            Thing meals = ThingMaker.MakeThing(mealDef);
            meals.stackCount = 10;
            GenSpawn.Spawn(meals, cell, map);

            placed++;
        }
    }

    private IntVec3 FindSurvivalMealShelfPos(
        CellRect interior,
        ThingDef shelfDef,
        Rot4 shelfRot,
        bool useNorthWall)
    {
        IntVec3 bestPos = IntVec3.Invalid;
        int bestScore = int.MaxValue;

        foreach (IntVec3 candidate in interior.Cells)
        {
            CellRect occupied = GenAdj.OccupiedRect(candidate, shelfRot, shelfDef.size);

            if (!interior.Contains(new IntVec3(occupied.minX, 0, occupied.minZ)) ||
                !interior.Contains(new IntVec3(occupied.maxX, 0, occupied.maxZ)))
                continue;

            // Put the shelf on the far right/east wall of the room.
            if (occupied.maxX != interior.maxX)
                continue;

            // Keep it on the same side of the room as the table:
            // north-wall table => shelf near north end of east wall
            // south-wall table => shelf near south end of east wall
            int targetZ = useNorthWall ? interior.maxZ : interior.minZ;

            int score = Mathf.Abs(occupied.CenterCell.z - targetZ);

            if (score < bestScore)
            {
                bestScore = score;
                bestPos = candidate;
            }
        }

        return bestPos;
    }
}