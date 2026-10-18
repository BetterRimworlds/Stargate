// ==== Source/Saving/StargateLoadIdRemapper.cs ====
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
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
    /// Describes one inbound pawn whose numeric ID changed. The saved name is
    /// included so a relationship snapshot can be updated only when it really
    /// points at that pawn, not at an unrelated thing that reused the old ID.
    public sealed class PawnIdRemap
    {
        public readonly int PreviousId;
        public readonly int NewId;
        public readonly Name PawnName;

        public PawnIdRemap(int previousId, int newId, Name pawnName)
        {
            this.PreviousId = previousId;
            this.NewId = newId;
            this.PawnName = pawnName;
        }
    }

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

    /// Gives imported pawns a destination-world thingIDNumber when their saved ID
    /// is already used by another pawn. Cross-world buffers carry IDs from the
    /// origin save's sequence, which can collide with unrelated pawns here.
    public static int RemapCollidingPawnThingIds(IEnumerable<Pawn> importedPawns)
    {
        return RemapCollidingPawnThingIds(importedPawns, out _);
    }

    public static int RemapCollidingPawnThingIds(
        IEnumerable<Pawn> importedPawns,
        out List<PawnIdRemap> remaps)
    {
        remaps = new List<PawnIdRemap>();

        if (importedPawns == null || Find.UniqueIDsManager == null)
        {
            return 0;
        }

        List<Pawn> inbound = importedPawns.Where(pawn => pawn != null).ToList();

        if (inbound.Count == 0)
        {
            return 0;
        }

        // Pawn.GetHashCode() uses thingIDNumber, so use reference identity: two
        // distinct imported pawns with the same ID must still both be considered.
        var inboundSet = new HashSet<Pawn>(inbound, ReferenceEqualityComparer.Instance);
        var usedIds = new HashSet<int>();

        foreach (Pawn pawn in PawnsFinder.All_AliveOrDead)
        {
            if (pawn != null && !inboundSet.Contains(pawn))
            {
                usedIds.Add(pawn.thingIDNumber);
            }
        }

        foreach (Pawn pawn in Find.WorldPawns.AllPawnsAliveOrDead)
        {
            if (pawn != null && !inboundSet.Contains(pawn))
            {
                usedIds.Add(pawn.thingIDNumber);
            }
        }

        int remappedCount = 0;

        foreach (Pawn pawn in inbound)
        {
            if (usedIds.Add(pawn.thingIDNumber))
            {
                continue;
            }

            int previousId = pawn.thingIDNumber;
            int newId;

            do
            {
                newId = Find.UniqueIDsManager.GetNextThingID();
            }
            while (!usedIds.Add(newId));

            pawn.thingIDNumber = newId;
            remaps.Add(new PawnIdRemap(previousId, newId, pawn.Name));
            remappedCount++;

            Log.Warning(
                $"[Stargate] Remapped duplicate pawn thingID {previousId} -> {newId} " +
                $"({pawn.LabelShortCap}) to avoid cross-world ID collisions."
            );
        }

        return remappedCount;
    }

    /// Rewrites snapshot relation IDs after their pawns were remapped. A
    /// name check prevents a relation from being reassigned to a different
    /// pawn that merely shared the old numeric ID.
    public static int ApplyPawnIdRemapsToRelationships(
        IEnumerable<StargateRelation> relationships,
        IReadOnlyList<PawnIdRemap> remaps)
    {
        if (relationships == null || remaps == null || remaps.Count == 0)
        {
            return 0;
        }

        var remapsByPreviousId = new Dictionary<int, List<PawnIdRemap>>();

        foreach (PawnIdRemap remap in remaps)
        {
            if (!remapsByPreviousId.TryGetValue(remap.PreviousId, out List<PawnIdRemap> candidates))
            {
                candidates = new List<PawnIdRemap>();
                remapsByPreviousId[remap.PreviousId] = candidates;
            }

            candidates.Add(remap);
        }

        int updatedCount = 0;

        foreach (StargateRelation relationship in relationships)
        {
            if (relationship == null
                || !remapsByPreviousId.TryGetValue(relationship.pawnID, out List<PawnIdRemap> candidates))
            {
                continue;
            }

            PawnIdRemap match = candidates.FirstOrDefault(candidate =>
                NamesEqual(candidate.PawnName, relationship.pawnName));

            if (match == null || relationship.pawnID == match.NewId)
            {
                continue;
            }

            relationship.pawnID = match.NewId;
            updatedCount++;
        }

        return updatedCount;
    }

    private static bool NamesEqual(Name first, Name second)
    {
        if (first == null || second == null)
        {
            return first == null && second == null;
        }

        return first.Equals(second);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<Pawn>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public bool Equals(Pawn x, Pawn y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(Pawn obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
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

            // Verb-giving hediff comps (ArchotechArm etc.) own a VerbTracker.
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
    /// changes. Verbs live on <see cref="HediffComp_VerbGiver"/> (not Hediff),
    /// and their loadIDs are prefixed by the parent hediff unique ID
    /// (for example Hediff_12160_0_0_Smash). Both import remapping and
    /// duplicate-save recovery must rewrite them before cross-ref registration.
    public static int RemapOwnedVerbLoadIds(
        Hediff hediff,
        string previousUniqueId)
    {
        if (hediff is not HediffWithComps hediffWithComps
            || hediffWithComps.comps == null
            || previousUniqueId.NullOrEmpty())
        {
            return 0;
        }

        string newUniqueId = hediff.GetUniqueLoadID();
        int remappedCount = 0;

        foreach (HediffComp comp in hediffWithComps.comps)
        {
            if (comp is not HediffComp_VerbGiver verbGiver
                || verbGiver.verbTracker?.AllVerbs == null)
            {
                continue;
            }

            foreach (Verb verb in verbGiver.verbTracker.AllVerbs)
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
