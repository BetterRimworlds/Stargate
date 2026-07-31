// ==== Source/Scenario/StargateDestinationMapGen.Cavern.cs ====

using BetterRimworlds.Utilities;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate.Scenario;

public static partial class StargateDestinationMapGen
{
    private struct SecretLabPlan
    {
        public CellRect RoomRect;
        public Rot4 Side;
    }

    /// Tok'ra / impassable mountain base: solid rock with a carved cavern network
    /// and an adjacent secret laboratory.
    ///
    /// Cavern carving, terrain, flora, and ore all live in
    /// <see cref="CavernArchitect"/>. This method only owns the stargate preserve
    /// rect, secret-lab planning/exclusion, and post-cavern lab construction.
    private static void GenerateImpassableSurroundings(Map map)
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
        SecretLabPlan secretLabPlan = CreateSecretLabPlan(map, preserveRect);
        CellRect secretLabProtectedRect = secretLabPlan.RoomRect.ExpandedBy(2);

        // CavernArchitect owns the complete impassable map lifecycle. Keep the
        // stargate and planned laboratory protected while it fills/carves the map.
        CavernArchitect.GenerateCavernSystem(
            map,
            preserveRect,
            map.Tile,
            DailySeedUtility.GetDailySeed(),
            focalPoint: preserveRect.CenterCell,
            focalRoom: preserveRect,
            exclusionRects: new[] { secretLabProtectedRect });

        // Build the secret lab after generation so it can overwrite any incidental
        // ore/rock/floor placement inside its footprint.
        GenerateSecretLab(map, secretLabPlan);
    }

    private static SecretLabPlan CreateSecretLabPlan(Map map, CellRect baseRect)
    {
        int shortSide = 9;   // outer shell
        int longSide  = 12;  // outer shell

        Rot4 side = Rand.Element(Rot4.North, Rot4.South, Rot4.East, Rot4.West);
        bool vertical = side == Rot4.East || side == Rot4.West;

        int roomWidth  = vertical ? shortSide : longSide;
        int roomHeight = vertical ? longSide : shortSide;

        int centerX = baseRect.CenterCell.x;
        int centerZ = baseRect.CenterCell.z;
        int gap = Rand.RangeInclusive(4, 5);

        int startX = centerX - roomWidth / 2;
        int startZ = centerZ - roomHeight / 2;

        if (side == Rot4.North) startZ = baseRect.maxZ + gap;
        if (side == Rot4.South) startZ = baseRect.minZ - gap - roomHeight;
        if (side == Rot4.East)  startX = baseRect.maxX + gap;
        if (side == Rot4.West)  startX = baseRect.minX - gap - roomWidth;

        CellRect secretRoomRect = new CellRect(startX, startZ, roomWidth, roomHeight);
        secretRoomRect.ClipInsideMap(map);

        return new SecretLabPlan
        {
            RoomRect = secretRoomRect,
            Side = side
        };
    }

    private static CellRect GenerateSecretLab(Map map, SecretLabPlan plan)
    {
        CellRect secretRoomRect = plan.RoomRect;
        Rot4 side = plan.Side;

        if (secretRoomRect.Width < 7 || secretRoomRect.Height < 7)
        {
            return secretRoomRect;
        }

        TerrainDef sterileTile = DefDatabase<TerrainDef>.GetNamedSilentFail("SterileTile")
                                 ?? TerrainDefOf.Concrete;

        foreach (IntVec3 cell in secretRoomRect.Cells)
        {
            if (!cell.InBounds(map)) continue;

            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
            }

            map.terrainGrid.SetTerrain(cell, sterileTile);
        }

        // Pure plasteel perimeter walls with one plasteel door on the gate-room side.
        IntVec3 doorCell;
        if (side == Rot4.North)
        {
            doorCell = new IntVec3(secretRoomRect.CenterCell.x, 0, secretRoomRect.minZ);
        }
        else if (side == Rot4.South)
        {
            doorCell = new IntVec3(secretRoomRect.CenterCell.x, 0, secretRoomRect.maxZ);
        }
        else if (side == Rot4.East)
        {
            doorCell = new IntVec3(secretRoomRect.minX, 0, secretRoomRect.CenterCell.z);
        }
        else
        {
            doorCell = new IntVec3(secretRoomRect.maxX, 0, secretRoomRect.CenterCell.z);
        }

        foreach (IntVec3 wallCell in secretRoomRect.EdgeCells)
        {
            if (!wallCell.InBounds(map)) continue;

            Thing existing = map.thingGrid.ThingsListAt(wallCell)
                .FirstOrDefault(t => t.def.destroyable);
            existing?.Destroy(DestroyMode.Vanish);

            if (wallCell == doorCell)
            {
                Thing door = ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.Plasteel);
                GenSpawn.Spawn(door, wallCell, map, side, WipeMode.Vanish);
                continue;
            }

            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Plasteel);
            GenSpawn.Spawn(wall, wallCell, map, WipeMode.Vanish);
        }

        CellRect interior = new CellRect(
            secretRoomRect.minX + 1,
            secretRoomRect.minZ + 1,
            secretRoomRect.Width - 2,
            secretRoomRect.Height - 2
        );

        if (interior.Width < 3 || interior.Height < 3)
        {
            return secretRoomRect;
        }

        SecretLabPlacement.AddSecretLab(map, secretRoomRect, interior, side);

        return secretRoomRect;
    }
}
