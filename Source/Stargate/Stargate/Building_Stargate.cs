using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enhanced_Development.Stargate.Saving;
using Verse;
using UnityEngine;
using RimWorld;
using Verse.AI;

namespace BetterRimworlds.Stargate
{
    [StaticConstructorOnStartup]
    class Building_Stargate : Building_Storage, IThingHolder
    {

        #region Constants

        const int ADDITION_DISTANCE = 3;

        #endregion

        private static List<Building_Stargate> GateNetwork = new List<Building_Stargate>();
        protected StargateBuffer stargateBuffer;

        #region Variables

        protected static Texture2D UI_ADD_RESOURCES;
        protected static Texture2D UI_ADD_COLONIST;

        protected static Texture2D UI_GATE_IN;
        protected static Texture2D UI_GATE_OUT;

        protected static Texture2D UI_POWER_UP;
        protected static Texture2D UI_POWER_DOWN;

        public bool StargateAddResources = true;
        public bool StargateAddUnits = true;
        public bool StargateRetreave = true;

        private string FileLocationPrimary;
        private string FileLocationSecondary;

        static Graphic graphicActive;
        static Graphic graphicInactive;

        CompPowerTrader power;

        int currentCapacitorCharge = 1000;
        int requiredCapacitorCharge = 1000;
        int chargeSpeed = 1;

        protected Map currentMap;

        #endregion

        static Building_Stargate()
        {
            UI_ADD_RESOURCES = ContentFinder<Texture2D>.Get("UI/ADD_RESOURCES", true);
            UI_ADD_COLONIST = ContentFinder<Texture2D>.Get("UI/ADD_COLONIST", true);

            UI_GATE_IN = ContentFinder<Texture2D>.Get("UI/StargateGUI-In", true);
            UI_GATE_OUT = ContentFinder<Texture2D>.Get("UI/StargateGUI-Out", true);


            UI_POWER_UP = ContentFinder<Texture2D>.Get("UI/PowerUp", true);
            UI_POWER_DOWN = ContentFinder<Texture2D>.Get("UI/PowerDown", true);

#if RIMWORLD12
            GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active",   ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);
            GraphicRequest requestInactive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate", ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);
#endif
#if RIMWORLD13
            GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active",   ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null, null);
            GraphicRequest requestInactive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate", ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null, null);
#endif

            graphicActive = new Graphic_Single();
            graphicActive.Init(requestActive);

            graphicInactive = new Graphic_Single();
            graphicInactive.Init(requestInactive);
        }

        public Building_Stargate()
        {
            this.stargateBuffer = new StargateBuffer(this, false, LookMode.Deep);
        }


        #region Override

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            this.currentMap = map;
            base.SpawnSetup(map, respawningAfterLoad);

            this.power = base.GetComp<CompPowerTrader>();

            if (def is StargateThingDef)
            {
                //Read in variables from the custom MyThingDef
                FileLocationPrimary = ((StargateThingDef)def).FileLocationPrimary;
                FileLocationSecondary = ((StargateThingDef)def).FileLocationSecondary;

                //Log.Message("Setting FileLocationPrimary:" + FileLocationPrimary + " and FileLocationSecondary:" + FileLocationSecondary);
            }
            else
            {
                Log.Error("Stargate definition not of type \"StargateThingDef\"");
            }

            string stargateDirectory = Path.Combine(Verse.GenFilePaths.SaveDataFolderPath, "Stargate");
            Log.Warning("Stargate Directory: " + stargateDirectory);

            if (String.IsNullOrEmpty(FileLocationPrimary))
            {
                FileLocationPrimary = Path.Combine(Verse.GenFilePaths.SaveDataFolderPath, "Stargate", "Stargate.xml");
                Log.Warning("Stargate File: " + FileLocationPrimary);

                if (!System.IO.Directory.Exists(stargateDirectory))
                {
                    System.IO.Directory.CreateDirectory(stargateDirectory);
                }
            }

            if (String.IsNullOrEmpty(FileLocationSecondary))
            {
                FileLocationSecondary = Path.Combine(Verse.GenFilePaths.SaveDataFolderPath, "Stargate", "StargateBackup.xml");
                Log.Warning("Stargate Backup: " + FileLocationSecondary);
            }

