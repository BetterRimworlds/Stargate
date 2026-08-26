// ==== Source/Scenario/ScenPart_StargateFacility.Power.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility
{
    private void PlacePowerConduits(Map map, CellRect roomRect)
    {
        ThingDef conduitDef = ThingDefOf.PowerConduit;
        if (conduitDef == null) return;

        // The only conduits in the Gate Room are the deterministic in-wall
        // ring. Conduits are allowed to share wall cells; do not contract the
        // rect or replace the walls that were just built by
        // GenerateRoomStructure(). The previous cross-shaped "power spine"
        // through the center area was removed — it looked random and never
        // even reached the wall loop.
        foreach (IntVec3 cell in roomRect.EdgeCells)
        {
            TryPlaceConduit(map, cell);
        }
    }

    private void TryPlaceConduit(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return;
        if (ContainsThingDef(map, cell, ThingDefOf.PowerConduit)) return;

        // RimWorld's normal conduit placement can coexist with a wall. Check
        // the same wipe conditions used by BlueprintSpawner before spawning so
        // no wall (or other building/item) is removed or replaced.
        if (GenSpawn.WouldWipeAnythingWith(
                cell,
                Rot4.North,
                ThingDefOf.PowerConduit,
                map,
                thing => thing.def.category == ThingCategory.Building
                          || thing.def.category == ThingCategory.Item))
        {
            return;
        }

        Thing conduit = ThingMaker.MakeThing(ThingDefOf.PowerConduit);
        Thing spawned = GenSpawn.Spawn(conduit, cell, map, Rot4.North, WipeMode.Vanish);
        if (spawned != null && spawned.def.CanHaveFaction)
        {
            spawned.SetFaction(Faction.OfPlayer);
        }
    }
}
