using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate.Services
{
    /// Restores prisoner faction and guest-host state when a pawn crosses a Stargate.
    public static class StargatePrisonerService
    {
        public static void SetFaction(Pawn pawn)
        {
            if (!pawn.def.CanHaveFaction)
                return;
            if (pawn.guest == null || !pawn.guest.IsPrisoner)
            {
                pawn.SetFactionDirect(Faction.OfPlayer);
                return;
            }
            Faction faction = ResolveFaction(pawn);
            pawn.SetFactionDirect(faction);
            RestoreHost(pawn);
        }

        /// Ensures a rematerializing prisoner has HostFaction = player colony.
        /// Cross-world Stargate loads lose Scribe faction references, leaving
        /// IsPrisoner=true with HostFaction=null, which NREs IsPrisonerOfColony
        /// during MapPawns.RegisterPawn on spawn.
        public static void EnsurePrisonerHost(Pawn pawn)
        {
            if (pawn.guest == null || !pawn.guest.IsPrisoner)
                return;
            if (pawn.guest.HostFaction == Faction.OfPlayer)
                return;
            RestoreHost(pawn);
        }

        private static Faction ResolveFaction(Pawn pawn)
        {
            FactionDef origin = pawn.Faction != null ? pawn.Faction.def : null;
#if !RIMWORLD16
            if (origin == null && pawn.kindDef != null)
                origin = pawn.kindDef.defaultFactionType;
#endif
            string originName = origin != null ? origin.defName : string.Empty;
            string targetName = Classify(originName);
            if (targetName == null && pawn.kindDef != null)
            {
#if !RIMWORLD16
                targetName = Classify(pawn.kindDef.defaultFactionType != null
                    ? pawn.kindDef.defaultFactionType.defName : string.Empty);
#endif
                if (targetName == null)
                    targetName = Classify(pawn.kindDef.defName);
            }

            if (targetName != null)
            {
                FactionDef targetDef = DefDatabase<FactionDef>.GetNamedSilentFail(targetName);
                Faction faction = targetDef != null ? Find.FactionManager.FirstFactionOfDef(targetDef) : null;
                if (faction != null)
                    return faction;
                Log.Warning("Stargate: no existing faction found for prisoner origin " + originName +
                    " (wanted " + targetName + ").");
            }
            return Faction.OfAncientsHostile;
        }

        private static string Classify(string factionDefName)
        {
            if (factionDefName.IndexOf("pirate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pirate";
            if (factionDefName.StartsWith("tribe", StringComparison.OrdinalIgnoreCase) ||
                factionDefName.StartsWith("tribal", StringComparison.OrdinalIgnoreCase))
                return "TribeSavage";
            return null;
        }

        private static void RestoreHost(Pawn pawn)
        {
            if (pawn.guest == null || !pawn.guest.IsPrisoner)
                return;
            if (pawn.guest.HostFaction == Faction.OfPlayer)
                return;

            try
            {
                // Prefer a direct hostFactionInt write. SetGuestStatus on 1.4+ re-runs the
                // full capture pipeline (drops gear, recalculates resistance/will, requires
                // workSettings and kindDef resistance ranges) and can throw on unspawned
                // rematerializing pawns — leaving HostFaction null and breaking spawn via
                // IsPrisonerOfColony. Direct assignment preserves origin prisoner stats.
                if (TrySetHostFactionDirect(pawn.guest, Faction.OfPlayer))
                    return;

                // Fallback: HostFaction setter if present, else SetGuestStatus via reflection.
                var property = pawn.guest.GetType().GetProperty("HostFaction");
                if (property != null && property.CanWrite)
                {
                    property.SetValue(pawn.guest, Faction.OfPlayer, null);
                    return;
                }

                if (pawn.RaceProps.Humanlike && pawn.workSettings == null)
                    pawn.workSettings = new Pawn_WorkSettings(pawn);

                var method = pawn.guest.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "SetGuestStatus" && m.GetParameters().Length == 2);
                if (method != null)
                {
                    Type statusType = method.GetParameters()[1].ParameterType;
                    object status = statusType == typeof(bool) ? (object)true : Enum.Parse(statusType, "Prisoner");
                    method.Invoke(pawn.guest, new object[] { Faction.OfPlayer, status });
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Stargate: could not restore prisoner host for " + pawn.Label + ": " + ex);
            }
        }

        private static bool TrySetHostFactionDirect(Pawn_GuestTracker guest, Faction host)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo field = guest.GetType().GetField("hostFactionInt", flags);
            if (field == null || field.FieldType != typeof(Faction))
                return false;
            field.SetValue(guest, host);
            return true;
        }
    }
}
