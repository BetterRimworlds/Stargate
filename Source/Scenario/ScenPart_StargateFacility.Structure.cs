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

    private TerrainDef GetGateRoomFloorDef(Map map)
    {
        // Variant partials own destination detection and themed definitions;
        // this shared layout only provides the neutral fallback.
        return GetTokraFloorDef(map)
            ?? GetAtlantisFloorDef(map)
            ?? TerrainDefOf.Concrete;
    }

    private void GenerateRoomStructure(Map map, CellRect innerRect, CellRect outerRect)
    {
        string[] stoneTypes = { "BlocksGranite", "BlocksLimestone", "BlocksSlate" };
        // Atlantis uses limestone architecture; other destinations keep the random stone mix.
        ThingDef wallMaterial = IsAtlantisFacility(map)
            ? (DefDatabase<ThingDef>.GetNamedSilentFail("BlocksLimestone")
               ?? DefDatabase<ThingDef>.GetNamed(stoneTypes[0]))
            : DefDatabase<ThingDef>.GetNamed(stoneTypes[Rand.Range(0, stoneTypes.Length)]);

        ThingDef luminescentWallDef = IsAtlantisFacility(map)
            ? LuminescentWallsUtility.GetWallDef()
            : null;

        TerrainDef floorDef = GetGateRoomFloorDef(map);

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

        // Build OUTER wall layer. Atlantis uses Luminescent Limestone for
        // both wall rings; other destinations keep the ordinary stone mix.
        foreach (IntVec3 cell in outerRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;

            // The outer wall doorway must stay open.
            // The actual door goes on the inner wall.
            if (cell == outerDoorCell) continue;

            ClearCellForBuilding(map, cell);
            if (luminescentWallDef != null)
            {
                PlaceClaimed(map, luminescentWallDef, cell);
            }
            else
            {
                PlaceClaimed(map, ThingDefOf.Wall, cell, wallMaterial);
            }
        }

        // Build INNER wall layer. On Atlantis the entire interior ring is
        // Luminescent Limestone so the room is lit by the wall surface itself.
        foreach (IntVec3 cell in innerRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;

            // The inner wall receives the actual single centered door.
            if (cell == innerDoorCell) continue;

            ClearCellForBuilding(map, cell);

            if (luminescentWallDef != null)
            {
                // Dedicated wall def is not stuffable.
                PlaceClaimed(map, luminescentWallDef, cell);
            }
            else
            {
                PlaceClaimed(map, ThingDefOf.Wall, cell, wallMaterial);
            }
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
        Building_Door door = PlaceDoor(map, innerDoorCell, wallMaterial);
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
        // Root one row north of the south wall so the cell sits directly
        // adjacent to the wall conduit ring.
        ThingDef vanoDef = DefDatabase<ThingDef>.GetNamedSilentFail("VanometricPowerCell");
        if (vanoDef != null)
        {
            IntVec3 vanoPos = new IntVec3(roomRect.maxX - 2, 0, roomRect.minZ + 1);
            if (vanoPos.InBounds(map))
            {
                ClearCellForBuilding(map, vanoPos);
                PlaceClaimed(map, vanoDef, vanoPos);
            }
        }

        // 2. Secondary power source. Destination-specific substitutions live
        // in their variant partials; ordinary facilities keep the ZPM below.
        if (PlaceAtlantisSecondaryPower(map, roomRect))
        {
            // Atlantis has no starting ZPM.
        }
        else
        {
            // Archotech ZPM at 75% charge, if mod present.
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

    private Building_Door PlaceDoor(Map map, IntVec3 cell, ThingDef material)
    {
        // We use ClearCellForBuilding instead of ClearCellForDoorway
        // to ensure it clears the concrete floor too, if you don't want it under the wall.
        ClearCellForBuilding(map, cell);

        // NOTE:
        // This places a functional door. If you want the sealed-ancient-ruin aesthetic,
        // swap ThingDefOf.Door back to ThingDefOf.Wall here.
        //
        // Do NOT try to force the facing by passing a rotation here: a 1x1 door
        // ignores it. Building_Door.DoorPreDraw() re-derives Rotation from the
        // adjacent cells before every draw. The facing follows from which wall
        // the door sits in — north/south walls render north, east/west walls
        // render east. See GetAtlantisEntranceSide.
        var door = PlaceClaimed(map, ThingDefOf.Door, cell, material) as Building_Door;

        // // 1. Grab the private/protected fields and methods via reflection
        // var type = typeof(Building_Door);
        // var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        //
        // var holdOpenField = type.GetField("holdOpenInt", flags);
        // var doorOpenMethod = type.GetMethod("DoorOpen", flags);
        //
        // if (holdOpenField != null && doorOpenMethod != null)
        // {
        //     // 2. Set holdOpenInt to true so it stays open forever
        //     holdOpenField.SetValue(door, true);
        //
        //     // 3. Invoke DoorOpen(int ticksToClose)
        //     // We pass 110 as the default argument value seen in your decompiled code
        //     doorOpenMethod.Invoke(door, new object[] { 1100000000 });
        // }

        return door;
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
