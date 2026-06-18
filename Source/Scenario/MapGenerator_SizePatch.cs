// ==== Source/Patches/MapGenerator_SizePatch.cs ====
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using System.Collections.Generic;
using System.Reflection;

namespace BetterRimworlds.Stargate;

[StaticConstructorOnStartup]
public static class MapGenerator_SizePatch
{
    static MapGenerator_SizePatch()
    {
        var harmony = new Harmony("BetterRimworlds.Stargate.MapSizePatch");
        var generateMap = FindGenerateMapMethod();
        if (generateMap == null)
        {
            Log.Warning("[Stargate] Could not find MapGenerator.GenerateMap overload to patch.");
            return;
        }

        harmony.Patch(
            original: generateMap,
            prefix: new HarmonyMethod(typeof(MapGenerator_SizePatch), nameof(Prefix_GenerateMap))
        );
    }

    private static MethodInfo FindGenerateMapMethod()
    {
        // Newer RimWorld versions append optional callback parameters. Match the
        // stable leading parameters instead of requiring an exact overload length.
        var expected = new[]
        {
            typeof(IntVec3),
            typeof(MapParent),
            typeof(MapGeneratorDef),
            typeof(IEnumerable<GenStepWithParams>)
        };

        return typeof(MapGenerator)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method =>
                method.Name == "GenerateMap" &&
                method.GetParameters().Length >= expected.Length &&
                method.GetParameters().Take(expected.Length)
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(expected))
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault();
    }

    // This runs right before MapGenerator.GenerateMap. If we are generating our Stargate map,
    // it swaps the size vector based on the destination tile type.
    public static void Prefix_GenerateMap(ref IntVec3 mapSize, MapParent parent)
    {
        // GenerateMap is also used for quest, incident, and other temporary maps.
        // Restrict this patch to the one map created while a new Stargate scenario
        // is being initialized: no maps exist yet and the parent is the selected
        // starting tile. Save loads do not have GameInitData, so they naturally fail
        // this guard as well.
        if (!IsStartingMapGeneration(parent))
        {
            return;
        }

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
            mapSize = new IntVec3(90, 1, 90);
            return;
        }

        if (tile.hilliness == Hilliness.Impassable)
        {
            // Greatly increased the size of Tok'ra.
            mapSize = new IntVec3(380, 1, 100);
        }
    }

    private static bool IsStartingMapGeneration(MapParent parent)
    {
        if (parent == null || !StargateScenarioUtility.IsStargateBaseScenario())
        {
            return false;
        }

        if (Find.GameInitData == null || Find.GameInitData.startingTile != parent.Tile)
        {
            return false;
        }

        return Find.Maps != null && Find.Maps.Count == 0;
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
