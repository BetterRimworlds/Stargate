using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

// Ocean maps have no land biome weather table in RW 1.4+, so the vanilla
// initial-weather selection can dereference a missing entry.  Keep this
// narrowly scoped to water-covered maps in the Stargate scenario and seed a
// valid weather state instead of allowing generation to abort.
[StaticConstructorOnStartup]
internal static class WeatherDeciderPatch
{
    static WeatherDeciderPatch()
    {
        var harmony = new Harmony("BetterRimworlds.Stargate.WeatherDeciderPatch");
        harmony.Patch(
            AccessTools.Method(typeof(WeatherDecider), nameof(WeatherDecider.StartInitialWeather)),
            prefix: new HarmonyMethod(typeof(WeatherDeciderPatch), nameof(Prefix_StartInitialWeather)));
        harmony.Patch(
            AccessTools.Method(typeof(WeatherDecider), "ChooseNextWeather"),
            prefix: new HarmonyMethod(typeof(WeatherDeciderPatch), nameof(Prefix_ChooseNextWeather)));
    }

    public static bool Prefix_StartInitialWeather(WeatherDecider __instance)
    {
        Map map = (Map)AccessTools.Field(typeof(WeatherDecider), "map")?.GetValue(__instance);
        if (!IsStargateWaterMap(map))
        {
            return true;
        }

        // WeatherManager is normally constructed by Map, but some 1.4 map
        // generation paths leave it unset for water tiles.
        if (map.weatherManager == null)
        {
            AccessTools.Field(typeof(Map), "weatherManager")?.SetValue(map, new WeatherManager(map));
        }

        if (map.weatherManager != null)
        {
            map.weatherManager.TransitionTo(WeatherDefOf.Clear);
        }

        // Leave a normal first-weather duration for the decider.  Future
        // selections are handled by Prefix_ChooseNextWeather below.
        AccessTools.Field(typeof(WeatherDecider), "curWeatherDuration")?.SetValue(__instance, 10000);
        return false;
    }

    public static bool Prefix_ChooseNextWeather(WeatherDecider __instance, ref WeatherDef __result)
    {
        Map map = (Map)AccessTools.Field(typeof(WeatherDecider), "map")?.GetValue(__instance);
        if (!IsStargateWaterMap(map))
        {
            return true;
        }

        // Water-covered starting tiles have no biome weather table.  Keep the
        // decider on Clear so StartNextWeather can continue using normal
        // duration handling without dereferencing map.Biome.
        __result = WeatherDefOf.Clear;
        return false;
    }

    private static bool IsStargateWaterMap(Map map)
    {
        if (map == null || !StargateScenarioUtility.IsStargateBaseScenario() || map.Tile < 0 ||
            map.Tile >= Find.WorldGrid.TilesCount)
        {
            return false;
        }

#if RIMWORLD16
        return Find.WorldGrid[map.Tile].WaterCovered;
#else
        return Find.WorldGrid.tiles[map.Tile].WaterCovered;
#endif
    }
}
