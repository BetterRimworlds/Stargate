using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class StargateBuffer : ThingOwner<Thing>, IList<Thing>
    {
        protected String StargateBufferFilePath;
        protected int numberOfPawns = 0;

        private float storedMass = 0.0f;

        Thing IList<Thing>.this[int index]
        {
            get => this.GetAt(index);
            set => throw new InvalidOperationException("ThingOwner doesn't allow setting individual elements.");
        }

        private IntVec3 Position;

        public StargateBuffer(IThingHolder owner, bool oneStackOnly, LookMode contentsLookMode = LookMode.Deep) :
            base(owner, oneStackOnly, contentsLookMode)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
        }

        public StargateBuffer(IThingHolder owner): base(owner)
        {
            this.maxStacks = 5000;
            this.contentsLookMode = LookMode.Deep;
        }

        public void Init()
        {
            this.calculateStoredMass();
            Log.Warning("Total stored mass: " + this.storedMass + " kg");
            this.Position = ((Building_Stargate)this.owner).Position;
        }

        public float findThingMass(Thing thing)
        {
            return thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;
        }

        private void calculateStoredMass()
        {
            foreach (var thing in this.InnerListForReading)
            {
                float mass = this.findThingMass(thing);

                // Log.Message("Thing (" + thing.def.defName + ") = " + mass + " kg");
                this.storedMass += mass;
            }
        }

        public void SetStargateFilePath(String stargateBufferFilePath)
        {
            this.StargateBufferFilePath = stargateBufferFilePath;
        }

        public bool SetRequiredStargatePower()
        {
            var stargate = (Building_Stargate)this.owner;

            float requiredWatts = this.storedMass - 1_000f;
            if (requiredWatts > 0)
            {
                return stargate.UpdateRequiredPower(requiredWatts);
            }

            return true;
        }

        public void EjectLeastMassive()
        {
            this.InnerListForReading.Sort((x, y) =>
                this.findThingMass(y).CompareTo(this.findThingMass(x)));
            var mostMassive = this.InnerListForReading.Pop();
            this.storedMass -= this.findThingMass(mostMassive);

            Messages.Message("Due to lack of power, the Stargate lost " + mostMassive.Label + " x" + mostMassive.stackCount, MessageTypeDefOf.NegativeEvent);
            this.SetRequiredStargatePower();
        }

        public void EjectMostMassive()
        {
            // this.InnerListForReading.Sort((x, y) => x.OrderDate.CompareTo(y.OrderDate));
            // var list = new List<>()
            this.InnerListForReading.Sort((x, y) => this.findThingMass(x).CompareTo(this.findThingMass(y)));
            var mostMassive = this.InnerListForReading.Pop();
            var stargate = (Building_Stargate)this.owner;

            GenPlace.TryPlaceThing(mostMassive, this.Position + new IntVec3(0, 0, -2), Find.CurrentMap, ThingPlaceMode.Near);
            //stargate.StargateRecall()
            // this.TryDrop(mostMassive, this.Position + new IntVec3(0, 0, -2), Find.CurrentMap, ThingPlaceMode.Near);
            // this.TryDrop(mostMassive, this.Position + new IntVec3(0, 0, -2), this.currentMap, ThingPlaceMode.Near, out Thing unused);
            /*
              Thing thing,
              IntVec3 dropLoc,
              Map map,
              ThingPlaceMode mode,
             */
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            this.storedMass += this.findThingMass(item);
            Log.Message("Item Mass: " + this.findThingMass(item) + " kg");
            Log.Message("Total Storaged Mass: " + this.storedMass + " kg");
            this.SetRequiredStargatePower();

            // Increase the maxStacks size for every Pawn, as they don't affect the dispersion area.
            if (item is Pawn pawn)
            {
                // Increase the maxStacks size for every Pawn, as they don't affect the dispersion area.
                ++this.maxStacks;

                this.AttachGateTravelerImplant(pawn);
            }
            else
            {
                if (this.InnerListForReading.Count >= this.maxStacks)
                {
                    return false;
                }
            }

            // Clear its existing Holder (the actual Stargate).
            item.holdingOwner = null;
            if (!base.TryAdd(item, canMergeWithExistingStacks))
            {
                return false;
            }

            item.DeSpawn();

            return true;
        }

        public void TransmitContents()
        {
            Enhanced_Development.Stargate.Saving.SaveThings.save(this.InnerListForReading, this.StargateBufferFilePath);

            for (int a = this.InnerListForReading.Count - 1; a >= 0; --a)
            {
                var thing = this.InnerListForReading[a];

                thing.Destroy();
            }

            // Inform the Colonist Bar that 1 or more Colonists may be missing.
            Find.ColonistBar.MarkColonistsDirty();

            // Tell the MapDrawer that here is something that's changed.
            #if RIMWORLD15
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things, true, false);
            #else
            Find.CurrentMap.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things, true, false);
            #endif

            this.maxStacks = 5000;
            this.storedMass = 0;
        }

        public int getMaxStacks()
        {
            return this.maxStacks;
        }

        public float GetStoredMass()
        {
            return this.storedMass;
        }

        public void Empty()
        {
            this.storedMass = 0;
            this.Clear();
        }

        public void RebuildRelationships()
        {
            // var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("BetterRimworlds.Stargate.GateTravelerImplant");
            var implantDef = HediffDef.Named("GateTravelerImplant");
            var pawnsWithGateTravelerImplant = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive
                .Where(pawn => pawn.health.hediffSet.HasHediff(implantDef))
                .ToList();

            Log.Error("Pawns with Gate Traveler Implant: " + pawnsWithGateTravelerImplant.Count);

            // Now you can do whatever you need with that list:
            foreach (var pawn in pawnsWithGateTravelerImplant)
            {
                Log.Message($"{pawn.LabelShort} has a GateTravelerImplant.");
                var gateTravelImplant =
                    (GateTravelerImplant)pawn.health.hediffSet.hediffs.Find(
                        h => h.def.defName == "GateTravelerImplant");

                foreach (var relationship in gateTravelImplant.relationships)
                {
                    Log.Message($"Loading the relationship between {pawn.Name} and {relationship.pawnName}: {relationship.relationship}");
                    var pawn2 = PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive.FirstOrDefault(p =>
                        p.thingIDNumber == relationship.pawnID);

                    if (pawn2 == null)
                    {
                        pawn2 = Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(p => p.thingIDNumber == relationship.pawnID);
                        if (pawn2 == null)
                        {
                            Log.Warning("Generating missing relationship with " + relationship.pawnID + "...");
                            pawn2 = StargateBuffer.GenerateMissingRelationshipRecord(relationship.pawnID, relationship.pawnName, relationship.pawnGender);
                        }
                    }

                    Log.Warning("Pawn 1 (" + pawn.Name + ") with Pawn 2 (" + relationship.pawnName +
                                ") related: " + relationship.relationship);

                    PawnRelationDef pawnRelationDef =
                        DefDatabase<PawnRelationDef>.GetNamedSilentFail(relationship.relationship);

                    bool alreadyRelated = pawn.relations.DirectRelations
                        .Any(rel =>
                            rel.def == pawnRelationDef
                            && rel.otherPawn == pawn2);

                    if (alreadyRelated == false)
                    {
                        pawn.relations.AddDirectRelation(pawnRelationDef, pawn2);
                        pawn.ClearMind();
                    }
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

        private void AttachGateTravelerImplant(Pawn pawn)
        {
            HediffDef gateTravelerImplant = HediffDef.Named("GateTravelerImplant");

            BodyPartRecord brain = pawn.RaceProps.body.AllParts.Find(bpr => bpr.def.defName == "Brain");

            pawn.health.AddHediff(gateTravelerImplant, brain);
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

        public bool isOffworldTeleportEvent()
        {
            return System.IO.File.Exists(this.StargateBufferFilePath);
        }

        public Tuple<int, List<Thing>> receiveIncomingStream()
        {
            var inboundBuffer = new List<Thing>();
            int originalTimelineTicks;

            // Load off-world teams only if there isn't a local teleportation taking place.
            // bool offworldEvent = this.stargateBuffer.Count == 0;
            // bool offworldEvent = inboundBuffer.Any();
            bool offworldEvent = this.isOffworldTeleportEvent();
            Log.Warning("Is offworldEvent? " + offworldEvent);
            Log.Warning("Inbound Buffer Count? " + inboundBuffer.Count);

            if (!offworldEvent)
            {
                Messages.Message("No incoming wormhole detected.", MessageTypeDefOf.RejectInput);

                return null;
            }

            var loadResponse = Enhanced_Development.Stargate.Saving.SaveThings.load(ref inboundBuffer, this.StargateBufferFilePath);
            originalTimelineTicks = loadResponse.Item1;

            foreach (Pawn pawn in inboundBuffer.OfType<Pawn>())
            {
                // Clear any existing world pawn record for this pawn.
                ClearExistingWorldPawn(pawn);
            }

            // Log.Warning("Number of items in the wormhole: " + inboundBuffer.Count);

            return new Tuple<int, List<Thing>>(originalTimelineTicks, inboundBuffer);
        }
    }
}
