// ==== Source/Scenario/Page_SelectStargateScenario.cs ====
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

/// Destination-type chooser inserted into the new-game page chain before
/// Page_CreateWorldParams (by Patch_Scenario_GetFirstConfigPage) so the player
/// answers before "Generating World…". It only records the chosen
/// StargateScenarioKind; the tile itself is picked after the planet exists,
/// driven by the tile kind (ocean → Atlantis, impassable → Tok'ra, else surface).
public class Page_SelectStargateScenario : Page
{
    private StargateScenarioKind selectedKind = StargateScenarioKind.RandomTile;

    public override string PageTitle => "Choose Stargate Destination";

    public override void PreOpen()
    {
        // Fresh default each time this page opens so prior runs cannot stick.
        selectedKind = StargateScenarioKind.RandomTile;
        StargateAutomationPatches.SelectedScenarioKind = StargateScenarioKind.RandomTile;
        base.PreOpen();
    }

    public override void DoWindowContents(Rect rect)
    {
        DrawPageTitle(rect);

        Listing_Standard listing = new Listing_Standard();
        listing.Begin(GetMainRect(rect, 0f));

        listing.Label(
            "This is today's shared pseudo-multiplayer planet. Choose how your " +
            "Stargate destination should be selected on that planet."
        );
        listing.Gap(6f);
        listing.Label(
            "The world is generated next with today's daily seed. Your destination " +
            "tile is then picked automatically from that planet."
        );
        listing.GapLine();
        listing.Gap(12f);

        DrawOption(
            listing,
            StargateScenarioKind.RandomTile,
            "Random Tile",
            "Any valid world tile. Ocean sites become Atlantis-style facilities, " +
            "impassable mountains become Tok'ra-style bases, and everything else is a surface outpost."
        );

        listing.Gap(8f);

        DrawOption(
            listing,
            StargateScenarioKind.AtlantisRising,
            "Atlantis Rising",
            "Force an ocean tile and generate an Atlantis-style underwater facility."
        );

        listing.Gap(8f);

        DrawOption(
            listing,
            StargateScenarioKind.AbandonedTokraBase,
            "Abandoned Tok'ra Base",
            "Force an impassable mountain tile and generate a Tok'ra-style carved cavern base."
        );

        listing.End();

        DoBottomButtons(rect, "Next".Translate());
    }

    private void DrawOption(
        Listing_Standard listing,
        StargateScenarioKind kind,
        string label,
        string description
    )
    {
        if (listing.RadioButton(label, selectedKind == kind))
        {
            selectedKind = kind;
        }

        Text.Font = GameFont.Tiny;
        listing.Label(description);
        Text.Font = GameFont.Small;
    }

    protected override void DoNext()
    {
        // World does not exist yet — only remember the kind. Tile selection
        // happens after CreateWorldParams generates the daily planet.
        StargateAutomationPatches.SelectedScenarioKind = selectedKind;
        base.DoNext();
    }
}

/// Player-facing Stargate destination modes for the Daily Stargate Outpost scenario.
public enum StargateScenarioKind
{
    /// Any existing world tile (default).
    RandomTile = 0,

    /// Ocean / water-covered tile → Atlantis-style facility.
    AtlantisRising = 1,

    /// Impassable mountain tile → Tok'ra-style cavern base.
    AbandonedTokraBase = 2,
}
