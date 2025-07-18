using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enhanced_Development.Stargate.Saving;
using Verse;
using UnityEngine;
using RimWorld;
using Verse.AI;
using Verse.Sound;

namespace BetterRimworlds.Stargate
{
    [StaticConstructorOnStartup]
    class Building_Stargate : Building_Storage, IThingHolder
    {

        #region Constants

        const int ADDITION_DISTANCE = 3;

        #endregion

        private List<Building_Stargate> GateNetwork = new List<Building_Stargate>();
        protected StargateBuffer stargateBuffer;

        protected bool LocalTeleportEvent = false;
        protected bool PoweringUp = false;

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

        private int currentCapacitorCharge = 1000;
        private int requiredCapacitorCharge = 1000;
        private int chargeSpeed = 1;

        protected Map currentMap;

        #endregion

        public Dictionary<string, SoundDef> stargateSounds = new Dictionary<string, SoundDef>()
        {
            { "Stargate Open",  DefDatabase<SoundDef>.GetNamed("StargateOpen") },
            { "Stargate Close", DefDatabase<SoundDef>.GetNamed("StargateClose") },
        };

        static Building_Stargate()
        {
            UI_ADD_RESOURCES = ContentFinder<Texture2D>.Get("UI/ADD_RESOURCES", true);
            UI_ADD_COLONIST = ContentFinder<Texture2D>.Get("UI/ADD_COLONIST", true);

            UI_GATE_IN = ContentFinder<Texture2D>.Get("UI/StargateGUI-In", true);
            UI_GATE_OUT = ContentFinder<Texture2D>.Get("UI/StargateGUI-Out", true);


            UI_POWER_UP = ContentFinder<Texture2D>.Get("UI/Wormhole", true);
            UI_POWER_DOWN = ContentFinder<Texture2D>.Get("UI/PowerDown", true);
#if RIMWORLD12
            GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active",   ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);
            GraphicRequest requestInactive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate", ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);
#else
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

            // Link the Stargate to the Stargate Network inside 4D space.
            this.stargateBuffer.SetStargateFilePath(FileLocationPrimary);

            // Register this gate in the Gate Network.
            Log.Warning($"Registering this Gate ({this.ThingID}) in the Gate Network.");
            GateNetwork.Add(this);

            Log.Warning("Found some things in the stargate's buffer: " + this.stargateBuffer.Count);

            this.stargateBuffer.Init();
        }

