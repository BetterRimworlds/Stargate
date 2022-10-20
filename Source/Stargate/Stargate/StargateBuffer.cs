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
        private Dictionary<string, Dictionary<string, PawnRelationDef>> relationships = new Dictionary<string, Dictionary<string, PawnRelationDef>>();

        protected int PawnCount = 0;

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
            // Clear its existing Holder.
            item.holdingOwner = null;
            if (!base.TryAdd(item, canMergeWithExistingStacks))
            {
                Log.Error("Couldn't successfully load the item into the Stargate for unknown reasons.");
                return false;
            }

            if (item is Pawn pawn)
            {
                //pawn.Discard();
                pawn.DeSpawn();
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
    }
}