// ==== Source/Saving/StargateHediffLoadIdPatches.cs ====
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace BetterRimworlds.Stargate.Saving;

/// Recovers saves that already contain duplicate hediff loadIDs (typically from
/// older Stargate imports that preserved foreign IDs).
///
/// During ResolveAllCrossReferences, each ILoadReferenceable is registered by
/// GetUniqueLoadID(). A second hediff with the same loadID would otherwise log:
///
///   Cannot register ... GateTravelerImplant ... Id already used by ...
///
/// and fail to register, breaking later cross-references. Remap the colliding
/// hediff onto a fresh destination-world ID before registration.
[HarmonyPatch(typeof(LoadedObjectDirectory), nameof(LoadedObjectDirectory.RegisterLoaded))]
public static class StargateHediffLoadIdPatches
{
    public static void Prefix(
        ILoadReferenceable reffable,
        Dictionary<string, ILoadReferenceable> ___allObjectsByLoadID
    )
    {
        if (reffable is not Hediff hediff)
        {
            return;
        }

        if (___allObjectsByLoadID == null || Find.UniqueIDsManager == null)
        {
            return;
        }

        string loadIdKey = hediff.GetUniqueLoadID();

        if (!___allObjectsByLoadID.ContainsKey(loadIdKey))
        {
            return;
        }

        int previousId = hediff.loadID;
        hediff.loadID = Find.UniqueIDsManager.GetNextHediffID();

        Log.Warning(
            $"[Stargate] Remapped duplicate hediff loadID {previousId} -> {hediff.loadID} " +
            $"({hediff.def?.defName} on {hediff.pawn?.LabelShortCap ?? "unknown pawn"}) " +
            "to repair save corruption from cross-world Stargate travel."
        );
    }
}
