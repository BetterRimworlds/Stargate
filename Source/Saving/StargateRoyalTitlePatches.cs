// ==== Source/Saving/StargateRoyalTitlePatches.cs ====
using HarmonyLib;
using RimWorld;
using Verse;
using BetterRimworlds.Stargate.Services;

namespace BetterRimworlds.Stargate.Saving
{
    /// Repairs corrupt RoyalTitle faction references as soon as a save (or a
    /// Stargate buffer file) finishes loading, before vanilla's PostLoadInit
    /// calls AssignHeirIfNone() with the null faction.
    ///
    /// This makes existing saves self-healing instead of requiring manual XML
    /// edits. StargateRecallOperator also calls the service directly as a
    /// second line of defence for pawns rematerialized from an existing buffer.
    [HarmonyPatch(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.ExposeData))]
    public static class StargateRoyalTitleLoadPatch
    {
        public static void Prefix(Pawn_RoyaltyTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            StargateRoyalTitleService.RepairFactionReferences(__instance?.pawn);
        }
    }

    /// Last-resort guard for any null faction that survives the repair (for
    /// example a mod recreating the title later). Dictionary.TryGetValue(null)
    /// throws ArgumentNullException; returning zero favor is the vanilla
    /// meaning of "no favor with that faction".
    [HarmonyPatch(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.GetFavor))]
    public static class StargateRoyalTitleNullFavorPatch
    {
        public static bool Prefix(Faction faction, ref int __result)
        {
            if (faction != null)
            {
                return true;
            }

            __result = 0;
            return false;
        }
    }
}
