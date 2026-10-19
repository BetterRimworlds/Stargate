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
            // Include world pawns and gravship/temporary pawns, not just pawns on
            // loaded maps. A colonist left behind on another world can still be
            // tracked as a world pawn, and its GateTravelerImplant snapshot is the
            // only place its relationships survive a cross-world trip.
            return PawnsFinder.All_AliveOrDead
                .Where(pawn => pawn != null && !pawn.Dead);
        }

        public static void RebuildRelationships()
        {
            var implantDef = HediffDef.Named("GateTravelerImplant");
            var alivePawns = GetAllAlivePawns().ToList();
            ILookup<int, Pawn> alivePawnsById = alivePawns.ToLookup(pawn => pawn.thingIDNumber);
            var missingPawnsById = new Dictionary<int, Pawn>();

            foreach (var pawn in alivePawns)
            {
                if (pawn?.health?.hediffSet == null || !pawn.health.hediffSet.HasHediff(implantDef))
                {
                    continue;
                }

                var gateTravelImplant = pawn.health.hediffSet.hediffs
                    .OfType<GateTravelerImplant>()
                    .FirstOrDefault();

                if (gateTravelImplant?.relationships == null)
                {
                    continue;
                }

                // Merge any newer live relations into the snapshot first. This is a
                // no-op for a just-rematerialized traveler (its tracker is empty)
                // but repairs world pawns whose live relations are incomplete.
                gateTravelImplant.RefreshRelationshipsForStargateEntry();

                bool addedRelationship = false;

                foreach (var relationship in gateTravelImplant.relationships.ToList())
                {
                    if (relationship == null)
                    {
                        continue;
                    }

                    Log.Message($"Processing relationship for {pawn.LabelShort}: {relationship.relationship} with {relationship.pawnName}.");

                    Pawn pawn2 = alivePawnsById[relationship.pawnID]
                        .FirstOrDefault(candidate => SnapshotMatches(candidate, relationship));

                    if (pawn2 == null)
                    {
                        pawn2 = Find.WorldPawns.AllPawnsAliveOrDead
                            .FirstOrDefault(p =>
                                p != null
                                && p.thingIDNumber == relationship.pawnID
                                && SnapshotMatches(p, relationship));
                    }

                    if (pawn2 == null
                        && (!missingPawnsById.TryGetValue(relationship.pawnID, out pawn2)
                            || !SnapshotMatches(pawn2, relationship)))
                    {
                        Log.Warning($"Could not find pawn {relationship.pawnName} ({relationship.pawnID}). Generating a 'Missing' record.");
                        pawn2 = FindExistingMissingRecord(relationship)
                            ?? GenerateMissingRelationshipRecord(relationship.pawnID, relationship.pawnName, relationship.pawnGender);
                        missingPawnsById[relationship.pawnID] = pawn2;
                    }

                    PawnRelationDef pawnRelationDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail(relationship.relationship);

                    if (pawnRelationDef == null)
                    {
                        Log.Error($"Could not find PawnRelationDef named '{relationship.relationship}'. Skipping.");
                        continue;
                    }

                    // Child, Stepchild, etc. are implied relations. Vanilla generates
                    // them automatically from the Parent/Spouse side, and refuses to
                    // add them as direct relations.
                    if (pawnRelationDef.implied)
                    {
                        Log.Warning($"Skipping implied relation '{pawnRelationDef.defName}' for {pawn.LabelShort}; it is generated automatically.");
                        continue;
                    }

                    if (StargateRelationshipSnapshot.HasConflictingLiveRelation(
                        pawn, pawn2, pawnRelationDef.defName))
                    {
                        // The counterpart may still have an old snapshot even
                        // after the first pawn's snapshot has been repaired.
                        gateTravelImplant.relationships.Remove(relationship);
                        continue;
                    }

                    // Match BOTH the relation def and the other pawn. One pawn can
                    // legitimately have several relations with the same def, such as
                    // a father and a mother (both stored as "Parent"). Matching on
                    // def alone treated the second parent as stale and deleted the
                    // first one, making a mother appear as a stepmother.
                    DirectPawnRelation existingRelation = pawn.relations.DirectRelations
                        .FirstOrDefault(rel => rel.def == pawnRelationDef && rel.otherPawn == pawn2);

                    if (existingRelation != null)
                    {
                        Log.Message($"Correct relationship between {pawn.LabelShort} and {pawn2.LabelShort} already exists. Skipping.");
                        continue;
                    }

                    // Replace a placeholder for the same pawn, but never remove a
                    // different pawn that happens to share this relation def.
                    DirectPawnRelation stalePlaceholder = pawn.relations.DirectRelations
                        .FirstOrDefault(rel =>
                            rel.def == pawnRelationDef &&
                            rel.otherPawn != null &&
                            rel.otherPawn != pawn2 &&
                            rel.otherPawn.thingIDNumber == pawn2.thingIDNumber);

                    if (stalePlaceholder != null)
                    {
                        Log.Warning($"Replacing stale relationship ({pawnRelationDef.defName}) for {pawn.LabelShort} with real pawn {pawn2.LabelShort}.");
                        pawn.relations.RemoveDirectRelation(stalePlaceholder);
                    }

                    Log.Message($"Adding direct relation {pawnRelationDef.defName} between {pawn.LabelShort} and {pawn2.LabelShort}.");
                    pawn.relations.AddDirectRelation(pawnRelationDef, pawn2);
                    addedRelationship = true;
                }

                // Clear thoughts and memories once, after all relations are restored,
                // so mood recalculates against the complete relationship set. World
                // pawns have no active mind to clear.
                if (addedRelationship && pawn.Spawned)
                {
                    pawn.ClearMind(true);
                }
            }
        }

        private static bool SnapshotMatches(Pawn candidate, StargateRelation relationship)
        {
            if (candidate == null)
            {
                return false;
            }

            if (relationship.pawnGender != Gender.None
                && candidate.gender != relationship.pawnGender)
            {
                return false;
            }

            if (relationship.pawnName == null)
            {
                return true;
            }

            return candidate.Name != null && candidate.Name.Equals(relationship.pawnName);
        }

        private static Pawn FindExistingMissingRecord(StargateRelation relationship)
        {
            return Find.WorldPawns.AllPawnsDead.FirstOrDefault(p =>
                p != null
                && p.Faction == null
                && p.kindDef == PawnKindDefOf.Colonist
                && p.relations?.everSeenByPlayer == true
                && SnapshotMatches(p, relationship));
        }

        private static bool IsThingIdInUse(int thingID)
        {
            if (PawnsFinder.All_AliveOrDead.Any(p => p != null && p.thingIDNumber == thingID)
                || Find.WorldPawns.AllPawnsAliveOrDead.Any(p => p != null && p.thingIDNumber == thingID))
            {
                return true;
            }

            foreach (Map map in Find.Maps)
            {
                if (map?.listerThings?.AllThings?.Any(t => t != null && t.thingIDNumber == thingID) == true)
                {
                    return true;
                }
            }

            return false;
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

            // Reuse the saved ID only when it is genuinely free. If another pawn
            // or thing owns it, keep the generated ID so the placeholder does not
            // recreate the duplicate-ID collision this pass just repaired.
            if (!IsThingIdInUse(thingID))
            {
                missingPawn.thingIDNumber = thingID;
            }

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

        public static GateTravelerImplant AttachGateTravelerImplant(Pawn pawn)
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

            implant?.RecordStargateBufferEntry();
            return implant;
        }

        /// A cross-world buffer can reuse an ID that belongs to a completely
        /// different dead pawn here. Only treat the dead record as the returning
        /// traveler when the identity actually matches; otherwise leave it alone
        /// and let the collision remapper give the newcomer a fresh ID.
        public static bool IsSamePawnRecord(Pawn incoming, Pawn existing)
        {
            if (incoming == null || existing == null || incoming.def != existing.def
                || incoming.gender != existing.gender)
            {
                return false;
            }

            Name incomingName = incoming.Name;
            Name existingName = existing.Name;

            if (incomingName == null || existingName == null)
            {
                return incomingName == null && existingName == null;
            }

            return incomingName.Equals(existingName);
        }

        public static bool ClearExistingWorldPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            // See if this pawn exists in the Dead WorldPawns, and if so, remove the record, because now she/he is back!
            // Match identity, not just the reused ID, so an unrelated dead pawn
            // and its historical relationships survive the return trip.
            Pawn pawnToRemove = Find.WorldPawns.AllPawnsDead.FirstOrDefault(p =>
                p != null
                && p.thingIDNumber == pawn.thingIDNumber
                && IsSamePawnRecord(pawn, p));

            if (pawnToRemove != null)
            {
                Log.Warning("Pawn with ID " + pawn.thingIDNumber + " already exists in the world.");
                Messages.Message($"Removed dead world pawn: {pawn.Name?.ToStringFull ?? pawn.LabelShort}", MessageTypeDefOf.NeutralEvent);

                // pawnToRemove.Discard();
                Find.WorldPawns.RemovePawn(pawnToRemove);
                return true;
            }

            return false;
        }
    }
}
