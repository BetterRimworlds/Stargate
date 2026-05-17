// ==== Source/Scenario/ScenPart_StargateFacility.Clearing.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility
{
    private void ClearFacilityFootprint(Map map, CellRect rect)
    {
        foreach (IntVec3 cell in rect.Cells)
        {
            if (!cell.InBounds(map)) continue;

            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;

                if (thing.def.destroyable ||
                    thing.def.category == ThingCategory.Building ||
                    thing.def.category == ThingCategory.Plant ||
                    thing.def.category == ThingCategory.Item)
                {
                    thing.Destroy();
                }
            }
        }
    }

    private void ClearCellForDoorway(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return;

        List<Thing> existing = map.thingGrid.ThingsListAt(cell).ToList();
        foreach (Thing thing in existing)
        {
            if (thing is Pawn) continue;

            if (thing.def == ThingDefOf.Wall ||
                thing.def == ThingDefOf.Door ||
                thing.def.destroyable ||
                thing.def.category == ThingCategory.Building ||
                thing.def.category == ThingCategory.Plant ||
                thing.def.category == ThingCategory.Item)
            {
                thing.Destroy();
            }
        }

        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
    }

    private void ClearCellForBuilding(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return;

        List<Thing> existing = map.thingGrid.ThingsListAt(cell).ToList();
        foreach (Thing thing in existing)
        {
            if (thing is Pawn) continue;

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
}