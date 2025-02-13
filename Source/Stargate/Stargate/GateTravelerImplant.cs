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

        private void RefreshRelationships()
        {
            // this.relationships.Clear();

            // Iterate through each direct relation of this pawn.
            foreach (DirectPawnRelation rel in this.pawn.relations.DirectRelations)
            {
                Pawn otherPawn = rel.otherPawn;
                // Optional: Check if otherPawn is null (this is rarely needed if your data is clean).
                if (otherPawn == null)
                {
                    continue;
                }

                //if (this.relationships.Contains())

                // Use the pawn's unique thingIDNumber as an identifier.
                int pawn1ID = this.pawn.thingIDNumber;

                // Record the relationship using its definition name.
                string relationshipName = rel.def.defName;

                // Create and add the new StargateRelation.
                var relationship = new StargateRelation(pawn1ID, otherPawn, relationshipName);
                if (this.relationships.Contains(relationship))
                {
                    continue;
                }

                relationships.Add(relationship);
                Messages.Message("Refreshed relationships", MessageTypeDefOf.PositiveEvent);
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            this.RefreshRelationships();
        }
    }
}
