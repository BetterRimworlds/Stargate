using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace Enhanced_Development.Stargate
{

    [StaticConstructorOnStartup]
    class Building_OffWorldStargate : Building
    {

        #region Variables

        private static Texture2D UI_ACTIVATE_GATE;

        public int warned = 0;

        private Map currentMap;

        #endregion

        static Building_OffWorldStargate()
        {
            UI_ACTIVATE_GATE = ContentFinder<Texture2D>.Get("UI/nuke", true);
        }

        #region Override

        public override void SpawnSetup(Map map)
        {
            this.currentMap = map;
            base.SpawnSetup(map);
        }

        //Saving game
        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void TickRare()
        {
            base.TickRare();
        }

        #endregion

        #region Commands

        public override IEnumerable<Gizmo> GetGizmos()
        {
            //Add the stock Gizmoes
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }


            if (true)
            {
                Command_Action act = new Command_Action();
                //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
                act.action = () => this.ActivateGate();
                act.icon = UI_ACTIVATE_GATE;
                act.defaultLabel = "Activate Gate";
                act.defaultDesc = "Activate Gate";
                act.activateSound = SoundDef.Named("Click");
                //act.hotKey = KeyBindingDefOf.DesignatorDeconstruct;
                //act.groupKey = 689736;
                yield return act;
            }
        }

        /*
        public override IEnumerable<Command> GetCommands()
        {
            IList<Command> CommandList = new List<Command>();
            IEnumerable<Command> baseCommands = base.GetCommands();

            if (baseCommands != null)
            {
                CommandList = baseCommands.ToList();
            }

            if (true)
            {
                //Upgrading
                Command_Action command_Action_AddResources = new Command_Action();

                command_Action_AddResources.defaultLabel = "Activate Gate";

                command_Action_AddResources.icon = UI_ACTIVATE_GATE;
                command_Action_AddResources.defaultDesc = "Activate Gate";

                command_Action_AddResources.activateSound = SoundDef.Named("Click");
                command_Action_AddResources.action = new Action(this.ActivateGate);

                CommandList.Add(command_Action_AddResources);
            }

            return CommandList.AsEnumerable<Command>();
        }
        */
        public void ActivateGate()
        {
            if (warned == 0)
            {
                Messages.Message("Warning!! Trans-dimentional construction will cause a huge psionic blast that will affect all normal biological life in the area!", MessageSound.SeriousAlert);
                warned += 1;
            }
            else if (warned == 1)
            {
                Messages.Message("Are you really certain you want to do that?", MessageSound.SeriousAlert);
                warned += 1;
            }
            else if (warned >= 2)
            {
                Messages.Message("Ok Fine.", MessageSound.SeriousAlert);

                this.Destroy(DestroyMode.Vanish);
                GenSpawn.Spawn(ThingDef.Named("Stargate"), this.Position, this.currentMap);

                foreach (Pawn pawn in Find.VisibleMap.mapPawns.AllPawns.ToList())
                {
                    if (pawn.def.race.fleshType != FleshType.Normal)
                    {
                        continue;
                    }

                    this.AddPsionicShock(pawn);
                }
            }
        }

        #endregion

        private void CauseHeartAttack(Pawn pawn)
        {
            HediffDef heartAttack = HediffDef.Named("HeartAttack");

            BodyPartRecord heart = pawn.RaceProps.body.AllParts.Find(bpr => bpr.def.defName == "Heart");
            Log.Message("Heart: " + heart.ToString());

            pawn.health.AddHediff(heartAttack, heart, null);
        }

        private void CauseSedation(Pawn pawn)
        {
            pawn.health.AddHediff(HediffDefOf.Anesthetic, null, null);
        }

        private void AddPsionicShock(Pawn pawn)
        {
            System.Random rand = new System.Random();

            int psychicSensitivity = 0;
            bool? shouldGiveHeartAttack = null;
            bool? shouldSedate = null;

            if (pawn.story?.traits != null && pawn.story.traits.HasTrait(TraitDef.Named("PsychicSensitivity")))
            {
                Trait psychicSensitivityTrait = pawn.story.traits.GetTrait(TraitDef.Named("PsychicSensitivity"));
                psychicSensitivity = psychicSensitivityTrait.Degree;
            }

            // If they're Psychically Deaf, do nothing:
            if (psychicSensitivity == -2)
            {
                return;
            }
            // If they're Psychically Dull, don't give them a heart attack.
            else if (psychicSensitivity == -1)
            {
                shouldGiveHeartAttack = false;
            }
            // If they're Psychically Sensitive, make sure they're passed out for a few hours.
            else if (psychicSensitivity == 1)
            {
                shouldSedate = true;
            }
            // If they're Psychically Hypersensitive, unfortunately, it will mean instant death :-(
            else if (psychicSensitivity >= 2)
            {
                Messages.Message(pawn.NameStringShort + " was psychically supersensitive and died because of the psionic blast.", MessageSound.SeriousAlert);
                HealthUtility.GiveInjuriesToKill(pawn);
            }

            Hediff shock = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn, null);
            pawn.health.AddHediff(shock, null, null);

            if (shouldGiveHeartAttack == null)
            {
                shouldGiveHeartAttack = rand.Next(1, 11) >= 3;
            }

            if (shouldGiveHeartAttack == true)
            {
                this.CauseHeartAttack(pawn);
            }

            if (shouldSedate == null)
            {
                int likelihood = rand.Next(1, 11);
                Log.Message(pawn.NameStringShort + " should sedate? " + likelihood);
                shouldSedate = likelihood >= 6;
            }

            if (shouldSedate == true)
            {
                this.CauseSedation(pawn);
            }

            DamageInfo psionicIntensity = new DamageInfo(DamageDefOf.Stun, 50);
            pawn.TakeDamage(psionicIntensity);
        }

        #region Graphics-text
        public override string GetInspectString()
        {

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("WARNING: Activating this Gate will cause most Creatures on the map to have a heart attack.");

            return stringBuilder.ToString();
        }

        #endregion


    }
}
