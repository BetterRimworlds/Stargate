// ==== Source/Scenario/StargateSeedUtility.cs ====
using System;
using Verse;

namespace BetterRimworlds.Stargate;

[Obsolete("Use BetterRimworlds.Utilities.DailySeedUtility instead. This class will be removed in a future version.")]
internal static class StargateSeedUtility
{
    internal static string GetDailySeed() =>
        BetterRimworlds.Utilities.DailySeedUtility.GetDailySeed();

    internal static int GetDailySubSeed(string purpose) =>
        BetterRimworlds.Utilities.DailySeedUtility.GetDailySubSeed(purpose);

    internal static int StableHash(string text) =>
        BetterRimworlds.Utilities.DailySeedUtility.StableHash(text);

    internal static T WithDailySubSeed<T>(string purpose, Func<T> action) =>
        BetterRimworlds.Utilities.DailySeedUtility.WithDailySubSeed(purpose, action);

    internal static void WithDailySubSeed(string purpose, Action action) =>
        BetterRimworlds.Utilities.DailySeedUtility.WithDailySubSeed(purpose, action);
}