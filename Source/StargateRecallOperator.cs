// ==== Source/StargateRecallOperation.cs ====
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace BetterRimworlds.Stargate
{
    /// Encapsulates the per-Thing rematerialization pipeline used by Building_Stargate.StargateRecall().
    /// This class is intentionally a stateless worker: it accepts an inbound buffer plus enough context
    /// (gate position, map, the owning buffer for re-adding failed placements) and performs the surgery
    /// on each Thing/Pawn. The owning Building_Stargate retains orchestration (sounds, IsRecalling flag,
    /// backup file move, relationship rebuild trigger, map mesh dirty).
    public class StargateRecallOperator
    {
        private readonly IntVec3 gatePosition;
        private readonly Map currentMap;
        private readonly StargateBuffer stargateBuffer;

        public StargateRecallOperator(IntVec3 gatePosition, Map currentMap, StargateBuffer stargateBuffer)
        {
            this.gatePosition = gatePosition;
            this.currentMap = currentMap;
            this.stargateBuffer = stargateBuffer;
        }

        /// Processes every Thing in <paramref name="inboundBuffer"/>. Unplaceable Things are re-added to
        /// the owning StargateBuffer (matching the historical behaviour). The list is cleared on exit.
        public void Execute(List<Thing> inboundBuffer, int originalTimelineTicks, bool offworldEvent, out bool hasTransmittedPawns)
        {
            hasTransmittedPawns = false;

            // this.stargateBuffer.Clear();
            foreach (Thing currentThing in inboundBuffer.ToList())
            {
                try
                {
                    // If it's just a teleport, destroy the thing first...
                    // Log.Warning("a1: is offworld? " + offworldEvent + " | Stargate Buffer count: " + this.stargateBuffer.Count);
                    bool wasPlaced = false;
                    if (!offworldEvent)
                    {
                        wasPlaced = HandleLocalTeleport(currentThing);
                        // Readd the unplaced Thing into the stargateBuffer.
                        if (!wasPlaced)
                        {
                            // this.stargateBuffer.TryAdd(currentThing);
                        }

                        continue;
                        // currentThing.Destroy();
                    }

                    // currentThing.thingIDNumber = -1;
                    // Verse.ThingIDMaker.GiveIDTo(currentThing);

                    // If it's an equippable object, like a gun, reset its verbs or ANY colonist that equips it *will* go insane...
                    // This is actually probably the root cause of Colonist Insanity (holding an out-of-phase item with IDs belonging
                    // to an alternate dimension). This is the equivalent of how Olivia goes insane in the TV series Fringe.
                    PrepareThingComps(currentThing);

                    // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                    // if they enter a Stargate after they've ever been drafted.
                    if (currentThing is Pawn pawn)
                    {
                        // Log.Warning("1");
                        hasTransmittedPawns = true;
                        PreparePawnForRematerialization(pawn, originalTimelineTicks, offworldEvent);
                    }

                    // Log.Warning("18");

                    // Try initial placement
                    wasPlaced = TryPlaceThingNearGate(currentThing);

                    // If it's a pawn and placement failed, try with different recovery strategies
                    // Use a different variable name here to avoid the naming conflict
                    if (!wasPlaced && currentThing is Pawn recoveryPawn)
                    {
                        // Log.Warning("19");
                        wasPlaced = AttemptPawnPlacementRecovery(recoveryPawn);
                    }
                    // Log.Warning("21");

                    // Readd the unplaced Thing into the stargateBuffer.
                    if (!wasPlaced)
                    {
                        Log.Warning("Could not place " + currentThing.Label + " after all attempts");
                        this.stargateBuffer.TryAdd(currentThing);
                    }
                    else
                    {
                        // Log.Warning("22");
                        if (currentThing is Pawn thisPawn)
                        {
                            FinalizeSpawnedPawn(thisPawn);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Could not spawn " + currentThing + " because: " + e.Message);
                    inboundBuffer.Remove(currentThing);
                    this.stargateBuffer.TryAdd(currentThing);

                    continue;
                }

                // inboundBuffer.Remove(currentThing);
            }

            inboundBuffer.Clear();
        }

        private bool HandleLocalTeleport(Thing thing)
        {
            bool wasPlaced = GenPlace.TryPlaceThing(thing, this.gatePosition + new IntVec3(0, 0, -2),
                this.currentMap, ThingPlaceMode.Near);
            if (!wasPlaced)
            {
                Log.Warning("Could not place " + thing.Label);
            }
            return wasPlaced;
        }

        private void PrepareThingComps(Thing thing)
        {
            if (thing is ThingWithComps item)
            {
                // item.InitializeComps();
            }
        }

        private void PreparePawnForRematerialization(Pawn pawn, int originalTimelineTicks, bool offworldEvent)
        {
            SetPawnFaction(pawn);
            // Log.Warning("4");

            FixPawnCrownType(pawn);
            // Log.Warning("6");

            pawn.relations = new Pawn_RelationsTracker(pawn);
            // Carry over injuries, sicknesses, addictions, and artificial body parts.
            RebuildPawnHealth(pawn);

            // @FIXME: Animals still have partial Stargate Insanity and many times will never fall asleep
            //         on the new planet. They will drop-down from sheer exhaustion.
            //         Some of them also become Godlings, literally unkillable except via the Dev Mode.
            // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
            // if any. This is effectively the cure of Stargate Insanity.
            // pawn.needs.SetInitialLevels();

            // pawn.verbTracker = new VerbTracker(pawn);
            // pawn.thinker = new Pawn_Thinker(pawn);
            // pawn.mindState = new Pawn_MindState(pawn);
            // pawn.jobs = new Pawn_JobTracker(pawn);
            // pawn.pather = new Pawn_PathFollower(pawn);
            // pawn.caller = new Pawn_CallTracker(pawn);
            // pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
            // pawn.interactions = new Pawn_InteractionsTracker(pawn);
            // pawn.stances = new Pawn_StanceTracker(pawn);
            if (offworldEvent)
            {
                // pawn.relations = new Pawn_RelationsTracker(pawn);
                // pawn.needs = new Pawn_NeedsTracker(pawn);
            }

            pawn.jobs = new Pawn_JobTracker(pawn);
            // pawn.verbTracker = new VerbTracker(pawn);
            // pawn.carryTracker = new Pawn_CarryTracker(pawn);
            pawn.verbTracker.directOwner = pawn;
            pawn.carryTracker.pawn = pawn;
            // Log.Warning("8");

            if (pawn.RaceProps.Humanlike)
            {
                // Log.Warning("9");
                ResetHumanlikeTrackers(pawn);
                ResetPawnCombatState(pawn);
            }
            else
            {
                // Log.Warning("13");
                pawn.ownership = new Pawn_Ownership(pawn);
            }

            // if (pawn.RaceProps.ToolUser)
            // {
            //     if (pawn.equipment == null)
            //         pawn.equipment = new Pawn_EquipmentTracker(pawn);
            //     if (pawn.apparel == null)
            //         pawn.apparel = new Pawn_ApparelTracker(pawn);
            //
            //     // Reset their equipped weapon's verbTrackers as well, or they'll go insane if they're carrying an out-of-phase weapon...
            // }
            ResetPawnEquipmentVerbs(pawn);

            // Remove memories or they will go insane...
            if (pawn.RaceProps.Humanlike)
            {
                // Log.Warning("15");
                ResetHumanlikeMemories(pawn);
            }
            // Log.Warning("16");

            // Alter the pawn's chronological age based upon the temporal drift between their origin universe
            // and the destination universe.
            //
            // The pawn is timeless while serialized inside the Stargate buffer. However, the destination
            // save may be earlier or later than the origin save. Without this correction, a pawn sent from
            // 5524 to 5502 would appear to have negative/incorrect chronological drift, and a pawn sent
            // from 5524 to 5530 would falsely gain years while in the buffer.
            //
            // This adjusts BirthAbsTicks so the pawn keeps the same apparent chronological age after
            // rematerialization, regardless of the destination save's current year.
            //
            // There are 60,000 ticks per day.
            ApplyTimelineCorrection(pawn, originalTimelineTicks);

            // The Gate Traveler implant tracks true lived time / true suspended time.
            // Entry into the Stargate buffer records lived time up to the event horizon.
            // Exit must NOT add the time spent serialized in the buffer, because the buffer is timeless.
            //
            // This reset must happen AFTER BirthAbsTicks is corrected, so the implant's future baseline
            // matches the pawn's corrected vanilla age values in this destination timeline.
            RecordGateTravelerExit(pawn);

            // Give them a brief psychic shock so that they will be given proper Melee Verbs and not act like a Visitor.
            // Hediff shock = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn, null);
            // pawn.health.AddHediff(shock, null, null);
            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);

            // Find.CurrentMap.mapPawns.AllPawnsUnspawned.Remove(pawn);
            // Find.WorldPawns.AllPawnsDead.Remove(pawn);
            RemoveFromDeadPawnsList(pawn);
        }

        private void SetPawnFaction(Pawn pawn)
        {
            if (!pawn.def.CanHaveFaction)
            {
                return;
            }
            // Log.Warning("2");

            if (pawn.guest == null || pawn.guest.IsPrisoner == false)
            {
                pawn.SetFactionDirect(Faction.OfPlayer);
            }
            else
            {
                // Log.Warning("3");
                #if RIMWORLD12
                pawn.SetFaction(Faction.Empire);
                #else
                pawn.SetFactionDirect(Faction.OfEmpire);
                #endif
            }
        }

        private void FixPawnCrownType(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike)
            {
                return;
            }
            // Log.Warning("5");

            // Compatibility shim between Rimworld v1.4 and v1.5 with v1.2 and v1.3.
            var crownTypesByVersion = new Dictionary<string, List<string>>()
            {
                { "1.2", new List<string>() { "Average", "Narrow" } }
            };

            #if RIMWORLD12 || RIMWORLD13
            if (pawn.story.crownType == CrownType.Undefined)
            {
                Log.Warning("Converting Pawn from future Rimworld version to current version.");
                pawn.story.crownType = CrownType.Average;
            }
            #endif
        }

        private void RebuildPawnHealth(Pawn pawn)
        {
            var hediffSet = pawn.health.hediffSet;

            pawn.health = new Pawn_HealthTracker(pawn);
            foreach (var hediff in hediffSet.hediffs.ToList())
            {
                if (hediff is Hediff_MissingPart)
                {
                    continue;
                }
                // Log.Warning("7");

                hediff.pawn = pawn;
                try
                {
                    pawn.health.AddHediff(hediff, hediff.Part);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not add hediff {hediff} to pawn {pawn.Label}: {ex.Message}");
                }
            }
        }

        private void ResetHumanlikeTrackers(Pawn pawn)
        {
            // pawn.thinker = new Pawn_Thinker(pawn);
            // pawn.mindState = new Pawn_MindState(pawn);
            // pawn.jobs = new Pawn_JobTracker(pawn);
            // pawn.pather = new Pawn_PathFollower(pawn);
            // pawn.caller = new Pawn_CallTracker(pawn);
            // pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
            // pawn.interactions = new Pawn_InteractionsTracker(pawn);
            // pawn.stances = new Pawn_StanceTracker(pawn);
            // pawn.relations = new Pawn_RelationsTracker(pawn);
            pawn.rotationTracker = new Pawn_RotationTracker(pawn);
            pawn.thinker = new Pawn_Thinker(pawn);
            pawn.mindState = new Pawn_MindState(pawn);
            pawn.drafter = new Pawn_DraftController(pawn);
            pawn.natives = null;
            pawn.outfits = new Pawn_OutfitTracker(pawn);
            pawn.pather = new Pawn_PathFollower(pawn);
            // pawn.records = new Pawn_RecordsTracker(pawn);
            // pawn.relations = new Pawn_RelationsTracker(pawn);
            pawn.caller = new Pawn_CallTracker(pawn);
            // pawn.needs = new Pawn_NeedsTracker(pawn);
            // pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
            pawn.interactions = new Pawn_InteractionsTracker(pawn);
            // pawn.stances = new Pawn_StanceTracker(pawn);
            // pawn.story = new Pawn_StoryTracker(pawn);
            // pawn.playerSettings = new Pawn_PlayerSettings(pawn);
            pawn.psychicEntropy = new Pawn_PsychicEntropyTracker(pawn);
            // pawn.workSettings = new Pawn_WorkSettings(pawn);

            // Reset Skills Since Midnight.
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                skill.xpSinceMidnight = 0;
                //lastXpSinceMidnightResetTimestamp
            }
        }

        private void ResetPawnCombatState(Pawn pawn)
        {
            if (pawn.equipment != null && pawn.equipment.HasAnything() && pawn.equipment.Primary != null)
            {
                // Log.Warning("10");

                // pawn.equipment.Primary.InitializeComps();
                if (pawn.equipment.PrimaryEq != null && pawn.equipment.PrimaryEq.verbTracker != null)
                {
                    // Log.Warning("11");

                    pawn.equipment.PrimaryEq.verbTracker = new VerbTracker(pawn);
                    pawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
                }
            }
            else
            {
                // Log.Warning("12");

                pawn.meleeVerbs = new Pawn_MeleeVerbs(pawn);
                pawn.verbTracker.AllVerbs.Add(new Verb_MeleeAttackDamage());
            }
        }

        private void ResetPawnEquipmentVerbs(Pawn pawn)
        {
            if (pawn.equipment != null && pawn.equipment.PrimaryEq != null)
            {
                // Log.Warning("14");

                pawn.equipment.PrimaryEq.verbTracker = new VerbTracker(pawn);
                pawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
            }
        }

        private void ResetHumanlikeMemories(Pawn pawn)
        {
            // pawn.guest = new Pawn_GuestTracker(pawn);
            #if RIMWORLD12
            pawn.guilt = new Pawn_GuiltTracker();
            #else
            pawn.guilt = new Pawn_GuiltTracker(pawn);
            #endif
            pawn.abilities = new Pawn_AbilityTracker(pawn);
            pawn.needs.mood.thoughts.memories = new MemoryThoughtHandler(pawn);
        }

        private void ApplyTimelineCorrection(Pawn pawn, int originalTimelineTicks)
        {
            long timelineTicksDiff = Current.Game.tickManager.TicksAbs - originalTimelineTicks;
            long oldAbsBirthdate = pawn.ageTracker.BirthAbsTicks;
            long newAbsBirthdate = oldAbsBirthdate + timelineTicksDiff;

            Log.Message(
                $"Applying Stargate timeline correction for {pawn.LabelShort}: " +
                $"timelineTicksDiff={timelineTicksDiff}, " +
                $"BirthAbsTicks {oldAbsBirthdate} -> {newAbsBirthdate}"
            );

            pawn.ageTracker.BirthAbsTicks = newAbsBirthdate;
        }

        private void RecordGateTravelerExit(Pawn pawn)
        {
            GateTravelerImplant gateTravelerImplant = pawn.health.hediffSet.hediffs
                .OfType<GateTravelerImplant>()
                .FirstOrDefault();

            gateTravelerImplant?.RecordStargateBufferExit();
        }

        private void RemoveFromDeadPawnsList(Pawn pawn)
        {
            Pawn pawnToRemove = Find.WorldPawns.AllPawnsDead
                .FirstOrDefault(p => p.thingIDNumber == pawn.thingIDNumber);
            if (pawnToRemove != null)
            {
                // Log.Warning("17");

                Find.WorldPawns.RemovePawn(pawnToRemove);
            }
        }

        private bool TryPlaceThingNearGate(Thing thing)
        {
            return GenPlace.TryPlaceThing(thing, this.gatePosition + new IntVec3(0, 0, -2),
                this.currentMap, ThingPlaceMode.Near);
        }

        private bool AttemptPawnPlacementRecovery(Pawn recoveryPawn)
        {
            Log.Warning($"Initial placement of {recoveryPawn.Label} failed. Attempting recovery strategies for version compatibility...");

            bool wasPlaced = false;
            // Try up to 5 different recovery strategies
            for (int attempt = 1; attempt <= 5 && !wasPlaced; attempt++)
            {
                try
                {
                    Log.Message($"Pawn recovery attempt #{attempt} for {recoveryPawn.Label}");

                    ApplyRecoveryStrategy(recoveryPawn, attempt);

                    // Try to place the pawn again after the fix
                    // Log.Warning("20");

                    wasPlaced = TryPlaceThingNearGate(recoveryPawn);

                    if (wasPlaced)
                    {
                        Log.Message($"Successfully placed {recoveryPawn.Label} after recovery attempt #{attempt}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error during recovery attempt #{attempt} for {recoveryPawn.Label}: {ex.Message}");
                }
            }

            return wasPlaced;
        }

        private void ApplyRecoveryStrategy(Pawn recoveryPawn, int attempt)
        {
            switch (attempt)
            {
                case 1:
                    // Reset apparel
                    Log.Message("Strategy 1: Resetting apparel tracker");
                    recoveryPawn.apparel = new Pawn_ApparelTracker(recoveryPawn);
                    break;

                case 2:
                    // Reset equipment
                    Log.Message("Strategy 2: Resetting equipment tracker");
                    recoveryPawn.equipment = new Pawn_EquipmentTracker(recoveryPawn);
                    if (recoveryPawn.equipment.Primary != null)
                    {
                        recoveryPawn.equipment.DestroyEquipment(recoveryPawn.equipment.Primary);
                    }
                    break;

                case 3:
                    // Reset health - strip problematic hediffs
                    Log.Message("Strategy 3: Sanitizing hediffs");
                    ApplyHediffSanitizationRecovery(recoveryPawn);
                    break;

                case 4:
                    // Reset both apparel and equipment entirely
                    Log.Message("Strategy 4: Resetting both apparel and equipment");
                    recoveryPawn.apparel = new Pawn_ApparelTracker(recoveryPawn);
                    recoveryPawn.equipment = new Pawn_EquipmentTracker(recoveryPawn);
                    break;

                case 5:
                    // Nuclear option - strip down to bare essentials
                    Log.Message("Strategy 5: Nuclear option - minimal pawn");
                    ApplyNuclearRecovery(recoveryPawn);
                    break;
            }
        }

        private void ApplyHediffSanitizationRecovery(Pawn recoveryPawn)
        {
            var hediffSet = recoveryPawn.health.hediffSet;
            recoveryPawn.health = new Pawn_HealthTracker(recoveryPawn);

            // Only keep core hediffs that exist in v1.2
            foreach (var hediff in hediffSet.hediffs.ToList())
            {
                if (hediff is Hediff_MissingPart || hediff is Hediff_Injury || hediff is Hediff_Addiction)
                {
                    hediff.pawn = recoveryPawn;
                    try
                    {
                        recoveryPawn.health.AddHediff(hediff, hediff.Part);
                    }
                    catch (Exception)
                    {
                        // Skip hediffs that can't be added - likely version incompatibility
                    }
                }
            }
        }

        private void ApplyNuclearRecovery(Pawn recoveryPawn)
        {
            // Reset all trackers to minimal state
            recoveryPawn.equipment = new Pawn_EquipmentTracker(recoveryPawn);
            recoveryPawn.apparel = new Pawn_ApparelTracker(recoveryPawn);
            recoveryPawn.health = new Pawn_HealthTracker(recoveryPawn);
            recoveryPawn.needs.SetInitialLevels();
            recoveryPawn.jobs = new Pawn_JobTracker(recoveryPawn);
            recoveryPawn.mindState = new Pawn_MindState(recoveryPawn);

            // Clear any custom data that might cause problems
            if (recoveryPawn.RaceProps.Humanlike)
            {
                #if RIMWORLD12 || RIMWORLD13
                recoveryPawn.story.childhood = BackstoryDatabase.RandomBackstory(BackstorySlot.Childhood);
                recoveryPawn.story.adulthood = BackstoryDatabase.RandomBackstory(BackstorySlot.Adulthood);
                #else
                recoveryPawn.story.Childhood = DefDatabase<BackstoryDef>
                    .AllDefsListForReading
                    .Where(bs => bs.slot == BackstorySlot.Childhood)
                    .RandomElement();
                recoveryPawn.story.Adulthood = DefDatabase<BackstoryDef>
                    .AllDefsListForReading
                    .Where(bs => bs.slot == BackstorySlot.Adulthood)
                    .RandomElement();
                #endif
            }
        }

        private void FinalizeSpawnedPawn(Pawn thisPawn)
        {
            this.currentMap.mapPawns.RegisterPawn(thisPawn);
            // Clear their mind (prevents Stargate Psychosis?).
            thisPawn.ClearMind(true);

            thisPawn.jobs.ClearQueuedJobs();

            thisPawn.thinker = new Pawn_Thinker(thisPawn);

            // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
            // if any. This is effectively the cure of Stargate Insanity.
            if (thisPawn.RaceProps.Humanlike)
            {
                // Log.Warning("22a");

                // thisPawn.equipment.DropAllEquipment(thisPawn.Position);
                thisPawn.drafter.Drafted = true;
                thisPawn.drafter.Drafted = false;
                thisPawn.drafter.pawn = thisPawn;
            }

            if (thisPawn.RaceProps.Animal)
            {
                // Log.Warning("22b");

                #if RIMWORLD12
                // thisPawn.training = new Pawn_TrainingTracker(thisPawn);
                #else
                thisPawn.training.pawn = thisPawn;
                #endif
            }

            if (thisPawn.RaceProps.ToolUser)
            {
                FinalizeToolUserEquipment(thisPawn);
            }
        }

        private void FinalizeToolUserEquipment(Pawn thisPawn)
        {
            // Log.Warning("23");

            if (thisPawn.equipment == null)
            {
                thisPawn.equipment = new Pawn_EquipmentTracker(thisPawn);
                // thisPawn.equipment.pawn = thisPawn;
            }

            if (thisPawn.apparel == null)
            {
                // thisPawn.apparel = new Pawn_ApparelTracker(thisPawn);
                thisPawn.apparel.pawn = thisPawn;
            }

            thisPawn.equipment.pawn = thisPawn;
            // thisPawn.equipment.Notify_PawnSpawned();
            // thisPawn.verbTracker = new VerbTracker(thisPawn);
            // thisPawn.meleeVerbs = new Pawn_MeleeVerbs(thisPawn);

            // // Reset their equipped weapon's verbTrackers as well, or they'll go insane if they're carrying an out-of-phase weapon...
            if (thisPawn.equipment.HasAnything() && thisPawn.equipment.Primary != null)
            {
                // Log.Warning("24");

                foreach (var verb in thisPawn.equipment.PrimaryEq.verbTracker.AllVerbs)
                {
                    verb.caster = thisPawn;
                }
                // thisPawn.equipment.Primary.InitializeComps();
                // thisPawn.equipment.PrimaryEq.verbTracker = new VerbTracker(thisPawn);
                // thisPawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
            }

            // thisPawn.verbTracker.AllVerbs.Clear();
            // thisPawn.verbTracker.AllVerbs.Add(new Verb_MeleeAttackDamage());
        }
    }
}