﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Stargate
{
    /**
     * Taken from https://github.com/Rikiki123456789/Rimworld/blob/master/MiningCo.%20Spaceship/Spaceship/PlaceWorker_OnlyOneBuilding.cs
     */
    class PlaceWorker_OnlyOneBuilding : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null)
        {
            List<Thing> blueprints = map.listerThings.ThingsOfDef(checkingDef.blueprintDef);
            List<Thing> frames = map.listerThings.ThingsOfDef(checkingDef.frameDef);
            if (((blueprints != null) && (blueprints.Count > 0))
                || ((frames != null) && (frames.Count > 0))
                || map.listerBuildings.ColonistsHaveBuilding(ThingDef.Named(checkingDef.defName)))
            {
                return "You can only build one " + checkingDef.defName + " per map.";
            }
            return true;
        }
    }
}
