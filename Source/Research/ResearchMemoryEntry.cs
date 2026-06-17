// ==== Source/Research/ResearchMemoryEntry.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

/// A single research memory entry for serialization.
/// Wraps a research project defName and its accumulated points.
public class ResearchMemoryEntry : IExposable
{
    public string defName;
    public float points;

    // Required for Scribe
    public ResearchMemoryEntry()
    {
    }

    public ResearchMemoryEntry(string defName, float points)
    {
        this.defName = defName;
        this.points = points;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref this.defName, "defName");
        Scribe_Values.Look(ref this.points, "points");
    }
}