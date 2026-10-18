// ==== Source/Scenario/SecretLab.cs ====
using RimWorld;
using Verse;
using BetterRimworlds.Utilities;

namespace BetterRimworlds.Stargate;

/// Shared Secret Lab used by the Tok'ra cavern base and Atlantis (Janus's Lab).
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

        /// True when the surrounding protected band also needs roofing (cavern
        /// maps exclude the whole protected rect from their mountain fill, so
        /// even the ring outside the lab shell would otherwise sit under open sky).
        public bool RoofProtectedBand;

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
            Side = side,
            RoofProtectedBand = true
        };
    }

    /// Floors the planned footprint, builds the luminescent plasteel shell, then furnishes the interior.
    /// Atlantis (Janus's Lab) can pass a custom <see cref="Plan"/> instead of using
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

        // Conduits before walls: RimWorld links a powered building to the first
        // transmitter that spawns nearby and never upgrades that link, so walls
        // built first ended up wired to distant conduits. Conduits may share
        // wall cells, so the ring goes down first (skipping the doorway) and each
        // luminescent wall connects to its own cell's conduit when it spawns.
        BlueprintSpawner spawner = new BlueprintSpawner(map);
        spawner.SpawnConduitRing(
            secretRoomRect,
            ThingDefOf.PowerConduit,
            skipCells: new[] { doorCell }
        );

        foreach (IntVec3 wallCell in secretRoomRect.EdgeCells)
        {
            if (!wallCell.InBounds(map)) continue;

            Thing existing = map.thingGrid.ThingsListAt(wallCell)
                .FirstOrDefault(t => t.def.destroyable && !PowerConduitUtility.IsConduit(t));
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

        // Roof the lab footprint. The cavern generator excludes the protected rect
        // from its mountain fill, so on maps with no pre-existing roof over the
        // footprint the completed lab would otherwise sit under open sky, losing
        // its sealed/indoor behavior and exposing its equipment.
        RoofDef roof = RoofDefOf.RoofConstructed;

        foreach (IntVec3 roofCell in secretRoomRect.Cells)
        {
            if (roofCell.InBounds(map))
            {
                map.roofGrid.SetRoof(roofCell, roof);
            }
        }

        // Cover the protected band too so no exposed ring is left between the lab
        // shell and the surrounding mountain roof (fills only where no roof exists,
        // mirroring BlueprintSpawner.PlaceRoof).
        if (plan.RoofProtectedBand)
        {
            foreach (IntVec3 bandCell in plan.ProtectedRect.Cells)
            {
                if (!bandCell.InBounds(map)) continue;
                if (map.roofGrid.RoofAt(bandCell) == null)
                {
                    map.roofGrid.SetRoof(bandCell, roof);
                }
            }
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
        ThingDef cryoDef            = DefDatabase<ThingDef>.GetNamedSilentFail("CryoRegenesisCasket");
        ThingDef hospitalBedDef     = DefDatabase<ThingDef>.GetNamed("HospitalBed");
        ThingDef vitalsMonitorDef   = DefDatabase<ThingDef>.GetNamed("VitalsMonitor");
        ThingDef glitterworldMedDef = DefDatabase<ThingDef>.GetNamed("MedicineUltratech");
        ThingDef compDef            = DefDatabase<ThingDef>.GetNamed("ComponentIndustrial");
        ThingDef advCompDef         = DefDatabase<ThingDef>.GetNamed("ComponentSpacer");
        ThingDef mealDef            = DefDatabase<ThingDef>.GetNamed("MealSurvivalPack");

        // ===== Layout (East-frame reference; At() and Rot() rotate it for the current side). =====
        //
        // Interior is 7 wide (x: 0..6) × 10 tall (z: 0..9). (0, 0) is the south-west cell,
        // adjacent to the west door wall and the south wall.

        const string roomName = "Lab";

        // Single Archotech ZPM at 10% charge; keep it in the established power-cell slot.
        Thing zpm = spawner.SpawnFixed(zpmDef, spawner.At(3, 8), spawner.Rot(Rot4.North), interior);
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

        spawner.SpawnStack(advCompDef,            spawner.At(0, 9), 25, roomName);

        spawner.SpawnStack(compDef,               spawner.At(0, 8), 50, roomName);
        spawner.SpawnStack(compDef,               spawner.At(1, 8), 50, roomName);

        // Meal grid: 2 wide × 5 tall in the East frame.
        for (int z = 3; z <= 7; z++)
        {
            spawner.SpawnStack(mealDef, spawner.At(1, z), 10, roomName);
            spawner.SpawnStack(mealDef, spawner.At(2, z), 10, roomName);
        }

        // The power ring is laid down in Generate() before the shell walls, so
        // each wall connects to its own cell's conduit as it spawns.
    }
}
