// ==== Source/Research/GateTravelerResearchPatches.cs ====
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

/// Captures pawn-authored research into the Gate Traveler implant and
/// reconstructs carried research when a Gate Traveler actively uses a
/// research bench.
///
/// Important model:
///
///   - RimWorld research progress is colony-global.
///   - ResearchPerformed(amount, researcher) gives us pawn authorship.
///   - The GateTravelerImplant stores only the pawn-carried cognitive imprint.
///   - Reconstruction only happens when the pawn performs real research.
///   - Prerequisites are enforced inside GateTravelerImplant.CanReconstructResearchNow().
///
/// This patch does NOT create Gate Traveler implants.
/// Ordinary colonists are ignored unless they already have the implant.
[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ResearchPerformed))]
public static class GateTravelerResearchPatches
{
    /// Fraction of personally produced research points copied into the Gate
    /// Traveler implant.
    ///
    /// ResearchPerformed fires every research tick, so a per-call random
    /// fraction between a min and max collapses to the midpoint by the law of
    /// large numbers (e.g. 5–40% always averaged to ~22.5%). Use a fixed
    /// lossy rate instead.
    ///
    /// Example:
    ///   If Theo produces 10 research points, his implant stores 2.0 points.
    private const float StoredResearchFraction = 0.20f;

    /// Enable when debugging research-memory capture/reconstruction.
    /// Leave false for normal gameplay because ResearchPerformed fires often.
    // private const bool DebugResearchMemory = false;

    public static void Postfix(float amount, Pawn researcher)
    {
        if (amount <= 0f)
        {
            return;
        }

        if (researcher == null)
        {
            return;
        }

        if (!researcher.IsColonist)
        {
            return;
        }

        GateTravelerImplant implant = GetExistingGateTravelerImplant(researcher);

        if (implant == null)
        {
            return;
        }

        #if RIMWORLD15 || RIMWORLD16
        ResearchProjectDef currentProject = Find.ResearchManager.GetProject();
        #else
        ResearchProjectDef currentProject = Find.ResearchManager.currentProj;
        #endif

        if (currentProject != null)
        {
            float storedPoints = GetStoredResearchPoints(amount);

            if (storedPoints > 0f)
            {
                implant.AddResearchMemory(currentProject, storedPoints);

                // if (DebugResearchMemory)
                // {
                //     Log.Warning(
                //         "[Stargate] Gate Traveler research memory captured: " +
                //         $"{researcher.LabelShortCap} researched {amount:0.###} points toward " +
                //         $"{currentProject.defName}; implant stored {storedPoints:0.###} points."
                //     );
                // }
            }
        }

        // This is the gameplay trigger:
        //
        // A pawn does not automatically teach the colony when arriving through
        // the Stargate. The pawn must sit down and perform real research.
        //
        // GateTravelerImplant.TryReconstructAnyAvailableResearch() handles:
        //   - skipped missing/unknown defs
        //   - skipped already-finished projects
        //   - prerequisite checks via project.CanStartNow
        //   - complete-memory check against project.baseCost
        //   - FinishProject()
        //   - letter notification
        // Research memory is preserved so the implant can carry fully learned
        // tech across future worlds.
        bool reconstructedResearch = implant.TryReconstructAnyAvailableResearch();

        // if (reconstructedResearch && DebugResearchMemory)
        // {
        //     Log.Warning(
        //         "[Stargate] Gate Traveler research memory reconstructed by " +
        //         $"{researcher.LabelShortCap}."
        //     );
        // }
    }

    private static float GetStoredResearchPoints(float researchedAmount)
    {
        if (researchedAmount <= 0f)
        {
            return 0f;
        }

        return researchedAmount * StoredResearchFraction;
    }

    private static GateTravelerImplant GetExistingGateTravelerImplant(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
        {
            return null;
        }

        if (StargateHediffDefOf.GateTravelerImplant == null)
        {
            return null;
        }

        return pawn.health.hediffSet.GetFirstHediffOfDef(
            StargateHediffDefOf.GateTravelerImplant
        ) as GateTravelerImplant;
    }
}
