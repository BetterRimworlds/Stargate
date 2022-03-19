using System;
using System.Collections.Generic;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateBuffer : ThingOwner<Thing>, IList<Thing>
    {
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
                //item.Discard();
                item.DeSpawn();
            }

            return true;
        }
    }
}