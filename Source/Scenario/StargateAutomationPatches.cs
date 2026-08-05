// ==== Source/Scenario/StargateAutomationPatches.cs ====
using System;
using System.Linq;
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

    /// Destination kind chosen on Page_SelectStargateScenario before world
    /// generation (Random Tile until confirmed).
    internal static StargateScenarioKind SelectedScenarioKind = StargateScenarioKind.RandomTile;

    /// When false, daily planet params are still applied, but the new-game
    /// pages are left in vanilla control (no auto-advance / site skip).
    internal static bool EnableNewGameAutomation = true;

    internal static bool CanAutomateNewGame() =>
        EnableNewGameAutomation && StargateScenarioUtility.IsStargateBaseScenario();

    static StargateAutomationPatches()
    {
        var harmony = new Harmony("com.betterrimworlds.stargate.automation");
        harmony.PatchAll();
        Log.Message("BetterRimworlds.Stargate: Harmony automation patches applied.");
    }
}

// 0. Insert the destination chooser BEFORE world generation.
// Vanilla GetFirstConfigPage appends ScenPart_ConfigPage pages after
// SelectStartingSite (i.e. after "Generating World…"), which is too late.
// We want: Storyteller → chooser → CreateWorldParams → …
[HarmonyPatch(typeof(Scenario), nameof(Scenario.GetFirstConfigPage))]
public static class Patch_Scenario_GetFirstConfigPage
{
    public static void Postfix(Scenario __instance, ref Page __result)
    {
        if (__result == null || !StargateScenarioUtility.ScenarioHasStargateFacility(__instance))
        {
            return;
        }

        // Drop any chooser a ScenPart_ConfigPage in the scenario XML might add,
        // so the player is not asked twice.
        RemovePagesOfType(ref __result, typeof(Page_SelectStargateScenario));

        if (!InsertPageBeforeType(ref __result, new Page_SelectStargateScenario(), typeof(Page_CreateWorldParams)))
        {
            Log.Error(
                "BetterRimworlds.Stargate: Could not insert Page_SelectStargateScenario " +
                "before Page_CreateWorldParams — CreateWorldParams missing from page chain."
            );
        }
    }

    // Unlinks every page of the given type from the doubly-linked page chain.
    private static void RemovePagesOfType(ref Page first, Type pageType)
    {
        Page page = first;
        Page prev = null;

        while (page != null)
        {
            Page next = page.next;

            if (pageType.IsInstanceOfType(page))
            {
                if (prev != null) prev.next = next;
                else first = next;
                if (next != null) next.prev = prev;

                // Detach the removed page from the chain.
                page.prev = null;
                page.next = null;
                page.nextAct = null;
            }
            else
            {
                prev = page;
            }

            page = next;
        }
    }

    // Inserts toInsert immediately before the first page of beforeType.
    // Returns false if that type is absent.
    private static bool InsertPageBeforeType(ref Page first, Page toInsert, Type beforeType)
    {
        Page page = first;
        Page prev = null;

        while (page != null)
        {
            if (beforeType.IsInstanceOfType(page))
            {
                toInsert.prev = prev;
                toInsert.next = page;
                page.prev = toInsert;
                if (prev != null) prev.next = toInsert;
                else first = toInsert;
                return true;
            }

            prev = page;
            page = page.next;
        }

        return false;
    }
}

