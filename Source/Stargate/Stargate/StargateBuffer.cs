using System;
using System.Collections.Generic;
using Enhanced_Development.Stargate.Saving;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateBuffer : ThingOwner<Thing>, IList<Thing>
    {
        // private Dictionary<string, Dictionary<stri-ng, PawnRelationDef>> relationships = new Dictionary<string, Dictionary<string, PawnRelationDef>>();
        public StargateRelations relationships = new StargateRelations();

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

        private bool SetRequiredStargatePower()
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
                foreach (var relationship in pawn.relations.DirectRelations)
                {
                    // See if this relation is already recorded using the other pawn as the primary.
                    if (relationships.ContainsRelationship(relationship.otherPawn.ThingID, pawn.ThingID))
                    {
                        continue;
                    }

                    // // Only record if the other pawn is in the same outgoing buffer.
                    // if (loadedPawnIds.Contains(relation.otherPawn.ThingID) == false)
                    // {
                    //     continue;
                    // }

                    relationships.Add(new StargateRelation(pawn.ThingID, relationship.otherPawn.ThingID, relationship.def.defName));
                }
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
        }

        public int getMaxStacks()
        {
            return this.maxStacks;
        }

        public float GetStoredMass()
        {
            return this.storedMass;
        }
    }
}
