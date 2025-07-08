//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UnityEngine;
//using Verse;

//namespace Enhanced_Development.Stargate.Saving.Dialog
//{
//    public class Dialog_SavePreset : Dialog_Preset
//    {
//        protected const float NewPresetNameButtonSpace = 20f;
//        protected const float NewPresetHeight = 35f;
//        protected const float NewPresetNameWidth = 400f;
//        private bool focusedPresetNameArea;

//        public Dialog_SavePreset()
//        {
//            this.interactButLabel = Translator.Translate("OverwriteButton");
//            this.bottomAreaHeight = 85f;
//            if (!"".Equals(Loadout.Instance.Filename))
//                return;
//            Loadout.Instance.Filename = PresetFiles.UnusedDefaultName();
//        }

//        protected override void DoMapEntryInteraction(string MapName)
//        {
//            Loadout.Instance.Filename = MapName;
//            PresetSaver.SaveToFile(Loadout.Instance, Loadout.Instance.Filename);
//            Messages.Message(Translator.Translate("SavedAs", (object)Loadout.Instance.Filename));
//            this.Close();
//        }

//        protected override void DoSpecialSaveLoadGUI(Rect inRect)
//        {
//            GUI.BeginGroup(inRect);
//            bool flag = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
//            float top = inRect.height - 52f;
//            Text.Font = GameFont.Small;
//            Text.Alignment = TextAnchor.MiddleLeft;
//            GUI.SetNextControlName("PresetNameField");
//            string str = Widgets.TextField(new Rect(5f, top, 400f, 35f), Loadout.Instance.Filename);
//            if (GenText.IsValidFilename(str))
//                Loadout.Instance.Filename = str;
//            if (!this.focusedPresetNameArea)
//            {
//                GUI.FocusControl("PresetNameField");
//                this.focusedPresetNameArea = true;
//            }
//            if (Widgets.TextButton(new Rect(420f, top, (float)((double)inRect.width - 400.0 - 20.0), 35f), Translator.Translate("EdB.SavePresetButton")) || flag)
//            {
//                if (Loadout.Instance.Filename.Length == 0)
//                {
//                    Messages.Message(Translator.Translate("NeedAName"), MessageSound.Reject);
//                }
//                else
//                {
//                    PresetSaver.SaveToFile(Loadout.Instance, Loadout.Instance.Filename);
//                    Messages.Message(Translator.Translate("SavedAs", (object)Loadout.Instance.Filename));
//                    this.Close();
//                }
//            }
//            Text.Alignment = TextAnchor.UpperLeft;
//            GUI.EndGroup();
//        }
//    }
//}
