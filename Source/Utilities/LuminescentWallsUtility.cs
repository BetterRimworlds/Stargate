// ==== Source/Utilities/LuminescentWallsUtility.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

/// Soft lookup for luminescent walls from HopeSeekr.BetterRimworlds.LuminescentWalls.
/// Defs are resolved by name so Stargate does not reference that mod's assembly.
/// Callers already fall back to vanilla walls and standing lamps when the def is missing.
internal static class LuminescentWallsUtility
{
    internal static ThingDef GetWallDef()
    {
        return DefDatabase<ThingDef>.GetNamedSilentFail("BR_LuminescentLimestoneWall");
    }

    internal static bool IsAnyWall(ThingDef def)
    {
        if (def == null)
        {
            return false;
        }

        if (def == ThingDefOf.Wall)
        {
            return true;
        }

        if (def.defName != null
            && def.defName.StartsWith("BR_Luminescent")
            && def.defName.EndsWith("Wall"))
        {
            return true;
        }

        return def.building?.isNaturalRock == true;
    }
}
