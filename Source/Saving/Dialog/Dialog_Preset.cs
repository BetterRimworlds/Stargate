//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using Verse;
//using UnityEngine;
//using System.IO;

//namespace Enhanced_Development.Stargate.Saving.Dialog
//{
//    public abstract class Dialog_Preset : Layer
//    {
//        private static readonly Color ManualSaveTextColor = new Color(1f, 1f, 0.6f);
//        private static readonly Color AutosaveTextColor = new Color(0.75f, 0.75f, 0.75f);
//        public static readonly Texture2D DeleteX = ContentFinder<Texture2D>.Get("UI/Buttons/Delete", true);
//        private Vector2 scrollPosition = Vector2.zero;
//        protected string interactButLabel = "Error";
//        protected const float DeleteButtonSpace = 5f;
//        protected const float MapDateExtraLeftMargin = 220f;
//        protected const float MapEntrySpacing = 8f;
//        protected const float BoxMargin = 20f;
//        protected const float MapNameExtraLeftMargin = 15f;
//        protected const float MapEntryMargin = 6f;
//        protected float bottomAreaHeight;

//        public Dialog_Preset()
//        {
//            this.SetCentered(600f, 700f);
//            this.category = LayerCategory.GameDialog;
//            this.closeOnEscapeKey = true;
//            this.doCloseButton = true;
//            this.doCloseX = true;
//            this.absorbAllInput = true;
//            this.forcePause = true;
//        }

//        protected abstract void DoMapEntryInteraction(string mapName);

//        protected virtual void DoSpecialSaveLoadGUI(Rect inRect)
//        {
//        }

//        protected override void OnPreClose()
//        {
//            Page_CharMakerCarefully charMakerCarefully = Find.LayerStack.FirstLayerOfType<Page_CharMakerCarefully>();
//            if (charMakerCarefully != null)
//            {
//                charMakerCarefully.Show();
//            }
//            else
//            {
//                Page_Equipment pageEquipment = Find.LayerStack.FirstLayerOfType<Page_Equipment>();
//                if (pageEquipment != null)
//                    pageEquipment.Show();
//                else
//                    Find.LayerStack.Add((Layer)new Page_CharMakerCarefully(false));
//            }
//            GUI.FocusControl((string)null);
//        }

//        protected override void FillWindow(Rect inRect)
//        {
//            Vector2 vector2_1 = new Vector2(inRect.width - 16f, 48f);
//            Vector2 vector2_2 = new Vector2(100f, vector2_1.y - 12f);
//            inRect.height -= 45f;
//            List<FileInfo> list = Enumerable.ToList<FileInfo>(PresetFiles.AllFiles);
//            float num = vector2_1.y + 8f;
//            float height = (float)list.Count * num;
//            Rect viewRect = new Rect(0.0f, 0.0f, inRect.width - 16f, height);
//            Rect outRect = new Rect(GenUI.AtZero(inRect));
//            outRect.height -= this.bottomAreaHeight;
//            this.scrollPosition = Widgets.BeginScrollView(outRect, this.scrollPosition, viewRect);
//            float top = 0.0f;
//            foreach (FileInfo fileInfo in list)
//            {
//                Rect rect1 = new Rect(0.0f, top, vector2_1.x, vector2_1.y);
//                Widgets.DrawMenuSection(rect1);
//                Rect innerRect = GenUI.GetInnerRect(rect1, 6f);
//                GUI.BeginGroup(innerRect);
//                string withoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
//                GUI.color = Dialog_Preset.ManualSaveTextColor;
//                Rect rect2 = new Rect(15f, 0.0f, innerRect.width, innerRect.height);
//                Text.Alignment = TextAnchor.MiddleLeft;
//                Text.Font = GameFont.Small;
//                Widgets.Label(rect2, withoutExtension);
//                GUI.color = Color.white;
//                Rect rect3 = new Rect(220f, 0.0f, innerRect.width, innerRect.height);
//                Text.Font = GameFont.Tiny;
//                GUI.color = new Color(1f, 1f, 1f, 0.5f);
//                Widgets.Label(rect3, fileInfo.LastWriteTime.ToString());
//                GUI.color = Color.white;
//                Text.Alignment = TextAnchor.UpperLeft;
//                Text.Font = GameFont.Small;
//                float left = vector2_1.x - 12f - vector2_2.x - vector2_2.y;
//                if (Widgets.TextButton(new Rect(left, 0.0f, vector2_2.x, vector2_2.y), this.interactButLabel))
//                    this.DoMapEntryInteraction(Path.GetFileNameWithoutExtension(fileInfo.Name));
//                Rect rect4 = new Rect((float)((double)left + (double)vector2_2.x + 5.0), 0.0f, vector2_2.y, vector2_2.y);
//                if (Widgets.ImageButton(rect4, Dialog_Preset.DeleteX))
//                {
//                    FileInfo localFile = fileInfo;
//                    Find.UIRoot.layers.Add((Layer)new Dialog_Confirm(Translator.Translate("EdB.ConfirmPresetDelete", (object)localFile.Name), (Action)(() => localFile.Delete()), true));
//                }
//                TooltipHandler.TipRegion(rect4, (TipSignal)Translator.Translate("EdB.DeleteThisPreset"));
//                GUI.EndGroup();
//                top += vector2_1.y + 8f;
//            }
//            Widgets.EndScrollView();
//            this.DoSpecialSaveLoadGUI(GenUI.AtZero(inRect));
//        }
//    }
//}
