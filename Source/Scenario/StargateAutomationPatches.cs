// ==== Source/Scenario/StargateAutomationPatches.cs ====
using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate;

[StaticConstructorOnStartup]
public static class StargateAutomationPatches
{
    internal static StargateDailyPlanetConditions LastPlanetConditions;

    static StargateAutomationPatches()
    {
        var harmony = new Harmony("com.betterrimworlds.stargate.automation");
        harmony.PatchAll();
        Log.Message("BetterRimworlds.Stargate: Harmony automation patches applied.");
    }

    // CRITICAL:
    // Skipping Page_SelectStartingSite means we also skip vanilla side effects.
    //
    // Setting Find.GameInitData.startingTile is not enough. RimWorld expects a player
    // faction base / settlement world object to exist before the starting map is
    // generated. Without it, PageUtility.InitGameStart can fail with:
    //
    //   "Could not generate starting map because there is no any player faction base."
    //
    // This recreates the required vanilla state for the automated Stargate start.
    internal static void EnsurePlayerFactionBaseAtStartingTile()
    {
        if (!StargateScenarioUtility.IsStargateBaseScenario())
        {
            return;
        }

        if (Find.GameInitData == null)
        {
            Log.Error("BetterRimworlds.Stargate: Cannot ensure player faction base because GameInitData is null.");
            return;
        }

        int tile = Find.GameInitData.startingTile;
        if (tile < 0)
        {
            Log.Error("BetterRimworlds.Stargate: Cannot ensure player faction base because startingTile is invalid: " + tile);
            return;
        }

        if (Find.WorldObjects == null)
        {
            Log.Error("BetterRimworlds.Stargate: Cannot ensure player faction base because Find.WorldObjects is null.");
            return;
        }

        // Avoid creating duplicates if vanilla, another mod, or a future refactor has
        // already created the player settlement.
        var worldObjects = Find.WorldObjects.AllWorldObjects;
        for (int i = 0; i < worldObjects.Count; i++)
        {
            WorldObject worldObject = worldObjects[i];

            if (worldObject == null)
            {
                continue;
            }

            if (worldObject.Faction == Faction.OfPlayer && worldObject is Settlement)
            {
                if (worldObject.Tile != tile)
                {
                    worldObject.Tile = tile;
                }

                return;
            }
        }

        Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
        settlement.Tile = tile;
        settlement.SetFaction(Faction.OfPlayer);

        Find.WorldObjects.Add(settlement);

        Log.Message("BetterRimworlds.Stargate: Created player faction base at Stargate destination tile " + tile + ".");
    }
}

// 1. Auto-configure world parameters and click Next automatically.
//
// This is where the daily pseudo-multiplayer planet is created.
//
// Important:
//   - The daily seed controls the planet.
//   - The daily seed controls planet-level conditions.
//   - The daily seed does NOT control the Stargate starting tile.
//
// In other words:
//
//   Same UTC day  => same planet.
//   New game      => random Stargate destination on that planet.
[HarmonyPatch(typeof(Page_CreateWorldParams), "PostOpen")]
public static class Patch_Page_CreateWorldParams_PostOpen
{
    public static void Postfix(Page_CreateWorldParams __instance)
    {
        if (!StargateScenarioUtility.IsStargateBaseScenario())
        {
            return;
        }

        StargateDailyPlanetConditions conditions = StargateDailyPlanetConditions.Generate();

        SetPrivateField(__instance, "seedString", conditions.SeedString);
        SetPrivateField(__instance, "planetCoverage", conditions.PlanetCoverage);
        SetPrivateField(__instance, "rainfall", conditions.Rainfall);
        SetPrivateField(__instance, "temperature", conditions.Temperature);
        SetPrivateField(__instance, "population", conditions.Population);

        StargateAutomationPatches.LastPlanetConditions = conditions;

        MethodInfo canDoNext = AccessTools.Method(typeof(Page_CreateWorldParams), "CanDoNext");
        MethodInfo doNext = AccessTools.Method(typeof(Page_CreateWorldParams), "DoNext");

        if (canDoNext == null || doNext == null)
        {
            Log.Error("BetterRimworlds.Stargate: Could not find Page_CreateWorldParams.CanDoNext or DoNext.");
            return;
        }

        if ((bool)canDoNext.Invoke(__instance, null))
        {
            doNext.Invoke(__instance, null);
        }
    }

    private static void SetPrivateField<T>(Page_CreateWorldParams page, string fieldName, T value)
    {
        FieldInfo field = AccessTools.Field(typeof(Page_CreateWorldParams), fieldName);

        if (field == null)
        {
            Log.Error("BetterRimworlds.Stargate: Could not find Page_CreateWorldParams." + fieldName + ".");
            return;
        }

        field.SetValue(page, value);
    }
}

// 2. Skip the site selection screen entirely.
//
// Important:
// The planet is deterministic.
// The Stargate destination is intentionally NOT deterministic.
//
// That means:
//   - Same UTC date = same planet.
//   - Each new game = random Stargate destination on that planet.
//
// Ocean and impassable tiles are valid because this scenario has special gameplay:
//   - Ocean       => Atlantis-style underwater base.
//   - Impassable  => Tok'ra-style mountain base.
//
// CRITICAL:
// Do NOT show the Scenario Message Box here.
//
// This page happens before the map exists. The scenario message is an in-game intro
// beat and must happen after the map, Stargate facility, fog, equipment, and home area
// are fully generated, but before StargateRecall().
[HarmonyPatch(typeof(Page_SelectStartingSite), "PreOpen")]
public static class Patch_Page_SelectStartingSite_PreOpen
{
    public static bool Prefix(Page_SelectStartingSite __instance)
    {
        if (!StargateScenarioUtility.IsStargateBaseScenario())
        {
            return true;
        }

        int selectedTile = SelectRandomStargateDestinationTile();
        // Hardcode startTile
        // selectedTile = 23175;

        Find.GameInitData.startingTile = selectedTile;

        __instance.Close(false);

        Find.WindowStack.Add(new Page_ConfigureStartingPawns());

        return false;
    }

