// ==== Source/Scenario/StargateDestinationMapGen.Cavern.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
    // Lowered threshold: 0.80 means only the top ~20% of noise values become caverns.
    private const float CavernThreshold = 0.55f;
    private const float CavernFrequency = 0.06f;

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
        GenerateMountainTerrain(map);

        // 2. Clear the preserved area for the stargate room
        ClearPreservedArea(map, preserveRect);

        // 3. Generate the secret room
        CellRect secretRoomRect = GenerateSecretBuriedRoom(map, preserveRect);

        // 4. Carve out the caverns (now with proximity boost & guaranteed starter cavern)
        List<IntVec3> cavernCells = new List<IntVec3>();
        GeneratePerlinCaverns(map, preserveRect, secretRoomRect, cavernCells);

        // 5. Guarantee a cavern touching or within 4 tiles of the stargate room
        GuaranteeStargateCavern(map, preserveRect, cavernCells);

        // 6. Populate the caverns
        EnforceSolidRockEdge(map, 5);
        PlantCavernFlora(map, cavernCells);
        ScatterRichOreDeposits(map);
        PlaceCavernWallVeins(map, cavernCells);

        map.GetComponent<MapComponent_SealedFromSky>().isSealed = true;
    }

    private static void GenerateMountainTerrain(Map map)
    {
        string[] rockTypes = { "Granite", "Limestone", "Sandstone", "Marble", "Slate" };
        ThingDef primaryRock = DefDatabase<ThingDef>.GetNamedSilentFail(rockTypes[Rand.Range(0, rockTypes.Length)]);
        ThingDef secondaryRock = DefDatabase<ThingDef>.GetNamedSilentFail(rockTypes[Rand.Range(0, rockTypes.Length)]);
        if (primaryRock == null)
        {
            Log.Warning("BetterRimworlds.Stargate: No rock defs found, skipping mountain fill.");
            return;
        }

        TerrainDef underlayStone = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                                   ?? TerrainDefOf.Soil;

        // Phase 1: fill the map with solid rock + thick stone roof.
        foreach (IntVec3 cell in map.AllCells)
        {
            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
            }

            map.terrainGrid.SetTerrain(cell, underlayStone);

            ThingDef rockDef = Rand.Chance(0.85f) ? primaryRock : (secondaryRock ?? primaryRock);
            GenSpawn.Spawn(ThingMaker.MakeThing(rockDef), cell, map, WipeMode.Vanish);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
        }
    }

    private static void ClearPreservedArea(Map map, CellRect preserveRect)
    {
        TerrainDef underlayStone = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel") 
                                   ?? TerrainDefOf.Soil;

        foreach (IntVec3 cell in preserveRect.Cells)
        {
            if (!cell.InBounds(map)) continue;

            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy();
            }

            map.terrainGrid.SetTerrain(cell, underlayStone);
            map.roofGrid.SetRoof(cell, null);
        }
    }

    private static void GeneratePerlinCaverns(Map map, CellRect preserveRect, CellRect secretRoomRect, List<IntVec3> outCells)
    {
        float offsetX = Rand.Range(0f, 10000f);
        float offsetZ = Rand.Range(0f, 10000f);

        // We will calculate the distance from the stargate to boost nearby noise
        IntVec3 stargateCenter = preserveRect.CenterCell;
        float boostRadius = 18f; // Tiles from stargate to apply the noise boost

        foreach (IntVec3 cell in map.AllCells)
        {
            if (preserveRect.Contains(cell)) continue;
            if (secretRoomRect.ExpandedBy(1).Contains(cell)) continue;
            if (IsInEdgeBand(cell, map, 5)) continue;

            float noise = Mathf.PerlinNoise(
                (cell.x + offsetX) * CavernFrequency, 
                (cell.z + offsetZ) * CavernFrequency
            );

            // Proximity Boost: Force noise higher near the stargate so it connects out
            float distance = cell.DistanceTo(stargateCenter);
            if (distance < boostRadius)
            {
                // Smoothly boost noise from +0.2 right next to the stargate, fading to +0 at the edge
                float boost = Mathf.Lerp(0.2f, 0f, distance / boostRadius);
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
        string[] rockTypes = { "Granite", "Limestone", "Sandstone", "Marble", "Slate" };
        ThingDef edgeRock = DefDatabase<ThingDef>.GetNamedSilentFail(rockTypes[Rand.Range(0, rockTypes.Length)]);
        if (edgeRock == null)
        {
            Log.Warning("BetterRimworlds.Stargate: No rock defs found, skipping edge rock band.");
            return;
        }

        TerrainDef underlayStone = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                                   ?? TerrainDefOf.Soil;

        foreach (IntVec3 cell in map.AllCells)
        {
            if (!IsInEdgeBand(cell, map, edgeBand)) continue;

            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
            }

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

    /// <summary>
    /// Carves a small cavern right next to the stargate room to guarantee connectivity.
    /// </summary>
    private static void GuaranteeStargateCavern(Map map, CellRect preserveRect, List<IntVec3> cavernCells)
    {
        // Pick a random cardinal direction to place the starter cavern
        IntVec3 dir = GenAdj.CardinalDirections[Rand.Range(0, 4)];
        
        // Position the starter cavern adjacent to the preserve rect
        IntVec3 cavernCenter = preserveRect.CenterCell + (dir * (RoomSize / 2 + 6));

        // Carve a 5x5 cavern
        CellRect starterCavern = new CellRect(cavernCenter.x - 2, cavernCenter.z - 2, 5, 5);
        starterCavern.ClipInsideMap(map);

        TerrainDef cavernFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                                 ?? TerrainDefOf.Soil;

        foreach (IntVec3 cell in starterCavern.Cells)
        {
            Thing rock = map.thingGrid.ThingsListAt(cell)
                .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);

            if (rock != null)
            {
                rock.Destroy(DestroyMode.Vanish);
            }

            map.terrainGrid.SetTerrain(cell, cavernFloor);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);

            if (!cavernCells.Contains(cell))
            {
                cavernCells.Add(cell);
            }
        }
    }

    private static CellRect GenerateSecretBuriedRoom(Map map, CellRect baseRect)
    {
        string[] rockTypes = {
            "Granite", "Limestone", "Sandstone", "Marble", "Slate"
        };
        ThingDef wallRockDef = DefDatabase<ThingDef>.GetNamedSilentFail(rockTypes[Rand.Range(0, rockTypes.Length)]);
        if (wallRockDef == null)
        {
            Log.Warning("BetterRimworlds.Stargate: No rock defs found, skipping secret room wall gen.");
            return new CellRect();
        }

        int roomWidth = 10;
        int roomHeight = 5;
        bool placeNorth = Rand.Chance(0.5f);

        int startX = baseRect.CenterCell.x - (roomWidth / 2);
        int startZ;

        if (placeNorth)
        {
            startZ = baseRect.maxZ + 3; 
        }
        else
        {
            startZ = baseRect.minZ - 3 - roomHeight;
        }

        CellRect secretRoomRect = new CellRect(startX, startZ, roomWidth, roomHeight);
        secretRoomRect.ClipInsideMap(map);

        foreach (IntVec3 cell in secretRoomRect.Cells)
        {
            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy();
            }

            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
        }

        foreach (IntVec3 cell in secretRoomRect.ExpandedBy(1).Cells)
        {
            if (secretRoomRect.Contains(cell)) continue;
            if (!cell.InBounds(map)) continue;
            
            Thing rock = map.thingGrid.ThingsListAt(cell)
                .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);
            
            if (rock != null) rock.Destroy(DestroyMode.Vanish);

            Thing wall = ThingMaker.MakeThing(wallRockDef);
            GenSpawn.Spawn(wall, cell, map, WipeMode.Vanish);
        }

        IntVec3 tablePos = secretRoomRect.CenterCell;
        ThingDef researchDef = DefDatabase<ThingDef>.GetNamedSilentFail("HiTechResearchBench");
        if (researchDef != null)
        {
            Thing table = ThingMaker.MakeThing(researchDef);
            GenSpawn.Spawn(table, tablePos, map, WipeMode.Vanish);
        }

        ThingDef advComponent = DefDatabase<ThingDef>.GetNamedSilentFail("ComponentAdvanced");
        ThingDef component    = DefDatabase<ThingDef>.GetNamedSilentFail("ComponentIndustrial");
        ThingDef meals        = DefDatabase<ThingDef>.GetNamedSilentFail("MealSurvivalPack");

        foreach (IntVec3 cell in secretRoomRect.Cells)
        {
            if (cell == tablePos) continue;

            float roll = Rand.Value;

            if (roll < 0.25f && advComponent != null)
            {
                Thing loot = ThingMaker.MakeThing(advComponent);
                loot.stackCount = Rand.Range(5, 15);
                GenSpawn.Spawn(loot, cell, map, WipeMode.Vanish);
            }
            else if (roll < 0.60f && component != null)
            {
                Thing loot = ThingMaker.MakeThing(component);
                loot.stackCount = Rand.Range(15, 30);
                GenSpawn.Spawn(loot, cell, map, WipeMode.Vanish);
            }
            else if (roll < 0.85f && meals != null)
            {
                Thing loot = ThingMaker.MakeThing(meals);
                loot.stackCount = Rand.Range(20, 50);
                GenSpawn.Spawn(loot, cell, map, WipeMode.Vanish);
            }
        }

        return secretRoomRect;
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

        TerrainDef cavernFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                                 ?? TerrainDefOf.Soil;

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
        ScatterOre(map, "MineableSteel",      10);
        ScatterOre(map, "MineableSilver",     16);
        ScatterOre(map, "MineableGold",       10);
        ScatterOre(map, "MineableUranium",     8);
        ScatterOre(map, "MineablePlasteel",    15);
        ScatterOre(map, "MineableJade",        4);
        ScatterOre(map, "MineableComponentIndustrialScattered", 8);
    }

    private static void ScatterOre(Map map, string defName, int countPer10kCells)
    {
        ThingDef oreDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (oreDef == null)
        {
            Log.Warning($"BetterRimworlds.Stargate: Ore def '{defName}' not found, skipping.");
            return;
        }

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
            ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
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
}
