// ==== Source/Scenario/ScenPart_StargateFacility_Atlantis.cs ====
using System.Linq;
using BetterRimworlds.Utilities;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

/// Ocean-tile (Atlantis) specializations for the starting gate room.
/// Gated by <see cref="IsAtlantisFacility"/> — no-ops on non-ocean maps.
internal partial class ScenPart_StargateFacility
{
    private bool IsAtlantisFacility(Map map)
    {
        return DescribeTile(map.Tile) == "Ocean";
    }

    private TerrainDef GetAtlantisFloorDef(Map map)
    {
        return IsAtlantisFacility(map)
            ? DefDatabase<TerrainDef>.GetNamed("BR_AtlantisAncientFloor")
            : null;
    }

    /// Atlantis has no starting ZPM. The ZPM slot is filled with a second
    /// vanometric power cell so the facility still has free, endless power.
    private bool PlaceAtlantisSecondaryPower(Map map, CellRect roomRect)
    {
        if (!IsAtlantisFacility(map)) return false;

        ThingDef vanoDef = DefDatabase<ThingDef>.GetNamedSilentFail("VanometricPowerCell");
        if (vanoDef == null) return true;

        // Flush against the west wall (footprint rows maxZ-2..maxZ-1 sit one
        // cell from the south wall) so the cell's root position is directly
        // adjacent to the wall conduit ring. The shared ZPM slot at
        // (minX + 2, maxZ - 2) sits one cell further from the ring and can end
        // up outside the power grid.
        IntVec3 vanoPos = new IntVec3(roomRect.minX + 1, 0, roomRect.maxZ - 2);
        if (!vanoPos.InBounds(map)) return true;

        CellRect interior = roomRect.ContractedBy(1);
        BlueprintSpawner spawner = new BlueprintSpawner(map);

        ClearCellForBuilding(map, vanoPos);
        spawner.SpawnFixed(
            vanoDef,
            vanoPos,
            Rot4.North,
            interior,
            claimForPlayer: true
        );

        return true;
    }

    /// Atlantis-only furniture layered on after the shared facility layout.
    /// Places a plasteel Simple Research Bench against the southern wall so the
    /// ocean research chain (Atlantis Rising → …) can start immediately.
    private void PlaceAtlantisFacilityExtras(Map map, IntVec3 center, CellRect roomRect)
    {
        if (!IsAtlantisFacility(map)) return;

        PlaceAtlantisSouthernResearchBench(map, center, roomRect);
    }

    private void PlaceAtlantisSouthernResearchBench(Map map, IntVec3 center, CellRect roomRect)
    {
        ThingDef benchDef = DefDatabase<ThingDef>.GetNamedSilentFail("SimpleResearchBench");
        if (benchDef == null)
        {
            Log.Warning("BetterRimworlds.Stargate: SimpleResearchBench def missing; Atlantis research bench skipped.");
            return;
        }

        ThingDef plasteelDef = ThingDefOf.Plasteel;
        if (plasteelDef == null)
        {
            Log.Warning("BetterRimworlds.Stargate: Plasteel def missing; Atlantis research bench skipped.");
            return;
        }

        CellRect interior = roomRect.ContractedBy(1);
        // Face south so the interaction cell is north of the bench (into the room).
        Rot4 benchRot = Rot4.South;

        IntVec3 benchPos = FindAtlantisSouthWallBenchPos(map, interior, benchDef, benchRot, center);
        if (!benchPos.IsValid)
        {
            Log.Warning("BetterRimworlds.Stargate: Could not place Atlantis research bench on southern wall.");
            return;
        }

        CellRect occupied = GenAdj.OccupiedRect(benchPos, benchRot, benchDef.size);
        foreach (IntVec3 cell in occupied.Cells)
        {
            ClearCellForBuilding(map, cell);
        }

        BlueprintSpawner spawner = new BlueprintSpawner(map);
        Thing bench = spawner.SpawnFixed(
            benchDef,
            benchPos,
            benchRot,
            interior,
            plasteelDef,
            claimForPlayer: true
        );

        if (bench == null)
        {
            Log.Warning("BetterRimworlds.Stargate: BlueprintSpawner failed to place Atlantis research bench.");
        }
    }

    private IntVec3 FindAtlantisSouthWallBenchPos(
        Map map,
        CellRect interior,
        ThingDef benchDef,
        Rot4 benchRot,
        IntVec3 center)
    {
        IntVec3 bestPos = IntVec3.Invalid;
        int bestScore = int.MaxValue;

        foreach (IntVec3 candidate in interior.Cells)
        {
            CellRect occupied = GenAdj.OccupiedRect(candidate, benchRot, benchDef.size);
            if (!BlueprintSpawner.RectFullyInside(interior, occupied)) continue;

            // Flush against the southern interior edge (just inside the south wall).
            if (occupied.minZ != interior.minZ) continue;

            // Stay out of the kawoosh kill zone around the gate.
            if (occupied.Cells.Any(c => c.DistanceTo(center) <= WooshRadius)) continue;

            // Leave the doorway column free when the entrance is on the south wall.
            if (_entranceSide == Rot4.South)
            {
                int doorX = interior.CenterCell.x;
                if (occupied.minX <= doorX && occupied.maxX >= doorX) continue;
            }

            if (occupied.Cells.Any(c => !CanPlaceAtlantisBenchOn(map, c))) continue;

            // Prefer a centered position along the south wall.
            int score = System.Math.Abs(occupied.CenterCell.x - interior.CenterCell.x);
            if (score < bestScore)
            {
                bestScore = score;
                bestPos = candidate;
            }
        }

        return bestPos;
    }

    private static bool CanPlaceAtlantisBenchOn(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return false;
        if (!cell.Walkable(map)) return false;

        foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
        {
            if (thing is Pawn) continue;
            if (thing.def == ThingDefOf.PowerConduit) continue;
            if (thing.def.category == ThingCategory.Building) return false;
            if (thing.def.category == ThingCategory.Item) return false;
        }

        return true;
    }
}
