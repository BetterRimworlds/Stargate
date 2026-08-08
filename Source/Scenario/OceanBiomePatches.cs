// ==== Source/Scenario/OceanBiomePatches.cs ====
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

/// <summary>
/// Ocean (and Lake) tiles have an empty wild-plant set. Vanilla trees set
/// <c>mustBeWildToSow</c>, so <see cref="Command_SetPlantToGrow.IsPlantAvailable"/>
/// hides them unless the map biome reports the plant with commonality &gt; 0 via
/// <c>WildPlantSpawner.AllWildPlants</c>.
///
/// Register oak on those biomes through <see cref="PlantProperties.wildBiomes"/>
/// (public on all supported RimWorld versions; <c>BiomeDef.wildPlants</c> is private
/// before 1.6) so Atlantis garden soil can grow trees after TreeSowing is researched.
/// </summary>
[StaticConstructorOnStartup]
public static class OceanBiomePatches
{
    private const string OakDefName = "Plant_TreeOak";

    // Modest commonality: enough for sow-availability; low enough that any dry
    // cells on ocean maps do not become dense wild forests.
    private const float OakCommonality = 0.5f;

    private static readonly string[] WaterBiomeDefNames = { "Ocean", "Lake" };

    private static readonly string[] BiomePlantCacheFields =
    {
        "cachedPlantCommonalities",
        "cachedWildPlants",
        "cachedLowestWildPlantOrder",
        "cachedMaxWildPlantsClusterRadius",
        "cachedPlantCommonalitiesSum",
    };

    static OceanBiomePatches()
    {
        ThingDef treeOak = DefDatabase<ThingDef>.GetNamedSilentFail(OakDefName);
        if (treeOak?.plant == null)
        {
            Log.Warning("BetterRimworlds.Stargate: Plant_TreeOak not found; Ocean tree-sowing patch skipped.");
            return;
        }

        if (treeOak.plant.wildBiomes == null)
        {
            treeOak.plant.wildBiomes = new List<PlantBiomeRecord>();
        }

        int patched = 0;
        foreach (string biomeName in WaterBiomeDefNames)
        {
            BiomeDef biome = DefDatabase<BiomeDef>.GetNamedSilentFail(biomeName);
            if (biome == null) continue;

            if (TryRegisterPlantForBiome(treeOak, biome, OakCommonality))
            {
                InvalidateBiomePlantCaches(biome);
                patched++;
            }
        }

        if (patched > 0)
        {
            Log.Message($"BetterRimworlds.Stargate: Enabled oak tree sowing on {patched} water biome(s).");
        }
    }

    private static bool TryRegisterPlantForBiome(ThingDef plant, BiomeDef biome, float commonality)
    {
        List<PlantBiomeRecord> wildBiomes = plant.plant.wildBiomes;
        for (int i = 0; i < wildBiomes.Count; i++)
        {
            if (wildBiomes[i].biome == biome)
            {
                return false;
            }
        }

        wildBiomes.Add(new PlantBiomeRecord
        {
            biome = biome,
            commonality = commonality,
        });
        return true;
    }

    private static void InvalidateBiomePlantCaches(BiomeDef biome)
    {
        // BiomeDef keeps private [Unsaved] plant caches. Clear them so the next
        // AllWildPlants / CommonalityOfPlant access rebuilds from wildBiomes.
        // Reference/nullable fields accept null; non-nullable value types (e.g.
        // float cachedPlantCommonalitiesSum on RW 1.2–1.5) need a default.
        foreach (string fieldName in BiomePlantCacheFields)
        {
            FieldInfo field = AccessTools.Field(typeof(BiomeDef), fieldName);
            if (field == null) continue;

            object value = null;
            if (field.FieldType.IsValueType && Nullable.GetUnderlyingType(field.FieldType) == null)
            {
                value = Activator.CreateInstance(field.FieldType);
            }

            try
            {
                field.SetValue(biome, value);
            }
            catch (Exception e)
            {
                Log.Warning($"BetterRimworlds.Stargate: Failed to clear BiomeDef.{fieldName}: {e.Message}");
            }
        }
    }
}
