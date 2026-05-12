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
            Scribe_Values.Look(ref currentCapacitorCharge, "currentCapacitorCharge");
            Scribe_Values.Look(ref requiredCapacitorCharge, "requiredCapacitorCharge");
            Scribe_Values.Look(ref chargeSpeed, "chargeSpeed", 1);
            Scribe_Values.Look(ref PoweringUp, "poweringUp");
            Scribe_Values.Look(ref IsRecalling, "isRecalling", true);

            Scribe_Deep.Look(ref this.stargateBuffer, "stargateBuffer", new object[]
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
                    #if RIMWORLD15 || RIMWORLD16
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
                #if RIMWORLD15 || RIMWORLD16
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
                #if RIMWORLD15 || RIMWORLD16
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
            #if RIMWORLD15 || RIMWORLD16
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
            #if RIMWORLD15 || RIMWORLD16
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

            // Delegate the per-Thing rematerialization pipeline to StargateRecallOperation.
            // Building_Stargate retains orchestration: sounds, IsRecalling flag, backup file move,
            // relationship rebuild trigger, map mesh dirty.
            var recallOperation = new StargateRecallOperator(this.Position, this.currentMap, this.stargateBuffer);
            recallOperation.Execute(inboundBuffer, originalTimelineTicks, offworldEvent, out hasTransmittedPawns);

            // Tell the MapDrawer that here is something that's changed
            #if RIMWORLD15 || RIMWORLD16
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
