using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateRelation: IExposable
    {
        public int pawnID;
        public Name pawnName;
        public Gender pawnGender;
        public string relationship;

        public StargateRelation()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnID, "pawn");
            Scribe_Deep.Look(ref pawnName, "name");
            Scribe_Values.Look(ref pawnGender, "gender");
            Scribe_Values.Look(ref relationship, "relationship");
        }

        public StargateRelation(Pawn pawn, string relationship, DirectPawnRelation realRelationship)
        {
            this.pawnID = pawn.thingIDNumber;
            this.pawnName = pawn.Name;
            this.pawnGender = pawn.gender;
            this.relationship = relationship;
        }
    }
}
