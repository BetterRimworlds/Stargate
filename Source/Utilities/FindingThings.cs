

using System.Collections.Generic;     // For List<T>
using System.Collections.ObjectModel; // For ReadOnlyCollection<T>
using System.Linq;                    // For LINQ methods like Where()
using Verse;
namespace BetterRimworlds.Utilities;
public class Utilities {
    public static ReadOnlyCollection<Pawn> findClosePawns(IntVec3 position, float radius) {
        var pawns = Find.CurrentMap.mapPawns.AllPawnsSpawned.ToList();
        var closePawns = pawns.Where(t => t.Position.InHorDistOf(position, radius)).ToList();
        return new ReadOnlyCollection<Pawn>(closePawns); }
    public static Thing FindItemThingsInAutoLoader(Thing centerBuilding) {
        var hopperDef = ThingDef.Named("AutoLoader");
        return GenAdj.CellsAdjacentCardinal(centerBuilding)
            .Select(cell => Find.CurrentMap.thingGrid.ThingsAt(cell))
            .FirstOrDefault(things =>
                things.Any(t => t.def == hopperDef) &&
                things.Any(t => t.def.category == ThingCategory.Item))
            ?.FirstOrDefault(t => t.def.category == ThingCategory.Item);
     }

    public static List<Thing> FindItemThingsNearBuilding(Thing centerBuilding, int radius, Map map)
     {
        return GenRadial.RadialDistinctThingsAround(centerBuilding.Position, map, radius, true)
            .Where(t => t.def.category == ThingCategory.Item)
            .ToList();
     }
 }
