using System.Collections.Generic;
using System.Linq;
using Verse;

namespace BetterRimworlds.Stargate.Services
{
    internal static class StargateRelationshipSnapshot
    {
        // Keep this list explicit: family relations and modded defs are not
        // mutually exclusive merely because they concern the same pawn.
        // Vanilla divorce/remarriage remove Spouse/ExSpouse respectively;
        // romance and breakup similarly replace Lover/Fiance with ExLover.
        internal static bool Incompatible(string first, string second)
        {
            return (first == "Spouse" && second == "ExSpouse")
                || (first == "ExSpouse" && second == "Spouse")
                || (first == "Spouse" && second == "Fiance")
                || (first == "Fiance" && second == "Spouse")
                || (first == "Spouse" && second == "Lover")
                || (first == "Lover" && second == "Spouse")
                || (first == "Lover" && second == "Fiance")
                || (first == "Fiance" && second == "Lover")
                || (first == "ExLover" && (second == "Lover" || second == "Fiance"))
                || (second == "ExLover" && (first == "Lover" || first == "Fiance"));
        }

        internal static List<StargateRelation> Merge(
            IEnumerable<StargateRelation> saved, IEnumerable<StargateRelation> live)
        {
            var snapshot = new List<StargateRelation>(saved ?? Enumerable.Empty<StargateRelation>());
            var current = live.ToList();

            // Positive live evidence supersedes an incompatible saved state even
            // when that live entry was already present in the snapshot.
            snapshot.RemoveAll(existing => existing != null && current.Any(rel =>
                existing.pawnID == rel.pawnID && Incompatible(existing.relationship, rel.relationship)));

            foreach (var rel in current)
            {
                if (rel == null)
                {
                    continue;
                }

                // Merge each live pair individually. Multi-valued relation types
                // (Parent, Child, Sibling, Kin, ExSpouse) legitimately hold many
                // entries, so wholesale replacement by relationship type would
                // drop saved counterparts that are merely not visible in the
                // partially loaded live tracker.
                bool alreadyKnown = snapshot.Any(existing => existing != null
                    && existing.relationship == rel.relationship
                    && existing.pawnID == rel.pawnID);

                if (alreadyKnown)
                {
                    continue;
                }

                // Positive live evidence supersedes conflicting saved states for
                // the same pawn only (engagement -> marriage, breakup, divorce).
                snapshot.RemoveAll(existing => existing != null
                    && existing.pawnID == rel.pawnID
                    && Incompatible(existing.relationship, rel.relationship));

                snapshot.Add(rel);
            }

            return snapshot;
        }

        internal static bool HasConflictingLiveRelation(Pawn pawn, Pawn counterpart, string relationship)
        {
            // Romantic relations above are reflexive in vanilla. Consult BOTH
            // trackers before AddDirectRelation can propagate a stale snapshot
            // to the other pawn, independent of which snapshot rebuilds first.
            return HasConflict(pawn, counterpart, relationship)
                || HasConflict(counterpart, pawn, relationship);
        }

        private static bool HasConflict(Pawn pawn, Pawn counterpart, string relationship)
        {
            return pawn?.relations?.DirectRelations?.Any(rel => rel?.otherPawn == counterpart
                && rel.def != null && Incompatible(relationship, rel.def.defName)) == true;
        }
    }
}
