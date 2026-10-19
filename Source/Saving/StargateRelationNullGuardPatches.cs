// ==== Source/Saving/StargateRelationNullGuardPatches.cs ====
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate.Saving
{
    /// Drops direct relations whose counterpart never resolved during load.
    ///
    /// Stargate buffer files carry pawns whose Pawn_RelationsTracker is
    /// serialized wholesale, including relations to pawns that stayed in the
    /// origin world (parents, siblings, in-laws). Those cross-references cannot
    /// resolve in the destination save, so Scribe leaves otherPawn null.
    /// Vanilla's Pawn_RoyaltyTracker.ExposeData then runs AssignHeirIfNone
    /// during PostLoadInit, and RoyalTitleInheritanceWorker.FindHeir ->
    /// Pawn_RelationsTracker.OpinionOf walks those relations, where workers
    /// such as PawnRelationWorker_ParentInLaw follow me -> spouse -> spouse's
    /// parents chains and dereference the null pawn:
    ///
    ///   Could not do PostLoadInit on RimWorld.Pawn_RoyaltyTracker:
    ///   NullReferenceException at PawnRelationWorker_ParentInLaw.InRelation
    ///
    /// A relation with a null otherPawn is unusable by vanilla code under any
    /// circumstance, so removing the entry is strictly safer than keeping it.
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.ExposeData))]
    public static class StargateNullRelationCleanupPatch
    {
        internal static readonly FieldInfo PawnField =
            AccessTools.Field(typeof(Pawn_RelationsTracker), "pawn");

        public static void Postfix(Pawn_RelationsTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            RemoveUnresolvedRelations(
                __instance.DirectRelations, PawnOf(__instance));
        }

        // The owning pawn field is private in current RimWorld versions.
        internal static Pawn PawnOf(Pawn_RelationsTracker tracker)
        {
            return PawnField?.GetValue(tracker) as Pawn;
        }

        internal static void RemoveUnresolvedRelations(
            List<DirectPawnRelation> relations, Pawn pawn)
        {
            if (relations == null)
            {
                return;
            }

            int removed = relations.RemoveAll(rel => rel == null || rel.otherPawn == null);

            if (removed > 0)
            {
                Log.Warning(
                    $"[Stargate] Removed {removed} unresolved relation(s) from " +
                    $"{pawn?.LabelShortCap ?? "an unknown pawn"} whose counterpart " +
                    "did not load with the Stargate buffer."
                );
            }
        }
    }

    /// OpinionOf is the path vanilla heir search uses (FindHeir -> OpinionOf ->
    /// GetRelations -> worker InRelation). Pawn registration order does not
    /// guarantee that every related tracker has been cleaned before a royalty
    /// tracker is processed, so sanitize both pawns here before vanilla
    /// enumerates, and treat a missing counterpart or tracker as a neutral
    /// opinion instead of crashing the whole PostLoadInit pass.
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.OpinionOf))]
    public static class StargateOpinionOfNullRelationGuardPatch
    {
        public static bool Prefix(
            Pawn_RelationsTracker __instance, Pawn other, ref int __result)
        {
            StargateNullRelationCleanupPatch.RemoveUnresolvedRelations(
                __instance.DirectRelations, StargateNullRelationCleanupPatch.PawnOf(__instance));

            if (other == null || other.relations == null)
            {
                __result = 0;
                return false;
            }

            StargateNullRelationCleanupPatch.RemoveUnresolvedRelations(
                other.relations.DirectRelations, other);

            return true;
        }
    }
}
