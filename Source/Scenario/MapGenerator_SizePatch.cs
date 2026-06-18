// ==== Source/Patches/MapGenerator_SizePatch.cs ====
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate;

[StaticConstructorOnStartup]
public static class MapGenerator_SizePatch
{
    static MapGenerator_SizePatch()
    {
        var harmony = new Harmony("BetterRimworlds.Stargate.MapSizePatch");
        harmony.Patch(
            original: AccessTools.Method(typeof(MapGenerator), "GenerateMap"),
            prefix: new HarmonyMethod(typeof(MapGenerator_SizePatch), nameof(Prefix_GenerateMap))
        );
    }

    // This runs right before MapGenerator.GenerateMap. If we are generating our Stargate map,
    // it swaps the size vector based on the destination tile type.
    public static void Prefix_GenerateMap(ref IntVec3 mapSize, MapParent parent)
    {
        if (parent == null || parent.Tile < 0 || parent.Tile >= Find.WorldGrid.TilesCount)
        {
            return;
        }

        Tile tile = GetTile(parent.Tile);
        if (tile == null)
        {
            return;
        }

        if (tile.WaterCovered)
        {
            Log.Message("BetterRimworlds.Stargate: ocean tile detected, using 90x90 map.");
            mapSize = new IntVec3(90, 1, 90);
            return;
        }

        if (tile.hilliness == Hilliness.Impassable)
        {
            // Greatly increased the size of Tok'ra.
            Log.Message("BetterRimworlds.Stargate: impassable tile detected, using 300x300 map.");
            mapSize = new IntVec3(380, 1, 100);
        }
    }

    private static Tile GetTile(int tileId)
    {
#if RIMWORLD16
        return Find.WorldGrid[tileId];
#else
        return Find.WorldGrid.tiles[tileId];
#endif
    }
}
