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
            if (item is Pawn)
            {
                ++this.maxStacks;
            }

            // Clear its existing Holder.
            item.holdingOwner = null;
            if (!base.TryAdd(item, canMergeWithExistingStacks))
            {
                Log.Error("Couldn't successfully load the item into the Stargate for unknown reasons.");
                return false;
            }

            if (item is Pawn pawn)
            {
                pawn.DeSpawn();

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
                // item.Discard();
                item.DeSpawn();
            }

            return true;
        }

        public void TransmitContents()
        {
            Enhanced_Development.Stargate.Saving.SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);
            // this.RemoveAll(item => item is Pawn);
            foreach (Pawn p in this.InnerListForReading.OfType<Pawn>())
            {
                p.Destroy();
            }

            this.Clear();

        }

        public int getMaxStacks()
        {
            return this.maxStacks;
        }
    }
}