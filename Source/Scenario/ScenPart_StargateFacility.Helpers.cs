// ==== Source/Scenario/ScenPart_StargateFacility.Helpers.cs ====
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate;

internal partial class ScenPart_StargateFacility
{
    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    // Spawns a thing at the target cell and properly factions it to the player.
    //
    // Why this exists:
    //   SetFactionDirect is a raw field setter that bypasses Notify_OwnerChanged,
    //   designation manager registration, and faction tracking. Calling it before
    //   GenPlace.TryPlaceThing on an unspawned Thing leaves the building "placed"
    //   but not actually owned by the player - claims don't register, the building
    //   isn't selectable as ours, and the map drawer doesn't auto-unfog around it.
    //
    //   The correct sequence is: MakeThing -> GenSpawn.Spawn -> SetFaction after.
    //   SetFaction, not SetFactionDirect, fires the ownership notifications.
    private Thing PlaceClaimed(Map map, ThingDef def, IntVec3 cell, ThingDef stuff = null, Rot4? rotation = null)
    {
        if (!cell.InBounds(map)) return null;

        Thing thing = ThingMaker.MakeThing(def, stuff);
        // Some DLC/modded building defs produce a MinifiedThing wrapper.
        // GenSpawn.Spawn will place it as a boxed item on the floor rather
        // than installing it. Unwrap the inner building before spawning.
        if (thing is MinifiedThing minified)
        {
            Thing inner = minified.InnerThing;
            minified.InnerThing = null; // prevents the wrapper from owning/destroying it
            thing = inner;
        }

        if (thing.def.useHitPoints)
        {
            thing.HitPoints = thing.MaxHitPoints;
        }

        Rot4 rot = rotation ?? Rot4.North;
        Thing spawned = GenSpawn.Spawn(thing, cell, map, rot, WipeMode.Vanish);

        if (spawned != null && spawned.def.CanHaveFaction)
        {
            spawned.SetFaction(Faction.OfPlayer);
        }

        return spawned;
    }

    // Returns the cardinal Rot4 that points from one cell toward another,
    // picking the dominant axis. Handles arbitrary offsets, unlike
    // Rot4.FromIntVec3 which only accepts cardinal unit vectors.
    private Rot4 GetRotationFacing(IntVec3 from, IntVec3 to)
    {
        int dx = to.x - from.x;
        int dz = to.z - from.z;

        if (System.Math.Abs(dx) >= System.Math.Abs(dz))
        {
            return dx >= 0 ? Rot4.East : Rot4.West;
        }

        return dz >= 0 ? Rot4.North : Rot4.South;
    }

    private IntVec3 GetCenteredEdgeCell(CellRect rect, Rot4 side)
    {
        if (side == Rot4.North)
        {
            return new IntVec3(rect.CenterCell.x, 0, rect.maxZ);
        }

        if (side == Rot4.South)
        {
            return new IntVec3(rect.CenterCell.x, 0, rect.minZ);
        }

        if (side == Rot4.East)
        {
            return new IntVec3(rect.maxX, 0, rect.CenterCell.z);
        }

        return new IntVec3(rect.minX, 0, rect.CenterCell.z);
    }

    private bool ContainsThingDef(Map map, IntVec3 cell, ThingDef def)
    {
        if (!cell.InBounds(map)) return false;

        List<Thing> things = map.thingGrid.ThingsListAt(cell);
        for (int i = 0; i < things.Count; i++)
        {
            if (things[i].def == def)
            {
                return true;
            }
        }

        return false;
    }

    private Tile GetTile(int tileId)
    {
#if RIMWORLD16
        return Find.WorldGrid[tileId];
#else
        return Find.WorldGrid.tiles[tileId];
#endif
    }

    private string DescribeTile(int tileId)
    {
        if (tileId < 0 || tileId >= Find.WorldGrid.TilesCount) return "Unknown";

        Tile tile = GetTile(tileId);
        if (tile == null) return "Unknown";

        if (tile.WaterCovered) return "Ocean";

        // This is the actual impassable-mountain marker. biome.impassable is a
        // different concept (biomes that can never be settled) and is effectively
        // unused in vanilla 1.2.
        if (tile.hilliness == Hilliness.Impassable) return "Impassable";

#if RIMWORLD16
        if (tile.PrimaryBiome != null) return tile.PrimaryBiome.defName;
#else
        if (tile.biome != null) return tile.biome.defName;
#endif

        return "Unknown";
    }
}