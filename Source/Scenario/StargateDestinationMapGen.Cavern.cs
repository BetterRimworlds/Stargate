// ==== Source/Scenario/StargateDestinationMapGen.Cavern.cs ====
using BetterRimworlds.Utilities;
using Verse;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
    /// Tok'ra / impassable mountain base: solid rock with a carved cavern network.
    /// All generation logic lives in <see cref="CavernArchitect"/>; this method only
    /// defines the stargate preserve rect and hands off.
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

        CavernArchitect.GenerateCavernSystem(
            map,
            preserveRect,
            map.Tile,
            DailySeedUtility.GetDailySeed(),
            focalPoint: preserveRect.CenterCell,
            focalRoom: preserveRect,
            entranceSide: entranceSide);
    }
}
