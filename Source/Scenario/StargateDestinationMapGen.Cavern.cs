// ==== Source/Scenario/StargateDestinationMapGen.Cavern.cs ====
using BetterRimworlds.Utilities;
using Verse;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
    /// Tok'ra / impassable mountain base: solid rock with a carved cavern network
    /// and an adjacent secret laboratory.
    ///
    /// Cavern carving lives in <see cref="CavernArchitect"/>. The laboratory lives
    /// in <see cref="SecretLab"/>. This method only defines the stargate
    /// preserve rect, asks the lab to plan around it, then hands off.
    private static void GenerateImpassableSurroundings(Map map, Rot4 entranceSide)
    {
        IntVec3 center = map.Center;
        int halfSize  = RoomSize / 2;

        // Preserve room + outer wall + one approach cell on each side.
        CellRect preserveRect = new CellRect(
            center.x - halfSize - 2,
            center.z - halfSize - 2,
            RoomSize + 4,
            RoomSize + 4
        );

        // Plan the secret lab before carving caverns so cavern generation can avoid it.
        SecretLab.Plan secretLabPlan =
            SecretLab.PlanAdjacentTo(map, preserveRect, entranceSide);

        CavernArchitect.GenerateCavernSystem(
            map,
            preserveRect,
            map.Tile,
            DailySeedUtility.GetDailySeed(),
            focalPoint: preserveRect.CenterCell,
            focalRoom: preserveRect,
            exclusionRects: new[] { secretLabPlan.ProtectedRect },
            entranceSide: entranceSide);

        // Build the secret lab after generation so it can overwrite any incidental
        // ore/rock/floor placement inside its footprint.
        SecretLab.Generate(map, secretLabPlan);
    }
}
