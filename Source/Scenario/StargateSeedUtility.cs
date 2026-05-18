// ==== Source/Scenario/StargateSeedUtility.cs ====
using System;
using Verse;

namespace BetterRimworlds.Stargate;

/// Centralized seed helper for the Stargate Base scenario.
///
/// This class exists because the scenario has two very different kinds of randomness:
///
///   1. DAILY PLANET DETERMINISM
///      Every RimWorld player who starts the Stargate Base scenario on the same UTC date
///      should receive the same base planet.
///
///      Example:
///          2026-05-13
///
///      That date string becomes the shared daily planet seed. Planet-level settings
///      like rainfall, temperature, population, and planet coverage should be derived
///      from that date so the "daily planet" is reproducible worldwide.
///
///   2. LOCAL STARGATE DESTINATION RANDOMNESS
///      The actual starting tile should NOT be derived from the daily seed.
///
///      The planet is shared.
///      The Stargate address is random.
///
///      That means two players starting on the same UTC day can get the same planet,
///      but different destinations:
///
///          Player A: ocean tile       -> Atlantis-style underwater base
///          Player B: impassable tile  -> Tok'ra-style mountain base
///          Player C: normal tile      -> surface Stargate facility
///
/// This utility is only for the deterministic daily planet layer.
/// Do not use this class to pick the starting tile unless you intentionally want every
/// player to get the exact same destination tile for that UTC day.
internal static class StargateSeedUtility
{
    /// Returns the shared daily planet seed.
    ///
    /// This uses UTC, not local time, because players may be in different time zones.
    /// The same UTC date should mean the same daily planet for everyone worldwide.
    ///
    /// Example return value:
    ///     "2026-05-13"
    ///
    /// This string is suitable for RimWorld's world seed field.
    internal static string GetDailySeed()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    /// Produces a deterministic sub-seed for one specific planet-generation purpose.
    ///
    /// Why sub-seeds exist:
    ///
    /// If we used the raw daily seed directly for every setting, then all settings would
    /// share the same random stream. That makes the code fragile. Adding one new random
    /// roll for rainfall could accidentally change temperature, population, or other
    /// settings.
    ///
    /// Instead, each planetary setting gets its own named deterministic seed:
    ///
    ///     2026-05-13|planet-coverage
    ///     2026-05-13|overall-rainfall
    ///     2026-05-13|overall-temperature
    ///     2026-05-13|overall-population
    ///
    /// This keeps each setting stable and independent.
    internal static int GetDailySubSeed(string purpose)
    {
        return StableHash(GetDailySeed() + "|" + purpose);
    }

    /// Stable string hash for deterministic cross-player seeds.
    ///
    /// Important:
    /// Do NOT use string.GetHashCode() for shared deterministic gameplay.
    ///
    /// In modern .NET runtimes, string.GetHashCode() is not guaranteed to be stable
    /// across processes, platforms, framework versions, or runtime configurations.
    /// RimWorld version differences and Mono/.NET differences can also make that a bad
    /// foundation for "everyone gets the same planet" behavior.
    ///
    /// This method uses FNV-1a, a simple deterministic hash algorithm. Given the same
    /// input string, it returns the same integer every time.
    ///
    /// We mask to 0x7fffffff so the result is non-negative and safe to pass into
    /// Verse.Rand.PushState().
    internal static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }

            return (int)(hash & 0x7fffffff);
        }
    }

    /// Temporarily replaces RimWorld's current random state with a deterministic daily
    /// sub-seed, runs the supplied function, then restores the previous random state.
    ///
    /// Use this when deriving a daily planet setting:
    ///
    ///     OverallRainfall rainfall = StargateSeedUtility.WithDailySubSeed(
    ///         "overall-rainfall",
    ///         () => RandomEnumValue&lt;OverallRainfall&gt;()
    ///     );
    ///
    /// The try/finally is critical. If an exception happens while the deterministic
    /// seed is active, Rand.PopState() still runs and RimWorld's global RNG stack is
    /// restored correctly.
    ///
    /// Do NOT wrap TileFinder.RandomStartingTile() in this helper unless you want the
    /// destination tile to become deterministic for the whole UTC day.
    internal static T WithDailySubSeed<T>(string purpose, Func<T> action)
    {
        Rand.PushState(GetDailySubSeed(purpose));

        try
        {
            return action();
        }
        finally
        {
            Rand.PopState();
        }
    }

    /// Void-returning version of WithDailySubSeed.
    ///
    /// This is useful when the deterministic daily operation mutates something directly
    /// instead of returning a value.
    ///
    /// Same warning:
    /// This is for shared daily planet generation only. Do not use it for the random
    /// Stargate destination tile.
    internal static void WithDailySubSeed(string purpose, Action action)
    {
        Rand.PushState(GetDailySubSeed(purpose));

        try
        {
            action();
        }
        finally
        {
            Rand.PopState();
        }
    }
}