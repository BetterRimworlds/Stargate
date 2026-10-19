// ==== Source/StargateBuffer.cs ====

using BetterRimworlds.Stargate.Saving;
using BetterRimworlds.Stargate.Services;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateBuffer : ThingOwner<Thing>, IList<Thing>
    {
        protected String StargateBufferFilePath;
        protected int numberOfPawns = 0;

        private float storedMass = 0.0f;

        Thing IList<Thing>.this[int index]
        {
            get => this.GetAt(index);
            set => throw new InvalidOperationException("ThingOwner doesn't allow setting individual elements.");
        }

        private IntVec3 Position;

        public StargateBuffer(IThingHolder owner, bool oneStackOnly, LookMode contentsLookMode = LookMode.Deep) :
            base(owner, oneStackOnly, contentsLookMode)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
            this.EnableEventHorizonStasis();
        }

        public StargateBuffer(IThingHolder owner): base(owner)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
            this.EnableEventHorizonStasis();
        }

        public void Init()
        {
            // Re-assert stasis after load: older saves may have scribed
            // dontTickContents as false (the 1.6 default).
            this.EnableEventHorizonStasis();

            this.calculateStoredMass();
            Log.Warning("Total stored mass: " + this.storedMass + " kg");
            this.Position = ((Building_Stargate)this.owner).Position;
        }

        /// Event-horizon matter stream: no subjective time passes for anything
        /// held in the buffer. In 1.6+, ThingOwner can refuse to tick contents.
        private void EnableEventHorizonStasis()
        {
#if RIMWORLD16
            this.dontTickContents = true;
#endif
        }

        public float findThingMass(Thing thing)
        {
            return thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
        }

        private void calculateStoredMass()
        {
            foreach (var thing in this.InnerListForReading)
            {
                float mass = this.findThingMass(thing);

                // Log.Message("Thing (" + thing.def.defName + ") = " + mass + " kg");
                this.storedMass += mass;
            }
        }

        public void SetStargateFilePath(String stargateBufferFilePath)
        {
            this.StargateBufferFilePath = stargateBufferFilePath;
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
            this.InnerListForReading.Sort((x, y) =>
                this.findThingMass(y).CompareTo(this.findThingMass(x)));
            var mostMassive = this.InnerListForReading.Pop();
            this.storedMass -= this.findThingMass(mostMassive);

            Messages.Message("Due to lack of power, the Stargate lost " + mostMassive.Label + " x" + mostMassive.stackCount, MessageTypeDefOf.NegativeEvent);
            this.SetRequiredStargatePower();
        }

        public void EjectMostMassive()
        {
            // this.InnerListForReading.Sort((x, y) => x.OrderDate.CompareTo(y.OrderDate));
            // var list = new List<>()
            this.InnerListForReading.Sort((x, y) => this.findThingMass(x).CompareTo(this.findThingMass(y)));
            var mostMassive = this.InnerListForReading.Pop();
            var stargate = (Building_Stargate)this.owner;

            GenPlace.TryPlaceThing(mostMassive, this.Position + new IntVec3(0, 0, -2), Find.CurrentMap, ThingPlaceMode.Near);
            //stargate.StargateRecall()
            // this.TryDrop(mostMassive, this.Position + new IntVec3(0, 0, -2), Find.CurrentMap, ThingPlaceMode.Near);
            // this.TryDrop(mostMassive, this.Position + new IntVec3(0, 0, -2), this.currentMap, ThingPlaceMode.Near, out Thing unused);
            /*
              Thing thing,
              IntVec3 dropLoc,
              Map map,
              ThingPlaceMode mode,
             */
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            this.storedMass += this.findThingMass(item);
            Log.Message("Item Mass: " + this.findThingMass(item) + " kg");
            Log.Message("Total Storaged Mass: " + this.storedMass + " kg");
            this.SetRequiredStargatePower();

            // Increase the maxStacks size for every Pawn, as they don't affect the dispersion area.
            if (item is Pawn)
            {
                ++this.maxStacks;
            }
            else
            {
                if (this.InnerListForReading.Count >= this.maxStacks)
                {
                    return false;
                }
            }

            // A pawn entering from the map still has its live relations. A pawn
            // re-added after a failed placement has already had them cleared for
            // rematerialization, so only snapshot genuine map entries.
            bool wasSpawned = item.Spawned;

            // Clear its existing Holder (the actual Stargate).
            item.holdingOwner = null;
            if (!base.TryAdd(item, canMergeWithExistingStacks))
            {
                return false;
            }

            if (item is Pawn pawnInBuffer)
            {
                GateTravelerImplant implant = StargatePawnService.AttachGateTravelerImplant(pawnInBuffer);

                if (wasSpawned)
                {
                    implant?.RefreshRelationshipsForStargateEntry(allowEmpty: true);
                }
            }

            if (item.Spawned)
            {
                item.DeSpawn();
            }

            return true;
        }

        public void TransmitContents()
        {
            // Flush runtime implant research into its scribed representation immediately
            // before buffer XML write.
            this.PreparePawnsForStargateSerialization();

            SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);

            for (int a = this.InnerListForReading.Count - 1; a >= 0; --a)
            {
                var thing = this.InnerListForReading[a];

                if (thing.Destroyed == false)
                {
                    thing.Destroy();
                }
                else
                {
                    thing.Discard();
                }
            }

            // this.Clear();

            // Inform the Colonist Bar that 1 or more Colonists may be missing.
            Find.ColonistBar.MarkColonistsDirty();

            // Tell the MapDrawer that here is something that's changed.
            #if RIMWORLD15 || RIMWORLD16
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
            #else
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            #endif

            this.maxStacks = 5000;
            this.storedMass = 0;
        }

        public int getMaxStacks()
        {
            return this.maxStacks;
        }

        public float GetStoredMass()
        {
            return this.storedMass;
        }

        public void Empty()
        {
            this.storedMass = 0;
            this.Clear();
        }

        public void RebuildRelationships()
        {
            StargatePawnService.RebuildRelationships();
        }

        public static Pawn GenerateMissingRelationshipRecord(int thingID, Name pawnName, Gender pawnGender)
        {
            return StargatePawnService.GenerateMissingRelationshipRecord(thingID, pawnName, pawnGender);
        }

        private void PreparePawnsForStargateSerialization()
        {
            foreach (Thing thing in this.InnerListForReading)
            {
                if (thing is not Pawn pawn)
                {
                    continue;
                }

                if (pawn.RaceProps?.Humanlike != true)
                {
                    continue;
                }

                GateTravelerImplant implant = pawn.health?.hediffSet?.hediffs
                    .OfType<GateTravelerImplant>()
                    .FirstOrDefault();

                if (implant == null)
                {
                    continue;
                }

                Log.Message(
                    $"[Stargate] Serializing research for {pawn.LabelShortCap}: " +
                    $"{implant.Research?.Count ?? 0} project(s), " +
                    $"{implant.GetTotalResearchMemoryPoints():0.#} points."
                );
            }
        }

        public static bool ClearExistingWorldPawn(Pawn pawn)
        {
            // Only the genuine returning traveler's dead/placeholder record may
            // be cleared. An unrelated destination pawn that happens to share
            // the origin-world ID must stay in the world so RemapCollidingPawnThingIds
            // sees the collision and gives the incoming pawn a new ID.
            return StargatePawnService.ClearExistingWorldPawn(pawn);
        }

        public bool isOffworldTeleportEvent()
        {
            return System.IO.File.Exists(this.StargateBufferFilePath);
        }

        public Tuple<int, List<Thing>> receiveIncomingStream()
        {
            var inboundBuffer = new List<Thing>();
            int originalTimelineTicks;

            // Load off-world teams only if there isn't a local teleportation taking place.
            // bool offworldEvent = this.stargateBuffer.Count == 0;
            // bool offworldEvent = inboundBuffer.Any();
            bool offworldEvent = this.isOffworldTeleportEvent();
            Log.Warning("Is offworldEvent? " + offworldEvent);
            Log.Warning("Inbound Buffer Count? " + inboundBuffer.Count);

            if (!offworldEvent)
            {
                Messages.Message("No incoming wormhole detected.", MessageTypeDefOf.RejectInput);

                return null;
            }

            var loadResponse = SaveThings.load(ref inboundBuffer, this.StargateBufferFilePath);
            originalTimelineTicks = loadResponse.Item1;

            List<Pawn> inboundPawns = inboundBuffer.OfType<Pawn>().ToList();

            // Remove matching dead world-pawn records first so a returning
            // traveler can keep their original ID when it is still available.
            // ClearExistingWorldPawn matches identity (name, gender and def),
            // not just the reused ID, so an unrelated dead pawn stays put and
            // the collision scan below remaps the incoming pawn instead.
            foreach (Pawn pawn in inboundPawns)
            {
                ClearExistingWorldPawn(pawn);
            }

            // Cross-world buffers carry IDs from the origin save. If an ID is
            // already used here, give the incoming pawn a destination-world ID
            // instead of letting two pawns share one ID.
            StargateLoadIdRemapper.RemapCollidingPawnThingIds(
                inboundPawns,
                out List<StargateLoadIdRemapper.PawnIdRemap> pawnIdRemaps);

            foreach (Pawn pawn in inboundPawns)
            {
                GateTravelerImplant implant = pawn.health?.hediffSet?.hediffs
                    .OfType<GateTravelerImplant>()
                    .FirstOrDefault();

                // A co-traveler's snapshot still holds the origin-world ID. Update
                // it before merging live relations, otherwise a remapped parent or
                // child is either dropped or resolved to an unrelated pawn.
                if (implant?.relationships != null && pawnIdRemaps.Count > 0)
                {
                    int updatedRelationshipIds =
                        StargateLoadIdRemapper.ApplyPawnIdRemapsToRelationships(
                            implant.relationships,
                            pawnIdRemaps);

                    if (updatedRelationshipIds > 0)
                    {
                        Log.Message(
                            $"[Stargate] Updated {updatedRelationshipIds} relationship ID(s) "
                            + $"for {pawn.LabelShortCap} after remapping co-travelers."
                        );
                    }
                }

                // The pawns loaded from the buffer file still have the direct
                // relations they had when they stepped through the gate. Capture
                // them now, before rematerialization replaces the relations
                // tracker, so any relations changed since the last transit are
                // not lost and stale snapshots are repaired. This also records
                // any thingIDs remapped above.
                implant?.RefreshRelationshipsForStargateEntry();
            }

            // Log.Warning("Number of items in the wormhole: " + inboundBuffer.Count);

            return new Tuple<int, List<Thing>>(originalTimelineTicks, inboundBuffer);
        }
    }
}
