using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;
using RimWorld;

namespace BetterRimworlds.Stargate
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
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            this.currentMap = map;
            base.SpawnSetup(map, respawningAfterLoad);
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

        public void ActivateGate()
        {
            if (warned == 0)
            {
                Messages.Message("Warning!! Trans-dimentional construction will cause a huge psionic blast that will affect all normal biological life in the area!", MessageTypeDefOf.ThreatBig);
                warned += 1;
            }
            else if (warned == 1)
            {
                Messages.Message("Are you really certain you want to do that?", MessageTypeDefOf.ThreatBig);
                warned += 1;
            }
            else if (warned >= 2)
            {
                Messages.Message("BOOOOM!", MessageTypeDefOf.ThreatBig);

                this.Destroy(DestroyMode.Vanish);
                var stargate = (Building_TransdimensionalStargate)GenSpawn.Spawn(ThingDef.Named("TransdimensionalStargate"), this.Position, this.currentMap);
                stargate.SetFactionDirect(Faction.OfPlayer);
                this.currentMap.listerBuildings.Add(stargate);
                stargate.StargateRecall();

                foreach (Pawn pawn in Find.CurrentMap.mapPawns.AllPawns.ToList())
                {
                    if (pawn.RaceProps.FleshType != FleshTypeDefOf.Normal)
                    {
                        continue;
                    }

                    // this.AddPsionicShock(pawn);
                    (new PsionicBlast()).AddPsionicShock(pawn);
                }

                stargate.StargateRecall();
            }
        }

        #endregion


        #region Graphics-text
        public override string GetInspectString()
        {
            return "WARNING: Activating this Gate will cause a huge psionic blast affecting the entire area!";
        }

        #endregion
    }
}
