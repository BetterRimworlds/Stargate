// ==== Source/Scenario/CavernArchitect.cs ====
using RimWorld;
using UnityEngine;
using Verse;
using BetterRimworlds.Utilities;

namespace BetterRimworlds.Stargate;

/// Standalone deterministic cave system generator for RimWorld maps.
///
/// Design philosophy:
/// - Single public entry point: GenerateCavernSystem(). Handles the entire lifecycle.
/// - Tight, claustrophobic cavern networks with high rock coverage
/// - Soil-dominant floors where caverns exist — viable for mushroom farming
/// - Bioluminescent flora as the ONLY light source — oppressive darkness between
/// - Scattered shallow water as hazard and atmosphere
/// - Proximity-boosted Perlin noise near an optional focal point (e.g., Stargate)
/// - Deterministic per tile+date for reproducibility via GetTileSubSeed
///
/// This class is intentionally decoupled from Stargate-specific logic so it can be
/// reused by other scenario builders that need ancient cave ecosystems.
public static class CavernArchitect
{
    // Noise configuration: tight, fragmented caverns
    private const float CavernThreshold = 0.55f;
    private const float CavernFrequency = 0.06f;

    // Proximity boost configuration
    private const float BoostRadius = 18f;
    private const float BoostRadiusSq = BoostRadius * BoostRadius;
    private const float InvBoostRadius = 1f / BoostRadius;
    private const float BoostMagnitude = 0.2f;

    // Bridge carving: connect caverns separated by this many rock tiles
    private const int MaxBridgeGap = 2;

    // Water cluster configuration
    private const int WaterClusterCountMin = 2;
    private const int WaterClusterCountMax = 4;
    private const int WaterClusterSizeMin = 8;
    private const int WaterClusterSizeMax = 15;

    // Mushroom palette — vanilla bioluminescent cave flora
    private static readonly string[] MushroomDefs = { "Plant_Glowstool", "Plant_Agarilux", "Plant_Bryolux" };

    // Rock type palette for edge and mountain generation
    private static readonly string[] RockTypes = { "Granite", "Limestone", "Sandstone", "Marble", "Slate" };

    // Ore palette for wall veins
    private static readonly (string defName, float weight)[] OreOptions =
    {
        ("MineableSteel",                  3.0f),
        ("MineableSilver",                 2.0f),
        ("MineableGold",                   1.0f),
        ("MineablePlasteel",               1.0f),
        ("MineableUranium",                1.0f),
        ("MineableJade",                   0.8f),
        ("MineableComponentsIndustrial",   0.8f),
    };

    // Rich ore deposit scattering counts (per 10k cells)
    private static readonly (string defName, int count)[] RichOreCounts =
    {
        ("MineableSteel",               10),
        ("MineableSilver",              16),
        ("MineableGold",                10),
        ("MineableUranium",              8),
        ("MineablePlasteel",            15),
        ("MineableJade",                 4),
        ("MineableComponentsIndustrial", 8),
    };

