// ==== Source/Scenario/SkyfallerPatches.cs ====
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

[StaticConstructorOnStartup]
public static class SkyfallerPatches
{
    private static readonly HashSet<string> SkyAccessIncidentDefNames = new()
    {
        "CargoPodCrash",
        "RefugeePodCrash",   // Vanilla "Transport pod crash"
        "ShipChunkDrop",
    };

    static SkyfallerPatches()
    {
        var harmony = new Harmony("betterrimworlds.stargate.skyfallers");

        foreach (Type type in typeof(IncidentWorker).Assembly.GetTypes()
                     .Where(t => typeof(IncidentWorker).IsAssignableFrom(t)))
        {
            PatchDeclaredMethod(harmony, type, nameof(IncidentWorker.CanFireNow), postfix: nameof(CanFireNow_Postfix));
            PatchDeclaredMethod(harmony, type, nameof(IncidentWorker.TryExecute), prefix: nameof(TryExecute_Prefix));
        }
    }

    private static void PatchDeclaredMethod(Harmony harmony, Type type, string methodName, string? prefix = null, string? postfix = null)
    {
        MethodInfo? method = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
        );

        if (method == null) return;

        harmony.Patch(
            method,
            prefix: prefix == null ? null : new HarmonyMethod(typeof(SkyfallerPatches), prefix),
            postfix: postfix == null ? null : new HarmonyMethod(typeof(SkyfallerPatches), postfix)
        );
    }

    public static void CanFireNow_Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (!__result) return;
        if (!ShouldBlockSkyIncident(__instance, parms)) return;

        __result = false;
    }

    public static bool TryExecute_Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (!ShouldBlockSkyIncident(__instance, parms)) return true;

        __result = false;
        return false;
    }

    private static bool ShouldBlockSkyIncident(IncidentWorker worker, IncidentParms parms)
    {
        if (worker?.def == null) return false;
        if (parms.target is not Map map) return false;

        bool requiresSky =
            worker.def.HasModExtension<RequiresSkyAccess>() ||
            SkyAccessIncidentDefNames.Contains(worker.def.defName);

        if (!requiresSky) return false;

        var sealedComp = map.GetComponent<MapComponent_SealedFromSky>();
        return sealedComp?.isSealed == true;
    }
}
