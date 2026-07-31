using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Enhanced_Development.Stargate.Saving;

[StaticConstructorOnStartup]
internal static class StargateHediffLoadPatches
{
    static StargateHediffLoadPatches()
    {
        Harmony harmony = new Harmony("betterrimworlds.stargate.hediff-load");
        TryPatch(harmony, "InitLoading", nameof(InitLoadingPrefix), null, new[] { typeof(string) });
        TryPatch(harmony, "FinalizeLoading", null, nameof(CleanupPostfix));
        TryPatch(harmony, "ForceStop", null, nameof(CleanupPostfix));
    }

    private static void TryPatch(Harmony harmony, string methodName, string prefixName, string postfixName, Type[] argumentTypes = null)
    {
        MethodInfo target = AccessTools.Method(typeof(ScribeLoader), methodName, argumentTypes);
        if (target == null)
        {
            Log.Warning("Stargate hediff compatibility hook not found: ScribeLoader." + methodName);
            return;
        }

        harmony.Patch(
            target,
            prefix: prefixName == null ? null : new HarmonyMethod(typeof(StargateHediffLoadPatches), prefixName),
            postfix: postfixName == null ? null : new HarmonyMethod(typeof(StargateHediffLoadPatches), postfixName));
    }

    private static void InitLoadingPrefix(object[] __args)
    {
        if (__args == null || __args.Length == 0 || !(__args[0] is string path) || StargateHediffXmlCompatibility.IsPreparedFile(path))
        {
            return;
        }

        try
        {
            string preparedPath = StargateHediffXmlCompatibility.PrepareLoadFile(path);
            __args[0] = preparedPath;
        }
        catch (Exception e)
        {
            Log.Warning("Stargate hediff compatibility preprocessing failed; loading original file: " + e.Message);
            __args[0] = path;
        }
    }

    private static void CleanupPostfix()
    {
        StargateHediffXmlCompatibility.CleanupPreparedFiles();
    }
}
