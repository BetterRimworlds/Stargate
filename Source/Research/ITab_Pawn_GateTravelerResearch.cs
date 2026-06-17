// ==== Source/Research/ITab_Pawn_GateTravelerResearch.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

public class ITab_Pawn_GateTravelerResearch : ITab
{
    private const float RowHeight = 28f;
    private const float HeaderHeight = 34f;
    private const float FooterHeight = 34f;

    private Vector2 scrollPosition = Vector2.zero;

    public ITab_Pawn_GateTravelerResearch()
    {
        this.size = new Vector2(520f, 420f);

        // this.labelKey = "🔬💡";
        this.labelKey = "R&D";
    }

    public override bool IsVisible
    {
        get
        {
            Pawn pawn = this.SelectedPawn;

            if (pawn == null)
            {
                return false;
            }

            return GetGateTravelerImplant(pawn) != null;
        }
    }

    private Pawn SelectedPawn
    {
        get
        {
            Thing selectedThing = this.SelThing;

            if (selectedThing is Pawn pawn)
            {
                return pawn;
            }

            if (selectedThing is Corpse corpse)
            {
                return corpse.InnerPawn;
            }

            return null;
        }
    }

    protected override void FillTab()
    {
        Rect rect = new Rect(
            0f,
            0f,
            this.size.x,
            this.size.y
        ).ContractedBy(12f);

        Pawn pawn = this.SelectedPawn;
        GateTravelerImplant implant = pawn == null
            ? null
            : GetGateTravelerImplant(pawn);

        if (pawn == null || implant == null)
        {
            Widgets.Label(
                rect,
                "No Gate Traveler research memory available."
            );

            return;
        }

        Text.Font = GameFont.Medium;

        Rect titleRect = new Rect(
            rect.x,
            rect.y,
            rect.width,
            HeaderHeight
        );

        float totalStoredPoints = implant.GetTotalResearchMemoryPoints();
        float storagePercent =
            totalStoredPoints / GateTravelerImplant.MaxStoredResearchPoints * 100f;

        Widgets.Label(
            titleRect,
            $"{pawn.LabelShortCap}'s R&D Ledger - Storage used: {storagePercent:0.00}%"
        );

        Text.Font = GameFont.Small;

        Rect bodyRect = new Rect(
            rect.x,
            titleRect.yMax + 6f,
            rect.width,
            rect.height - HeaderHeight - FooterHeight - 12f
        );

        DrawResearchMemory(bodyRect, implant);

        Rect footerRect = new Rect(
            rect.x,
            bodyRect.yMax + 6f,
            rect.width,
            FooterHeight
        );

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;

        Widgets.Label(
            footerRect,
            "Research memory is pawn-carried knowledge remembered in the Gate Traveler implant. " +
            "When a tech is learned 100% by a colonist, they will bring this knowledge with them to " +
            "other worlds."
        );

        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private void DrawResearchMemory(Rect rect, GateTravelerImplant implant)
    {
        implant.EnsureResearchMemoryInitialized();

        if (implant.Research.Count == 0)
        {
            Widgets.Label(
                rect,
                "This Gate Traveler implant contains no research memory in their metacortex."
            );

            return;
        }

        List<ResearchMemoryRow> rows = implant.Research
            .Where(entry => !entry.Key.NullOrEmpty() && entry.Value > 0f)
            .Select(entry => MakeResearchMemoryRow(entry.Key, entry.Value))
            .OrderByDescending(row => row.PercentComplete)
            .ThenBy(row => row.Label)
            .ToList();

        float viewHeight = rows.Count * RowHeight + 8f;

        Rect viewRect = new Rect(
            0f,
            0f,
            rect.width - 16f,
            viewHeight
        );

        Widgets.BeginScrollView(
            rect,
            ref this.scrollPosition,
            viewRect
        );

        float y = 0f;

        foreach (ResearchMemoryRow row in rows)
        {
            Rect rowRect = new Rect(
                0f,
                y,
                viewRect.width,
                RowHeight
            );

            DrawResearchMemoryRow(rowRect, row, implant);

            y += RowHeight;
        }

        Widgets.EndScrollView();
    }

    private static void DrawResearchMemoryRow(Rect rect, ResearchMemoryRow row, GateTravelerImplant implant)
    {
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        const float DeleteButtonSize = 22f;

        Rect deleteRect = new Rect(
            rect.xMax - DeleteButtonSize,
            rect.y + 3f,
            DeleteButtonSize,
            rect.height - 6f
        );

        Rect labelRect = new Rect(
            rect.x,
            rect.y,
            rect.width * 0.40f,
            rect.height
        );

        Rect pointsRect = new Rect(
            labelRect.xMax + 8f,
            rect.y,
            rect.width * 0.22f,
            rect.height
        );

        Rect statusRect = new Rect(
            pointsRect.xMax + 8f,
            rect.y,
            deleteRect.x - pointsRect.xMax - 16f,
            rect.height
        );

        bool prevWrap = Text.WordWrap;
        Text.WordWrap = false;

        Widgets.Label(labelRect, row.Label);
        Widgets.Label(pointsRect, $"{row.StoredPoints:0.#}/{row.BaseCost:0.#}");
        Widgets.Label(statusRect, row.StatusLabel);

        Text.WordWrap = prevWrap;

        TooltipHandler.TipRegion(
            rect,
            row.Tooltip
        );

        if (Widgets.ButtonText(deleteRect, "x"))
        {
            Find.WindowStack.Add(
                Dialog_MessageBox.CreateConfirmation(
                    $"Forget {row.Label} from this Gate Traveler implant?",
                    () => implant.RemoveResearchMemory(row.DefName)
                )
            );
        }

        TooltipHandler.TipRegion(
            deleteRect,
            $"Forget {row.Label}"
        );
    }

    private static ResearchMemoryRow MakeResearchMemoryRow(
        string researchProjectDefName,
        float storedPoints
    )
    {
        ResearchProjectDef project =
            DefDatabase<ResearchProjectDef>.GetNamedSilentFail(researchProjectDefName);

        if (project == null)
        {
            return new ResearchMemoryRow
            {
                DefName = researchProjectDefName,
                Label = researchProjectDefName,
                StoredPoints = storedPoints,
                BaseCost = storedPoints,
                PercentComplete = 1f,
                StatusLabel = "Unknown project",
                Tooltip = "This research memory references a ResearchProjectDef that does not exist in this save. " +
                          "This can happen when the origin save had a modded research project that the destination save does not."
            };
        }

        float baseCost = project.baseCost <= 0f
            ? 1f
            : project.baseCost;

        float percentComplete = storedPoints / baseCost;

        string statusLabel;

        if (project.IsFinished)
        {
            statusLabel = $"Mastered {percentComplete * 100f:0.#}%";
        }
        else if (!project.CanStartNow)
        {
            statusLabel = $"{percentComplete * 100f:0.#}% / prerequisites missing";
        }
        else if (storedPoints >= project.baseCost)
        {
            statusLabel = "Ready to reconstruct";
        }
        else
        {
            statusLabel = $"{percentComplete * 100f:0.#}% in metacortex";
        }

        return new ResearchMemoryRow
        {
            DefName = researchProjectDefName,
            Label = project.LabelCap,
            StoredPoints = storedPoints,
            BaseCost = project.baseCost,
            PercentComplete = percentComplete,
            StatusLabel = statusLabel,
            Tooltip =
                $"Project: {project.defName}\n" +
                $"Stored points: {storedPoints:0.###}\n" +
                $"Required points: {project.baseCost:0.###}\n" +
                $"Stored: {percentComplete * 100f:0.##}%\n" +
                $"Can start locally: {(project.CanStartNow ? "Yes" : "No")}\n" +
                $"Already finished: {(project.IsFinished ? "Yes" : "No")}"
        };
    }

    private static GateTravelerImplant GetGateTravelerImplant(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return null;
        }

        return pawn.health.hediffSet.hediffs
            .OfType<GateTravelerImplant>()
            .FirstOrDefault();
    }

    private sealed class ResearchMemoryRow
    {
        public string DefName;
        public string Label;
        public float StoredPoints;
        public float BaseCost;
        public float PercentComplete;
        public string StatusLabel;
        public string Tooltip;
    }
}
