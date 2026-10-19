// ==== Source/Services/StargateRoyalTitleService.cs ====
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate.Services
{
    /// Repairs Royalty title faction references after a save load or a
    /// cross-world Stargate transfer.
    ///
    /// Scribe stores a RoyalTitle's faction as a loadID reference. That loadID
    /// belongs to the origin game's faction list and may not mean the same
    /// thing in the destination game (the Empire is regenerated with a new
    /// FactionDef each game, and loadIDs are not stable across worlds). When
    /// the reference cannot be resolved it becomes null, which later crashes
    /// Pawn_RoyaltyTracker.PostLoadInit and the pawn's Bio tab in GetFavor().
    ///
    /// Titles are therefore re-bound by RoyalTitleDef against the destination
    /// world's factions. The saved faction loadID is never trusted. If no
    /// faction in this world awards the title, the title is removed so the
    /// pawn can still be displayed and used normally.
    public static class StargateRoyalTitleService
    {
        /// Repairs every RoyalTitle on <paramref name="pawn"/>. Safe to call
        /// repeatedly and on pawns without Royalty data.
        /// <returns>True when at least one title was rebound or removed.</returns>
        public static bool RepairFactionReferences(Pawn pawn)
        {
            if (pawn == null || pawn.royalty == null)
            {
                return false;
            }

            List<RoyalTitle> titles = pawn.royalty.AllTitlesForReading;
            if (titles == null || titles.Count == 0)
            {
                return false;
            }

            bool changed = false;

            // Iterate backwards because invalid titles are removed in place.
            for (int i = titles.Count - 1; i >= 0; i--)
            {
                RoyalTitle title = titles[i];

                if (title == null || title.def == null)
                {
                    // Vanilla removes null-def titles too, but doing it here
                    // happens before AssignHeirIfNone() dereferences the title.
                    titles.RemoveAt(i);
                    changed = true;
                    continue;
                }

                Faction replacement = ResolveFactionForTitle(pawn, title);

                if (replacement == null)
                {
                    Log.Warning(
                        $"[Stargate] Removed royal title '{title.def.defName}' from " +
                        $"{pawn.LabelShortCap}: no faction in this world awards that title."
                    );
                    titles.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (!ReferenceEquals(title.faction, replacement))
                {
                    Log.Warning(
                        $"[Stargate] Rebound royal title '{title.def.defName}' on " +
                        $"{pawn.LabelShortCap} to faction '{replacement.Name}'."
                    );
                    title.faction = replacement;
                    changed = true;
                }
            }

            if (changed)
            {
                RemoveDuplicateTitles(titles);
            }

            RemoveNullFactionPermits(pawn);

            return changed;
        }

        /// Returns a faction in the current world that awards this title.
        /// A still-valid faction reference is kept; null or stale references
        /// are re-resolved by title def rather than by saved loadID.
        private static Faction ResolveFactionForTitle(Pawn pawn, RoyalTitle title)
        {
            RoyalTitleDef titleDef = title.def;

            if (FactionAwardsTitle(title.faction, titleDef))
            {
                return title.faction;
            }

            // Prefer the pawn's own faction if it legitimately awards the title.
            if (FactionAwardsTitle(pawn.Faction, titleDef))
            {
                return pawn.Faction;
            }

            FactionManager factionManager = Find.FactionManager;
            if (factionManager == null)
            {
                return null;
            }

            // Bias toward the current game's Empire when the title is Imperial.
            // The Empire FactionDef is regenerated every game, so resolve it
            // through FactionDefOf.Empire instead of remembering its loadID.
            Faction empire = factionManager.FirstFactionOfDef(FactionDefOf.Empire);
            if (FactionAwardsTitle(empire, titleDef))
            {
                return empire;
            }

            List<Faction> factions = factionManager.AllFactionsListForReading;
            if (factions == null)
            {
                return null;
            }

            // The Empire is regenerated with a new FactionDef/loadID each game,
            // so match by the title definitions on that faction's def. This
            // also covers modded royal factions and non-Empire titles.
            for (int i = 0; i < factions.Count; i++)
            {
                Faction faction = factions[i];
                if (FactionAwardsTitle(faction, titleDef))
                {
                    return faction;
                }
            }

            return null;
        }

        private static bool FactionAwardsTitle(Faction faction, RoyalTitleDef titleDef)
        {
            if (faction?.def == null || titleDef == null)
            {
                return false;
            }

            List<RoyalTitleDef> factionTitles = faction.def.RoyalTitlesAllInSeniorityOrderForReading;
            return factionTitles != null && factionTitles.Contains(titleDef);
        }

        /// A rebind can collapse two previously-null titles onto the same
        /// faction. Keep the first occurrence so royalty UI and heir logic do
        /// not see duplicate titles.
        private static void RemoveDuplicateTitles(List<RoyalTitle> titles)
        {
            for (int i = titles.Count - 1; i >= 0; i--)
            {
                RoyalTitle title = titles[i];
                if (title == null)
                {
                    continue;
                }

                for (int j = 0; j < i; j++)
                {
                    RoyalTitle other = titles[j];
                    if (other == null)
                    {
                        continue;
                    }

                    if (ReferenceEquals(other.faction, title.faction) && other.def == title.def)
                    {
                        titles.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// Permits use the same saved faction reference and have no public
        /// setter. Drop only the ones whose faction failed to resolve; a permit
        /// with no owner faction cannot be used or displayed correctly.
        private static void RemoveNullFactionPermits(Pawn pawn)
        {
            List<FactionPermit> permits = pawn.royalty.AllFactionPermits;
            if (permits == null || permits.Count == 0)
            {
                return;
            }

            int removed = permits.RemoveAll(permit => permit == null || permit.Faction == null);
            if (removed > 0)
            {
                Log.Warning(
                    $"[Stargate] Removed {removed} royal permit(s) with missing factions from " +
                    $"{pawn.LabelShortCap}."
                );
            }
        }
    }
}
