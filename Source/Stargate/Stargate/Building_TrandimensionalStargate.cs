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
    class Building_TransdimensionalStargate : Building_Stargate
    {
        /**
         * This is needed because Transdimensional Stargates are powered via the Zero-Point Field.
         */
        public override void TickRare()
        {
            base.BaseTickRare();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            Command_Action act = new Command_Action();
            //act.action = () => Designator_Deconstruct.DesignateDeconstruct(this);
            act.action = () => this.StargateRecall();
            act.icon = UI_GATE_IN;
            act.defaultLabel = "Recall";
            act.defaultDesc = "Recall";
            act.activateSound = SoundDef.Named("Click");
            act.hotKey = KeyBindingDefOf.Designator_Deconstruct;

            yield return act;
        }

        public override bool StargateRecall()
        {
            // If an Offworld Team was not successfully recalled, bail out now.
            if (!base.StargateRecall())
            {
                return false;
            }

            // If they were successfully recalled, destroy the gate.
            this.Destroy(DestroyMode.Vanish);
            Messages.Message("The Transdimensional Transfer was a success!", MessageTypeDefOf.PositiveEvent);

            return true;
        }

        public override string GetInspectString()
        {
            return "Waiting for incoming off-world activation.";
        }
    }
}
