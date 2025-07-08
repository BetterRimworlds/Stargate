//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using Verse;

//namespace Enhanced_Development.Stargate.Saving.Dialog
//{
//    public class Dialog_LoadPreset : Dialog_Preset
//    {
//        public Dialog_LoadPreset()
//        {
//            this.interactButLabel = Translator.Translate("EdB.LoadPresetButton");
//        }

//        protected override void DoMapEntryInteraction(string presetName)
//        {
//            if (PresetLoader.LoadFromFile(Loadout.Instance, presetName))
//                Messages.Message(Translator.Translate("EdB.LoadedPreset", (object)presetName));
//            this.RemovePageFromStack();
//            this.Close();
//        }

//        protected void RemovePageFromStack()
//        {
//            Layer dialog = (Layer)Find.LayerStack.FirstLayerOfType<Page_CharMakerCarefully>() ?? (Layer)Find.LayerStack.FirstLayerOfType<Page_Equipment>();
//            if (dialog == null)
//                return;
//            Find.LayerStack.Remove(dialog);
//        }
//    }
//}
