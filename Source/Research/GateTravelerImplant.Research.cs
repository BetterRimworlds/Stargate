// ==== Source/Research/GateTravelerImplant.Research.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

public partial class GateTravelerImplant
{
    public const float MaxStoredResearchPoints = 20000f;

    // Pawn-carried research memory.
    //
    // Key:   ResearchProjectDef.defName
    // Value: Research points personally encoded into this pawn's implant.
    //
    // This is intentionally per-pawn and per-implant. It does not represent
    // the colony's global research progress. It represents what this pawn
    // personally carried through the Stargate.
    public Dictionary<string, float> Research = new Dictionary<string, float>();

    public void ExposeResearchData()
    {
        Scribe_Collections.Look(ref this.Research, "research", LookMode.Value, LookMode.Value);
        this.Research ??= new Dictionary<string, float>();
    }

    public void EnsureResearchMemoryInitialized()
    {
        this.Research ??= new Dictionary<string, float>();

        this.TrimResearchMemoryToCapacity();
    }

    public void AddResearchMemory(ResearchProjectDef project, float points)
    {
        if (project == null)
        {
            return;
        }

        this.AddResearchMemory(project.defName, points);
    }

    public void AddResearchMemory(string researchProjectDefName, float points)
    {
        if (researchProjectDefName.NullOrEmpty())
        {
            return;
        }

        if (points <= 0f)
        {
            return;
        }

        this.EnsureResearchMemoryInitialized();

        float remainingCapacity = MaxStoredResearchPoints - this.GetTotalResearchMemoryPoints();

        if (remainingCapacity <= 0f)
        {
            return;
        }

        points = System.Math.Min(points, remainingCapacity);

        if (!this.Research.ContainsKey(researchProjectDefName))
        {
            this.Research[researchProjectDefName] = 0f;
        }

        this.Research[researchProjectDefName] += points;
    }

    public float GetTotalResearchMemoryPoints()
    {
        this.EnsureResearchMemoryInitialized();

        if (this.Research == null)
        {
            return 0f;
        }

        float total = 0f;

        foreach (float points in this.Research.Values)
        {
            if (points > 0f)
            {
                total += points;
            }
        }

        return total;
    }

    public float GetResearchMemoryPoints(ResearchProjectDef project)
    {
        if (project == null)
        {
            return 0f;
        }

        return this.GetResearchMemoryPoints(project.defName);
    }

    public float GetResearchMemoryPoints(string researchProjectDefName)
    {
        if (researchProjectDefName.NullOrEmpty())
        {
            return 0f;
        }

        this.EnsureResearchMemoryInitialized();

        if (this.Research == null)
        {
            return 0f;
        }

        return this.Research.TryGetValue(researchProjectDefName, out float points)
            ? points
            : 0f;
    }

    public bool HasCompleteResearchMemory(ResearchProjectDef project)
    {
        if (project == null)
        {
            return false;
        }

        return this.GetResearchMemoryPoints(project) >= project.baseCost;
    }

    public bool CanReconstructResearchNow(ResearchProjectDef project)
    {
        if (project == null)
        {
            return false;
        }

        if (project.IsFinished)
        {
            return false;
        }

        // Important:
        // This preserves the local colony's tech tree progression.
        //
        // If Theo carries 100% of GeothermalPower but the destination colony
        // has not finished the prerequisites, the implant does nothing yet.
        //
        // Once the destination colony can legitimately start the project,
        // the pawn can reconstruct the carried research at a research bench.
        if (!project.CanStartNow)
        {
            return false;
        }

        return this.HasCompleteResearchMemory(project);
    }

    public bool TryReconstructResearch(ResearchProjectDef project)
    {
        if (!this.CanReconstructResearchNow(project))
        {
            return false;
        }

        if (Find.ResearchManager == null)
        {
            return false;
        }

        Find.ResearchManager.FinishProject(project, doCompletionDialog: false);

        string pawnName = this.pawn?.LabelShortCap ?? "A Gate Traveler";

        Find.LetterStack.ReceiveLetter(
            "Research Reconstructed",
            $"{pawnName}'s Gate Traveler implant contained a complete cognitive imprint of {project.LabelCap}. After interfacing with the research bench, the colony reconstructed the technology.",
            LetterDefOf.PositiveEvent,
            this.pawn
        );

        return true;
    }

    public bool TryReconstructAnyAvailableResearch()
    {
        this.EnsureResearchMemoryInitialized();

        if (this.Research == null)
        {
            return false;
        }

        if (this.Research.Count == 0)
        {
            return false;
        }

        List<string> researchDefNames = this.Research.Keys.ToList();

        foreach (string researchDefName in researchDefNames)
        {
            ResearchProjectDef project =
                DefDatabase<ResearchProjectDef>.GetNamedSilentFail(researchDefName);

            if (project == null)
            {
                continue;
            }

            if (this.TryReconstructResearch(project))
            {
                return true;
            }
        }

        return false;
    }

    public void ConsumeResearchMemory(ResearchProjectDef project, float points)
    {
        if (project == null)
        {
            return;
        }

        this.ConsumeResearchMemory(project.defName, points);
    }

    public void ConsumeResearchMemory(string researchProjectDefName, float points)
    {
        if (researchProjectDefName.NullOrEmpty())
        {
            return;
        }

        if (points <= 0f)
        {
            return;
        }

        if (this.Research == null)
        {
            return;
        }

        if (!this.Research.ContainsKey(researchProjectDefName))
        {
            return;
        }

        this.Research[researchProjectDefName] -= points;

        if (this.Research[researchProjectDefName] <= 0.001f)
        {
            this.Research.Remove(researchProjectDefName);
        }
    }

    public void RemoveResearchMemory(string researchProjectDefName)
    {
        if (researchProjectDefName.NullOrEmpty())
        {
            return;
        }

        this.EnsureResearchMemoryInitialized();
        this.Research?.Remove(researchProjectDefName);
    }

    private void TrimResearchMemoryToCapacity()
    {
        if (this.Research == null)
        {
            return;
        }

        foreach (string defName in this.Research
                     .Where(entry => entry.Key.NullOrEmpty() || entry.Value <= 0f)
                     .Select(entry => entry.Key)
                     .ToList())
        {
            this.Research.Remove(defName);
        }

        float total = this.Research.Values.Sum();

        if (total <= MaxStoredResearchPoints)
        {
            return;
        }

        float excess = total - MaxStoredResearchPoints;

        // Evict the least valuable memories first, based on stored points.
        // DefName is only a deterministic tie-breaker; key ordering must not
        // decide which research is discarded when point totals differ.
        foreach (string defName in this.Research
                     .OrderBy(entry => entry.Value)
                     .ThenBy(entry => entry.Key, System.StringComparer.Ordinal)
                     .Select(entry => entry.Key)
                     .ToList())
        {
            if (excess <= 0f)
            {
                break;
            }

            float pointsToRemove = System.Math.Min(this.Research[defName], excess);
            this.Research[defName] -= pointsToRemove;
            excess -= pointsToRemove;

            if (this.Research[defName] <= 0.001f)
            {
                this.Research.Remove(defName);
            }
        }
    }
}
