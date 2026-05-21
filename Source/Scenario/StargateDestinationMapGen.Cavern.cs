// ==== Source/Scenario/StargateDestinationMapGen.Cavern.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using BetterRimworlds.Utilities;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
    // Lowered threshold: 0.55 means only the top ~45% of noise values become caverns.
    private const float CavernThreshold = 0.55f;
    private const float CavernFrequency = 0.06f;

    // Rock type palette for mountain and edge generation.
    private static readonly string[] RockTypes = { "Granite", "Limestone", "Sandstone", "Marble", "Slate" };

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

        // 1. Fill the entire map with mountain terrain
        GenerateMountainTerrain(map, preserveRect);

        // 2. Clear the preserved area for the stargate room
        ClearPreservedArea(map, preserveRect);

        // 3. Carve out the caverns (now with proximity boost & guaranteed starter cavern)
        List<IntVec3> cavernCells = new List<IntVec3>();
        GeneratePerlinCaverns(map, preserveRect, cavernCells);

        // 4. Guarantee a cavern touching or within 4 tiles of the stargate room
        GuaranteeStargateCavern(map, preserveRect, cavernCells);

        // 5. Populate the caverns
        EnforceSolidRockEdge(map, 5);
        PlantCavernFlora(map, cavernCells);
        ScatterRichOreDeposits(map);
        PlaceCavernWallVeins(map, cavernCells);

        map.GetComponent<MapComponent_SealedFromSky>().isSealed = true;
    }

    /// Destroys all destroyable things in a cell, except pawns.
    /// Iterates backward for safe removal during enumeration.
    private static void DestroyThingsInCell(Map map, IntVec3 cell)
    {
        // Iterating backward through the original list is a more performant way to handle
        // object destruction during map generation.
        var things = map.thingGrid.ThingsListAt(cell);
        for (int i = things.Count - 1; i >= 0; i--)
        {
            var thing = things[i];
            if (thing is Pawn) continue;
            if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static void GenerateMountainTerrain(Map map, CellRect preserveRect)
    {
        ThingDef primaryRock = DefDatabase<ThingDef>.GetNamed(RockTypes[Rand.Range(0, RockTypes.Length)]);
        ThingDef secondaryRock = DefDatabase<ThingDef>.GetNamed(RockTypes[Rand.Range(0, RockTypes.Length)]);
        if (primaryRock == null)
        {
            Log.Warning("BetterRimworlds.Stargate: No rock defs found, skipping mountain fill.");
            return;
        }

        TerrainDef underlayStone = TerrainDefOf.Gravel;

        // Phase 1: fill the map with solid rock + thick stone roof.
        foreach (IntVec3 cell in map.AllCells)
        {
            if (preserveRect.Contains(cell)) continue;

            DestroyThingsInCell(map, cell);

            map.terrainGrid.SetTerrain(cell, underlayStone);

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

    private static void GeneratePerlinCaverns(Map map, CellRect preserveRect, List<IntVec3> outCells)
    {
        float offsetX = Rand.Range(0f, 10000f);
        float offsetZ = Rand.Range(0f, 10000f);

        // We will calculate the distance from the stargate to boost nearby noise
        IntVec3 stargateCenter = preserveRect.CenterCell;
        const float boostRadius    = 18f;                       // Tiles from stargate to apply the noise boost
        const float boostRadiusSq  = boostRadius * boostRadius; // Compare squared distances to skip the sqrt
        const float invBoostRadius = 1f / boostRadius;          // Precomputed reciprocal avoids a per-cell divide

        foreach (IntVec3 cell in map.AllCells)
        {
            if (preserveRect.Contains(cell)) continue;
            if (IsInEdgeBand(cell, map, 5)) continue;

            float noise = Mathf.PerlinNoise(
                (cell.x + offsetX) * CavernFrequency,
                (cell.z + offsetZ) * CavernFrequency
            );

            // Proximity Boost: Force noise higher near the stargate so it connects out
            // Squared-distance cull: only cells inside the boost circle pay for the sqrt
            int dx = cell.x - stargateCenter.x;
            int dz = cell.z - stargateCenter.z;
            int distSq = dx * dx + dz * dz;
            if (distSq < boostRadiusSq)
            {
                // Smoothly boost noise from +0.2 right next to the stargate, fading to +0 at the edge
                float boost = 0.2f * (1f - Mathf.Sqrt(distSq) * invBoostRadius);
                noise += boost;
            }

            if (noise > CavernThreshold)
            {
                Thing rock = map.thingGrid.ThingsListAt(cell)
                    .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);

                if (rock != null)
                {
                    rock.Destroy(DestroyMode.Vanish);
                }

                map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);
                outCells.Add(cell);
            }
        }
    }

    private static void EnforceSolidRockEdge(Map map, int edgeBand)
    {
        ThingDef edgeRock = DefDatabase<ThingDef>.GetNamed(RockTypes[Rand.Range(0, RockTypes.Length)]);
        if (edgeRock == null)
        {
            Log.Warning("BetterRimworlds.Stargate: No rock defs found, skipping edge rock band.");
            return;
        }

        TerrainDef underlayStone = TerrainDefOf.Soil;

        foreach (IntVec3 cell in map.AllCells)
        {
            if (!IsInEdgeBand(cell, map, edgeBand)) continue;

            DestroyThingsInCell(map, cell);

            map.terrainGrid.SetTerrain(cell, underlayStone);
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

    /// Carves a starter cavern that opens directly at the stargate room's door,
    /// plus a connector corridor through the solid rock so connectivity is
    /// deterministic instead of depending on Perlin noise opening the gap.
    private static void GuaranteeStargateCavern(Map map, CellRect preserveRect, List<IntVec3> cavernCells)
    {
        // Open the cavern mouth at the stargate's door. Carving the 5x5 so its inner edge sits
        // adjacent to the door makes connectivity geometric, not Perlin-dependent. No corridor needed.
        Building_Door door = preserveRect.Cells
            .Where(c => c.InBounds(map))
            .Select(c => c.GetEdifice(map) as Building_Door)
            .FirstOrDefault(d => d != null);

        IntVec3 delta = (door?.Position ?? preserveRect.CenterCell) - preserveRect.CenterCell;
        IntVec3 dir = (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            ? new IntVec3(delta.x >= 0 ? 1 : -1, 0, 0)
            : new IntVec3(0, 0, delta.z >= 0 ? 1 : -1);

        // Mouth = the door cell, or a perimeter cell if the door isn't built yet at this gen stage.
        IntVec3 mouth = door?.Position ?? preserveRect.CenterCell + dir * (RoomSize / 2 + 2);

        // Inner edge lands one cell outside the mouth -> cardinally adjacent -> guaranteed contact.
        IntVec3 cavernCenter = mouth + dir * 3;
        CellRect starterCavern = new CellRect(cavernCenter.x - 2, cavernCenter.z - 2, 5, 5);
        starterCavern.ClipInsideMap(map);

        Thing sampleRock = map.thingGrid.ThingsListAt(starterCavern.CenterCell)
            .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);
        TerrainDef cavernFloor = GetCavernFloorTerrain(sampleRock?.def);

        foreach (IntVec3 cell in starterCavern.Cells)
        {
            map.thingGrid.ThingsListAt(cell)
                .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock)
                ?.Destroy(DestroyMode.Vanish);

            map.terrainGrid.SetTerrain(cell, cavernFloor);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);
            if (!cavernCells.Contains(cell)) cavernCells.Add(cell);
        }
    }

    private static void PlantCavernFlora(Map map, List<IntVec3> cavernCells)
    {
        ThingDef glowstool = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Glowstool");
        ThingDef agarilux  = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Agarilux");
        ThingDef bryolux   = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Bryolux");

        List<ThingDef> palette = new List<ThingDef>();
        if (glowstool != null) palette.Add(glowstool);
        if (agarilux  != null) palette.Add(agarilux);
        if (bryolux   != null) palette.Add(bryolux);

        if (palette.Count == 0) return;

        // Look up the primary rock type for smarter terrain selection
        ThingDef primaryRock = DefDatabase<ThingDef>.GetNamed(RockTypes[Rand.Range(0, RockTypes.Length)]);
        TerrainDef cavernFloor = GetCavernFloorTerrain(primaryRock);

        foreach (IntVec3 cell in cavernCells)
        {
            if (!cell.InBounds(map)) continue;
            if (!cell.Walkable(map)) continue;

            // Mushrooms need a growable terrain. Gravel works in vanilla 1.2.
            map.terrainGrid.SetTerrain(cell, cavernFloor);

            if (!Rand.Chance(0.65f)) continue; // ~65% density — clusters, not carpet

            ThingDef plantDef = palette.RandomElement();
            Plant plant = (Plant)ThingMaker.MakeThing(plantDef);
            plant.Growth = Rand.Range(0.45f, 1.0f);
            GenSpawn.Spawn(plant, cell, map, WipeMode.Vanish);
        }
    }

    private static void ScatterRichOreDeposits(Map map)
    {
        // countPer10kCells is roughly 2-4x vanilla density.
        // Vanilla steel is ~10, gold ~2-3.
        ScatterOre(map, "MineableSteel",               10);
        ScatterOre(map, "MineableSilver",              16);
        ScatterOre(map, "MineableGold",                10);
        ScatterOre(map, "MineableUranium",              8);
        ScatterOre(map, "MineablePlasteel",            15);
        ScatterOre(map, "MineableJade",                 4);
        ScatterOre(map, "MineableComponentsIndustrial", 8);
    }

    private static void ScatterOre(Map map, string defName, int countPer10kCells)
    {
        ThingDef oreDef = TryGetOre(defName);
        if (oreDef == null) return;

        var scatter = new GenStep_ScatterLumpsMineable();
        scatter.forcedDefToScatter   = oreDef;
        scatter.countPer10kCellsRange = new FloatRange(countPer10kCells, countPer10kCells);
        scatter.Generate(map, new GenStepParams());
    }

    private static void PlaceCavernWallVeins(Map map, List<IntVec3> cavernCells)
    {
        // Weighted palette: common metals more likely, rare materials sparse.
        // These are the veins that reward carving into the cavern walls;
        // ScatterRichOreDeposits already handled the bulk-rock distribution.
        var oreOptions = new (string defName, float weight)[]
        {
            ("MineableSteel",                          3.0f),
            ("MineableSilver",                         2.0f),
            ("MineableGold",                           1.0f),
            ("MineablePlasteel",                       1.0f),
            ("MineableUranium",                        1.0f),
            ("MineableJade",                           0.8f),
            ("MineableComponentIndustrialScattered",   0.6f),
        };

        var palette = new List<(ThingDef def, float weight)>();
        foreach (var (name, weight) in oreOptions)
        {
            ThingDef d = TryGetOre(name);
            if (d != null) palette.Add((d, weight));
        }
        if (palette.Count == 0) return;

        float totalWeight = 0f;
        foreach (var entry in palette) totalWeight += entry.weight;

        // Walk every cavern cell, find rock walls touching it, swap a fraction.
        // ~25% conversion rate: dense enough to feel like a deliberate vein system,
        // sparse enough that the caverns don't read as ore galleries.
        HashSet<IntVec3> processed = new HashSet<IntVec3>();
        foreach (IntVec3 cavernCell in cavernCells)
        {
            foreach (IntVec3 dir in GenAdj.CardinalDirections)
            {
                IntVec3 wall = cavernCell + dir;
                if (!wall.InBounds(map)) continue;
                if (!processed.Add(wall)) continue;

                Thing rock = map.thingGrid.ThingsListAt(wall)
                    .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);
                if (rock == null) continue;

                if (!Rand.Chance(0.25f)) continue;

                ThingDef oreDef = WeightedPickOre(palette, totalWeight);
                rock.Destroy(DestroyMode.Vanish);
                GenSpawn.Spawn(ThingMaker.MakeThing(oreDef), wall, map, WipeMode.Vanish);
            }
        }
    }

    private static ThingDef WeightedPickOre(List<(ThingDef def, float weight)> palette, float totalWeight)
    {
        float roll = Rand.Range(0f, totalWeight);
        float acc  = 0f;
        foreach (var (def, weight) in palette)
        {
            acc += weight;
            if (roll <= acc) return def;
        }

        return palette[palette.Count - 1].def;
    }

    /// Returns a contextually appropriate cavern floor terrain.
    /// If the underlying rock is Sandstone, has a 20% chance of sandy terrain.
    /// Otherwise randomly picks between Soil and Gravel.
    private static TerrainDef GetCavernFloorTerrain(ThingDef underlyingRock)
    {
        // Sandstone  get a chance for sandy terrain
        if (underlyingRock != null
            && underlyingRock.defName.Contains("Sandstone")
            && Rand.Chance(0.2f))
        {
            TerrainDef sand = DefDatabase<TerrainDef>.GetNamed("Sand");
            if (sand != null) return sand;
        }

        // Mix of Soil and Gravel, randomly
        return Rand.Chance(0.5f) ? TerrainDefOf.Soil : TerrainDefOf.Gravel;
    }

    /// Attempts to retrieve a ThingDef by name, returning null if not found.
    private static ThingDef TryGetOre(string defName)
    {
        return DefDatabase<ThingDef>.GetNamed(defName);
    }
}