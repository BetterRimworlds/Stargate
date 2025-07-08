using System;
using System.Collections.Generic;
using Verse;
using System.Xml;
using BetterRimworlds.Stargate;

namespace Enhanced_Development.Stargate.Saving
{
    class SaveThings
    {
        public static void save(List<Thing> thingsToSave, string fileLocation)
        {
            Log.Message("Saving to: " + fileLocation);
            Scribe.saver.InitSaving(fileLocation, "Stargate");

            //Log.Message("Starting Save");
            //Save Pawn

            var sortedThingsToSave = new List<Thing>();

            foreach (var item in thingsToSave)
            {
                if (item is Pawn pawn)
                {
                    //pawn.Discard();
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
            if (root == null)
            {
                Log.Error("Root node is null in SaveThings.save");
                return;
            }

            XmlNodeList xpResetTimestampNodes = root.SelectNodes("//lastXpSinceMidnightResetTimestamp");
            if (xpResetTimestampNodes != null)
            {
                foreach (XmlNode xpResetTimestampNode in xpResetTimestampNodes)
                {
                    xpResetTimestampNode.InnerText = "-1";
                }
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
