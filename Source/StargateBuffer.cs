// ==== Source/StargateBuffer.cs ====
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using BetterRimworlds.Stargate.Services;

namespace BetterRimworlds.Stargate
{
    public class StargateBuffer : ThingOwner<Thing>, IList<Thing>
    {
        public bool usingScenarioSeedBuffer = false;

        protected string StargateBufferFilePath;
        protected string StargateBackupFilePath;
        private string ScenarioSeedBufferFilePath;
        private bool pathsInitialized = false;
        // Set when a recall actually loaded the packaged seed; consumed after rematerialization.
        private bool loadedFromScenarioSeedThisStream = false;
        protected int numberOfPawns = 0;
        private float storedMass = 0.0f;
        private IntVec3 Position;

        Thing IList<Thing>.this[int index]
        {
            get => this.GetAt(index);
            set => throw new InvalidOperationException("ThingOwner doesn't allow setting individual elements.");
        }

        public StargateBuffer(IThingHolder owner, bool oneStackOnly, LookMode contentsLookMode = LookMode.Deep) :
            base(owner, oneStackOnly, contentsLookMode)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
        }

        public StargateBuffer(IThingHolder owner) : base(owner)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
        }

        public void InitializePaths()
        {
            this.EnsurePathsInitialized();
        }

        private void EnsurePathsInitialized()
        {
            if (this.pathsInitialized)
            {
                return;
            }

            string baseDirectory = Path.Combine(Verse.GenFilePaths.SaveDataFolderPath, "Stargate");

            this.StargateBufferFilePath = Path.Combine(baseDirectory, "Stargate.xml");
            this.StargateBackupFilePath = Path.Combine(baseDirectory, "StargateBackup.xml");
            this.ScenarioSeedBufferFilePath = null;
            this.usingScenarioSeedBuffer = false;

            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }

            // Player Stargate.xml always wins and permanently retires packaged SG-1 for this savegame.
            if (File.Exists(this.StargateBufferFilePath))
            {
                Stargate.DisableScenarioSeedTeam("player Stargate.xml is present");
            }
            else if (Stargate.IsScenarioSeedTeamAllowed())
            {
                string scenarioSeedBufferPath = Path.Combine(
                    Stargate.ModRootPath(),
                    GetScenarioSeedBufferVersionDirectory(),
                    "StargateBuffer.xml"
                );

                if (File.Exists(scenarioSeedBufferPath))
                {
                    this.ScenarioSeedBufferFilePath = scenarioSeedBufferPath;
                    Log.Message("[Stargate] Using scenario seed StargateBuffer.xml: " + scenarioSeedBufferPath);
                }
                else
                {
                    Log.Error("[Stargate] Stargate base scenario detected, but could not find StargateBuffer.xml at: " + scenarioSeedBufferPath);
                }
            }

            this.pathsInitialized = true;
        }

        private static string GetScenarioSeedBufferVersionDirectory()
        {
#if RIMWORLD12 || RIMWORLD13
            return "1.2";
#else
            return "1.4";
#endif
        }

        private string GetIncomingBufferFilePath()
        {
            // Always prefer a real player off-world buffer when present.
            if (File.Exists(this.StargateBufferFilePath))
            {
                this.usingScenarioSeedBuffer = false;
                Stargate.DisableScenarioSeedTeam("player Stargate.xml is present");
                return this.StargateBufferFilePath;
            }

            // Packaged SG-1 seed is savegame-scoped (all maps/stargates), not per-building.
            if (Stargate.IsScenarioSeedTeamAllowed()
                && !string.IsNullOrEmpty(this.ScenarioSeedBufferFilePath)
                && File.Exists(this.ScenarioSeedBufferFilePath))
            {
                this.usingScenarioSeedBuffer = true;
                return this.ScenarioSeedBufferFilePath;
            }

            this.usingScenarioSeedBuffer = false;
            return null;
        }

        /// Clears local seed path state and permanently disables packaged scenario-seed teams
        /// for the entire savegame (see <see cref="Stargate.DisableScenarioSeedTeam"/>).
        public void ConsumeScenarioSeedBuffer()
        {
            this.ScenarioSeedBufferFilePath = null;
            this.usingScenarioSeedBuffer = false;
            this.loadedFromScenarioSeedThisStream = false;
            Stargate.DisableScenarioSeedTeam("scenario seed team was recalled");
        }

        public void Init()
        {
            this.EnsurePathsInitialized();

            this.calculateStoredMass();
            Log.Warning("Total stored mass: " + this.storedMass + " kg");
            this.Position = ((Building_Stargate)this.owner).Position;
        }

        public float findThingMass(Thing thing)
        {
            return thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
        }

        private void calculateStoredMass()
        {
            foreach (var thing in this.InnerListForReading)
            {
                this.storedMass += this.findThingMass(thing);
            }
        }

        public void SetStargateFilePath(string stargateBufferFilePath)
        {
            this.StargateBufferFilePath = stargateBufferFilePath;
            this.ScenarioSeedBufferFilePath = null;
            this.usingScenarioSeedBuffer = false;
            this.pathsInitialized = true;
        }

        public bool SetRequiredStargatePower()
        {
            var stargate = (Building_Stargate)this.owner;
            float requiredWatts = this.storedMass - 1_000f;
            if (requiredWatts > 0)
            {
                return stargate.UpdateRequiredPower(requiredWatts);
            }
            return true;
        }

        public void EjectLeastMassive()
        {
            this.InnerListForReading.Sort((x, y) => this.findThingMass(y).CompareTo(this.findThingMass(x)));
            var mostMassive = this.InnerListForReading.Pop();
            this.storedMass -= this.findThingMass(mostMassive);

            Messages.Message("Due to lack of power, the Stargate lost " + mostMassive.Label + " x" + mostMassive.stackCount, MessageTypeDefOf.NegativeEvent);
            this.SetRequiredStargatePower();
        }

        public void EjectMostMassive()
        {
            this.InnerListForReading.Sort((x, y) => this.findThingMass(x).CompareTo(this.findThingMass(y)));
            var mostMassive = this.InnerListForReading.Pop();

            GenPlace.TryPlaceThing(mostMassive, this.Position + new IntVec3(0, 0, -2), Find.CurrentMap, ThingPlaceMode.Near);
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            this.EnsurePathsInitialized();

            this.storedMass += this.findThingMass(item);
            Log.Message("Item Mass: " + this.findThingMass(item) + " kg");
            Log.Message("Total Storaged Mass: " + this.storedMass + " kg");
            this.SetRequiredStargatePower();

            if (item is Pawn pawn)
            {
                ++this.maxStacks;
                StargatePawnService.AttachGateTravelerImplant(pawn);
            }
            else
            {
                if (this.InnerListForReading.Count >= this.maxStacks)
                {
                    return false;
                }
            }

            item.holdingOwner = null;
            if (!base.TryAdd(item, canMergeWithExistingStacks))
            {
                return false;
            }

            if (item.Spawned)
            {
                item.DeSpawn();
            }

            return true;
        }

        public void TransmitContents()
        {
            this.EnsurePathsInitialized();

            Enhanced_Development.Stargate.Saving.SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);
            // Writing a real player buffer permanently retires packaged SG-1 for this savegame.
            Stargate.DisableScenarioSeedTeam("player Stargate.xml was written");

            for (int a = this.InnerListForReading.Count - 1; a >= 0; --a)
            {
                var thing = this.InnerListForReading[a];
                if (!thing.Destroyed)
                {
                    thing.Destroy();
                }
                else
                {
                    thing.Discard();
                }
            }

            Find.ColonistBar.MarkColonistsDirty();

            #if RIMWORLD15 || RIMWORLD16
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
            #else
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            #endif

            this.maxStacks = 5000;
            this.storedMass = 0;
        }

        public List<Thing> Flush()
        {
            var items = new List<Thing>(this.InnerListForReading);
            this.Clear();
            this.storedMass = 0;
            this.maxStacks = 5000;
            return items;
        }

        public int getMaxStacks() => this.maxStacks;
        public float GetStoredMass() => this.storedMass;

        public void Empty()
        {
            this.storedMass = 0;
            this.Clear();
        }

        public bool isOffworldTeleportEvent()
        {
            this.EnsurePathsInitialized();

            return this.GetIncomingBufferFilePath() != null;
        }

        public bool hasIncomingWormhole()
        {
            this.EnsurePathsInitialized();

            return this.GetIncomingBufferFilePath() != null;
        }

        public Tuple<int, List<Thing>> receiveIncomingStream()
        {
            this.EnsurePathsInitialized();

            var inboundBuffer = new List<Thing>();
            string incomingBufferFilePath = this.GetIncomingBufferFilePath();
            if (incomingBufferFilePath == null)
            {
                Messages.Message("No incoming wormhole detected.", MessageTypeDefOf.RejectInput);
                return null;
            }

            // Capture before load side-effects; seed must stay visible until rematerialization finishes.
            this.loadedFromScenarioSeedThisStream = this.usingScenarioSeedBuffer;

            var loadResponse = Enhanced_Development.Stargate.Saving.SaveThings.load(ref inboundBuffer, incomingBufferFilePath);
            int originalTimelineTicks = loadResponse.Item1;

            foreach (Pawn pawn in inboundBuffer.OfType<Pawn>())
            {
                StargatePawnService.ClearExistingWorldPawn(pawn);
            }

            return new Tuple<int, List<Thing>>(originalTimelineTicks, inboundBuffer);
        }

        public void MoveToBackup()
        {
            this.EnsurePathsInitialized();

            if (this.usingScenarioSeedBuffer || this.loadedFromScenarioSeedThisStream)
            {
                // Packaged seed lives in the mod folder and must not be moved; mark it one-shot instead.
                Log.Message("[Stargate] Scenario seed StargateBuffer.xml was used; skipping MoveToBackup().");
                this.ConsumeScenarioSeedBuffer();
                this.loadedFromScenarioSeedThisStream = false;
                return;
            }

            try
            {
                if (File.Exists(this.StargateBackupFilePath))
                {
                    int index = 1;
                    string baseDir = Path.GetDirectoryName(this.StargateBackupFilePath);
                    string newFile = Path.Combine(baseDir, $"StargateBackup-{index}.xml");

                    while (File.Exists(newFile))
                    {
                        ++index;
                        newFile = Path.Combine(baseDir, $"StargateBackup-{index}.xml");
                    }

                    File.Move(this.StargateBackupFilePath, newFile);
                }

                if (File.Exists(this.StargateBufferFilePath))
                {
                    File.Move(this.StargateBufferFilePath, this.StargateBackupFilePath);
                }
            }
            catch (Exception e)
            {
                Log.Error("Couldn't move the stargate buffer to backup: " + e.Message);
            }
        }
    }
}
