using System;
using System.Collections.Generic;
using System.Linq;
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


        Thing IList<Thing>.this[int index]
        {
            get => this.GetAt(index);
            set => throw new InvalidOperationException("ThingOwner doesn't allow setting individual elements.");
        }

        public StargateBuffer(): base()
        {
            this.maxStacks = 500;
            this.contentsLookMode = LookMode.Deep;
        }

        public StargateBuffer(IThingHolder owner, bool oneStackOnly, LookMode contentsLookMode = LookMode.Deep) :
            base(owner, oneStackOnly, contentsLookMode)
        {
            this.maxStacks = 500;
            this.contentsLookMode = LookMode.Deep;
        }

        public StargateBuffer(IThingHolder owner): base(owner)
        {
            this.maxStacks = 500;
            this.contentsLookMode = LookMode.Deep;
        }

        public void SetStargateFilePath(String stargateBufferFilePath)
        {
            this.StargateBufferFilePath = stargateBufferFilePath;
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            // Increase the maxStacks size for every Pawn, as they don't affect the dispersion area.
            // if (item is Pawn)
            // {
            //     ++this.maxStacks;
            // }

            if (item is Pawn pawn)
            {
                ++this.numberOfPawns;
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

        public void TransmitContentsV1()
        {
            Enhanced_Development.Stargate.Saving.SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);
            // this.RemoveAll(item => item is Pawn);
            foreach (Pawn p in this.InnerListForReading.OfType<Pawn>())
            {
                // p.Discard();
                p.Destroy();
            }

            this.Clear();
        }

        public void TransmitContents()
        {
            Enhanced_Development.Stargate.Saving.SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);

            for (int a = this.InnerListForReading.Count - 1; a >= 0; --a)
            {
                var thing = this.InnerListForReading[a];
                thing.Destroy();
            }
        }

        public int getMaxStacks()
        {
            return this.maxStacks + this.numberOfPawns;
        }
    }
}