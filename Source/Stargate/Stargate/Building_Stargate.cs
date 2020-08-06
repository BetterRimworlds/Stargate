using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace Enhanced_Development.Stargate
{

    [StaticConstructorOnStartup]
    class Building_Stargate : Building
    {

        #region Constants

        const int ADDITION_DISTANCE = 3;

        #endregion

        #region Variables
        //TODO: Saving the Building
        List<Thing> listOfBufferThings = new List<Thing>();

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




            //GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active", def.graphic.Shader, new Vector2(3, 3), Color.white, Color.white, new GraphicData());

            GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active",   ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);

            graphicActive = new Graphic_Single();
            graphicActive.Init(requestActive);

            GraphicRequest requestInactive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate", ShaderDatabase.DefaultShader, new Vector2(3, 3), Color.white, Color.white, new GraphicData(), 0, null);

            graphicInactive = new Graphic_Single();
            graphicInactive.Init(requestInactive);

            //GraphicRequest requestActive = new GraphicRequest(Type.GetType("Graphic_Single"), "Things/Buildings/Stargate-Active", def.graphic.Shader, new Vector2(3, 3), Color.white, Color.white);

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


        }

        //Saving game
        public override void ExposeData()
        {

            //Log.Message("Expose Data start");
            base.ExposeData();

            //Scribe_Deep.LookDeep(ref listOfThingLists, "listOfThingLists");

            Scribe_Values.Look<int>(ref currentCapacitorCharge, "currentCapacitorCharge");
            Scribe_Values.Look<int>(ref requiredCapacitorCharge, "requiredCapacitorCharge");
            Scribe_Values.Look<int>(ref chargeSpeed, "chargeSpeed");


            /*Scribe_Values.LookValue<bool>(ref DropPodDeepStrike, "DropPodDeepStrike");
            Scribe_Values.LookValue<bool>(ref DropPodAddUnits, "DropPodAddUnits");
            Scribe_Values.LookValue<bool>(ref DropPodAddResources, "DropPodAddResources");*/

            //Log.Message("Expose Data - look list");
            Scribe_Collections.Look<Thing>(ref listOfBufferThings, "listOfBufferThings", LookMode.Deep);
            //Scribe_Collections.LookList<Thing>(ref listOfOffworldThings, "listOfOffworldThings", LookMode.Deep, (object)null);

            //Log.Message("Expose Data about to start");

        }

        public override void TickRare()
        {
            base.TickRare();
            if (this.power.PowerOn)
            {
                currentCapacitorCharge += chargeSpeed;
            }

            if (currentCapacitorCharge > requiredCapacitorCharge)
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
            //Add the stock Gizmoes
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


        /*
        public override IEnumerable<Command> GetCommands()
        {
            IList<Command> CommandList = new List<Command>();
            IEnumerable<Command> baseCommands = base.GetCommands();

            if (baseCommands != null)
            {
                CommandList = baseCommands.ToList();
            }

            if (true)
            {
                //Upgrading
                Command_Action command_Action_AddResources = new Command_Action();

                command_Action_AddResources.defaultLabel = "Add Resources";

                command_Action_AddResources.icon = UI_ADD_RESOURCES;
                command_Action_AddResources.defaultDesc = "Add Resources";

                command_Action_AddResources.activateSound = SoundDef.Named("Click");
                command_Action_AddResources.action = new Action(this.AddResources);

                CommandList.Add(command_Action_AddResources);
            }

            if (true)
            {
                Command_Action command_Action_AddColonist = new Command_Action();

                command_Action_AddColonist.defaultLabel = "Add Colonist";

                command_Action_AddColonist.icon = UI_ADD_COLONIST;
                command_Action_AddColonist.defaultDesc = "Add Colonist";

                command_Action_AddColonist.activateSound = SoundDef.Named("Click");
                command_Action_AddColonist.action = new Action(this.AddColonist);

                CommandList.Add(command_Action_AddColonist);
            }

            if (true)
            {
                //Upgrading
                Command_Action command_Action_DialOut = new Command_Action();

                command_Action_DialOut.defaultLabel = "Dial Out";

                command_Action_DialOut.icon = UI_GATE_OUT;
                command_Action_DialOut.defaultDesc = "Dial Out";

                command_Action_DialOut.activateSound = SoundDef.Named("Click");
                command_Action_DialOut.action = new Action(this.StargateDialOut);

                CommandList.Add(command_Action_DialOut);
            }

            if (true)
            {
                //Upgrading
                Command_Action command_Action_IncomingWormhole = new Command_Action();

                command_Action_IncomingWormhole.defaultLabel = "Inbound WormHole";

                command_Action_IncomingWormhole.icon = UI_GATE_IN;
                command_Action_IncomingWormhole.defaultDesc = "Inbound Wormhole";

                command_Action_IncomingWormhole.activateSound = SoundDef.Named("Click");
                command_Action_IncomingWormhole.action = new Action(this.StargateIncomingWormhole);

                CommandList.Add(command_Action_IncomingWormhole);
            }

            if (true)
            {
                //Upgrading
                Command_Action command_Action_IncreasePower = new Command_Action();

                command_Action_IncreasePower.defaultLabel = "Increase Power";

                command_Action_IncreasePower.icon = UI_GATE_IN;
                command_Action_IncreasePower.defaultDesc = "Increase Power";

                command_Action_IncreasePower.activateSound = SoundDef.Named("Click");
                command_Action_IncreasePower.action = new Action(this.PowerRateIncrease);

                CommandList.Add(command_Action_IncreasePower);
            }

            if (true)
            {
                //Upgrading
                Command_Action command_Action_DecreasePower = new Command_Action();

                command_Action_DecreasePower.defaultLabel = "Decrease Power";

                command_Action_DecreasePower.icon = UI_GATE_IN;
                command_Action_DecreasePower.defaultDesc = "Decrease Power";

                command_Action_DecreasePower.activateSound = SoundDef.Named("Click");
                command_Action_DecreasePower.action = new Action(this.PowerRateDecrease);

                CommandList.Add(command_Action_DecreasePower);
            }


            return CommandList.AsEnumerable<Command>();
        }
        */
        public void AddResources()
        {

            if (this.fullyCharged)
            {

                //Thing foundThing = Enhanced_Development.Utilities.Utilities.FindItemThingsInAutoLoader(this);
                Thing foundThing = Enhanced_Development.Utilities.Utilities.FindItemThingsNearBuilding(this, Building_Stargate.ADDITION_DISTANCE, this.currentMap);

                if (foundThing != null)
                {
                    if (foundThing.Spawned)
                    {
                        List<Thing> thingList = new List<Thing>();
                        //thingList.Add(foundThing);
                        listOfBufferThings.Add(foundThing);
                        foundThing.DeSpawn();

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
                            List<Thing> thingList = new List<Thing>();
                            listOfBufferThings.Add(currentPawn);
                            currentPawn.DeSpawn();
                            //currentPawn.outfits.CurrentOutfit = null;
                            int tempHealth = currentPawn.HitPoints;
                            //currentPawn.Destroy(DestroyMode.Vanish);
                            currentPawn.HitPoints = tempHealth;
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

            if (this.fullyCharged)
            {
                if (System.IO.File.Exists(this.FileLocationPrimary))
                {
                    Messages.Message("Please Recall Offworld Teams First", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    Enhanced_Development.Stargate.Saving.SaveThings.save(listOfBufferThings, this.FileLocationPrimary, this);

                    this.listOfBufferThings.Clear();

                    // Tell the MapDrawer that here is something thats changed
                    Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);

                    this.currentCapacitorCharge -= this.requiredCapacitorCharge;
                }
            }
            else
            {
                Messages.Message("Insufficient power to establish connection.", MessageTypeDefOf.RejectInput);
            }
        }

        public virtual bool StargateRecall()
        {
            if (!System.IO.File.Exists(this.FileLocationPrimary))
            {
                Messages.Message("No Off-world Teams were found", MessageTypeDefOf.RejectInput);

                return false;
            }

            Messages.Message("Recalling Off-world Teams", MessageTypeDefOf.PositiveEvent);

            //List<Thing> inboundBuffer = new List<Thing>();
            List<Thing> inboundBuffer = (List<Thing>)null;

            //Log.Message("start list contains: " + inboundBuffer.Count);
            Enhanced_Development.Stargate.Saving.SaveThings.load(ref inboundBuffer, this.FileLocationPrimary, this);
            //Log.Message("end list contains: " + inboundBuffer.Count);

            foreach (Thing currentThing in inboundBuffer)
            {
                //Log.Message("Placing Thing");
                if (currentThing.def.CanHaveFaction)
                {
                    currentThing.SetFactionDirect(Faction.OfPlayer);
                }
                
                // Fixes a bug w/ support for B19+ and later where colonists go *crazy*
                // if they enter a Stargate after they've ever been drafted.
                if (currentThing is Pawn pawn && pawn.IsColonist)
                {
                    pawn.verbTracker = new VerbTracker(pawn);
                }

                GenPlace.TryPlaceThing(currentThing, this.Position + new IntVec3(0, 0, -2), this.currentMap, ThingPlaceMode.Near);
            }
            //Log.Message("End of Placing");
            inboundBuffer.Clear();

            // Tell the MapDrawer that here is something thats changed
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);

            this.MoveToBackup();

            return true;
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
                if (this.listOfBufferThings.Count > 0)
                {
                    return Building_Stargate.graphicActive;
                }
                else
                {
                    return Building_Stargate.graphicInactive;

                }
                //return base.Graphic;
            }
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n"
                + "Buffer Items: " + this.listOfBufferThings.Count + "\n"
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
            else
            {
                //Log.Warning("Building_Stargate.MoveToBackup(), file at FileLocationPrimary not found.");
            }
        }

    }
}
