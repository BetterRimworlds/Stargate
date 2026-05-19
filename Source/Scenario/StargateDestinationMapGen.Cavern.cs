// ==== Source/Scenario/StargateDestinationMapGen.Cavern.cs ====
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
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

        // One dominant rock type per map matches how vanilla impassable maps look.
        string[] rockTypes = {
            "Granite", "Limestone", "Sandstone", "Marble", "Slate"
        };
        ThingDef primaryRock   = DefDatabase<ThingDef>.GetNamedSilentFail(rockTypes[Rand.Range(0, rockTypes.Length)]);
        ThingDef secondaryRock = DefDatabase<ThingDef>.GetNamedSilentFail(rockTypes[Rand.Range(0, rockTypes.Length)]);
        if (primaryRock == null)
        {
            Log.Warning("BetterRimworlds.Stargate: No rock defs found, skipping mountain gen.");
            return;
        }

        RoofDef     thickRoof      = RoofDefOf.RoofRockThick;
        TerrainDef  underlayStone  = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                                     ?? TerrainDefOf.Soil;

        int impassableCount = 0;
        int notImpassableCount = 0;

        // Phase 1: fill the map with solid rock + thick stone roof.
        foreach (IntVec3 cell in map.AllCells)
        {
            if (preserveRect.Contains(cell)) continue;

            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy();
            }

            map.terrainGrid.SetTerrain(cell, underlayStone);

            ThingDef rockDef = Rand.Chance(0.85f) ? primaryRock : (secondaryRock ?? primaryRock);
            Thing rockThing = ThingMaker.MakeThing(rockDef);
            GenSpawn.Spawn(rockThing, cell, map, WipeMode.Vanish);

            map.roofGrid.SetRoof(cell, thickRoof);

            if (rockThing.def.passability != Traversability.Impassable)
            {
                Log.Error(
                    $"BetterRimworlds.Stargate: Tile {cell} is NOT IMPASSABLE after spawning. " +
                    $"Def: {rockThing.def.defName}, Passability: {rockThing.def.passability}"
                );
                notImpassableCount++;
            }
            else
            {
                impassableCount++;
            }
        }

        Log.Message(
            $"BetterRimworlds.Stargate: Phase 1 Impassable Check Summary - " +
            $"Impassable: {impassableCount}, Not Impassable: {notImpassableCount}"
        );

        // Phase 2: carve a handful of small caverns. Small on purpose — you wanted
        // no major open spaces. Each cavern is 6–14 cells.
        int cavernCount = Rand.Range(5, 9);
        List<IntVec3> cavernCells = new List<IntVec3>();
        for (int i = 0; i < cavernCount; i++)
        {
            IntVec3 seed = CellFinder.RandomCell(map);
            if (preserveRect.ExpandedBy(3).Contains(seed)) continue;

            int budget = Rand.Range(6, 15);
            CarveCavern(map, seed, budget, cavernCells);
        }

        // Phase 3: plant cavern flora in the carved cells.
        PlantCavernFlora(map, cavernCells);

        // Phase 4: re-scatter ore into the remaining rock.
        ScatterRichOreDeposits(map);
        
        // Phase 5: guaranteed veins on the cavern walls themselves.
        // Runs after the general scatter so it overrides plain rock that the
        // scatter pass missed — every cavern reliably has something worth mining.
        PlaceCavernWallVeins(map, cavernCells);

        // Mark this map as sealed from the sky so airdrop incidents are blocked.
        map.GetComponent<MapComponent_SealedFromSky>().isSealed = true;
    }

    private static void CarveCavern(Map map, IntVec3 seed, int budget, List<IntVec3> outCells)
    {
        Queue<IntVec3> frontier = new Queue<IntVec3>();
        HashSet<IntVec3> visited = new HashSet<IntVec3>();
        frontier.Enqueue(seed);

        while (frontier.Count > 0 && budget > 0)
        {
            IntVec3 cell = frontier.Dequeue();
            if (!cell.InBounds(map)) continue;
            if (visited.Contains(cell)) continue;
            visited.Add(cell);

            // Remove the rock at this cell.
            Thing rock = map.thingGrid.ThingsListAt(cell)
                .FirstOrDefault(t => t.def.building != null && t.def.building.isNaturalRock);
            if (rock == null) continue;

            rock.Destroy(DestroyMode.Vanish);

            // Thin roof so the cavern reads as a small natural void, not deep mountain.
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);
            outCells.Add(cell);
            budget--;

            // Spread to neighbors with some randomness — produces organic shapes.
            foreach (IntVec3 dir in GenAdj.CardinalDirections)
            {
                if (Rand.Chance(0.55f)) frontier.Enqueue(cell + dir);
            }
        }
    }

    private static void PlantCavernFlora(Map map, List<IntVec3> cavernCells)
    {
        // Glowstool is vanilla. Agarilux/Bryolux are Royalty (present in 1.2+).
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
        ScatterOre(map, "MineableSteel",      25);
        ScatterOre(map, "MineableSilver",     16);
        ScatterOre(map, "MineableGold",       10);
        ScatterOre(map, "MineableUranium",     8);
        ScatterOre(map, "MineablePlasteel",    6);
        ScatterOre(map, "MineableJade",        4);
        ScatterOre(map, "MineableComponentIndustrialScattered", 5);
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