// ==== Source/Scenario/ScenPart_StargateFacility.Structure.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility
{
    private Pawn _guardianPawn;
    private Rot4 _entranceSide;

    private void GenerateRoomStructure(Map map, CellRect innerRect, CellRect outerRect)
    {
        string[] stoneTypes = { "BlocksGranite", "BlocksLimestone", "BlocksSlate" };
        ThingDef wallMaterial = DefDatabase<ThingDef>.GetNamed(stoneTypes[Rand.Range(0, stoneTypes.Length)]);
        TerrainDef floorDef = TerrainDefOf.Concrete;

        _entranceSide = Rand.Element(Rot4.North, Rot4.South, Rot4.East, Rot4.West);

        IntVec3 innerDoorCell = GetCenteredEdgeCell(innerRect, _entranceSide);
        IntVec3 outerDoorCell = GetCenteredEdgeCell(outerRect, _entranceSide);
        IntVec3 outsideApproachCell = outerDoorCell + _entranceSide.FacingCell;

        // Floor the interior.
        // Mountain roof is intentionally preserved if present.
        foreach (IntVec3 cell in innerRect.Cells)
        {
            if (!cell.InBounds(map)) continue;

            map.terrainGrid.SetTerrain(cell, floorDef);
        }

        // Build OUTER wall layer.
        foreach (IntVec3 cell in outerRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;

            // The outer wall doorway must stay open.
            // The actual door goes on the inner wall.
            if (cell == outerDoorCell) continue;

            ClearCellForBuilding(map, cell);
            PlaceClaimed(map, ThingDefOf.Wall, cell, wallMaterial);
        }

        // Build INNER wall layer.
        foreach (IntVec3 cell in innerRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;

            // The inner wall receives the actual single centered door.
            if (cell == innerDoorCell) continue;

            ClearCellForBuilding(map, cell);
            PlaceClaimed(map, ThingDefOf.Wall, cell, wallMaterial);
        }

        // Doorway cells are sacred.
        // Clear the inner wall door cell, the outer wall passage cell,
        // and one exterior approach cell so no natural rock wall can block access.
        ClearCellForDoorway(map, innerDoorCell);
        ClearCellForDoorway(map, outerDoorCell);
        ClearCellForDoorway(map, outsideApproachCell);

        // Exactly one door, centered, randomly oriented N/S/E/W by entrance side.
        // Placing the door on the inner wall preserves the 2-thick ancient wall look
        // while preventing the outer wall from blocking it.
        PlaceDoor(map, innerDoorCell, wallMaterial);
    }

    private void PlaceStargate(Map map, IntVec3 center)
    {
        ThingDef stargateDef = DefDatabase<ThingDef>.GetNamedSilentFail("Stargate");

        if (stargateDef == null)
        {
            Log.Warning("BetterRimworlds.Stargate: Stargate ThingDef not found.");
            return;
        }

        CellRect stargateRect = new CellRect(center.x - 1, center.z - 1, 3, 3);

        // CRITICAL:
        // Remove ANY steam geysers in the stargate area and immediate vicinity.
        foreach (IntVec3 cell in stargateRect.ExpandedBy(2).Cells)
        {
            if (!cell.InBounds(map)) continue;

            List<Thing> things = map.thingGrid.ThingsListAt(cell).ToList();
            foreach (Thing thing in things)
            {
                if (thing is Pawn) continue;

                if (thing.def == ThingDefOf.SteamGeyser)
                {
                    thing.Destroy();
                }
                else if (thing.def.destroyable && thing.def != ThingDefOf.Wall)
                {
                    thing.Destroy();
                }
            }
        }

        Thing stargate = PlaceClaimed(map, stargateDef, center);
        if (stargate != null)
        {
            CompPowerTrader powerComp = stargate.TryGetComp<CompPowerTrader>();
            if (powerComp != null)
            {
                powerComp.PowerOn = true;
            }
        }
    }

    private void PlaceSupportEquipment(Map map, IntVec3 center, CellRect roomRect)
    {
        // 1. Vanometric Power Cell (Royalty) - mandatory power source.
        ThingDef vanoDef = DefDatabase<ThingDef>.GetNamedSilentFail("VanometricPowerCell");
        if (vanoDef != null)
        {
            IntVec3 vanoPos = new IntVec3(roomRect.maxX - 2, 0, roomRect.minZ + 2);
            if (vanoPos.InBounds(map))
            {
                ClearCellForBuilding(map, vanoPos);
                PlaceClaimed(map, vanoDef, vanoPos);
            }
        }

        // 2. Archotech ZPM at 75% charge, if mod present.
        ThingDef zpmDef = DefDatabase<ThingDef>.GetNamedSilentFail("ArchotechZPM");
        if (zpmDef != null)
        {
            IntVec3 zpmPos = new IntVec3(roomRect.minX + 2, 0, roomRect.maxZ - 2);
            if (zpmPos.InBounds(map))
            {
                ClearCellForBuilding(map, zpmPos);

                Thing zpm = PlaceClaimed(map, zpmDef, zpmPos);
                if (zpm != null)
                {
                    CompPowerBattery batteryComp = zpm.TryGetComp<CompPowerBattery>();
                    if (batteryComp != null)
                    {
                        batteryComp.SetStoredEnergyPct(0.75f);
                    }
                }
            }
        }

        // 3. DHD placement.
        ThingDef dhdDef = DefDatabase<ThingDef>.GetNamedSilentFail("StargateDHD");
        if (dhdDef != null)
        {
            IntVec3 dhdPos = center + new IntVec3(3, 0, 0);
            if (dhdPos.InBounds(map) && dhdPos.Walkable(map))
            {
                ClearCellForBuilding(map, dhdPos);
                PlaceClaimed(map, dhdDef, dhdPos);
            }
        }

        // 4. The Guardian's Casket
        IntVec3 casketPos = center + new IntVec3(-4, 0, 0);
        if (casketPos.InBounds(map))
        {
            ClearCellForBuilding(map, casketPos);

            Building_CryptosleepCasket casket = (Building_CryptosleepCasket)PlaceClaimed(
                map, ThingDefOf.AncientCryptosleepCasket, casketPos, rotation: Rot4.East
            );

            _guardianPawn = GetGuardianPawn();
            if (_guardianPawn != null && casket != null)
            {
                casket.TryAcceptThing(_guardianPawn, false);
            }
        }
        
    }

    private Pawn GetGuardianPawn()
    {
        if (Find.GameInitData == null || Find.GameInitData.startingAndOptionalPawns == null)
        {
            return null;
        }

        return Find.GameInitData.startingAndOptionalPawns.FirstOrDefault();
    }

    private void PlaceDoor(Map map, IntVec3 cell, ThingDef material)
    {
        // We use ClearCellForBuilding instead of ClearCellForDoorway
        // to ensure it clears the concrete floor too, if you don't want it under the wall.
        ClearCellForBuilding(map, cell);

        // NOTE:
        // This places a functional door. If you want the sealed-ancient-ruin aesthetic,
        // swap ThingDefOf.Door back to ThingDefOf.Wall here.
        PlaceClaimed(map, ThingDefOf.Door, cell, material);
    }

    private void PlaceRoof(Map map, CellRect roomRect)
    {
        RoofDef roof = RoofDefOf.RoofConstructed;

        foreach (IntVec3 cell in roomRect.Cells)
        {
            if (!cell.InBounds(map)) continue;
            map.roofGrid.SetRoof(cell, roof);
        }

        // Roof the 2-thick outer wall band too so there's no gap at the edges.
        foreach (IntVec3 cell in roomRect.ExpandedBy(1).Cells)
        {
            if (!cell.InBounds(map)) continue;
            if (map.roofGrid.RoofAt(cell) == null)
            {
                map.roofGrid.SetRoof(cell, roof);
            }
        }
    }
}