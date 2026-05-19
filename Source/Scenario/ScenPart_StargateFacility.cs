// ==== Source/Scenario/ScenPart_StargateFacility.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility : ScenPart
{
    // Must match StargateDestinationMapGen.RoomSize exactly.
    private const int RoomSize = 15;
    private const float WooshRadius = 4.9f; // Kawoosh kills everything within ~5 tiles

    public override void GenerateIntoMap(Map map)
    {
        IntVec3 center = map.Center;
        int halfSize = RoomSize / 2;

        // Define room boundaries.
        // This rect includes the inner wall layer on its edge.
        CellRect roomRect = new CellRect(
            center.x - halfSize,
            center.z - halfSize,
            RoomSize,
            RoomSize
        );

        // Outer rect for 2-thick walls.
        CellRect outerWallRect = roomRect.ExpandedBy(1);

        // Critical:
        // The map generator may have already placed natural rock, ruins, plants,
        // chunks, geysers, fogged mountain spaces, or other obstructions here.
        // The facility footprint must be made clean before our generated structure
        // is placed, otherwise doors can be sealed behind stone walls.
        ClearFacilityFootprint(map, outerWallRect.ExpandedBy(2));

        // Handle ocean and impassable tiles.
        StargateDestinationMapGen.Apply(map, DescribeTile(map.Tile));

        GenerateRoomStructure(map, roomRect, outerWallRect);
        PlaceRoof(map, roomRect);
        PlacePowerConduits(map, center, roomRect);
        PlaceStargate(map, center);
        PlaceSupportEquipment(map, center, roomRect);
        PlaceDiningArea(map, roomRect, center);
        AddFacilityDetails(map, roomRect, center);
        ClaimHomeArea(map, roomRect);
        SpawnColonists(map, center);

        // Defer the fog override until RimWorld has finished its default passes.
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            // Force MapDrawer.sections allocation before anything touches FogGrid.
            // FogGrid.Unfog -> MapDrawer.MapMeshDirty -> MapDrawer.SectionAt[null] NPEs
            // when called this early. The base game guards on ProgramState.Playing,
            // but RocketMan's UnfogWorker patch bypasses that guard. RegenerateEverythingNow
            // is the cheapest public API that lazily allocates the sections grid.
            map.mapDrawer.RegenerateEverythingNow();

            // Step 1: re-fog the entire map.
#if RIMWORLD15 || RIMWORLD16
            map.fogGrid.Refog(new CellRect(0, 0, map.Size.x, map.Size.z));
#else
            bool[] fog = map.fogGrid.fogGrid;
            for (int i = 0; i < fog.Length; i++) fog[i] = true;
#endif

            // Step 2: flood-unfog from inside the room. Same mechanism RimWorld uses
            // when colonists mine through a wall and discover the cavity behind it.
            IntVec3 unfogRoot = FindWalkableSeedCell(map, roomRect, center);
            if (unfogRoot.IsValid)
            {
                FloodFillerFog.FloodUnfog(unfogRoot, map);
            }
            else
            {
                Log.Warning("BetterRimworlds.Stargate: No walkable seed cell found for fog flood-fill.");
            }

            // Step 3: final redraw to flush any deferred dirty flags.
#if RIMWORLD15 || RIMWORLD16
            map.mapDrawer.WholeMapChanged(MapMeshFlagDefOf.FogOfWar);
            map.mapDrawer.WholeMapChanged(MapMeshFlagDefOf.Things);
#else
            map.mapDrawer.WholeMapChanged(MapMeshFlag.FogOfWar);
            map.mapDrawer.WholeMapChanged(MapMeshFlag.Things);
#endif

            // Step 4: Show the Scenario Message Box after the map is fully loaded
            ShowScenarioMessage(map);
        });
    }

    private void ShowScenarioMessage(Map map)
    {
        string msg = BuildScenarioMessage(map);

        Find.WindowStack.Add(new Dialog_MessageBox(
            msg,
            "OK",
            () =>
            {
                // TriggerStargateRecall(map);
            }
        ));
    }

    private string BuildScenarioMessage(Map map)
    {
        int selectedTile = map.Tile;
        var tileInfo = map.TileInfo;
        if (tileInfo.WaterCovered)
        {
        }

        var c = StargateAutomationPatches.LastPlanetConditions;

        return
            "DAILY STARGATE PLANET CONDITIONS\n\n" +
            "This is today's shared pseudo-multiplayer planet. All players on the same " +
            "UTC date receive the same planet and planet-level conditions.\n\n" +
            "Seed:        " + (c != null ? c.SeedString : "N/A") + "\n" +
            "Coverage:    " + (c != null ? c.PlanetCoverage.ToString() : "N/A") + "\n" +
            "Rainfall:    " + (c != null ? c.Rainfall.ToString() : "N/A") + "\n" +
            "Temperature: " + (c != null ? c.Temperature.ToString() : "N/A") + "\n" +
            "Population:  " + (c != null ? c.Population.ToString() : "N/A") + "\n\n" +
            "===========================================\n\n" +
            "RANDOM STARGATE DESTINATION SELECTED\n\n" +
            "Daily planet seed:  " + StargateSeedUtility.GetDailySeed() + "\n" +
            "Selected tile:      " + selectedTile + "\n" +
            "Tile kind:          " + DescribeTile(selectedTile) + "\n\n" +
            "Same planet. Different Stargate.\n" +
            "Ocean → Atlantis  |  Impassable → Tok'ra  |  Normal → Surface facility";
    }

    // Locates the Stargate building instance on the provided map and triggers its recall sequence.
    private void TriggerStargateRecall(Map map)
    {
        ThingDef stargateDef = ThingDef.Named("Stargate");

        Thing spawnedGate = map.listerThings.ThingsOfDef(stargateDef).FirstOrDefault();

        if (spawnedGate is Building_Stargate stargate)
        {
            stargate.StargateRecall();
        }
        else if (spawnedGate != null)
        {
            Log.Warning("BetterRimworlds.Stargate: Found a Stargate Thing, but could not cast it to the 'Stargate' class.");
        }
        else
        {
            Log.Warning("BetterRimworlds.Stargate: Stargate ThingDef exists, but no Stargate was found on the map to trigger recall.");
        }
    }

    // Finds a walkable cell inside the room to seed FloodFillerFog.FloodUnfog.
    // The room center is occupied by the impassable Stargate, so we pick a cell
    // adjacent to it and fall back to a spiral search if that's blocked.
    private IntVec3 FindWalkableSeedCell(Map map, CellRect roomRect, IntVec3 center)
    {
        // Primary candidate: 2 cells north of center (just past the stargate's footprint).
        IntVec3 candidate = new IntVec3(center.x, 0, center.z + 2);
        if (candidate.InBounds(map) && candidate.Walkable(map) && roomRect.Contains(candidate))
        {
            return candidate;
        }

        // Fallback: spiral outward from center, take the first walkable cell in the room.
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, RoomSize, true))
        {
            if (!cell.InBounds(map)) continue;
            if (!roomRect.Contains(cell)) continue;
            if (!cell.Walkable(map)) continue;

            return cell;
        }

        return IntVec3.Invalid;
    }
}
