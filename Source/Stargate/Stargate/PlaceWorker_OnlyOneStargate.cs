﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Stargate
{
    class PlaceWorker_OnlyOneStargate : PlaceWorker_OnlyOneBuilding
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            //Messages.Message("Has transdimensional? " + map.listerBuildings.ColonistsHaveBuilding(ThingDef.Named("TransdimensionalStargate")), MessageTypeDefOf.NeutralEvent);
            List<Thing> blueprints = map.listerThings.ThingsOfDef(checkingDef.blueprintDef);
            List<Thing> frames = map.listerThings.ThingsOfDef(checkingDef.frameDef);
            if (
                ((blueprints != null) && (blueprints.Count > 0))
               || ((frames != null) && (frames.Count > 0))
               || map.listerBuildings.ColonistsHaveBuilding(ThingDef.Named(checkingDef.defName))
               || map.listerBuildings.ColonistsHaveBuilding(ThingDef.Named("Stargate"))
               || map.listerBuildings.ColonistsHaveBuilding(ThingDef.Named("TransdimensionalStargate"))
               )
            {
                return "You can only build one Off-world Stargate per map.";
            }

            return true;
        }
    }
}
