// ==== Source/Scenario/ScenPart_StargateFacility.Power.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility
{
    private void PlacePowerConduits(Map map, IntVec3 center, CellRect roomRect)
    {
        ThingDef conduitDef = ThingDefOf.PowerConduit;
        if (conduitDef == null) return;

        // Main deterministic conduit loop just inside the inner wall.
        // This replaces the previous random broken-grid behavior.
        CellRect conduitLoopRect = roomRect.ContractedBy(1);
        foreach (IntVec3 cell in conduitLoopRect.EdgeCells)
        {
            TryPlaceConduit(map, cell);
        }

        // Power spine through the center area.
        // This connects the Stargate/DHD area to the wall loop.
        for (int x = roomRect.minX + 2; x <= roomRect.maxX - 2; x++)
        {
            TryPlaceConduit(map, new IntVec3(x, 0, center.z));
        }

        for (int z = roomRect.minZ + 2; z <= roomRect.maxZ - 2; z++)
        {
            TryPlaceConduit(map, new IntVec3(center.x, 0, z));
        }

        // Explicit spurs toward known equipment positions.
        // DrawConduitLine(map, center, new IntVec3(roomRect.maxX - 2, 0, roomRect.minZ + 2));
        // DrawConduitLine(map, center, new IntVec3(roomRect.minX + 2, 0, roomRect.maxZ - 2));
        // DrawConduitLine(map, center, center + new IntVec3(3, 0, 0));
    }

    private void TryPlaceConduit(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map)) return;
        if (cell.Impassable(map)) return;
        if (ContainsThingDef(map, cell, ThingDefOf.PowerConduit)) return;

        PlaceClaimed(map, ThingDefOf.PowerConduit, cell);
    }

    private void DrawConduitLine(Map map, IntVec3 start, IntVec3 end)
    {
        IntVec3 current = start;

        while (current.x != end.x)
        {
            current = new IntVec3(
                current.x + (current.x < end.x ? 1 : -1),
                0,
                current.z
            );

            TryPlaceConduit(map, current);
        }

        while (current.z != end.z)
        {
            current = new IntVec3(
                current.x,
                0,
                current.z + (current.z < end.z ? 1 : -1)
            );

            TryPlaceConduit(map, current);
        }
    }
}