        // For displaying contents to the user.
        public ThingOwner GetDirectlyHeldThings() => this.stargateBuffer;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>) this.GetDirectlyHeldThings());

        // Saving game
        public override void ExposeData()
        {
            Scribe_Values.Look<int>(ref currentCapacitorCharge, "currentCapacitorCharge");
            Scribe_Values.Look<int>(ref requiredCapacitorCharge, "requiredCapacitorCharge");
            Scribe_Values.Look<int>(ref chargeSpeed, "chargeSpeed", 1);
            Scribe_Values.Look<bool>(ref PoweringUp, "poweringUp");
            Scribe_Values.Look<bool>(ref IsRecalling, "isRecalling", true);

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

        private bool IsRecalling = false;

        public override void TickRare()
        {
            base.TickRare();

            if (!this.stargateBuffer.Any())
            {
                if (this.fullyCharged == true)
                {
                    this.stargateBuffer.SetRequiredStargatePower();
                    chargeSpeed = 0;
                    this.updatePowerDrain();
                }

                if (this.fullyCharged == false && this.PoweringUp == true)
                {
                    // Log.Warning("1: " + chargeSpeed);
                    currentCapacitorCharge += chargeSpeed;
                    this.updatePowerDrain();


                    if (this.power.PowerOn == false)
                    {
                        chargeSpeed -= 2;
                        this.updatePowerDrain();
                    }

                    float excessPower = this.power.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
                    // Log.Warning("2: Excess Power: " + excessPower + " | Current Stored Energy: " +
                                // (this.power.PowerNet.CurrentStoredEnergy() * 1000));
                    // Log.Warning("Excess Power Calculation: CurrentEnergyGainRate: " +
                                // this.power.PowerNet.CurrentEnergyGainRate() + " | WattsToWattDaysPerTick: " +
                                // CompPower.WattsToWattDaysPerTick);

                    if (excessPower + (this.power.PowerNet.CurrentStoredEnergy() * 1000) > 5000)
                    {
                        chargeSpeed = (int)Math.Round(((excessPower - (excessPower % 1_000)) / 1000) +
                                                      this.power.PowerNet.CurrentStoredEnergy() * 0.25 / 10);
                        // Log.Warning("3a: Charge Speed: " + chargeSpeed + " | Excess Power: " +
                                    // ((int)Math.Round(((excessPower - (excessPower % 1_000)) / 1000))) +
                                    // " | Stored Energy: " + (this.power.PowerNet.CurrentStoredEnergy() * 0.25 / 10));
                        this.updatePowerDrain();
                    }
                    else if (excessPower + (this.power.PowerNet.CurrentStoredEnergy() * 1000) > 1000)
                    {
                        chargeSpeed += 1;
                        // Log.Warning("3b: Charge Speed: " + chargeSpeed);
                        this.updatePowerDrain();
                    }
                    else
                    {
                        chargeSpeed -= (int)Math.Round((excessPower - (excessPower % 1_000)) / 1000);
                        // Log.Warning("3c: Charge Speed: " + chargeSpeed);
                        this.updatePowerDrain();
                    }
                }
            }

            // Stop using power if it's full.
            if (this.fullyCharged == true)
            {
                this.PoweringUp = false;
                currentCapacitorCharge = requiredCapacitorCharge;
                this.chargeSpeed = 0;
                this.updatePowerDrain();

                bool hasNoPower = this.power.PowerNet == null || !this.power.PowerNet.HasActivePowerSource;
                bool hasInsufficientPower = this.power.PowerOn == false;
                if (hasNoPower || hasInsufficientPower)
                {
                    // if (hasNoPower)
                    // {
                    //     Log.Error("NO POWER");
                    // }
                    //
                    // if (hasInsufficientPower)
                    // {
                    //     Log.Error("INSUFFICIENT POWER");
                    // }

                    // Ignore power requirements during a solar flare.
                    #if RIMWORLD15
                    // Solar flares do not exist in Rimworld v1.5.
                    var solarFlareDef = DefDatabase<GameConditionDef>.GetNamed("SolarFlare");
                    bool isSolarFlare = this.currentMap.gameConditionManager.ConditionIsActive(solarFlareDef);
                    #else
                    bool isSolarFlare = this.currentMap.gameConditionManager.ConditionIsActive(GameConditionDefOf.SolarFlare);
                    #endif
                    if (isSolarFlare)
                    {
                        return;
                    }

                    // Log.Error("========= NOT ENOUGH POWER +========");
                    if (this.IsRecalling == false && this.stargateBuffer.GetStoredMass() > 1_000f)
                    {
                        this.EjectLeastMassive();
                    }

                    return;
                }

                // Auto-add stuff if it's inside the Stargate area.
                this.AddResources();
            }

            // if (this.currentCapacitorCharge < 0)
            // {
            //     this.currentCapacitorCharge = 0;
            //     this.chargeSpeed = 0;
            //     this.updatePowerDrain();
            // }
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Add the stock Gizmoes
            foreach (var g in base.GetGizmos())
            {
                if (
                    g is Command command &&
                    command.Label != "Reconnect" &&
                    command.Label != "Copy settings" &&
                    command.Label != "Paste settings" &&
                    command.Label != "Link settings"
                )
                {
                    yield return g;
                }
            }

            if (this.fullyCharged == true)
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

            if (this.HasThingsInBuffer())
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

            if (this.HasThingsInBuffer() || this.hasIncomingWormhole())
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

            if (this.PoweringUp == false && this.fullyCharged == false)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.PoweringUp = true;
                act.icon = UI_POWER_UP;
                act.defaultLabel = "Create Subspace Pocket";
                act.defaultDesc = "Create Subspace Pocket";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }
            // +57 320-637-6544
        }

        public void AddResources()
        {
            if (this.fullyCharged == false) {
                return;
            }

            List<Thing> foundThings = BetterRimworlds.Utilities.Utilities.FindItemThingsNearBuilding(this, Building_Stargate.ADDITION_DISTANCE, this.currentMap);

            foreach (Thing foundThing in foundThings)
            {
                if (!this.stargateBuffer.Any())
                {
                    this.stargateSounds["Stargate Open"].PlayOneShotOnCamera();
                }

                this.stargateBuffer.TryAdd(foundThing);

                // Tell the MapDrawer that here is something thats changed
                #if RIMWORLD15
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
                #else
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
                #endif
            }
        }

        public void AddPawns()
        {
            if (!this.fullyCharged)
            {
                Messages.Message("Insufficient Power to add Colonist", MessageTypeDefOf.RejectInput);
                return;
            }

            var closePawns = BetterRimworlds.Utilities.Utilities.findClosePawns(this.Position, Building_Stargate.ADDITION_DISTANCE);

            if (closePawns != null)
            {
                foreach (Pawn pawn in closePawns.ToList())
                {
                    if (!pawn.Spawned)
                    {
                        continue;
                    }

                    // // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                    // // if they enter a Stargate after they've ever been drafted.
                    // if (pawn.verbTracker != null)
                    // {
                    //     pawn.verbTracker = new VerbTracker(pawn);
                    // }

                    // Remove memories or they will go insane...
                    // if (pawn.def.defName == "Human")
                    // {
                    //     pawn.needs.mood.thoughts.memories = new MemoryThoughtHandler(pawn);
                    // }

                    if (!this.stargateBuffer.Any())
                    {
                        this.stargateSounds["Stargate Open"].PlayOneShotOnCamera();
                    }

                    this.stargateBuffer.TryAdd(pawn);
                }

                // Tell the MapDrawer that here is something thats changed
                #if RIMWORLD15
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
                #else
                Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
                #endif
            }
        }

        public void StargateDialOut()
        {
            if (!this.fullyCharged)
            {
                Messages.Message("Insufficient power to establish connection.", MessageTypeDefOf.RejectInput);
                return;
            }

            if (this.stargateBuffer.isOffworldTeleportEvent())
            {
                Messages.Message("Please Recall Offworld Teams First", MessageTypeDefOf.RejectInput);
                return;
            }

            this.stargateBuffer.TransmitContents();

            // Tell the MapDrawer that here is something thats changed
            #if RIMWORLD15
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
            #else
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            #endif

            this.stargateSounds["Stargate Close"].PlayOneShotOnCamera();

            this.currentCapacitorCharge = 0;
        }

        public bool HasThingsInBuffer()
        {
            return this.stargateBuffer.Count > 0;
        }

        public List<Thing> Teleport()
        {
            var itemsToTeleport = new List<Thing>();
            itemsToTeleport.AddRange(this.stargateBuffer);
            this.stargateBuffer.Empty();

            // Tell the MapDrawer that here is something that's changed.
            #if RIMWORLD15
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
            #else
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            #endif

            this.currentCapacitorCharge = 0;

            return itemsToTeleport;
        }

        public Tuple<int, List<Thing>> receiveMatterStream()
        {
            int originalTimelineTicks = Current.Game.tickManager.TicksAbs;

            // var inboundBuffer = this.stargateBuffer.ToList();
            // this.stargateBuffer.Clear();
            Log.Message("Number of stargates on this planet: " + GateNetwork.Count);

            // See if any of the stargates on this planet (including this gate) have items in their buffer...
            // and if so, recall them here.
            // @FIXME: Use  DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => typeof(Building_Stargate)
            this.LocalTeleportEvent = false;
            foreach (var stargate in GateNetwork)
            {
                Log.Message("Found a Stargate with the ID of " + stargate.ThingID);

                if (!stargate.HasThingsInBuffer())
                {
                    Log.Warning("Nothing in this Stargate's buffer....");
                    continue;
                }

                Log.Warning($"Stargate {stargate.ThingID} has something in its buffer.");
                this.LocalTeleportEvent = true;

                return new Tuple<int, List<Thing>>(originalTimelineTicks, stargate.Teleport());
            }

            if (this.stargateBuffer.isOffworldTeleportEvent() == false)
            {
                Messages.Message("No incoming wormhole detected.", MessageTypeDefOf.RejectInput);

                return null;
            }

            Messages.Message("You really must save and reload the game to fix Stargate Syndrome.", MessageTypeDefOf.ThreatBig);

            return this.stargateBuffer.receiveIncomingStream();
        }

        private void cleanseHistoricalRecord(Pawn transmittedPawn)
        {
            StargateBuffer.ClearExistingWorldPawn(transmittedPawn);
        }

        public virtual bool StargateRecall()
        {
            bool hasTransmittedPawns = false;

            /* Tuple<int, List<Thing>> **/
            var recallData = this.receiveMatterStream();
            if (recallData == null)
            {
                Messages.Message("WARNING: The Stargate buffer was empty!!", MessageTypeDefOf.ThreatBig);
                return false;
            }

            this.IsRecalling = true;

            int originalTimelineTicks = recallData.Item1;
            List<Thing> inboundBuffer = recallData.Item2;
            bool offworldEvent = this.stargateBuffer.isOffworldTeleportEvent();

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
                        wasPlaced = GenPlace.TryPlaceThing(currentThing, this.Position + new IntVec3(0, 0, -2),
                            this.currentMap, ThingPlaceMode.Near);
                        // Readd the unplaced Thing into the stargateBuffer.
                        if (!wasPlaced)
                        {
                            Log.Warning("Could not place " + currentThing.Label);
                            this.stargateBuffer.TryAdd(currentThing);
                        }

                        continue;
                        // currentThing.Destroy();
                    }

                    // currentThing.thingIDNumber = -1;
                    // Verse.ThingIDMaker.GiveIDTo(currentThing);

                    // If it's an equippable object, like a gun, reset its verbs or ANY colonist that equips it *will* go insane...
                    // This is actually probably the root cause of Colonist Insanity (holding an out-of-phase item with IDs belonging
                    // to an alternate dimension). This is the equivalent of how Olivia goes insane in the TV series Fringe.
                    if (currentThing is ThingWithComps item)
                    {
                        // item.InitializeComps();
                    }

                    // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                    // if they enter a Stargate after they've ever been drafted.
                    if (currentThing is Pawn pawn)
                    {
                        hasTransmittedPawns = true;
                        if (pawn.def.CanHaveFaction)
                        {
                            if (pawn.guest == null || pawn.guest.IsPrisoner == false)
                            {
                                pawn.SetFactionDirect(Faction.OfPlayer);
                            }
                            else
                            {
                                // v1 attempt
                                // // Handle Prisoners and Guests.
                                // float resistanceLevel = pawn.guest.Resistance;
                                //
                                // pawn.guest = new Pawn_GuestTracker(pawn);
                                // // pawn.guest.isPrisonerInt = true;
                                // // pawn.guest.SetGuestStatus(Faction.OfPlayer, true);
                                // #if RIMWORLD12
                                // pawn.guest.SetGuestStatus(Faction.OfPlayer, pawn.guest.IsPrisoner);
                                // pawn.SetFactionDirect(Faction.Empire);
                                // #else
                                // GuestStatus status = GuestStatus.Guest; // Default value
                                //
                                // if (pawn.guest.IsPrisoner)
                                // {
                                //     status = GuestStatus.Prisoner;
                                // }
                                // else if (pawn.guest.IsSlave)
                                // {
                                //     status = GuestStatus.Slave;
                                // }
                                //
                                // pawn.guest.SetGuestStatus(Faction.OfPlayer, status);
                                // #endif
                                // pawn.guest.resistance = resistanceLevel;

                                // v2 attempt
                                #if RIMWORLD12
                                pawn.SetFaction(Faction.Empire);
                                #else
                                pawn.SetFactionDirect(Faction.OfEmpire);
                                #endif
                            }
                        }

                        if (pawn.RaceProps.Humanlike)
                        {
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

                        pawn.relations = new Pawn_RelationsTracker(pawn);
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
                            try
                            {
                                pawn.health.AddHediff(hediff, hediff.Part);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"Could not add hediff {hediff} to pawn {pawn.Label}: {ex.Message}");
                            }
                        }

                        // @FIXME: Animals still have partial Stargate Insanity and many times will never fall asleep
                        //         on the new planet. They will drop-down from sheer exhaustion.
                        //         Some of them also become Godlings, literally unkillable except via the Dev Mode.
                        // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
                        // if any. This is effectively the cure of Stargate Insanity.
                        pawn.needs.SetInitialLevels();

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

                        if (pawn.RaceProps.Humanlike)
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

                            pawn.skills.SkillsTick();
                            // Reset Skills Since Midnight.
                            foreach (SkillRecord skill in pawn.skills.skills)
                            {
                                skill.xpSinceMidnight = 0;
                                //lastXpSinceMidnightResetTimestamp
                            }

                            if (pawn.equipment != null && pawn.equipment.HasAnything() && pawn.equipment.Primary != null)
                            {
                                // pawn.equipment.Primary.InitializeComps();
                                if (pawn.equipment.PrimaryEq != null && pawn.equipment.PrimaryEq.verbTracker != null)
                                {
                                    pawn.equipment.PrimaryEq.verbTracker = new VerbTracker(pawn);
                                    pawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
                                }
                            }
                            else
                            {
                                pawn.meleeVerbs = new Pawn_MeleeVerbs(pawn);
                                pawn.verbTracker.AllVerbs.Add(new Verb_MeleeAttackDamage());
                            }
                        }
                        else
                        {
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
                        if (pawn.equipment != null && pawn.equipment.PrimaryEq != null)
                        {
                            pawn.equipment.PrimaryEq.verbTracker = new VerbTracker(pawn);
                            pawn.equipment.PrimaryEq.verbTracker.AllVerbs.Add(new Verb_Shoot());
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
                        //
                        // There are 60,000 ticks per day.
                        long timelineTicksDiff = Current.Game.tickManager.TicksAbs - originalTimelineTicks;
                        long newAbsBirthdate = pawn.ageTracker.BirthAbsTicks + timelineTicksDiff;
                        Log.Message(
                            $"Subtracting {timelineTicksDiff} from the pawn's absolute ticks. From {pawn.ageTracker.BirthAbsTicks} to {newAbsBirthdate}");
                        pawn.ageTracker.BirthAbsTicks = newAbsBirthdate;

                        // Give them a brief psychic shock so that they will be given proper Melee Verbs and not act like a Visitor.
                        // Hediff shock = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn, null);
                        // pawn.health.AddHediff(shock, null, null);
                        PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);

                        // Find.CurrentMap.mapPawns.AllPawnsUnspawned.Remove(pawn);
                        // Find.WorldPawns.AllPawnsDead.Remove(pawn);
                        Pawn pawnToRemove = Find.WorldPawns.AllPawnsDead
                            .FirstOrDefault(p => p.thingIDNumber == pawn.thingIDNumber);
                        if (pawnToRemove != null)
                        {
                            Find.WorldPawns.RemovePawn(pawnToRemove);
                        }
                    }

                    // Try initial placement
                    wasPlaced = GenPlace.TryPlaceThing(currentThing, this.Position + new IntVec3(0, 0, -2),
                        this.currentMap, ThingPlaceMode.Near);

                    // If it's a pawn and placement failed, try with different recovery strategies
                    // Use a different variable name here to avoid the naming conflict
                    if (!wasPlaced && currentThing is Pawn recoveryPawn)
                    {
                        Log.Warning($"Initial placement of {recoveryPawn.Label} failed. Attempting recovery strategies for version compatibility...");

                        // Try up to 5 different recovery strategies
                        for (int attempt = 1; attempt <= 5 && !wasPlaced; attempt++)
                        {
                            try
                            {
                                Log.Message($"Pawn recovery attempt #{attempt} for {recoveryPawn.Label}");

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
                                        break;
                                }

                                // Try to place the pawn again after the fix
                                wasPlaced = GenPlace.TryPlaceThing(recoveryPawn, this.Position + new IntVec3(0, 0, -2),
                                    this.currentMap, ThingPlaceMode.Near);

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
                    }

                    // Readd the unplaced Thing into the stargateBuffer.
                    if (!wasPlaced)
                    {
                        Log.Warning("Could not place " + currentThing.Label + " after all attempts");
                        this.stargateBuffer.TryAdd(currentThing);
                    }
                    else
                    {
                        if (currentThing is Pawn thisPawn)
                        {
                            this.currentMap.mapPawns.RegisterPawn(thisPawn);
                            // Clear their mind (prevents Stargate Psychosis?).
                            thisPawn.ClearMind();

                            thisPawn.jobs.ClearQueuedJobs();

                            thisPawn.thinker = new Pawn_Thinker(thisPawn);

                            // Quickly draft and undraft the Colonist. This will cause them to become aware of the newly-in-phase weapon they are holding,
                            // if any. This is effectively the cure of Stargate Insanity.
                            if (thisPawn.RaceProps.Humanlike)
                            {
                                // thisPawn.equipment.DropAllEquipment(thisPawn.Position);
                                thisPawn.drafter.Drafted = true;
                                thisPawn.drafter.Drafted = false;
                                thisPawn.drafter.pawn = thisPawn;
                            }

                            if (thisPawn.RaceProps.Animal)
                            {
                                #if RIMWORLD12
                                // thisPawn.training = new Pawn_TrainingTracker(thisPawn);
                                #else
                                thisPawn.training.pawn = thisPawn;
                                #endif
                            }

                            if (thisPawn.RaceProps.ToolUser)
                            {
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

            // Tell the MapDrawer that here is something that's changed
            #if RIMWORLD15
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
            #else
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            #endif

            if (offworldEvent && hasTransmittedPawns)
            {
                // Re-add relationships.
                this.stargateBuffer.RebuildRelationships();
            }

            if (offworldEvent)
            {
                try
                {
                    this.MoveToBackup();
                }
                catch (Exception e)
                {
                    Log.Error("Couldn't move the stargate buffer to backup: " + e.Message);
                }
            }

            if (this.HasThingsInBuffer() == false)
            {
                this.stargateSounds["Stargate Close"].PlayOneShotOnCamera();
            }

            if (this.HasThingsInBuffer() == false)
            {
                this.IsRecalling = false;
            }

            return this.HasThingsInBuffer() == false;
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
                //Log.Error("Has thing in buffer? " + this.HasThingsInBuffer());
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
            // float excessPower = this.power.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
            return base.GetInspectString() + "\n"
                                           + "Buffer Items: " + this.stargateBuffer.Count + " / " +
                                           this.stargateBuffer.getMaxStacks() + "\n"
                                           + "Capacitor Charge: " + this.currentCapacitorCharge + " / " + this.requiredCapacitorCharge + "\n"
                                           + "New Power Req: " + this.power.powerOutputInt + "\n"
                                           + "Stored Mass: " + this.stargateBuffer.GetStoredMass() + " kg"
                // + "Gain Rate: " + excessPower + "\n"
                                           // + "Stored Energy: " + this.power.PowerNet.CurrentStoredEnergy()
                                           ;
        }

        #endregion

        private void MoveToBackup()
        {
            String newFile;
            if (System.IO.File.Exists(this.FileLocationSecondary))
            {
                int index = 1;
                newFile = Path.Combine(Verse.GenFilePaths.SaveDataFolderPath, "Stargate", $"StargateBackup-{index}.xml");

                while (System.IO.File.Exists(newFile))
                {
                    ++index;
                    newFile = Path.Combine(Verse.GenFilePaths.SaveDataFolderPath, "Stargate", $"StargateBackup-{index}.xml");
                }

                System.IO.File.Move(this.FileLocationSecondary, newFile);
            }

            if (System.IO.File.Exists(this.FileLocationPrimary))
            {
                System.IO.File.Move(this.FileLocationPrimary, this.FileLocationSecondary);
            }
        }

        public bool UpdateRequiredPower(float extraPower)
        {
            //this.requiredCapacitorCharge += extraPower;
            Log.Warning("===== New Power Req: " + this.power.powerOutputInt + "=====");
            this.power.powerOutputInt = -1 * extraPower;


            return true;
        }

        public void EjectLeastMassive()
        {
            // Drop the lightest items first.
            this.stargateBuffer.EjectLeastMassive();
        }

        public bool hasIncomingWormhole()
        {
            return String.IsNullOrEmpty(this.FileLocationPrimary) == false &&
                   System.IO.File.Exists(this.FileLocationPrimary);
        }
    }
}