    private static int SelectRandomStargateDestinationTile()
    {
        int tilesCount = Find.WorldGrid.TilesCount;

        if (tilesCount <= 0)
        {
            Log.Error("BetterRimworlds.Stargate: WorldGrid has no tiles. Falling back to tile 0.");
            return 0;
        }

        // This is intentionally normal RNG, not daily-seeded RNG.
        //
        // DO NOT wrap this in StargateSeedUtility.WithDailySubSeed(...).
        // DO NOT wrap this in Rand.PushState(dailySeed).
        //
        // The whole point is:
        //   same daily planet,
        //   random Stargate destination.
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            int tileId = Rand.Range(0, tilesCount);

            if (TileExists(tileId))
            {
                return tileId;
            }
        }

        // Extremely defensive fallback.
        for (int tileId = 0; tileId < tilesCount; tileId++)
        {
            if (TileExists(tileId))
            {
                return tileId;
            }
        }

        Log.Error("BetterRimworlds.Stargate: Could not find any usable world tile. Falling back to tile 0.");
        return 0;
    }

    private static bool TileExists(int tileId)
    {
        if (tileId < 0 || tileId >= Find.WorldGrid.TilesCount)
        {
            return false;
        }

        Tile tile = GetTile(tileId);
        return tile != null;
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

// 3. Skip colonist selection and start game immediately.
[HarmonyPatch(typeof(Page_ConfigureStartingPawns), "PostOpen")]
public static class Patch_Page_ConfigureStartingPawns_PostOpen
{
    public static void Postfix(Page_ConfigureStartingPawns __instance)
    {
        if (!StargateScenarioUtility.IsStargateBaseScenario())
        {
            return;
        }

        // CRITICAL:
        // Do not clear startingAndOptionalPawns here.
        //
        // The Stargate facility ScenPart uses the generated pawn list during map
        // generation. The first pawn is placed into the Guardian's casket.
        //
        // Clearing this list here makes the scenario part unable to place the Guardian.

        // CRITICAL:
        // Because this automation bypasses vanilla Page_SelectStartingSite.DoNext, we
        // must recreate the player faction base world object before InitGameStart().
        StargateAutomationPatches.EnsurePlayerFactionBaseAtStartingTile();

        PageUtility.InitGameStart();
    }
}

internal sealed class StargateDailyPlanetConditions
{
    internal static StargateDailyPlanetConditions Generate()
    {
        string seedString = StargateSeedUtility.GetDailySeed();

        float planetCoverage = StargateSeedUtility.WithDailySubSeed(
            "planet-coverage",
            () =>
            {
                float[] coverages = { 0.3f, 0.5f, 1.0f };
                return coverages[Rand.Range(0, coverages.Length)];
            }
        );

        OverallRainfall rainfall = StargateSeedUtility.WithDailySubSeed(
            "overall-rainfall",
            RandomEnumValue<OverallRainfall>
        );

        OverallTemperature temperature = StargateSeedUtility.WithDailySubSeed(
            "overall-temperature",
            RandomEnumValue<OverallTemperature>
        );

        OverallPopulation population = StargateSeedUtility.WithDailySubSeed(
            "overall-population",
            LoreWeightedPopulation
        );

        return new StargateDailyPlanetConditions(
            seedString,
            planetCoverage,
            rainfall,
            temperature,
            population
        );
    }

    private static T RandomEnumValue<T>()
    {
        Array values = Enum.GetValues(typeof(T));
        return (T)values.GetValue(Rand.Range(0, values.Length));
    }

    private StargateDailyPlanetConditions(
        string seedString,
        float planetCoverage,
        OverallRainfall rainfall,
        OverallTemperature temperature,
        OverallPopulation population
    )
    {
        SeedString = seedString;
        PlanetCoverage = planetCoverage;
        Rainfall = rainfall;
        Temperature = temperature;
        Population = population;
    }

    internal string SeedString { get; }

    internal float PlanetCoverage { get; }

    internal OverallRainfall Rainfall { get; }

    internal OverallTemperature Temperature { get; }

    internal OverallPopulation Population { get; }
    
    private static OverallPopulation LoreWeightedPopulation()
    {
        float roll = Rand.Value;

        if (roll < 0.20f) return OverallPopulation.AlmostNone;     // Abandoned ruins, tiny outpost
        if (roll < 0.45f) return OverallPopulation.Little;         // Primitive villages, mining camps
        if (roll < 0.65f) return OverallPopulation.LittleBitLess;  // Sparse subject world, frontier colony
        if (roll < 0.80f) return OverallPopulation.Normal;         // Modest Goa'uld subject world
        if (roll < 0.90f) return OverallPopulation.LittleBitMore;  // Established civilization
        if (roll < 0.97f) return OverallPopulation.High;           // Significant population center
        return                   OverallPopulation.VeryHigh;       // Exceptional — Langara-tier
    }
}

internal static class StargateScenarioUtility
{
    internal static bool IsStargateBaseScenario()
    {
        return Find.Scenario != null && Find.Scenario.name == "Daily Stargate Outpost";
    }
}
