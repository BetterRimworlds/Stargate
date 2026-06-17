// ==== Source/Research/Dialog_ResearchMemory.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

public class Dialog_ResearchMemory : Window
{
    private readonly Pawn pawn;
    private readonly GateTravelerImplant implant;

    private Vector2 scrollPosition = Vector2.zero;
    private const float RowHeight = 28f;
    private const float HeaderHeight = 40f;
    private const float FooterHeight = 30f;

    public override Vector2 InitialSize => new Vector2(560f, 480f);

    public Dialog_ResearchMemory(Pawn pawn, GateTravelerImplant implant)
    {
        this.pawn = pawn;
        this.implant = implant;
        this.doCloseButton = true;
        this.closeOnClickedOutside = true;
        this.absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Title
        Text.Font = GameFont.Medium;
        GUI.color = new Color(0.4f, 0.8f, 1f);

        Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight);
        float totalStoredPoints = implant.GetTotalResearchMemoryPoints();
        float storagePercent =
            totalStoredPoints / GateTravelerImplant.MaxStoredResearchPoints * 100f;
        Widgets.Label(
            titleRect,
            $"🔬 {pawn.LabelShortCap}'s R&D Ledger - Storage used: {storagePercent:0.00}%"
        );

        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        // Separator
        Widgets.DrawLineHorizontal(inRect.x, titleRect.yMax, inRect.width);

        // Main content area
        Rect contentRect = new Rect(
            inRect.x,
            titleRect.yMax + 8f,
            inRect.width,
            inRect.height - HeaderHeight - FooterHeight - 16f
        );

        DrawResearchContent(contentRect);

        // Footer note
        Rect footerRect = new Rect(
            inRect.x,
            contentRect.yMax + 8f,
            inRect.width,
            FooterHeight
        );

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(footerRect,
            "Research memory persists across worlds when learned 100%.");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private void DrawResearchContent(Rect rect)
    {
        implant.EnsureResearchMemoryInitialized();

        if (implant.Research.Count == 0)
        {
            GUI.color = Color.gray;
            Widgets.Label(rect, "No research stored in metacortex.");
            GUI.color = Color.white;
            return;
        }

        var rows = implant.Research
            .Where(e => !e.Key.NullOrEmpty() && e.Value > 0f)
            .Select(e => CreateRow(e.Key, e.Value))
            .OrderByDescending(r => r.PercentComplete)
            .ThenBy(r => r.Label)
            .ToList();

        float viewHeight = rows.Count * RowHeight;
        Rect viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);

        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float y = 0f;
        foreach (var row in rows)
        {
            DrawRow(new Rect(0f, y, viewRect.width, RowHeight), row);
            y += RowHeight;
        }

        Widgets.EndScrollView();
    }

    private void DrawRow(Rect rect, ResearchRow row)
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

        // Project name
        Rect nameRect = new Rect(rect.x + 4f, rect.y, rect.width * 0.42f, rect.height);
        Text.Font = GameFont.Small;
        Widgets.Label(nameRect, row.Label.Truncate(nameRect.width));

        // Progress bar
        Rect barRect = new Rect(
            nameRect.xMax + 8f,
            rect.y + 6f,
            rect.width * 0.32f,
            rect.height - 12f
        );

        Widgets.FillableBar(barRect, row.PercentComplete, row.BarTexture,
            BaseContent.GreyTex, false);

        // Percent text
        Rect pctRect = new Rect(barRect.xMax + 6f, rect.y,
            deleteRect.x - barRect.xMax - 10f, rect.height);

        Text.Font = GameFont.Tiny;
        GUI.color = row.StatusColor;
        Widgets.Label(pctRect, $"{row.PercentComplete * 100f:0.#}%");
        GUI.color = Color.white;

        // Tooltip
        TooltipHandler.TipRegion(rect, row.Tooltip);

        if (Widgets.ButtonText(deleteRect, "x"))
        {
            Find.WindowStack.Add(
                Dialog_MessageBox.CreateConfirmation(
                    $"Forget {row.Label} from this Gate Traveler implant?",
                    () => implant.RemoveResearchMemory(row.DefName)
                )
            );
        }

        TooltipHandler.TipRegion(deleteRect, $"Forget {row.Label}");
    }

    private ResearchRow CreateRow(string defName, float storedPoints)
    {
        var project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);

        if (project == null)
        {
            return new ResearchRow
            {
                DefName = defName,
                Label = defName,
                PercentComplete = 1f,
                BarTexture = BaseContent.GreyTex,
                StatusColor = Color.gray,
                Tooltip = $"Unknown project: {defName}\n(Mod may be missing)"
            };
        }

        float baseCost = project.baseCost > 0 ? project.baseCost : 1f;
        float pct = Mathf.Clamp01(storedPoints / baseCost);

        string status;
        Color statusColor;

        if (project.IsFinished)
        {
            status = "Already researched";
            statusColor = Color.green;
        }
        else if (pct >= 1f)
        {
            status = "Ready to reconstruct";
            statusColor = Color.yellow;
        }
        else
        {
            status = "In metacortex";
            statusColor = Color.white;
        }

        return new ResearchRow
        {
            DefName = defName,
            Label = project.LabelCap,
            PercentComplete = pct,
            BarTexture = pct >= 1f ? BaseContent.YellowTex : BaseContent.GreyTex,
            StatusColor = statusColor,
            Tooltip =
                $"{project.LabelCap}\n" +
                $"Progress: {storedPoints:0.#} / {baseCost:0.#}\n" +
                $"Complete: {pct * 100f:0.##}%\n" +
                $"Status: {status}\n" +
                $"Can start: {(project.CanStartNow ? "Yes" : "Prerequisites needed")}"
        };
    }

    private class ResearchRow
    {
        public string DefName;
        public string Label;
        public float PercentComplete;
        public Texture2D BarTexture;
        public Color StatusColor;
        public string Tooltip;
    }
}