    /// Generates a complete cave system on the given map.
    /// This is the ONLY public method. It handles the entire generation lifecycle internally.
    ///
    /// Returns all cavern floor cells for downstream use (spawning, zoning, etc.).
    /// <param name="map"></param>
    /// <param name="preserveRect"></param>
    /// <param name="tileID"></param>
    /// <param name="dateSeed"></param>
    /// <param name="soilRatio"></param>
    /// <param name="mushroomDensity"></param>
    /// <param name="focalPoint">Optional center point for proximity noise boost (e.g., stargate room center).</param>
    /// <param name="focalRoom">Optional room rect to guarantee a cavern connection at its door.</param>
    public static List<IntVec3> GenerateCavernSystem(
        Map map,
        CellRect preserveRect,
        int tileID,
        string dateSeed,
        float soilRatio = 0.70f,
        float mushroomDensity = 0.65f,
        IntVec3? focalPoint = null,
        CellRect? focalRoom = null,
        IEnumerable<CellRect> exclusionRects = null,
        Rot4? entranceSide = null)
    {
        List<CellRect> exclusions = exclusionRects?.ToList() ?? new List<CellRect>();
        exclusions.Insert(0, preserveRect);
        int edgeBand = DetermineEdgeBand(tileID, dateSeed);

        List<IntVec3> cavernCells = new List<IntVec3>();

        // Deterministic generation for this tile+date
        int cavernSeed = DailySeedUtility.GetTileSubSeed(tileID, $"{dateSeed}|cavern-system");
        Rand.PushState(cavernSeed);

        try
        {
            // 1. Fill map with solid rock + thick roof
            FillWithRock(map, exclusions, edgeBand);

            // 2. Clear preserved area (stargate room)
            ClearPreservedArea(map, preserveRect);

            // 3. Carve caverns from noise (with proximity boost)
            GenerateNoiseCaverns(map, exclusions, edgeBand, cavernCells, focalPoint);

            // 4. Guarantee a cavern opening at the focal room door (using original Stargate logic)
            if (focalRoom.HasValue)
            {
                GuaranteeCavernConnection(map, focalRoom.Value, cavernCells, soilRatio, exclusions, entranceSide);
            }

            // 5. Post-process: bridge narrow gaps for connectivity
            BridgeCavernGaps(map, cavernCells, MaxBridgeGap, exclusions);

            // 6. Expand to include walkable neighbors for complete terrain coverage
            ExpandCavernCells(map, cavernCells, exclusions);

            // 7. Set terrain: soil-dominant with gravel pockets (and sandstone awareness)
            ApplyCavernTerrain(map, cavernCells, soilRatio);

            // 8. Scatter shallow water pools BEFORE mushrooms
            PlaceWaterPools(map, cavernCells, tileID, dateSeed);

            // 9. Plant bioluminescent flora on remaining dry soil (with forced terrain standardization)
            PlantMushrooms(map, cavernCells, mushroomDensity, soilRatio);

            // 10. Place ore veins in rock walls adjacent to cavern cells
            PlaceOreVeins(map, cavernCells, tileID, dateSeed);

            // 11. Scatter rich ore lumps across the map (bulk rock distribution)
            ScatterRichOreDeposits(map);

            // ScatterLumpsMineable works map-wide and has no exclusion hook. Remove
            // any lump it may have placed in protected footprints before continuing.
            ClearGeneratedThingsInExclusions(map, exclusions);

            // 12. Enforce solid rock edge border
            EnforceRockEdge(map, edgeBand, exclusions);

            // Bridging and expansion can add edge-band cells that were just
            // restored to solid rock. Do not return them as cavern floor.
            cavernCells.RemoveAll(cell => IsInEdgeBand(cell, map, edgeBand));

            // 13. Seal the map from the sky
            map.GetComponent<MapComponent_SealedFromSky>().isSealed = true;
        }
        finally
        {
            Rand.PopState();
        }

        return cavernCells;
    }

    // === Private implementation ===

    private static int DetermineEdgeBand(int tileID, string dateSeed)
    {
        int seed = DailySeedUtility.GetTileSubSeed(tileID, $"{dateSeed}|edge-band");
        Rand.PushState(seed);
        try
        {
            return Rand.Range(5, 11); // 5 to 10 inclusive
        }
        finally
        {
            Rand.PopState();
        }
    }

