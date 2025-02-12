using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
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
        }

        public StargateBuffer(IThingHolder owner): base(owner)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
        }

        public void Init()
        {
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
            Log.Warning("Item Mass: " + this.findThingMass(item) + " kg");
            Log.Warning("Total Storaged Mass: " + this.storedMass + " kg");
            this.SetRequiredStargatePower();

            // Increase the maxStacks size for every Pawn, as they don't affect the dispersion area.
            if (item is Pawn pawn)
            {
                // Increase the maxStacks size for every Pawn, as they don't affect the dispersion area.
                ++this.maxStacks;
            }
            else
            {
                if (this.InnerListForReading.Count >= this.maxStacks)
                {
                    return false;
                }
            }

            // Clear its existing Holder (the actual Stargate).
            item.holdingOwner = null;
            if (!base.TryAdd(item, canMergeWithExistingStacks))
            {
                return false;
            }

            item.DeSpawn();

            return true;
        }

        public void TransmitContents()
        {
            Enhanced_Development.Stargate.Saving.SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);

            for (int a = this.InnerListForReading.Count - 1; a >= 0; --a)
            {
                var thing = this.InnerListForReading[a];

                thing.Destroy();
            }

            // Inform the Colonist Bar that 1 or more Colonists may be missing.
            Find.ColonistBar.MarkColonistsDirty();

            // Tell the MapDrawer that here is something that's changed.
            #if RIMWORLD15
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

        public static Pawn GenerateMissingRelationshipRecord(int thingID, Name pawnName)
        {
            // Create a pawn generation request.
            // Here we use PawnKindDefOf.Colonist as a placeholder.
            // You may wish to use another kind that fits your mod better.
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: PawnKindDefOf.Colonist,
                faction: null, // No faction: it’s not really “alive” in this game.
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true);

            Pawn missingPawn = PawnGenerator.GeneratePawn(request);
            missingPawn.relations.everSeenByPlayer = true;

            // Set the pawn's name to what we want.
            missingPawn.Name = pawnName;

            // If an existing pawn has the same thingID then,
            var existingPawn = Find.WorldPawns.AllPawnsAliveOrDead.Find(p => p.thingIDNumber == thingID);
            if (existingPawn != null)
            {
                // Give them a random ThingID if they aren't named the same. It won't really matter.
                if (existingPawn.Name != pawnName)
                {
                    existingPawn.thingIDNumber = -1;
                    Verse.ThingIDMaker.GiveIDTo(existingPawn);
                }
                else
                {
                    // If they have the same name, they're the same entity from another universe.
                    // So give the new guy a random ThingID...
                    Verse.ThingIDMaker.GiveIDTo(missingPawn);
                }
            }

            // Now override the automatically-assigned thingIDNumber with our saved thingID.
            missingPawn.thingIDNumber = thingID;

            // Ensure the pawn is not spawned anywhere.
            if (missingPawn.Spawned)
            {
                missingPawn.DeSpawn();
            }

            // Now destroy the pawn so that she/he is marked as "Missing" and can never respawn.
            missingPawn.Destroy();

            // Register the pawn with the WorldPawns list.
            // This will flag the pawn as a “world pawn” (i.e. not present on any map)
            // so that if other pawns refer to it in relationships, it appears as Missing.
            Find.WorldPawns.PassToWorld(missingPawn, PawnDiscardDecideMode.Decide);

            return missingPawn;
        }
    }
}
