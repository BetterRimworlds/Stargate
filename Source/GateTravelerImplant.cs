// ==== Source/GateTravelerImplant.cs ====
using System.Collections.Generic;
using System.Linq;
using Enhanced_Development.Stargate.Saving;
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
    public class GateTravelerImplant : Hediff_Implant
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

        private void RefreshRelationships()
        {
            if (this.pawn?.relations?.DirectRelations == null)
            {
                return;
            }

            foreach (DirectPawnRelation rel in this.pawn.relations.DirectRelations)
            {
                Pawn otherPawn = rel.otherPawn;

                if (otherPawn == null)
                {
                    continue;
                }

                string relationshipName = rel.def.defName;

                bool alreadyStored = this.relationships.Any(existing =>
                    existing.pawnID == otherPawn.thingIDNumber &&
                    existing.relationship == relationshipName
                );

                if (alreadyStored)
                {
                    continue;
                }

                this.relationships.Add(new StargateRelation(
                    otherPawn,
                    relationshipName,
                    rel
                ));
            }
        }
    }
}