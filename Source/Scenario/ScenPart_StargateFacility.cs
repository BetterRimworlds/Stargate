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
        // The starting map is generated while it is the ONLY map in the game.
        // Every other map (quests, new settlements, enemy raids, caravan sites,
        // pocket maps, etc.) is generated after the home map already exists in
        // Find.Maps, so Find.Maps.Count will be >= 2 at that point.
        //
        // This avoids Find.GameInitData.startingTile entirely — that field's
        // fill order relative to GenerateIntoMap isn't guaranteed, which is why
        // the previous check either never matched (see Bug 2) or matched on
        // every map (see Bug 1) depending on timing.
        if (Find.Maps.Count != 1 || Find.Maps[0] != map)
        {
            return;
        }

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

        // Choose the entrance side up front so the cavern generator can carve
        // the guaranteed opening right outside the real doorway. The door is
        // only placed later in GenerateRoomStructure(), so the side must be
        // decided here and threaded through both steps.
        _entranceSide = Rand.Element(Rot4.North, Rot4.South, Rot4.East, Rot4.West);

        // Atlantis overrides the roll: its door must always render facing north.
        _entranceSide = GetAtlantisEntranceSide(map, _entranceSide);

        // Handle ocean and impassable tiles.
        StargateDestinationMapGen.Apply(map, DescribeTile(map.Tile), _entranceSide);

        GenerateRoomStructure(map, roomRect, outerWallRect);
        PlaceRoof(map, roomRect);
        PlacePowerConduits(map, roomRect);
        PlaceStargate(map, center);
        PlaceSupportEquipment(map, center, roomRect);
        PlaceDiningArea(map, roomRect, center);
        // Destination-themed extras (Atlantis ocean / Tok'ra impassable) after
        // shared furniture so BlueprintSpawner sees occupied cells, but before
        // debris so random slag cannot block the southern research bench.
        PlaceAtlantisFacilityExtras(map, center, roomRect);
        PlaceTokraFacilityExtras(map, center, roomRect);
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
        Find.WindowStack.Add(new Dialog_MessageBox(BuildScenarioMessage(map), "OK"));
    }

    private string BuildScenarioMessage(Map map)
    {
        var c = StargateAutomationPatches.LastPlanetConditions;
        int selectedTile = map.Tile;

        // Null-safe formatting for the daily conditions block.
        string Value<T>(T v) => v?.ToString() ?? "N/A";

        return
            "DAILY STARGATE PLANET CONDITIONS\n\n" +
            "This is today's shared pseudo-multiplayer planet. All players on the same " +
            "UTC date receive the same planet and planet-level conditions.\n\n" +
            "Seed:        " + Value(c?.SeedString) + "\n" +
            "Coverage:    " + Value(c?.PlanetCoverage) + "\n" +
            "Rainfall:    " + Value(c?.Rainfall) + "\n" +
            "Temperature: " + Value(c?.Temperature) + "\n" +
            "Population:  " + Value(c?.Population) + "\n\n" +
            "===========================================\n\n" +
            "STARGATE DESTINATION SELECTED\n\n" +
            "Scenario:           " + DescribeScenarioKind(StargateAutomationPatches.SelectedScenarioKind) + "\n" +
            "Daily planet seed:  " + StargateSeedUtility.GetDailySeed() + "\n" +
            "Selected tile:      " + selectedTile + "\n" +
            "Tile kind:          " + DescribeTile(selectedTile) + "\n\n" +
            "Same planet. Different Stargate.\n" +
            "Ocean → Atlantis  |  Impassable → Tok'ra  |  Normal → Surface facility";
    }

    private static string DescribeScenarioKind(StargateScenarioKind kind)
    {
        switch (kind)
        {
            case StargateScenarioKind.AtlantisRising:
                return "Atlantis Rising";
            case StargateScenarioKind.AbandonedTokraBase:
                return "Abandoned Tok'ra Base";
            case StargateScenarioKind.RandomTile:
            default:
                return "Random Tile";
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
