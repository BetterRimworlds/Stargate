// ==== Source/Scenario/StargateDestinationMapGen.Ocean.cs ====
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
    private static void GenerateOceanSurroundings(Map map)
    {
        IntVec3 center = map.Center;
        int halfSize   = RoomSize / 2;

        CellRect facilityRect = new CellRect(
            center.x - halfSize,
            center.z - halfSize,
            RoomSize,
            RoomSize
        );

        // Shallow water apron: 2–10 tiles beyond the facility wall edge.
        // The outerWallRect adds 1, so the real wall edge is halfSize + 1.
        float wallEdge     = halfSize + 1f;
        float shallowEdge  = wallEdge + Rand.Range(2, 11);

        foreach (IntVec3 cell in map.AllCells)
        {
            // Skip facility interior — GenerateRoomStructure owns that terrain.
            if (facilityRect.Contains(cell)) continue;

            // Clear anything sitting in what will become water
            // (rocks, plants, chunks — not pawns).
            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable)
                {
                    thing.Destroy();
                }
            }

            float dist = cell.DistanceTo(center);

            if (dist <= shallowEdge)
            {
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.WaterShallow);
            }
            else
            {
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.WaterDeep);
            }
        }
    }
}