            // Register this gate in the Gate Network.
            Log.Warning($"Registering this Gate ({this.ThingID}) in the Gate Network.");
            GateNetwork.Add(this);

            Log.Warning("Found some things in the stargate's buffer: " + this.stargateBuffer.Count);
        }

        // For displaying contents to the user.
        public ThingOwner GetDirectlyHeldThings() => this.stargateBuffer;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>) this.GetDirectlyHeldThings());

        // Saving game
        public override void ExposeData()
        {

            Scribe_Values.Look<int>(ref currentCapacitorCharge, "currentCapacitorCharge");
            Scribe_Values.Look<int>(ref requiredCapacitorCharge, "requiredCapacitorCharge");
            Scribe_Values.Look<int>(ref chargeSpeed, "chargeSpeed");

            Scribe_Deep.Look<StargateBuffer>(ref this.stargateBuffer, "stargateBuffer", new object[]
            {
                this
            });

            base.ExposeData();
        }

        protected void BaseTickRare()
        {
            base.TickRare();
        }

        public override void TickRare()
        {
            base.TickRare();
            if (this.power.PowerOn)
            {
                currentCapacitorCharge += chargeSpeed;
                if (this.power.PowerNet.CurrentEnergyGainRate() > 1000)
                {
                    chargeSpeed += 1;
                    this.updatePowerDrain();
                }
            }

            // Stop using power if it's full.
            if (currentCapacitorCharge >= requiredCapacitorCharge)
            {
                currentCapacitorCharge = requiredCapacitorCharge;
                this.chargeSpeed = 0;
                this.updatePowerDrain();
            }

            if (this.currentCapacitorCharge < 0)
            {
                this.currentCapacitorCharge = 0;
                this.chargeSpeed = 0;
                this.updatePowerDrain();
            }
        }

        #endregion

        #region Commands

        private bool fullyCharged
        {
            get
            {
                return (this.currentCapacitorCharge >= this.requiredCapacitorCharge);
            }
        }

        protected IEnumerable<Gizmo> GetDefaultGizmos()
        {
            return base.GetGizmos();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Add the stock Gizmoes
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }

            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.AddResources();
                act.icon = UI_ADD_RESOURCES;
                act.defaultLabel = "Add Resources";
                act.defaultDesc = "Add Resources";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }

            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.AddPawns();
                act.icon = UI_ADD_COLONIST;
                act.defaultLabel = "Add Colonist";
                act.defaultDesc = "Add Colonist";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }

            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.StargateDialOut();
                act.icon = UI_GATE_OUT;
                act.defaultLabel = "Dial Out";
                act.defaultDesc = "Dial Out";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }

            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.StargateRecall();
                act.icon = UI_GATE_IN;
                act.defaultLabel = "Recall";
                act.defaultDesc = "Recall";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }

            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.PowerRateIncrease();
                act.icon = UI_POWER_UP;
                act.defaultLabel = "Increase Power";
                act.defaultDesc = "Increase Power";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }
            // +57 320-637-6544
            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.PowerRateDecrease();
                act.icon = UI_POWER_DOWN;
                act.defaultLabel = "Decrease Power";
                act.defaultDesc = "Decrease Power";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }
        }

        public void AddResources()
        {
            if (this.fullyCharged)
            {
                List<Thing> foundThings = Enhanced_Development.Utilities.Utilities.FindItemThingsNearBuilding(this, Building_Stargate.ADDITION_DISTANCE, this.currentMap);

                foreach (Thing foundThing in foundThings)
                {
                    if (foundThing.Spawned && this.stargateBuffer.Count < 500)
                    {
                        this.stargateBuffer.TryAdd(foundThing);

                        //Building_OrbitalRelay.listOfThingLists.Add(thingList);
                    }
                }

                // Tell the MapDrawer that here is something that's changed.
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            }
            else
            {
                Messages.Message("Insufficient Power to add Resources", MessageTypeDefOf.RejectInput);
            }
        }

        public void AddPawns()
        {
            if (this.fullyCharged)
            {
                // 60,000 ticks per day.
                var ticksPassed = GenDate.DaysPassed * 60_000L;

                //Log.Message("CLick AddColonist");
                IEnumerable<Pawn> closePawns = Enhanced_Development.Utilities.Utilities.findPawnsInColony(this.Position, Building_Stargate.ADDITION_DISTANCE);

                if (closePawns != null)
                {
                    foreach (Pawn pawn in closePawns.ToList())
                    {
                        if (!pawn.Spawned)
                        {
                            continue;
                        }

                        // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                        // if they enter a Stargate after they've ever been drafted.
                        if (pawn.verbTracker != null)
                        {
                            pawn.verbTracker = new VerbTracker(pawn);
                        }

                        // Remove memories or they will go insane...
                        if (pawn.def.defName == "Human")
                        {
                            pawn.needs.mood.thoughts.memories = new MemoryThoughtHandler(pawn);
                        }

                        this.stargateBuffer.TryAdd(pawn);
                    }
                }

                // Tell the MapDrawer that here is something thats changed
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            }
            else
            {
                Messages.Message("Insufficient Power to add Colonist", MessageTypeDefOf.RejectInput);
            }
        }

        public void StargateDialOut()
        {
            if (!this.fullyCharged)
            {
                Messages.Message("Insufficient power to establish connection.", MessageTypeDefOf.RejectInput);
                return;
            }

            if (System.IO.File.Exists(this.FileLocationPrimary))
            {
                Messages.Message("Please Recall Offworld Teams First", MessageTypeDefOf.RejectInput);
                return;
            }

            Enhanced_Development.Stargate.Saving.SaveThings.save(this.stargateBuffer.ToList(), this.FileLocationPrimary, this);
            this.stargateBuffer.Clear();

            // Tell the MapDrawer that here is something thats changed
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);

            this.currentCapacitorCharge -= this.requiredCapacitorCharge;
        }

        public bool HasThingsInBuffer()
        {
            return this.stargateBuffer.Count > 0;
        }

        public List<Thing> Teleport()
        {
            var itemsToTeleport = new List<Thing>();
            itemsToTeleport.AddRange(this.stargateBuffer);
            this.stargateBuffer.Clear();

            // Tell the MapDrawer that here is something thats changed
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);

            this.currentCapacitorCharge -= this.requiredCapacitorCharge;

            return itemsToTeleport;
        }

        public Tuple<int, List<Thing>> recall1()
        {
            // List<Thing> inboundBuffer = (List<Thing>)null;
            int originalTimelineTicks = Current.Game.tickManager.TicksAbs;

            var inboundBuffer = new List<Thing>();
            Log.Message("Number of stargates on this planet: " + GateNetwork.Count);
            // See if any of the stargates on this planet (including this gate) have items in their buffer...
            // and if so, recall them here.
            // @FIXME: Use  DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => typeof(Building_Stargate)
            foreach (var stargate in GateNetwork)
            {
                Log.Message("Found a Stargate with the ID of " + stargate.ThingID);
                if (!stargate.HasThingsInBuffer())
                {
                    continue;
                }

                Log.Warning($"Stargate {stargate.ThingID} has something in its buffer.");
                inboundBuffer.AddRange(stargate.Teleport());
            }

            // Load off-world teams only if there isn't a local teleportation taking place.
            bool offworldEvent = this.stargateBuffer.Count == 0;
            Log.Warning("Is offworldEvent? " + this.stargateBuffer.Count);
            Log.Warning("Inbound Buffer Count? " + inboundBuffer.Count);
            if (offworldEvent && !inboundBuffer.Any())
            {
                // Log.Warning("Found an off-world wormhole.");
                if (!System.IO.File.Exists(this.FileLocationPrimary))
                {
                    Messages.Message("No Off-world Teams were found", MessageTypeDefOf.RejectInput);

                    return null;
                }

                // 
                var loadResponse = Enhanced_Development.Stargate.Saving.SaveThings.load(ref inboundBuffer, this.FileLocationPrimary, this);
                originalTimelineTicks = loadResponse.Item1;
                List<StargateRelation> relations = loadResponse.Item2;
                this.rebuildRelationships(inboundBuffer, relations);
                
                // Log.Warning("Number of items in the wormhole: " + inboundBuffer.Count);
            }

            Messages.Message("Incoming wormhole!", MessageTypeDefOf.PositiveEvent);
            Messages.Message("You really must save and reload the game to fix Stargate Syndrome.", MessageTypeDefOf.ThreatBig);

            return new Tuple<int, List<Thing>>(originalTimelineTicks, inboundBuffer);
        }

        // @FIXME: Need to refactor this to a StargateBuffer.
        private void rebuildRelationships(List<Thing> inboundBuffer, List<StargateRelation> relationships)
        {
            // Re-add the relationships.
            foreach (var relationship in relationships)
            {
                Log.Error($"Loading the relationship between {relationship.pawn1ID} and {relationship.pawn2ID}: {relationship.relationship}");

                // p => p.Item1 == item.ThingID
                var target = (Pawn)inboundBuffer.Find(t =>
                {
                    if (t is Pawn p)
                    {
                        return p.ThingID == relationship.pawn1ID;
                    }

                    return false;
                });

                var relatedPawn = (Pawn)inboundBuffer.Find(t =>
                {
                    if (t is Pawn p)
                    {
                        return p.ThingID == relationship.pawn2ID;
                    }

                    return false;
                });

                PawnRelationDef pawnRelationDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail(relationship.relationship);
                target.relations.AddDirectRelation(pawnRelationDef, relatedPawn);
                Log.Error($"Loaded the relationship between {relationship.pawn1ID} and {relationship.pawn2ID}: {relationship.relationship}");
            }
        }

        public void recall2()
        {
            
        }

        public void recall3()
        {
            
        }

        public virtual bool StargateRecall()
        {
            /* Tuple<int, List<Thing>> **/
            var recallData = this.recall1();
            if (recallData == null)
            {
                return false;
            }
            
            int originalTimelineTicks = recallData.Item1;
            List<Thing> inboundBuffer = recallData.Item2;
            bool offworldEvent = this.stargateBuffer.Count == 0;

            foreach (Thing currentThing in inboundBuffer)
            {
                // currentThing.thingIDNumber = -1;
                // Verse.ThingIDMaker.GiveIDTo(currentThing);

                // If it's an equippable object, like a gun, reset its verbs or ANY colonist that equips it *will* go insane...
                // This is actually probably the root cause of Colonist Insanity (holding an out-of-phase item with IDs belonging
                // to an alternate dimension). This is the equivalent of how Olivia goes insane in the TV series Fringe.
                if (currentThing is ThingWithComps item)
                {
                    // item.InitializeComps();
                }

                if (currentThing.def.CanHaveFaction)
                {
                    currentThing.SetFactionDirect(Faction.OfPlayer);
                }
                
                // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                // if they enter a Stargate after they've ever been drafted.
                if (currentThing is Pawn pawn)
                {
                    // Offset their chronological age by the current time in the game and offset by the Year 5500.
                    // We will reduce their age if they come in after the Year 5500...
                    pawn.ageTracker.AgeChronologicalTicks += ticksPassed;

                    // Carry over injuries, sicknesses, addictions, and artificial body parts.
                    var hediffSet = pawn.health.hediffSet;

                    pawn.health = new Pawn_HealthTracker(pawn);
                    foreach (var hediff in hediffSet.hediffs.ToList())
                    {
                        if (hediff is Hediff_MissingPart)
                        {
                            continue;
                        }
                        hediff.pawn = pawn;
                        pawn.health.AddHediff(hediff, hediff.Part);
                    }

                    // @FIXME: Animals still have partial Stargate Insanity and many times will never fall asleep
                    //         on the new planet. They will drop-down from sheer exhaustion.
                    //         Some of them also become Godlings, literally unkillable except via the Dev Mode.
                    // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
                    // if any. This is effectively the cure of Stargate Insanity.
                    pawn.needs = new Pawn_NeedsTracker(pawn);

                    if (pawn.RaceProps.Humanlike)
                    {
                        //pawn.ownership = new Pawn_Ownership(pawn);
                        // pawn.outfits = new Pawn_OutfitTracker(pawn);
                        // pawn.records = new Pawn_RecordsTracker(pawn);
                        // pawn.relations = new Pawn_RelationsTracker(pawn);
                        pawn.caller = new Pawn_CallTracker(pawn);
                        // pawn.needs = new Pawn_NeedsTracker(pawn);
                        pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
                        pawn.stances = new Pawn_StanceTracker(pawn);
                        // pawn.story = new Pawn_StoryTracker(pawn);
                        // pawn.playerSettings = new Pawn_PlayerSettings(pawn);
                        pawn.psychicEntropy = new Pawn_PsychicEntropyTracker(pawn);
                        // pawn.workSettings = new Pawn_WorkSettings(pawn);

                        pawn.skills.SkillsTick();
                        // Reset Skills Since Midnight.
                        foreach (SkillRecord skill in pawn.skills.skills)
                        {
                            skill.xpSinceMidnight = 0;
                            //lastXpSinceMidnightResetTimestamp
                            
                        }
                    }
                    else
                    {
                        pawn.needs = new Pawn_NeedsTracker(pawn);
                    }
 
                    if (pawn.RaceProps.ToolUser)
                    {
                        if (pawn.equipment == null)
                            pawn.equipment = new Pawn_EquipmentTracker(pawn);
                        if (pawn.apparel == null)
                            pawn.apparel = new Pawn_ApparelTracker(pawn);

                        // Reset their equipped weapon's verbTrackers as well, or they'll go insane if they're carrying an out-of-phase weapon...
                        if (pawn.equipment.Primary != null)
                        {
                            pawn.equipment.Primary.InitializeComps();
                            pawn.equipment.PrimaryEq.verbTracker = new VerbTracker(pawn);
                            pawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
                        }

                        // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
                        // if any. This is effectively the cure of Stargate Insanity.
                        pawn.drafter.Drafted = true;
                        pawn.drafter.Drafted = false;

                    }               

                    // Remove memories or they will go insane...
                    if (pawn.RaceProps.Humanlike)
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
                    
                    // Alter the pawn's chronological age based upon the temporal drift between their origin universe
                    // and the destination universe.
                    //
                    // This is the only way in which even the pawns themselves and their co-travelers, dopplegangers
                    // in parallel realities, and the Observer can possibly tell how Old they really are...
                    long timelineTicksDiff = Current.Game.tickManager.TicksAbs - originalTimelineTicks;
                    long newAbsBirthdate = pawn.ageTracker.BirthAbsTicks + timelineTicksDiff;
                    Log.Message($"Subtracting {timelineTicksDiff} from the pawn's absolute ticks. From {pawn.ageTracker.BirthAbsTicks} to {newAbsBirthdate}");
                    pawn.ageTracker.BirthAbsTicks = newAbsBirthdate;

                    // Give them a brief psychic shock so that they will be given proper Melee Verbs and not act like a Visitor.
                    // Hediff shock = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn, null);
                    // pawn.health.AddHediff(shock, null, null);
                    PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
                }

                GenPlace.TryPlaceThing(currentThing, this.Position + new IntVec3(0, 0, -2), this.currentMap, ThingPlaceMode.Near);
            }

            inboundBuffer.Clear();

            // Tell the MapDrawer that here is something that's changed
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);

            if (offworldEvent)
            {
                this.MoveToBackup();
            }

            return true;
        }

        private void PowerStopUsing()
        {
            this.chargeSpeed = 0;
            this.updatePowerDrain();
        }

        private void PowerRateIncrease()
        {
            this.chargeSpeed += 1;
            this.updatePowerDrain();
        }

        private void PowerRateDecrease()
        {
            this.chargeSpeed -= 1;
            this.updatePowerDrain();
        }

        private void updatePowerDrain()
        {
            this.power.powerOutputInt = -1000 * this.chargeSpeed;
        }

        #endregion

        #region Graphics-text

        public override Graphic Graphic
        {
            get
            {
                if (this.HasThingsInBuffer())
                {
                    return Building_Stargate.graphicActive;
                }
                else
                {
                    return Building_Stargate.graphicInactive;
                }
            }
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n"
                + "Buffer Items: " + this.stargateBuffer.Count + " / 500\n"
                + "Capacitor Charge: " + this.currentCapacitorCharge + " / " + this.requiredCapacitorCharge;
        }

        #endregion

        private void MoveToBackup()
        {
            if (System.IO.File.Exists(this.FileLocationSecondary))
            {
                System.IO.File.Delete(this.FileLocationSecondary);
            }

            if (System.IO.File.Exists(this.FileLocationPrimary))
            {
                System.IO.File.Move(this.FileLocationPrimary, this.FileLocationSecondary);
            }
        }
    }
}