// 1. Auto-configure world parameters and click Next automatically.
//
// The daily seed controls the planet and its conditions, but NOT the Stargate
// starting tile. Same UTC day => same planet; new game => tile within the
// chosen kind, picked after the planet exists.
[HarmonyPatch(typeof(Page_CreateWorldParams), "PostOpen")]
public static class Patch_Page_CreateWorldParams_PostOpen
{
    public static void Postfix(Page_CreateWorldParams __instance)
    {
        // Conditions are applied even when automation is off; only auto-advance is gated below.
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

        if (!StargateAutomationPatches.EnableNewGameAutomation)
        {
            Log.Message("BetterRimworlds.Stargate: Automation disabled — leaving Page_CreateWorldParams open.");
            return;
        }

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

// 2. Skip the vanilla site-selection map; pick the tile now that the world exists.
//
// CRITICAL: hook PostOpen, not PreOpen. WindowStack.Add runs PreOpen before the
// window is in the stack, so Close() during PreOpen is a no-op and the page
// still ends up focused. By PostOpen the window is in the stack and Close()
// removes it.
//
// Tile selection uses the kind chosen before world gen:
//   Random Tile => any valid tile | Atlantis => ocean tile | Tok'ra => impassable tile
//
// Do NOT show the scenario intro dialog here — that fires after map gen.
[HarmonyPatch(typeof(Page_SelectStartingSite), "PostOpen")]
public static class Patch_Page_SelectStartingSite_PostOpen
{
    public static bool Prefix(Page_SelectStartingSite __instance)
    {
        if (!StargateAutomationPatches.CanAutomateNewGame())
        {
            return true;
        }

        StargateScenarioKind kind = StargateAutomationPatches.SelectedScenarioKind;
        int selectedTile = StargateDestinationSelector.SelectDestinationTile(kind);
        Find.GameInitData.startingTile = selectedTile;

        // PrepForMapGen indexes startingAndOptionalPawns by startingPawnCount.
        Find.GameInitData.startingPawnCount =
            Find.GameInitData.startingAndOptionalPawns?.Count ?? 0;

        Log.Message(
            "BetterRimworlds.Stargate: Scenario kind " + kind +
            " selected starting tile " + selectedTile + "."
        );

        // Capture the stitched chain before Close clears window state.
        Page next = __instance.next;
        Action nextAct = __instance.nextAct;

        // PostOpen: the window is in the stack, so Close removes it.
        __instance.Close(false);

        if (next != null)
        {
            Find.WindowStack.Add(next);
        }
        else if (nextAct != null)
        {
            // No further config pages — start the game (nextAct = InitGameStart).
            nextAct();
        }
        else
        {
            Log.Error("BetterRimworlds.Stargate: SelectStartingSite has no next page or nextAct.");
        }

        // Skip vanilla PostOpen (ChooseRandomStartingTile / planet camera tutorials).
        return false;
    }
}

/// Picks a starting tile for a Stargate scenario kind. Uses normal RNG (not the
/// daily seed) so each new game can land on a different destination on the same daily planet.
internal static class StargateDestinationSelector
{
    internal static int SelectDestinationTile(StargateScenarioKind kind)
    {
        switch (kind)
        {
            case StargateScenarioKind.AtlantisRising:
                return SelectMatchingStargateDestinationTile(IsOceanTile, "ocean (Atlantis Rising)");
            case StargateScenarioKind.AbandonedTokraBase:
                return SelectMatchingStargateDestinationTile(IsImpassableTile, "impassable (Abandoned Tok'ra Base)");
            default:
                return SelectMatchingStargateDestinationTile(tile => true, "random");
        }
    }

    private static int SelectMatchingStargateDestinationTile(Func<Tile, bool> predicate, string kindLabel)
    {
        int tilesCount = Find.WorldGrid.TilesCount;

        if (tilesCount <= 0)
        {
            Log.Error("BetterRimworlds.Stargate: WorldGrid has no tiles. Falling back to tile 0.");
            return 0;
        }

        // Deliberately NOT daily-seeded: same daily planet, random destination within the chosen kind.
        for (int attempt = 0; attempt < 2000; attempt++)
        {
            int tileId = Rand.Range(0, tilesCount);

            if (TileMatches(tileId, predicate))
            {
                return tileId;
            }
        }

        // Defensive fallbacks: linear scan for a matching tile, then any tile at all.
        for (int tileId = 0; tileId < tilesCount; tileId++)
        {
            if (TileMatches(tileId, predicate))
            {
                Log.Warning(
                    "BetterRimworlds.Stargate: Random sampling failed for " + kindLabel +
                    "; using linear-scan tile " + tileId + "."
                );
                return tileId;
            }
        }

        for (int tileId = 0; tileId < tilesCount; tileId++)
        {
            if (TileExists(tileId))
            {
                Log.Error(
                    "BetterRimworlds.Stargate: No " + kindLabel +
                    " tile found. Falling back to tile " + tileId + "."
                );
                return tileId;
            }
        }

        Log.Error("BetterRimworlds.Stargate: Could not find any usable world tile. Falling back to tile 0.");
        return 0;
    }

    private static bool IsOceanTile(Tile tile) => tile != null && tile.WaterCovered;

    private static bool IsImpassableTile(Tile tile) => tile != null && tile.hilliness == Hilliness.Impassable;

    private static bool TileMatches(int tileId, Func<Tile, bool> predicate) =>
        TileExists(tileId) && predicate(GetTile(tileId));

    private static bool TileExists(int tileId) =>
        tileId >= 0 && tileId < Find.WorldGrid.TilesCount && GetTile(tileId) != null;

    private static Tile GetTile(int tileId)
    {
#if RIMWORLD16
        return Find.WorldGrid[tileId];
#else
        return Find.WorldGrid.tiles[tileId];
#endif
    }
}

// 3. Safety net: if ConfigureStartingPawns is re-enabled in the scenario XML,
// skip colonist selection and start the game immediately.
[HarmonyPatch(typeof(Page_ConfigureStartingPawns), "PostOpen")]
public static class Patch_Page_ConfigureStartingPawns_PostOpen
{
    public static void Postfix(Page_ConfigureStartingPawns __instance)
    {
        if (!StargateAutomationPatches.CanAutomateNewGame())
        {
            return;
        }

        // Do not clear startingAndOptionalPawns — the facility ScenPart uses
        // the first pawn for the Guardian's cryptosleep casket.
        Find.GameInitData.startingPawnCount =
            Find.GameInitData.startingAndOptionalPawns?.Count ?? 0;

        // Settlement is created by ScenPart_PlayerFaction.PreMapGenerate inside
        // InitGameStart — do not pre-create it here (duplicate world objects).
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

        if (roll < 0.20f) return OverallPopulation.AlmostNone;
        if (roll < 0.45f) return OverallPopulation.Little;
        if (roll < 0.65f) return OverallPopulation.LittleBitLess;
        if (roll < 0.80f) return OverallPopulation.Normal;
        if (roll < 0.90f) return OverallPopulation.LittleBitMore;
        if (roll < 0.97f) return OverallPopulation.High;
        return                   OverallPopulation.VeryHigh;
    }
}

internal static class StargateScenarioUtility
{
    /// Identifies a Stargate scenario by its parts, not its translatable display
    /// name. The scenario-parameter variant is used while stitching the page
    /// chain, when Find.Scenario may not be the right object to trust alone.
    internal static bool ScenarioHasStargateFacility(Scenario scenario) =>
        scenario != null && scenario.AllParts.Any(p => p is ScenPart_StargateFacility);

    internal static bool IsStargateBaseScenario() => ScenarioHasStargateFacility(Find.Scenario);
}
