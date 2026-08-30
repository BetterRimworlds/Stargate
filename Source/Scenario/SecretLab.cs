// ==== Source/Scenario/SecretLab.cs ====
using RimWorld;
using Verse;
using BetterRimworlds.Utilities;

namespace BetterRimworlds.Stargate;

/// Shared Secret Lab used by the Tok'ra cavern base and the Atlantis Hidden Lab.
/// Owns site planning, the plasteel shell, and interior furnishings so destination
/// mapgen does not embed laboratory construction.
internal static class SecretLab
{
    // Reference layout dimensions (East orientation, vertical room).
    // All other sides are pure rotations of this layout around the room's centre.
    private const int RefWidth  = 7;
    private const int RefHeight = 10;

    private const int OuterShort = 9;
    private const int OuterLong  = 12;

    internal struct Plan
    {
        public CellRect RoomRect;
        public Rot4 Side;

        public CellRect ProtectedRect => RoomRect.ExpandedBy(2);
    }

    /// Adjacent annex used by impassable / Tok'ra mapgen. Skips <paramref name="avoidSide"/>
    /// so cavern carving can still open a doorway on the facility entrance.
    public static Plan PlanAdjacentTo(Map map, CellRect baseRect, Rot4 avoidSide)
    {
        Rot4[] candidateSides = { Rot4.North, Rot4.South, Rot4.East, Rot4.West };
        Rot4 side = candidateSides.Where(s => s != avoidSide).RandomElement();
        bool vertical = side == Rot4.East || side == Rot4.West;

        int roomWidth  = vertical ? OuterShort : OuterLong;
        int roomHeight = vertical ? OuterLong : OuterShort;

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

        return new Plan
        {
            RoomRect = secretRoomRect,
            Side = side
        };
    }

    /// Floors the planned footprint, builds the luminescent plasteel shell, then furnishes the interior.
    /// Atlantis Hidden Lab can pass a custom <see cref="Plan"/> instead of using
    /// <see cref="PlanAdjacentTo"/>.
    public static CellRect Generate(Map map, Plan plan)
    {
        CellRect secretRoomRect = plan.RoomRect;
        Rot4 side = plan.Side;

        if (secretRoomRect.Width < 7 || secretRoomRect.Height < 7)
        {
            return secretRoomRect;
        }

        TerrainDef sterileTile = DefDatabase<TerrainDef>.GetNamed("SterileTile");

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

        // Pure plasteel perimeter walls with one plasteel door on the approach side.
        IntVec3 doorCell = DoorCellFor(secretRoomRect, side);
        ThingDef luminescentPlasteelWall =
            DefDatabase<ThingDef>.GetNamedSilentFail("BR_LuminescentPlasteelWall");

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

            Thing wall = luminescentPlasteelWall != null
                ? ThingMaker.MakeThing(luminescentPlasteelWall)
                : ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Plasteel);
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

        AddSecretLab(map, secretRoomRect, interior, side);

