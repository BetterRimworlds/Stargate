﻿using System;
using System.Collections.Generic;
using Verse;
using System.Xml;
using BetterRimworlds.Stargate;

namespace Enhanced_Development.Stargate.Saving
{
    public class StargateRelations: List<StargateRelation>
    {
        private List<StargateRelation> relationships = new List<StargateRelation>();

        /** ThingID1, ThingID2 **/
        private List<Tuple<int, int>> pawnPairs = new List<Tuple<int, int>>();

        public new void Add(StargateRelation item)
        {
            // Only add a relationship if it doesn't exist.
            if (this.ContainsRelationship(item.pawn1ID, item.pawn2ID) == true)
            {
                return;
            }

            var tuple = new Tuple<int, int>(item.pawn1ID, item.pawn2ID);
            this.pawnPairs.Add(tuple);

            this.relationships.Add(item);
            Log.Message($"Recorded the relationship between {item.pawn1ID} and {item.pawn2ID}: {item.relationship}");
        }

        public new void Clear()
        {
            this.relationships.Clear();
        }

        public new bool Contains(StargateRelation item)
        {
            return this.relationships.Contains(item);
        }

        public new bool Remove(StargateRelation item)
        {
            // Remove the pairs.
            this.pawnPairs.RemoveAll( p => p.Item1 == item.pawn1ID);
            this.pawnPairs.RemoveAll( p => p.Item2 == item.pawn1ID);


            return this.relationships.Remove(item);
        }

        public new int Count => this.relationships.Count;
        public new int IndexOf(StargateRelation item)
        {
            return this.relationships.IndexOf(item);
        }

        public new void Insert(int index, StargateRelation item)
        {
            // Only add a relationship if it doesn't exist.
            if (this.ContainsRelationship(item.pawn1ID, item.pawn2ID) == true)
            {
                return;
            }

            var tuple = new Tuple<int, int>(item.pawn1ID, item.pawn2ID);
            this.pawnPairs.Add(tuple);

            this.relationships.Insert(index, item);
        }

        public new void RemoveAt(int index)
        {
            this.relationships.RemoveAt(index);
        }

        public new StargateRelation this[int index]
        {
            get => this.relationships[index];
            set => this.relationships[index] = value;
        }

        public bool ContainsRelationship(int pawnID1, int pawnID2)
        {
            if (pawnPairs.Contains(new Tuple<int, int>(pawnID1, pawnID2)))
            {
                return true;
            }

            return pawnPairs.Contains(new Tuple<int, int>(pawnID2, pawnID1));
        }

        public List<StargateRelation> ToList()
        {
            return this.relationships;
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref relationships, "relationships");
            // Scribe_Values.Look<DirectPawnRelation>(ref relationship, "relationship", LookMode.Deep);
            // Scribe_Deep.Look(ref relationship, "relationship");


            // Scribe_Defs.Look(ref relationshipDef, "relationship");

        }

    }

    class SaveThings
    {
        public static void save(List<Thing> thingsToSave, string fileLocation)
        {
            Log.Message("Saving to: " + fileLocation);
            Scribe.saver.InitSaving(fileLocation, "Stargate");

            //Log.Message("Starting Save");
            //Save Pawn

            var loadedPawns = new List<Pawn>();
            var loadedPawnIds = new List<string>();

            var sortedThingsToSave = new List<Thing>();

            foreach (var item in thingsToSave)
            {
                if (item is Pawn pawn)
                {
                    loadedPawns.Add(pawn);
                    loadedPawnIds.Add(pawn.ThingID);
                    // pawn.Discard();
                    sortedThingsToSave.Insert(0, pawn);
                }
                else
                {
                    sortedThingsToSave.Add(item);
                }
            }

            int currentTimelineTicks = Current.Game.tickManager.TicksAbs;
            Scribe_Values.Look<int>(ref currentTimelineTicks, "originalTimelineTicks");
            // Log.Error(relationshipsList.ToString());
            Scribe_Collections.Look<Thing>(ref sortedThingsToSave, "things", LookMode.Deep, (object)null);

            //Scribe.ExitNode();

            //Scribe.ExitNode();

            /*
            for (int i = 0; i < thingsToSave.Count; i++)
            {
                Scribe_Deep.LookDeep<Thing>(ref thingsToSave[i], thingsToSave[i].ThingID);
            }*/

            Scribe.saver.FinalizeSaving();
            Scribe.mode = LoadSaveMode.Inactive;
            //Log.Message("End Save");

            // Edit the XML to tweak things that the Rimworld devs won't let us change via C#.
            XmlDocument doc = new XmlDocument();
            doc.Load(fileLocation);
            XmlNode root = doc.DocumentElement;
            XmlNodeList xpResetTimestampNodes = root.SelectNodes("//lastXpSinceMidnightResetTimestamp");
            foreach (XmlNode xpResetTimestampNode in xpResetTimestampNodes)
            {
                xpResetTimestampNode.InnerText = "-1";
            }

            doc.Save(fileLocation);
        }

        /**
         * @return int The absolute ticks from when the team was first dematerialized.
         */
        public static Tuple<int> load(ref List<Thing> thingsToLoad, string fileLocation)
        {
            Log.Message("ScribeINIT, loding from:" + fileLocation);
            Scribe.loader.InitLoading(fileLocation);

            //Scribe.EnterNode("Stargate");

            Log.Message("DeepProfiler.Start()");
            DeepProfiler.Start("Load non-compressed things");

            int originalTimelineTicks = 0;
            Scribe_Values.Look<int>(ref originalTimelineTicks, "originalTimelineTicks");

            Log.Message("Scribe_Collections.LookList");
            Scribe_Collections.Look<Thing>(ref thingsToLoad, "things", LookMode.Deep);

            DeepProfiler.End();

            Scribe.mode = LoadSaveMode.Inactive;

            //Log.Message("list: " + thingsToLoad.Count.ToString());


            Log.Message("Exit Node");
            //Scribe.ExitNode();


            Log.Message("ResolveAllCrossReferences");
            //CrossRefHandler
            var c = new CrossRefHandler();
            c.ResolveAllCrossReferences();

            Log.Message("DoAllPostLoadInits");
            var p = new PostLoadIniter();
            p.DoAllPostLoadInits();

            return new Tuple<int>(originalTimelineTicks);
        }
    }
}
