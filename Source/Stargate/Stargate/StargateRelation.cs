using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateRelation: IExposable
    {
        public int pawn1ID;
        public int pawn2ID;
        public Name pawn2Name;
        public string relationship;

        public StargateRelation()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawn1ID, "pawn1");
            Scribe_Values.Look(ref pawn2ID, "pawn2");
            // Scribe_Values.Look(ref pawn2Name, "pawn2Name");
            Scribe_Deep.Look(ref pawn2Name, "pawn2Name");
            Scribe_Values.Look(ref relationship, "relationship");
            // Scribe_Values.Look<DirectPawnRelation>(ref relationship, "relationship", LookMode.Deep);
            // Scribe_Deep.Look(ref relationship, "relationship");


            // Scribe_Defs.Look(ref relationshipDef, "relationship");
        }

        public StargateRelation(int pawn1ID, Pawn pawn2, string relationship)
        {
            this.pawn1ID = pawn1ID;
            this.pawn2ID = pawn2.thingIDNumber;
            this.pawn2Name = pawn2.Name;
            this.relationship = relationship;
        }
    }
}
