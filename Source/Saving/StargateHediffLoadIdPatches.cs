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
///
/// Verb-owning hediffs (ArchotechArm and similar) also persist verb loadIDs
/// under the hediff unique-ID prefix. Rewrite those in the same pass so verb
/// registration does not collide and the next save/load stays consistent.
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

        // Capture before reassignment: verbs use this as their loadID prefix.
        string previousUniqueId = hediff.GetUniqueLoadID();

        if (!___allObjectsByLoadID.ContainsKey(previousUniqueId))
        {
            return;
        }

        int previousId = hediff.loadID;
        hediff.loadID = Find.UniqueIDsManager.GetNextHediffID();

        // Parents register before nested verbs, so this runs early enough for
        // the verbs' own RegisterLoaded calls to use the rewritten IDs.
        int remappedVerbs = StargateLoadIdRemapper.RemapOwnedVerbLoadIds(
            hediff,
            previousUniqueId
        );

        string verbNote = remappedVerbs > 0
            ? $" (also remapped {remappedVerbs} owned verb loadID(s))"
            : string.Empty;

        Log.Warning(
            $"[Stargate] Remapped duplicate hediff loadID {previousId} -> {hediff.loadID} " +
            $"({hediff.def?.defName} on {hediff.pawn?.LabelShortCap ?? "unknown pawn"}) " +
            $"to repair save corruption from cross-world Stargate travel.{verbNote}"
        );
    }
}
