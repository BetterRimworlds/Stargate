// ==== Source/Scenario/MapComponent_SealedFromSky.cs ====
using Verse;

namespace BetterRimworlds.Stargate;

public class MapComponent_SealedFromSky : MapComponent
{
    public bool isSealed = false;

    public MapComponent_SealedFromSky(Map map) : base(map) { }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref isSealed, "isSealed", false);
    }
}