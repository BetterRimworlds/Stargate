using System.Collections.Generic;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateBuffer : ThingOwner
    {
        List<Thing> bufferedThingsList = new List<Thing>();

        public override int Count
        {
            get
            {
                return bufferedThingsList.Count;
            }
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            this.bufferedThingsList.Add(item);

            return count;
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            this.bufferedThingsList.Add(item);

            if (item is Pawn pawn)
            {
                pawn.DeSpawn();
            }
            else
            {
                item.DeSpawn();
            }

            return true;
        }

        public override int IndexOf(Thing item)
        {
            return this.bufferedThingsList.IndexOf(item);
        }

        public override bool Remove(Thing item)
        {
            return this.bufferedThingsList.Remove(item);
        }

        protected override Thing GetAt(int index)
        {
            return this.bufferedThingsList[index];
        }
    }
}