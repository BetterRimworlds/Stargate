using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using System.Xml;

namespace Enhanced_Development.Stargate.Saving
{
    class SaveThings
    {
        public static void save(List<Thing> thingsToSave, string fileLocation, Thing source)
        {
            Log.Message("Saving to: " + fileLocation);
            Scribe.saver.InitSaving(fileLocation, "Stargate");

            //Log.Message("Starting Save");
            //Save Pawn

            //Scribe_Collections.LookList<Thing>(ref thingsToSave, "things", LookMode.Deep, (object)null);

            //Scribe.EnterNode("map");
            //Scribe.EnterNode("things");
            //source.ExposeData();
            Scribe_Collections.Look<Thing>(ref thingsToSave, "things", LookMode.Deep, (object)null);
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
        }

        public static void load(ref List<Thing> thingsToLoad, string fileLocation, Thing currentSource)
        {
            Log.Message("ScribeINIT, loading from:" + fileLocation);
            Scribe.loader.InitLoading(fileLocation);

            //Scribe.EnterNode("Stargate");

            // Reset all of the load IDs to avoid "Cannot register X (id=Y in loaded object directory. Id already used by X)
            var loadedObjectDirectory = new LoadedObjectDirectory();
            loadedObjectDirectory.Clear();

            Log.Message("DeepProfiler.Start()");
            DeepProfiler.Start("Load non-compressed things");

           // List<Thing> list2 = (List<Thing>)null;
            Log.Message("Scribe_Collections.LookList");
            Scribe_Collections.Look<Thing>(ref thingsToLoad, "things", LookMode.Deep);
            Log.Message("List1Count:" + thingsToLoad.Count);

            Log.Message("DeepProfiler.End()");
            DeepProfiler.End();

            //Scribe.ExitNode();
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

            Log.Message("Return");
        }
    }
}
