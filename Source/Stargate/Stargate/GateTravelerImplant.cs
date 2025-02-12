using System.Collections.Generic;
using Enhanced_Development.Stargate.Saving;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class GateTravelerImplant : Hediff_Implant
    {
        public List<StargateRelation> relationships = new List<StargateRelation>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<StargateRelation>(ref this.relationships, "relationships");
        }

        public override void PostMake()
        {
            base.PostMake();
        }
    }
}
