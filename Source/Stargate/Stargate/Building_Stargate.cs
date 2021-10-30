using System;
using System.Collections.Generic;
using System.Linq;
using BetterRimworlds.Stargate;
using Verse;
using UnityEngine;
using RimWorld;
using Verse.AI;

namespace Enhanced_Development.Stargate
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

            GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active",   ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);

            graphicActive = new Graphic_Single();
            graphicActive.Init(requestActive);

            GraphicRequest requestInactive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate", ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);

            graphicInactive = new Graphic_Single();
            graphicInactive.Init(requestInactive);
        }

        public Building_Stargate()
        {
            this.stargateBuffer = new StargateBuffer(this);
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
                FileLocationPrimary = ((Enhanced_Development.Stargate.StargateThingDef)def).FileLocationPrimary;
                FileLocationSecondary = ((Enhanced_Development.Stargate.StargateThingDef)def).FileLocationSecondary;

                //Log.Message("Setting FileLocationPrimary:" + FileLocationPrimary + " and FileLocationSecondary:" + FileLocationSecondary);
            }
            else
            {
                Log.Error("Stargate definition not of type \"StargateThingDef\"");
            }

            if (String.IsNullOrEmpty(FileLocationPrimary))
            {
                FileLocationPrimary = Verse.GenFilePaths.SaveDataFolderPath + @"\Stargate\Stargate.xml";

                if (!System.IO.Directory.Exists(Verse.GenFilePaths.SaveDataFolderPath + @"\Stargate\"))
                {
                    System.IO.Directory.CreateDirectory(Verse.GenFilePaths.SaveDataFolderPath + @"\Stargate\");
                }
            }

            if (String.IsNullOrEmpty(FileLocationSecondary))
            {
                FileLocationSecondary = Verse.GenFilePaths.SaveDataFolderPath + @"\Stargate\StargateBackup.xml";

                if (!System.IO.Directory.Exists(Verse.GenFilePaths.SaveDataFolderPath + @"\Stargate\"))
                {
                    System.IO.Directory.CreateDirectory(Verse.GenFilePaths.SaveDataFolderPath + @"\Stargate\");
                }
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
            base.ExposeData();

            Scribe_Values.Look<int>(ref currentCapacitorCharge, "currentCapacitorCharge");
            Scribe_Values.Look<int>(ref requiredCapacitorCharge, "requiredCapacitorCharge");
            Scribe_Values.Look<int>(ref chargeSpeed, "chargeSpeed");

            Scribe_Deep.Look<StargateBuffer>(ref this.stargateBuffer, "stargateBuffer", new object[]
            {
                this
            });
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
            }

            // Stop using power if it's full.
            if (currentCapacitorCharge >= requiredCapacitorCharge)
            {
                currentCapacitorCharge = requiredCapacitorCharge;
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
                act.action = () => this.AddColonist();
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
                Thing foundThing = Enhanced_Development.Utilities.Utilities.FindItemThingsNearBuilding(this, Building_Stargate.ADDITION_DISTANCE, this.currentMap);

                if (foundThing != null)
                {
                    if (foundThing.Spawned && this.stargateBuffer.Count < 500)
                    {
                        List<Thing> thingList = new List<Thing>();
                        //thingList.Add(foundThing);
                        this.stargateBuffer.TryAdd(foundThing);

                        //Building_OrbitalRelay.listOfThingLists.Add(thingList);

                        //Recursively Call to get Everything
                        this.AddResources();
                    }
                }

                // Tell the MapDrawer that here is something thats changed
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            }
            else
            {
                Messages.Message("Insufficient Power to add Resources", MessageTypeDefOf.RejectInput);
            }

        }

        public void AddColonist()
        {
            if (this.fullyCharged)
            {
                //Log.Message("CLick AddColonist");
                IEnumerable<Pawn> closePawns = Enhanced_Development.Utilities.Utilities.findPawnsInColony(this.Position, Building_Stargate.ADDITION_DISTANCE);

                if (closePawns != null)
                {
                    foreach (Pawn currentPawn in closePawns.ToList())
                    {
                        if (currentPawn.Spawned)
                        {
                            // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                            // if they enter a Stargate after they've ever been drafted.
                            if (currentPawn.verbTracker != null)
                            {
                                currentPawn.verbTracker = new VerbTracker(currentPawn);
                            }

                            // Remove memories or they will go insane...
                            if (currentPawn.def.defName == "Human")
                            {
                                currentPawn.needs.mood.thoughts.memories = new MemoryThoughtHandler(currentPawn);
                            }

                            this.stargateBuffer.TryAdd(currentPawn);
                        }
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

        public virtual bool StargateRecall()
        {
            // List<Thing> inboundBuffer = (List<Thing>)null;
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

                    return false;
                }

                Enhanced_Development.Stargate.Saving.SaveThings.load(ref inboundBuffer, this.FileLocationPrimary, this);
                // Log.Warning("Number of items in the wormhole: " + inboundBuffer.Count);
            }

            Messages.Message("Incoming wormhole!", MessageTypeDefOf.PositiveEvent);

            foreach (Thing currentThing in inboundBuffer)
            {
                currentThing.thingIDNumber = -1;
                Verse.ThingIDMaker.GiveIDTo(currentThing);

                // If it's an equippable object, like a gun, reset its verbs or ANY colonist that equips it *will* go insane...
                // This is actually probably the root cause of Colonist Insanity (holding an out-of-phase item with IDs belonging
                // to an alternate dimension). This is the equivalent of how Olivia goes insane in the TV series Fringe.
                if (currentThing is ThingWithComps item)
                {
                    item.InitializeComps();
                }

                if (currentThing.def.CanHaveFaction)
                {
                    currentThing.SetFactionDirect(Faction.OfPlayer);
                }
                
                // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                // if they enter a Stargate after they've ever been drafted.
                if (currentThing is Pawn pawn)
                {
                    // Carry over injuries, sicknesses, addictions, and artificial body parts.
                    var hediffSet = pawn.health.hediffSet;

                    pawn.health = new Pawn_HealthTracker(pawn);

                    foreach (var hediff in hediffSet.hediffs.ToList())
                    {
                        if (hediff is Hediff_MissingPart)
                        {
                            continue;
                        }
                        pawn.health.AddHediff(hediff.def, hediff.Part);
                    }

                    if (pawn.IsColonist)
                    {
                        pawn.verbTracker = new VerbTracker(pawn);
                        pawn.carryTracker = new Pawn_CarryTracker(pawn);
                        pawn.rotationTracker = new Pawn_RotationTracker(pawn);
                        pawn.thinker = new Pawn_Thinker(pawn);
                        pawn.mindState = new Pawn_MindState(pawn);
                        pawn.jobs = new Pawn_JobTracker(pawn);
                        pawn.ownership = new Pawn_Ownership(pawn);
                        pawn.drafter = new Pawn_DraftController(pawn);
                        pawn.natives = null;
                        // pawn.outfits = new Pawn_OutfitTracker(pawn);
                        pawn.pather = new Pawn_PathFollower(pawn);
                        // pawn.records = new Pawn_RecordsTracker(pawn);
                        pawn.relations = new Pawn_RelationsTracker(pawn);
                        pawn.caller = new Pawn_CallTracker(pawn);
                        // pawn.needs = new Pawn_NeedsTracker(pawn);
                        pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
                        pawn.interactions = new Pawn_InteractionsTracker(pawn);
                        pawn.stances = new Pawn_StanceTracker(pawn);
                        // pawn.story = new Pawn_StoryTracker(pawn);
                        // pawn.playerSettings = new Pawn_PlayerSettings(pawn);
                        // pawn.psychicEntropy = new Pawn_PsychicEntropyTracker(pawn);
                        // pawn.workSettings = new Pawn_WorkSettings(pawn);

                        pawn.meleeVerbs = new Pawn_MeleeVerbs(pawn);

                        pawn.skills.SkillsTick();
                        // Reset Skills Since Midnight.
                        foreach (SkillRecord skill in pawn.skills.skills)
                        {
                            skill.xpSinceMidnight = 0;
                            //lastXpSinceMidnightResetTimestamp
                            
                        }
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
                            // pawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
                        }

                        // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
                        // if any. This is effectively the cure of Stargate Insanity.
                        pawn.drafter.Drafted = true;
                        pawn.drafter.Drafted = false;

                    }               

                    // Remove memories or they will go insane...
                    if (pawn.RaceProps.Humanlike)
                    {
                        pawn.guest = new Pawn_GuestTracker(pawn);
                        pawn.guilt = new Pawn_GuiltTracker(pawn);
                        pawn.abilities = new Pawn_AbilityTracker(pawn);
                        pawn.needs.mood.thoughts.memories = new MemoryThoughtHandler(pawn);
                    }
                    
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
                // this.MoveToBackup();
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
