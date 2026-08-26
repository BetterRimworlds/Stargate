// ==== Source/Scenario/ScenPart_StargateFacility_Tokra.cs ====
using BetterRimworlds.Utilities;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

/// Impassable-tile (Tok'ra cavern base) specializations for the starting gate room.
/// Gated by <see cref="IsTokraFacility"/> — no-ops on non-impassable maps.
internal partial class ScenPart_StargateFacility
{
    private bool IsTokraFacility(Map map)
    {
        return DescribeTile(map.Tile) == "Impassable";
    }

    private TerrainDef GetTokraFloorDef(Map map)
    {
        return IsTokraFacility(map)
            ? DefDatabase<TerrainDef>.GetNamed("BR_TokraTile")
            : null;
    }

    /// Tok'ra-only furniture layered on after the shared facility layout.
    /// The current Tok'ra gate-room design has no evidence-backed unique
    /// furnishing to place here, so the hook remains intentionally explicit.
    private void PlaceTokraFacilityExtras(Map map, IntVec3 center, CellRect roomRect)
    {
        if (!IsTokraFacility(map)) return;

        // Shared equipment (vanometric, ZPM, DHD, guardian casket) is already placed.
        // Do not invent Tok'ra furnishings without a corresponding blueprint.
    }
}
