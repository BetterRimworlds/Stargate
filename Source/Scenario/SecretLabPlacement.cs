// ==== Source/Scenario/SecretLabPlacement.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using BetterRimworlds.Utilities;

namespace BetterRimworlds.Stargate;

internal static class SecretLabPlacement
{
    // Reference layout dimensions (East orientation, vertical room).
    // All other sides are pure rotations of this layout around the room's centre.
    private const int RefWidth  = 7;
    private const int RefHeight = 10;

    public static void AddSecretLab(Map map, CellRect secretRoomRect, CellRect interior, Rot4 side, bool vertical)
    {
        BlueprintSpawner spawner = new BlueprintSpawner(map);
        spawner.ConfigureLayout(interior, side, RefWidth, RefHeight);

        ThingDef researchDef        = DefDatabase<ThingDef>.GetNamed("HiTechResearchBench");
        ThingDef armchairDef        = DefDatabase<ThingDef>.GetNamed("Armchair");
        ThingDef devilstrandDef     = DefDatabase<ThingDef>.GetNamed("DevilstrandCloth");
        ThingDef analyzerDef        = DefDatabase<ThingDef>.GetNamed("MultiAnalyzer");
        ThingDef lampDef            = ThingDefOf.StandingLamp;
        ThingDef vanoDef            = DefDatabase<ThingDef>.GetNamed("VanometricPowerCell");
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

        spawner.SpawnFixed(vanoDef,            spawner.At(2, 9), spawner.Rot(Rot4.East), interior);

        spawner.SpawnFixed(researchDef,        spawner.At(5, 5), spawner.Rot(Rot4.East), interior);
        spawner.SpawnFixed(analyzerDef,        spawner.At(5, 8), spawner.Rot(Rot4.North), interior);
        spawner.SpawnFixed(armchairDef,        spawner.At(4, 5), spawner.Rot(Rot4.East), interior, devilstrandDef, QualityCategory.Legendary);

        spawner.SpawnFixed(cryoDef,            spawner.At(6, 0), spawner.Rot(Rot4.West), interior);
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

    public static bool ContainsThingOfDef(Map map, IntVec3 cell, ThingDef def)
    {
        return map.thingGrid.ThingsListAt(cell).Any(t => t.def == def);
    }
}
