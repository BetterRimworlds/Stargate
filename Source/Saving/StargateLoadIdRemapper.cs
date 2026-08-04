// ==== Source/Saving/StargateLoadIdRemapper.cs ====
using System.Collections.Generic;
using Verse;

namespace BetterRimworlds.Stargate.Saving;

/// Remaps load-reference IDs on things imported through the Stargate buffer.
///
/// Cross-world wormholes deep-load pawns from another save's ID space. Hediff
/// loadIDs are preserved from the origin world and are not coordinated with the
/// destination's UniqueIDsManager. That produces collisions such as:
///
///   Cannot register GateTravelerImplant ... id=Hediff_98472
///   Id already used by GateTravelerImplant ...
///
/// Always reassign imported hediff loadIDs from the destination world's
/// sequence so future GetNextHediffID() values cannot reuse them.
public static class StargateLoadIdRemapper
{
    public static void RemapImportedThingLoadIds(IEnumerable<Thing> things)
    {
        if (things == null)
        {
            return;
        }

        if (Find.UniqueIDsManager == null)
        {
            Log.Warning(
                "[Stargate] Cannot remap imported hediff loadIDs: UniqueIDsManager is null."
            );
            return;
        }

        int remappedCount = 0;

        foreach (Thing thing in things)
        {
            remappedCount += RemapThingTree(thing);
        }

        if (remappedCount > 0)
        {
            Log.Message(
                $"[Stargate] Remapped {remappedCount} hediff loadID(s) for imported Stargate cargo."
            );
        }
    }

    public static int RemapPawnHediffLoadIds(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return 0;
        }

        if (Find.UniqueIDsManager == null)
        {
            return 0;
        }

        int remappedCount = 0;

        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff == null)
            {
                continue;
            }

            // Some added-part hediffs, such as ArchotechArm, own a VerbTracker.
            // Their verbs persist IDs derived from the hediff ID (for example,
            // Hediff_12160_0_0_Smash), so keep those IDs in sync when the
            // hediff is moved into the destination world's ID space.
            string previousUniqueId = hediff.GetUniqueLoadID();
            int previousId = hediff.loadID;
            hediff.loadID = Find.UniqueIDsManager.GetNextHediffID();
            remappedCount++;

            remappedCount += RemapOwnedVerbLoadIds(hediff, previousUniqueId);

            if (Prefs.DevMode && previousId >= 0)
            {
                Log.Message(
                    $"[Stargate] Remapped hediff loadID {previousId} -> {hediff.loadID} " +
                    $"({hediff.def?.defName} on {pawn.LabelShortCap})."
                );
            }
        }

        return remappedCount;
    }

    /// Keeps hediff-owned verb loadIDs in sync after the hediff's own loadID
    /// changes. Verb IDs are persisted as prefixes of the hediff unique ID
    /// (for example Hediff_12160_0_0_Smash), so both import remapping and
    /// duplicate-save recovery must rewrite them before cross-ref registration.
    public static int RemapOwnedVerbLoadIds(
        Hediff hediff,
        string previousUniqueId)
    {
        if (hediff?.verbTracker?.AllVerbs == null || previousUniqueId.NullOrEmpty())
        {
            return 0;
        }

        string newUniqueId = hediff.GetUniqueLoadID();
        int remappedCount = 0;

        foreach (Verb verb in hediff.verbTracker.AllVerbs)
        {
            if (verb == null || verb.loadID.NullOrEmpty())
            {
                continue;
            }

            if (!verb.loadID.StartsWith(previousUniqueId))
            {
                continue;
            }

            string suffix = verb.loadID.Substring(previousUniqueId.Length);
            verb.loadID = newUniqueId + suffix;
            remappedCount++;
        }

        return remappedCount;
    }

    private static int RemapThingTree(Thing thing)
    {
        if (thing == null)
        {
            return 0;
        }

        int remappedCount = 0;

        if (thing is Pawn pawn)
        {
            remappedCount += RemapPawnHediffLoadIds(pawn);
        }
        else if (thing is Corpse corpse && corpse.InnerPawn != null)
        {
            remappedCount += RemapPawnHediffLoadIds(corpse.InnerPawn);
        }

        return remappedCount;
    }
}
