// ==== Source/GateTravelerImplant.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate
{
    [DefOf]
    public static class StargateHediffDefOf
    {
        public static HediffDef GateTravelerImplant;

        // Static constructor is required so RimWorld initializes this DefOf.
        static StargateHediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(StargateHediffDefOf));
        }
    }

    /// Tracks a pawn's true lived time, suspended time, and relationships
    /// across Stargate transits and CryoRegenesis de-aging events.
    ///
    /// Vanilla biological age is unreliable once CryoRegenesis enters the
    /// picture, so this implant maintains an independent ledger:
    ///
    ///   consciousAliveTicks        – time actually lived while aging
    ///   trueSuspendedTicks         – time in cryptosleep-style suspension
    ///   cryoRegenesisRemovedTicks  – biological age removed by CryoRegenesis
    ///
    /// The ledger is updated at two lifecycle boundaries:
    ///   1. Entry into the Stargate buffer  (RecordStargateBufferEntry)
    ///   2. Exit from the Stargate buffer   (RecordStargateBufferExit)
    public partial class GateTravelerImplant : Hediff_Implant
    {
        public List<StargateRelation> relationships = new List<StargateRelation>();

        // True lived-time ledger.
        // True once the implant has created its initial lived-time snapshot.
        // Prevents reinitializing the ledger every Stargate transit.
        public bool aliveYearsInitialized = false;

        // Total time the pawn has actually lived while biologically aging.
        // CryoRegenesis does NOT reduce this value.
        public long consciousAliveTicks = 0;

        // Total time the pawn has spent in true suspended states.
        // This includes vanilla cryptosleep-style age gaps, but NOT CryoRegenesis de-aging.
        public long trueSuspendedTicks = 0;

        // Total biological age removed by CryoRegenesis.
        // This is tracked separately so de-aging is not mistaken for cryptosleep.
        public long cryoRegenesisRemovedAgeTicks = 0;

        // Last vanilla biological age observed at a Stargate lifecycle boundary.
        // Used as the baseline for the next Stargate buffer entry.
        public long lastKnownBiologicalAgeTicks = -1;

        // Last vanilla chronological age observed at a Stargate lifecycle boundary.
        // Used as the baseline for the next Stargate buffer entry.
        public long lastKnownChronologicalAgeTicks = -1;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(
                ref this.relationships,
                "relationships",
                LookMode.Deep
            );

            Scribe_Values.Look(ref this.aliveYearsInitialized, "aliveYearsInitialized", false);
            Scribe_Values.Look(ref this.consciousAliveTicks, "consciousAliveTicks", 0L);
            Scribe_Values.Look(ref this.trueSuspendedTicks, "trueSuspendedTicks", 0L);
            Scribe_Values.Look(ref this.cryoRegenesisRemovedAgeTicks, "cryoRegenesisRemovedAgeTicks", 0L);
            Scribe_Values.Look(ref this.lastKnownBiologicalAgeTicks, "lastKnownBiologicalAgeTicks", -1L);
            Scribe_Values.Look(ref this.lastKnownChronologicalAgeTicks, "lastKnownChronologicalAgeTicks", -1L);

            // Always expose research memory. Hediff.pawn is null during LoadingVars
            // (only assigned in ResolvingCrossRefs), so gating on RaceProps here
            // would skip the Scribe calls on load and drop the data. Empty lists
            // are fine for animals that only carry the implant from transit.
            this.ExposeResearchData();
        }

        public override void PostMake()
        {
            base.PostMake();

            this.RefreshRelationships();
        }

        public void RecordStargateBufferEntry()
        {
            if (this.pawn == null)
            {
                return;
            }

            long currentBiologicalTicks = this.pawn.ageTracker.AgeBiologicalTicks;
            long currentChronologicalTicks = this.pawn.ageTracker.AgeChronologicalTicks;

            // First time this pawn ever receives a Gate Traveler age ledger.
            //
            // Before CryoRegenesis starts changing biological age, vanilla biological age
            // is the best estimate of how long the pawn has actually lived.
            //
            // Vanilla chronological age minus biological age is the best estimate of
            // true suspended time before this implant began tracking it.
            if (!this.aliveYearsInitialized)
            {
                this.InitializeAliveYearsLedger(
                    currentBiologicalTicks,
                    currentChronologicalTicks,
                    currentBiologicalTicks
                );

                return;
            }

            // The pawn has been materialized in the world since the last baseline.
            // Biological age normally advances only while the pawn is actually living.
            //
            // If CryoRegenesis has lowered biological age, AddCryoRegenesisRemovedAge()
            // resets this baseline so de-aging is not misread as cryptosleep.
            long livedTicksSinceLastBaseline =
                currentBiologicalTicks - this.lastKnownBiologicalAgeTicks;

            if (livedTicksSinceLastBaseline < 0)
            {
                livedTicksSinceLastBaseline = 0;
            }

            if (livedTicksSinceLastBaseline > 0)
            {
                this.consciousAliveTicks += livedTicksSinceLastBaseline;
            }

            // If chronological age advanced more than biological age, the difference
            // is true suspended time, such as cryptosleep.
            //
            // Stargate buffer time is not counted here because the pawn is timeless
            // while serialized in the event horizon / matter stream.
            long chronologicalTicksSinceLastBaseline =
                currentChronologicalTicks - this.lastKnownChronologicalAgeTicks;

            if (chronologicalTicksSinceLastBaseline < 0)
            {
                chronologicalTicksSinceLastBaseline = 0;
            }

            long suspendedTicksSinceLastBaseline =
                chronologicalTicksSinceLastBaseline - livedTicksSinceLastBaseline;

            if (suspendedTicksSinceLastBaseline > 0)
            {
                this.trueSuspendedTicks += suspendedTicksSinceLastBaseline;
            }

            this.ResetAgeBaselines(currentBiologicalTicks, currentChronologicalTicks);
        }

        public void RecordStargateBufferExit()
        {
            if (this.pawn == null)
            {
                return;
            }

            long currentBiologicalTicks = this.pawn.ageTracker.AgeBiologicalTicks;
            long currentChronologicalTicks = this.pawn.ageTracker.AgeChronologicalTicks;

            // Called after StargateRecall() has corrected BirthAbsTicks for
            // origin/destination timeline drift.
            //
            // The pawn spent no subjective time inside the Stargate buffer.
            // Therefore this method only resets the comparison baseline.
            //
            // It must not add consciousAliveTicks.
            // It must not add trueSuspendedTicks.
            if (!this.aliveYearsInitialized)
            {
                this.InitializeAliveYearsLedger(
                    currentBiologicalTicks,
                    currentChronologicalTicks,
                    currentBiologicalTicks
                );

                return;
            }

            this.ResetAgeBaselines(currentBiologicalTicks, currentChronologicalTicks);
        }

        public long GetTrueAliveTicks()
        {
            return this.consciousAliveTicks;
        }

        public long GetTrueSuspendedTicks()
        {
            return this.trueSuspendedTicks;
        }

        public long GetCryoRegenesisRemovedTicks()
        {
            return this.cryoRegenesisRemovedAgeTicks;
        }

        public void AddCryoRegenesisRemovedAge(long removedTicks)
        {
            if (removedTicks <= 0)
            {
                return;
            }

            if (this.pawn == null)
            {
                return;
            }

            long currentBiologicalTicks = this.pawn.ageTracker.AgeBiologicalTicks;
            long currentChronologicalTicks = this.pawn.ageTracker.AgeChronologicalTicks;

            // If CryoRegenesis creates the ledger for the first time, the pawn's
            // current biological age is already the post-Regenesis value.
            //
            // Add removedTicks back once to estimate the pawn's actually-lived time
            // before de-aging happened.
            if (!this.aliveYearsInitialized)
            {
                long preRegenesisBiologicalTicks = currentBiologicalTicks + removedTicks;

                this.InitializeAliveYearsLedger(
                    currentBiologicalTicks,
                    currentChronologicalTicks,
                    preRegenesisBiologicalTicks
                );
            }

            this.cryoRegenesisRemovedAgeTicks += removedTicks;

            // CryoRegenesis intentionally lowers vanilla biological age.
            //
            // That is not cryptosleep.
            // That is not suspended time.
            // That is not "time not lived."
            //
            // After de-aging, the pawn's new biological age becomes the future
            // comparison baseline. This prevents the next Stargate entry from
            // interpreting biological de-aging as fake suspended time.
            this.ResetAgeBaselines(currentBiologicalTicks, currentChronologicalTicks);
        }

        private void InitializeAliveYearsLedger(
            long currentBiologicalTicks,
            long currentChronologicalTicks,
            long livedTicksEstimate
        )
        {
            this.consciousAliveTicks = livedTicksEstimate;
            this.trueSuspendedTicks = currentChronologicalTicks - livedTicksEstimate;

            if (this.trueSuspendedTicks < 0)
            {
                this.trueSuspendedTicks = 0;
            }

            this.ResetAgeBaselines(currentBiologicalTicks, currentChronologicalTicks);
            this.aliveYearsInitialized = true;
        }

        private void ResetAgeBaselines(long currentBiologicalTicks, long currentChronologicalTicks)
        {
            this.lastKnownBiologicalAgeTicks = currentBiologicalTicks;
            this.lastKnownChronologicalAgeTicks = currentChronologicalTicks;
        }

        /// Refreshes the travel snapshot from the pawn's current direct relations.
        /// Called before the relations tracker is replaced during rematerialization.
        /// <param name="allowEmpty">
        /// When true, the live relation list replaces the saved snapshot, including
        /// clearing it when empty (used when a pawn genuinely enters the gate).
        /// When false, an empty or unresolved list leaves the saved snapshot intact
        /// (used while loading a buffer, where cross-world references may not
        /// resolve).
        /// </param>
        public void RefreshRelationshipsForStargateEntry(bool allowEmpty = false)
        {
            var directRelations = this.pawn?.relations?.DirectRelations;

            if (directRelations == null)
            {
                return;
            }

            if (directRelations.Count == 0)
            {
                if (allowEmpty)
                {
                    this.relationships = new List<StargateRelation>();
                }

                return;
            }

            foreach (DirectPawnRelation rel in directRelations)
            {
                if (rel?.def == null || rel.otherPawn == null)
                {
                    Log.Warning($"Skipping relationship refresh for {this.pawn.LabelShort}; a direct relation did not resolve.");
                    return;
                }
            }

            // A spawned pawn entering the gate has a complete live relation list,
            // so it is authoritative. Rebuild the snapshot from scratch instead of
            // merging, which also removes relation types that ended since the last
            // transit (for example, a Spouse that became an ExSpouse on divorce).
            // Merging by live def cannot see a def that disappeared entirely.
            if (allowEmpty)
            {
                this.RefreshRelationships();
                return;
            }

            // Buffer loads and world-pawn repairs may see an incomplete live list
            // (for example, only the father of a two-parent child), so merge into
            // the saved snapshot instead of replacing it. In that case the snapshot
            // is the better source.
            this.relationships = Services.StargateRelationshipSnapshot.Merge(
                this.relationships,
                directRelations.Select(rel => new StargateRelation(rel.otherPawn, rel.def.defName, rel)));
        }

        private void RefreshRelationships()
        {
            var directRelations = this.pawn?.relations?.DirectRelations;
            var snapshot = new List<StargateRelation>();

            if (directRelations != null)
            {
                foreach (DirectPawnRelation rel in directRelations)
                {
                    if (rel?.def == null || rel.otherPawn == null)
                    {
                        continue;
                    }

                    snapshot.Add(new StargateRelation(
                        rel.otherPawn,
                        rel.def.defName,
                        rel
                    ));
                }
            }

            // Replace the whole snapshot. A pawn may have several direct
            // relations of the same def (for example, two Parent relations),
            // so keep every entry instead of merging by def name.
            this.relationships = snapshot;
        }
    }
}
