using System.Collections.Generic;
using Enhanced_Development.Stargate;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class ITab_StargateBuffer : ITab_ContentsBase
    {
        public override IList<Thing> container
        {
            get
            {
                var stargate = base.SelThing as Building_Stargate;

                return stargate.GetDirectlyHeldThings();
            }
        }

        public ITab_StargateBuffer()
        {
            labelKey = "TabCasketContents";
            containedItemsKey = "ContainedItems";
            canRemoveThings = false;
        }
    }
}