    private static void FillWithRock(Map map, IEnumerable<CellRect> exclusions, int edgeBand)
    {
        // Strict Def Lookup for foundational elements
        ThingDef primaryRock = DefDatabase<ThingDef>.GetNamed(RockTypes[Rand.Range(0, RockTypes.Length)]);
        ThingDef secondaryRock = DefDatabase<ThingDef>.GetNamed(RockTypes[Rand.Range(0, RockTypes.Length)]);

        if (primaryRock == null)
        {
            Log.Warning("CavernArchitect: No rock defs found, skipping mountain fill.");
            return;
        }

        foreach (IntVec3 cell in map.AllCells)
        {
            if (IsExcluded(cell, exclusions)) continue;

            DestroyThingsInCell(map, cell);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Gravel);

            ThingDef rockDef = Rand.Chance(0.85f) ? primaryRock : (secondaryRock ?? primaryRock);
            GenSpawn.Spawn(ThingMaker.MakeThing(rockDef), cell, map, WipeMode.Vanish);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
        }
    }

    private static void ClearPreservedArea(Map map, CellRect preserveRect)
    {
        foreach (IntVec3 cell in preserveRect.Cells)
        {
            if (!cell.InBounds(map)) continue;
            DestroyThingsInCell(map, cell);
            map.roofGrid.SetRoof(cell, null);
        }
    }

    private static void GenerateNoiseCaverns(
        Map map,
        IEnumerable<CellRect> exclusions,
        int edgeBand,
        List<IntVec3> outCells,
        IntVec3? focalPoint)
    {
        float offsetX = Rand.Range(0f, 10000f);
        float offsetZ = Rand.Range(0f, 10000f);

        IntVec3 boostCenter = focalPoint ?? map.Center;

        foreach (IntVec3 cell in map.AllCells)
        {
            if (IsExcluded(cell, exclusions)) continue;
            if (IsInEdgeBand(cell, map, edgeBand)) continue;

            float noise = Mathf.PerlinNoise(
                (cell.x + offsetX) * CavernFrequency,
                (cell.z + offsetZ) * CavernFrequency
            );

            // Proximity Boost: Force noise higher near the focal point so it connects out
            int dx = cell.x - boostCenter.x;
            int dz = cell.z - boostCenter.z;
            int distSq = dx * dx + dz * dz;

            if (distSq < BoostRadiusSq)
            {
                float boost = BoostMagnitude * (1f - Mathf.Sqrt(distSq) * InvBoostRadius);
                noise += boost;
            }

            if (noise > CavernThreshold)
            {
                CarveCavernCell(map, cell, outCells);
            }
        }
    }

    private static void GuaranteeCavernConnection(
        Map map,
        CellRect focalRoom,
        List<IntVec3> cavernCells,
        float soilRatio,
        IEnumerable<CellRect> exclusions,
        Rot4? entranceSide)
    {
        int roomSize = focalRoom.Width;

        // The facility door is placed by GenerateRoomStructure() AFTER the
        // cavern generator runs, so it cannot be looked up here. Use the
        // entrance side chosen by the caller so the guaranteed opening lands
        // outside the real doorway instead of always defaulting east.
        IntVec3 dir;
        if (entranceSide.HasValue)
        {
            dir = entranceSide.Value.FacingCell;
        }
        else
        {
            Building_Door door = focalRoom.Cells
                .Where(c => c.InBounds(map))
                .Select(c => c.GetEdifice(map) as Building_Door)
                .FirstOrDefault(d => d != null);

            IntVec3 delta = (door?.Position ?? focalRoom.CenterCell) - focalRoom.CenterCell;
            dir = (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                ? new IntVec3(delta.x >= 0 ? 1 : -1, 0, 0)
                : new IntVec3(0, 0, delta.z >= 0 ? 1 : -1);
        }

        // Center the starter cavern one cell past the preserved footprint so
        // the doorway approach cell opens straight into carved floor instead
        // of leaving a gap of solid rock.
        IntVec3 mouth = focalRoom.CenterCell + dir * (roomSize / 2 + 1);
        CellRect starterCavern = new CellRect(mouth.x - 2, mouth.z - 2, 5, 5);
        starterCavern.ClipInsideMap(map);

        foreach (IntVec3 cell in starterCavern.Cells)
        {
            if (IsExcluded(cell, exclusions)) continue;
            DestroyThingsInCell(map, cell);

            map.terrainGrid.SetTerrain(cell, DetermineCavernTerrain(null, soilRatio));
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);

            if (!cavernCells.Contains(cell))
            {
                cavernCells.Add(cell);
            }
        }
    }

    private static void CarveCavernCell(Map map, IntVec3 cell, List<IntVec3> outCells)
    {
        DestroyThingsInCell(map, cell);

        map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);
        outCells.Add(cell);
    }

    private static void BridgeCavernGaps(
        Map map,
        List<IntVec3> cavernCells,
        int maxGap,
        IEnumerable<CellRect> exclusions)
    {
        HashSet<IntVec3> cavernSet = new HashSet<IntVec3>(cavernCells);
        List<IntVec3> newBridges = new List<IntVec3>();

        foreach (IntVec3 start in cavernCells)
        {
            foreach (IntVec3 dir in GenAdj.CardinalDirections)
            {
                for (int distance = 1; distance <= maxGap + 1; distance++)
                {
                    IntVec3 checkCell = start + (dir * distance);

                    if (!checkCell.InBounds(map)) break;
                    if (IsExcluded(checkCell, exclusions)) break;

                    // Already a cavern — check if we bridged anything useful
                    if (cavernSet.Contains(checkCell))
                    {
                        // Carve bridge, but only cells not already cavern
                        for (int bridge = 1; bridge < distance; bridge++)
                        {
                            IntVec3 bridgeCell = start + (dir * bridge);
                            if (!cavernSet.Contains(bridgeCell) && !newBridges.Contains(bridgeCell))
                            {
                                if (IsExcluded(bridgeCell, exclusions)) continue;
                                CarveCavernCell(map, bridgeCell, newBridges);
                            }
                        }
                        break;
                    }

                    // Stop if gap is too wide or we hit non-rock (edge case)
                    if (distance > maxGap)
                        break;

                    if (!HasNaturalRockAt(map, checkCell) && !cavernSet.Contains(checkCell))
                        break;
                }
            }
        }

        cavernCells.AddRange(newBridges);
    }

    private static void ExpandCavernCells(
        Map map,
        List<IntVec3> cavernCells,
        IEnumerable<CellRect> exclusions)
    {
        HashSet<IntVec3> expanded = new HashSet<IntVec3>(cavernCells);

        foreach (IntVec3 cell in cavernCells)
        {
            foreach (IntVec3 dir in GenAdj.AdjacentCells)
            {
                IntVec3 neighbor = cell + dir;
                if (!neighbor.InBounds(map)) continue;
                if (IsExcluded(neighbor, exclusions)) continue;
                if (expanded.Contains(neighbor)) continue;

                if (neighbor.Walkable(map))
                {
                    expanded.Add(neighbor);
                }
            }
        }

        cavernCells.Clear();
        cavernCells.AddRange(expanded);
    }

    private static void ClearGeneratedThingsInExclusions(Map map, IEnumerable<CellRect> exclusions)
    {
        foreach (CellRect rect in exclusions)
        {
            foreach (IntVec3 cell in rect.Cells)
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
                foreach (Thing thing in things)
                {
                    if (thing is Pawn) continue;
                    // isResourceRock covers ore lumps; isNaturalRock covers mountain rock.
                    // (BuildingProperties has no "mineable" field across supported RimWorld versions.)
                    bool isMineable = thing.def.building != null && thing.def.building.isResourceRock;
                    bool isNaturalRock = thing.def.building != null && thing.def.building.isNaturalRock;
                    if (isNaturalRock || isMineable)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }
    }

    private static void ApplyCavernTerrain(Map map, List<IntVec3> cavernCells, float soilRatio)
    {
        // Use primary rock type for contextual floor decisions.
        ThingDef primaryRock = DefDatabase<ThingDef>.GetNamedSilentFail(RockTypes[Rand.Range(0, RockTypes.Length)]);

        foreach (IntVec3 cell in cavernCells)
        {
            if (!cell.InBounds(map)) continue;

            TerrainDef terrain = DetermineCavernTerrain(primaryRock, soilRatio);
            map.terrainGrid.SetTerrain(cell, terrain);
        }
    }

    private static void PlaceWaterPools(Map map, List<IntVec3> cavernCells, int tileID, string dateSeed)
    {
        if (cavernCells.Count == 0) return;

        int waterSeed = DailySeedUtility.GetTileSubSeed(tileID, $"{dateSeed}|water-pools");
        Rand.PushState(waterSeed);

        try
        {
            int clusterCount = Rand.Range(WaterClusterCountMin, WaterClusterCountMax + 1);
            HashSet<IntVec3> waterCells = new HashSet<IntVec3>();
            HashSet<IntVec3> cavernSet = new HashSet<IntVec3>(cavernCells);

            // Sort by distance from center — "lowest" areas for water accumulation
            IntVec3 center = map.Center;
            List<IntVec3> sortedCells = cavernCells.OrderBy(c => c.DistanceTo(center)).ToList();

            for (int i = 0; i < clusterCount && sortedCells.Count > 0; i++)
            {
                int seedIndex = Rand.Range(0, Math.Min(sortedCells.Count / 3 + 1, sortedCells.Count));
                IntVec3 seedCell = sortedCells[seedIndex];

                int clusterSize = Rand.Range(WaterClusterSizeMin, WaterClusterSizeMax + 1);
                GrowWaterCluster(map, seedCell, clusterSize, cavernSet, waterCells);
            }

            foreach (IntVec3 cell in waterCells)
            {
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.WaterShallow);
            }
        }
        finally
        {
            Rand.PopState();
        }
    }

    private static void GrowWaterCluster(
        Map map,
        IntVec3 seedCell,
        int targetSize,
        HashSet<IntVec3> cavernSet,
        HashSet<IntVec3> waterCells)
    {
        Queue<IntVec3> frontier = new Queue<IntVec3>();
        HashSet<IntVec3> enqueued = new HashSet<IntVec3>();
        int clusterStartCount = waterCells.Count;

        frontier.Enqueue(seedCell);
        enqueued.Add(seedCell);

        // Stop once THIS cluster has grown by targetSize cells, not when the
        // map-wide total (shared across all clusters) reaches targetSize.
        while (frontier.Count > 0 && waterCells.Count - clusterStartCount < targetSize)
        {
            IntVec3 current = frontier.Dequeue();

            if (!current.InBounds(map)) continue;
            if (!cavernSet.Contains(current)) continue;
            if (waterCells.Contains(current)) continue;

            waterCells.Add(current);

            foreach (IntVec3 dir in GenAdj.AdjacentCells)
            {
                IntVec3 neighbor = current + dir;
                if (!enqueued.Add(neighbor)) continue;

                if (Rand.Chance(0.7f))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }
    }

    // Forced terrain standardization from original PlantCavernFlora,
    // while respecting water cells scattered previously.
    private static void PlantMushrooms(Map map, List<IntVec3> cavernCells, float density, float soilRatio)
    {
        List<ThingDef> palette = new List<ThingDef>();
        foreach (string defName in MushroomDefs)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null) palette.Add(def);
        }

        if (palette.Count == 0) return;

        // Look up the primary rock type for smarter terrain selection
        ThingDef primaryRock = DefDatabase<ThingDef>.GetNamedSilentFail(RockTypes[Rand.Range(0, RockTypes.Length)]);

        foreach (IntVec3 cell in cavernCells)
        {
            if (!cell.InBounds(map)) continue;
            if (!cell.Walkable(map)) continue;

            // Skip water cells — mushrooms don't grow in shallow water
            TerrainDef currentTerrain = map.terrainGrid.TerrainAt(cell);
            if (currentTerrain == TerrainDefOf.WaterShallow)
                continue;

            // Force standardize the floor terrain so mushrooms always have a valid growable surface.
            // Determine per cell to preserve the soil/gravel mix from ApplyCavernTerrain.
            map.terrainGrid.SetTerrain(cell, DetermineCavernTerrain(primaryRock, soilRatio));

            if (!Rand.Chance(density)) continue;

            ThingDef plantDef = palette.RandomElement();
            Plant plant = (Plant)ThingMaker.MakeThing(plantDef);
            plant.Growth = Rand.Range(0.45f, 1.0f);
            GenSpawn.Spawn(plant, cell, map, WipeMode.Vanish);
        }
    }

    private static void PlaceOreVeins(Map map, List<IntVec3> cavernCells, int tileID, string dateSeed)
    {
        int oreSeed = DailySeedUtility.GetTileSubSeed(tileID, $"{dateSeed}|ore-veins");
        Rand.PushState(oreSeed);

        try
        {
            var palette = BuildOrePalette();
            if (palette.Count == 0) return;

            float totalWeight = palette.Sum(e => e.weight);
            HashSet<IntVec3> processed = new HashSet<IntVec3>();

            foreach (IntVec3 cavernCell in cavernCells)
            {
                foreach (IntVec3 dir in GenAdj.CardinalDirections)
                {
                    IntVec3 wall = cavernCell + dir;
                    if (!wall.InBounds(map)) continue;
                    if (!processed.Add(wall)) continue;

                    Thing rock = FindNaturalRockAt(map, wall);
                    if (rock == null) continue;
                    if (!Rand.Chance(0.25f)) continue;

                    ThingDef oreDef = WeightedPickOre(palette, totalWeight);
                    if (oreDef == null) continue;

                    rock.Destroy(DestroyMode.Vanish);
                    GenSpawn.Spawn(ThingMaker.MakeThing(oreDef), wall, map, WipeMode.Vanish);
                }
            }
        }
        finally
        {
            Rand.PopState();
        }
    }

    private static void ScatterRichOreDeposits(Map map)
    {
        foreach (var (defName, count) in RichOreCounts)
        {
            ThingDef oreDef = TryGetOre(defName);
            if (oreDef == null) continue;

            var scatter = new GenStep_ScatterLumpsMineable();
            scatter.forcedDefToScatter   = oreDef;
            scatter.countPer10kCellsRange = new FloatRange(count, count);
            scatter.Generate(map, new GenStepParams());
        }
    }

    private static void EnforceRockEdge(Map map, int edgeBand, IEnumerable<CellRect> exclusions)
    {
        ThingDef edgeRock = DefDatabase<ThingDef>.GetNamedSilentFail(RockTypes[Rand.Range(0, RockTypes.Length)]);
        if (edgeRock == null)
        {
            Log.Warning("CavernArchitect: No rock defs found, skipping edge rock band.");
            return;
        }

        foreach (IntVec3 cell in map.AllCells)
        {
            if (!IsInEdgeBand(cell, map, edgeBand)) continue;
            if (IsExcluded(cell, exclusions)) continue;

            DestroyThingsInCell(map, cell);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);

            GenSpawn.Spawn(ThingMaker.MakeThing(edgeRock), cell, map, WipeMode.Vanish);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
        }
    }

    private static bool IsInEdgeBand(IntVec3 cell, Map map, int edgeBand)
    {
        return cell.x < edgeBand
               || cell.z < edgeBand
               || cell.x >= map.Size.x - edgeBand
               || cell.z >= map.Size.z - edgeBand;
    }

    private static bool IsExcluded(IntVec3 cell, IEnumerable<CellRect> exclusions)
    {
        return exclusions != null && exclusions.Any(rect => rect.Contains(cell));
    }

    private static void DestroyThingsInCell(Map map, IntVec3 cell)
    {
        var things = map.thingGrid.ThingsListAt(cell);
        for (int i = things.Count - 1; i >= 0; i--)
        {
            var thing = things[i];
            if (thing is Pawn) continue;
            if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static Thing FindNaturalRockAt(Map map, IntVec3 cell)
    {
        return map.thingGrid.ThingsListAt(cell)
            .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);
    }

    private static bool HasNaturalRockAt(Map map, IntVec3 cell)
    {
        return FindNaturalRockAt(map, cell) != null;
    }

    private static List<(ThingDef def, float weight)> BuildOrePalette()
    {
        var palette = new List<(ThingDef def, float weight)>();
        foreach (var (name, weight) in OreOptions)
        {
            // Optional elements use Safe Def Lookup
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (d != null) palette.Add((d, weight));
        }
        return palette;
    }

    private static ThingDef WeightedPickOre(List<(ThingDef def, float weight)> palette, float totalWeight)
    {
        if (palette == null || palette.Count == 0 || totalWeight <= 0f) return null;

        float roll = Rand.Range(0f, totalWeight);
        float acc = 0f;
        foreach (var (def, weight) in palette)
        {
            acc += weight;
            if (roll <= acc) return def;
        }
        return palette[palette.Count - 1].def;
    }

    /// Returns a contextually appropriate cavern floor terrain.
    /// If the underlying rock is Sandstone, has a 20% chance of sandy terrain.
    /// Otherwise uses soilRatio to choose between Soil and Gravel.
    private static TerrainDef DetermineCavernTerrain(ThingDef underlyingRock, float soilRatio)
    {
        soilRatio = Mathf.Clamp01(soilRatio);

        // Sandstone gets a chance for sandy terrain.
        if (underlyingRock != null
            && underlyingRock.defName.Contains("Sandstone")
            && Rand.Chance(0.2f))
        {
            // The vanilla rich soil def is "SoilRich" (not "RichSoil"); use
            // silent-fail lookup to avoid a missing-def error log when absent.
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");
            if (soil != null) return soil;
        }

        // Mix of Soil and Gravel, randomly
        return Rand.Chance(soilRatio) ? TerrainDefOf.Soil : TerrainDefOf.Gravel;
    }

    /// Attempts to retrieve a ThingDef by name, returning null if not found.
    private static ThingDef TryGetOre(string defName)
    {
        // Optional elements use Safe Def Lookup
        return DefDatabase<ThingDef>.GetNamedSilentFail(defName);
    }
}
