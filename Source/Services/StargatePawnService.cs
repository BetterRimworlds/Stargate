// ==== Source/Services/StargatePawnService.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate.Services
{
    public static class StargatePawnService
    {
        private static IEnumerable<Pawn> GetAllAlivePawns()
        {
            #if RIMWORLD16
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
            #else
            return PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive;
            #endif
        }

        public static void RebuildRelationships()
        {
            var implantDef = HediffDef.Named("GateTravelerImplant");
            var pawnsWithGateTravelerImplant = GetAllAlivePawns()
                .Where(pawn => pawn.health.hediffSet.HasHediff(implantDef))
                .ToList();

            // Now you can do whatever you need with that list:
            foreach (var pawn in pawnsWithGateTravelerImplant)
            {
                var gateTravelImplant = (GateTravelerImplant)pawn.health.hediffSet.hediffs.Find(h => h.def == implantDef);
                if (gateTravelImplant == null) continue;

                foreach (var relationship in gateTravelImplant.relationships)
                {
                    Log.Message($"Processing relationship for {pawn.LabelShort}: {relationship.relationship} with {relationship.pawnName}.");

                    var pawn2 = GetAllAlivePawns().FirstOrDefault(p => p.thingIDNumber == relationship.pawnID);

                    if (pawn2 == null)
                    {
                        pawn2 = Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(p => p.thingIDNumber == relationship.pawnID);
                        if (pawn2 == null)
                        {
                            Log.Warning($"Could not find pawn {relationship.pawnName} ({relationship.pawnID}). Generating a 'Missing' record.");
                            pawn2 = GenerateMissingRelationshipRecord(relationship.pawnID, relationship.pawnName, relationship.pawnGender);
                        }
                    }

                    PawnRelationDef pawnRelationDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail(relationship.relationship);

                    if (pawnRelationDef == null)
                    {
                        Log.Error($"Could not find PawnRelationDef named '{relationship.relationship}'. Skipping.");
                        continue;
                    }

                    // Find any existing direct relationship of this type (e.g., "Spouse").
                    // THIS IS THE CORRECTED LINE: We use LINQ's FirstOrDefault on the DirectRelations list.
                    var existingRelation = pawn.relations.DirectRelations.FirstOrDefault(rel => rel.def == pawnRelationDef);

                    if (existingRelation != null)
                    {
                        // If the existing relation is already with the correct pawn, do nothing.
                        if (existingRelation.otherPawn == pawn2)
                        {
                            Log.Message($"Correct relationship between {pawn.LabelShort} and {pawn2.LabelShort} already exists. Skipping.");
                            continue;
                        }

                        // Otherwise, remove the old, stale relationship (e.g., Lu -> "Missing" Ryan)
                        Log.Warning($"Found a stale relationship ({pawnRelationDef.defName}) for {pawn.LabelShort} with {existingRelation.otherPawn.LabelShort}. Removing it.");
                        pawn.relations.RemoveDirectRelation(existingRelation);
                    }

                    // Now, add the new, correct relationship.
                    Log.Message($"Adding direct relation {pawnRelationDef.defName} between {pawn.LabelShort} and {pawn2.LabelShort}.");
                    pawn.relations.AddDirectRelation(pawnRelationDef, pawn2);
                    // Clear thoughts and memories to recalculate mood based on the new relation.
                    pawn.ClearMind(true);
                }
            }
        }

        public static Pawn GenerateMissingRelationshipRecord(int thingID, Name pawnName, Gender pawnGender)
        {
            NameTriple fullName = null;
            Log.Warning("1");
            if (pawnName is NameTriple)
            {
                fullName = (NameTriple) pawnName;
            }

            Log.Warning("2");

            // Create a pawn generation request.
            // Here we use PawnKindDefOf.Colonist as a placeholder.
            // You may wish to use another kind that fits your mod better.
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: PawnKindDefOf.Colonist,
                faction: null, // No faction: it’s not really “alive” in this game.
                context: PawnGenerationContext.NonPlayer,
                fixedLastName: fullName?.Last,
                fixedGender: pawnGender,
                forceGenerateNewPawn: true
            );

            Log.Warning("3");
            Pawn missingPawn = PawnGenerator.GeneratePawn(request);
            Log.Warning("4");
            missingPawn.relations.everSeenByPlayer = true;

            // Set the pawn's name to what we want.
            missingPawn.Name = pawnName;

            // Now override the automatically-assigned thingIDNumber with our saved thingID.
            missingPawn.thingIDNumber = thingID;

            // Ensure the pawn is not spawned anywhere.
            if (missingPawn.Spawned)
            {
                Log.Warning("5");
                missingPawn.DeSpawn();
            }
            Log.Warning("6");

            // Now destroy the pawn so that she/he is marked as "Missing" and can never respawn.
            missingPawn.Destroy();
            Log.Warning("7");

            return missingPawn;
        }

        public static void AttachGateTravelerImplant(Pawn pawn)
        {
            HediffDef gateTravelerImplant = HediffDef.Named("GateTravelerImplant");

            // Find any existing implant hediff
            Hediff existingImplant = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def == gateTravelerImplant);

            GateTravelerImplant implant = existingImplant as GateTravelerImplant;
            if (implant == null)
            {
                BodyPartRecord brain = pawn.RaceProps.body.AllParts.Find(bpr => bpr.def.defName == "Brain");

                implant = pawn.health.AddHediff(gateTravelerImplant, brain) as GateTravelerImplant;
            }

            implant?.RefreshRelationshipsForStargateEntry();
        }

        public static bool ClearExistingWorldPawn(Pawn pawn)
        {
            // See if the pawn exists in the Dead WorldPawns, and if so, remove the record, because now she/he is back!
            Pawn pawnToRemove = Find.WorldPawns.AllPawnsDead.FirstOrDefault(p => p.thingIDNumber == pawn.thingIDNumber);
            if (pawnToRemove != null)
            {
                Log.Warning("Pawn with ID " + pawn.thingIDNumber + " already exists in the world.");
                Messages.Message($"Removed dead world pawn: {pawn.Name.ToStringFull}", MessageTypeDefOf.NeutralEvent);

                // pawnToRemove.Discard();
                Find.WorldPawns.RemovePawn(pawnToRemove);
                return true;
            }

            return false;
        }
    }
}
