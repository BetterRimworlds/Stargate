using System;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace BetterRimworlds.Stargate.Saving
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
            int originalTimelineTicks = 0;
            // Mid-colony deep load runs while ProgramState is Playing. On RimWorld 1.4+
            // (Biotech life stages, still present in 1.6), PostLoadInit clears the age
            // cache and RecalculateLifeStageIndex then fires Notify_LifeStageStarted with
            // a null previous stage. LifeStageWorker_HumanlikeAdult only skips its live
            // transition logic when state is not Playing; under Playing it can NRE or
            // rewrite body type/backstory as if the pawn just aged up. MapInitializing
            // matches normal map load and is safe on 1.2 as well (enum has always existed;
            // 1.2 has no humanlike life-stage workers, so this is a no-op there).
            ProgramState previousProgramState = Current.ProgramState;
            try
            {
                Current.ProgramState = ProgramState.MapInitializing;

                Log.Message("ScribeINIT, loading from:" + fileLocation);
                Scribe.loader.InitLoading(fileLocation);

                Log.Message("DeepProfiler.Start()");
                DeepProfiler.Start("Load non-compressed things");

                Scribe_Values.Look<int>(ref originalTimelineTicks, "originalTimelineTicks");

                Log.Message("Scribe_Collections.LookList");
                Scribe_Collections.Look<Thing>(ref thingsToLoad, "things", LookMode.Deep);

                DeepProfiler.End();

                // CRITICAL: use the loader's own crossRefs + post-load initer.
                // Creating empty CrossRefHandler/PostLoadIniter instances (the old path)
                // skipped GateTravelerImplant research rebuild and other PostLoadInit work,
                // so carried research never reappeared after wormhole arrival.
                Log.Message("FinalizeLoading (cross-refs + post-load inits)");
                Scribe.loader.FinalizeLoading();
            }
            finally
            {
                Current.ProgramState = previousProgramState;
            }

            // Origin-world hediff loadIDs must not enter the destination world's
            // UniqueIDsManager sequence. Remap them before rematerialization/save.
            StargateLoadIdRemapper.RemapImportedThingLoadIds(thingsToLoad);

            return new Tuple<int>(originalTimelineTicks);
        }
    }
}