        return secretRoomRect;
    }

    public static IntVec3 DoorCellFor(CellRect secretRoomRect, Rot4 side)
    {
        if (side == Rot4.North)
        {
            return new IntVec3(secretRoomRect.CenterCell.x, 0, secretRoomRect.minZ);
        }

        if (side == Rot4.South)
        {
            return new IntVec3(secretRoomRect.CenterCell.x, 0, secretRoomRect.maxZ);
        }

        if (side == Rot4.East)
        {
            return new IntVec3(secretRoomRect.minX, 0, secretRoomRect.CenterCell.z);
        }

        return new IntVec3(secretRoomRect.maxX, 0, secretRoomRect.CenterCell.z);
    }

    public static void AddSecretLab(Map map, CellRect secretRoomRect, CellRect interior, Rot4 side)
    {
        BlueprintSpawner spawner = new BlueprintSpawner(map);
        spawner.ConfigureLayout(interior, side, RefWidth, RefHeight);

        ThingDef researchDef        = DefDatabase<ThingDef>.GetNamed("HiTechResearchBench");
        ThingDef armchairDef        = DefDatabase<ThingDef>.GetNamed("Armchair");
        ThingDef hyperweaveDef      = DefDatabase<ThingDef>.GetNamed("Hyperweave");
        ThingDef analyzerDef        = DefDatabase<ThingDef>.GetNamed("MultiAnalyzer");
        ThingDef lampDef            = ThingDefOf.StandingLamp;
        ThingDef zpmDef             = DefDatabase<ThingDef>.GetNamed("ArchotechZPM");
        ThingDef conduitDef         = ThingDefOf.PowerConduit;
        ThingDef cryoDef            = DefDatabase<ThingDef>.GetNamedSilentFail("CryoRegenesisCasket");
        ThingDef hospitalBedDef     = DefDatabase<ThingDef>.GetNamed("HospitalBed");
        ThingDef vitalsMonitorDef   = DefDatabase<ThingDef>.GetNamed("VitalsMonitor");
        ThingDef glitterworldMedDef = DefDatabase<ThingDef>.GetNamed("MedicineUltratech");
        ThingDef compDef            = DefDatabase<ThingDef>.GetNamed("ComponentIndustrial");
        ThingDef advCompDef         = DefDatabase<ThingDef>.GetNamed("ComponentSpacer");
        ThingDef mealDef            = DefDatabase<ThingDef>.GetNamed("MealSurvivalPack");

        void SpawnLamps(params IntVec3[] cells)
        {
            if (lampDef == null) return;

            foreach (IntVec3 cell in cells.Distinct())
            {
                if (!cell.InBounds(map)) continue;
                if (!interior.Contains(cell)) continue;

                CellRect rect = new CellRect(cell.x, cell.z, 1, 1);
                if (!spawner.RectClear(rect)) continue;

                spawner.ReserveRect(rect);
                GenSpawn.Spawn(ThingMaker.MakeThing(lampDef), cell, map, WipeMode.Vanish);
            }
        }

        // ===== Layout (East-frame reference; At() and Rot() rotate it for the current side). =====
        //
        // Interior is 7 wide (x: 0..6) × 10 tall (z: 0..9). (0, 0) is the south-west cell,
        // adjacent to the west door wall and the south wall.

        const string roomName = "Lab";

        // Single Archotech ZPM at 10% charge; keep it in the established power-cell slot.
        Thing zpm = spawner.SpawnFixed(zpmDef, spawner.At(2, 9), spawner.Rot(Rot4.East), interior);
        zpm?.TryGetComp<CompPowerBattery>()?.SetStoredEnergyPct(0.10f);

        spawner.SpawnFixed(researchDef,        spawner.At(5, 5), spawner.Rot(Rot4.East), interior);
        spawner.SpawnFixed(analyzerDef,        spawner.At(5, 8), spawner.Rot(Rot4.North), interior);
        spawner.SpawnFixed(armchairDef,        spawner.At(4, 5), spawner.Rot(Rot4.East), interior, hyperweaveDef, QualityCategory.Legendary);

        if (cryoDef != null) {
            spawner.SpawnFixed(cryoDef, spawner.At(6, 0), spawner.Rot(Rot4.West), interior);
        }
        else {
            Log.Warning("[Stargate] SecretLabPlacement: CryoRegenesisCasket def not found; skipping cryo casket placement.");
        }

        spawner.SpawnFixed(hospitalBedDef,     spawner.At(0, 0), spawner.Rot(Rot4.East), interior, ThingDefOf.Steel);
        spawner.SpawnFixed(vitalsMonitorDef,   spawner.At(0, 1), spawner.Rot(Rot4.East), interior);

        spawner.SpawnStack(glitterworldMedDef,    spawner.At(2, 0), 25, roomName);
        spawner.SpawnStack(glitterworldMedDef,    spawner.At(3, 0), 25, roomName);

        SpawnLamps(spawner.At(3, 3), spawner.At(3, 6));

        spawner.SpawnStack(advCompDef,            spawner.At(0, 9), 25, roomName);

        spawner.SpawnStack(compDef,               spawner.At(0, 8), 50, roomName);
        spawner.SpawnStack(compDef,               spawner.At(1, 8), 50, roomName);

        // Meal grid: 2 wide × 5 tall in the East frame.
        for (int z = 3; z <= 7; z++)
        {
            spawner.SpawnStack(mealDef, spawner.At(1, z), 10, roomName);
            spawner.SpawnStack(mealDef, spawner.At(2, z), 10, roomName);
        }

        spawner.SpawnConduitRing(secretRoomRect, conduitDef);
    }
}
