using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Utilities;

public class BlueprintSpawner
{
    private readonly Map map;
    private readonly List<CellRect> occupiedRects = new();
    private CellRect layoutInterior;
    private Rot4 layoutSide = Rot4.East;
    private int layoutRefWidth;
    private int layoutRefHeight;

    public BlueprintSpawner(Map map)
    {
        this.map = map;
    }

    public void ConfigureLayout(CellRect interior, Rot4 side, int refWidth, int refHeight)
    {
        layoutInterior = interior;
        layoutSide = side;
        layoutRefWidth = refWidth;
        layoutRefHeight = refHeight;
    }

    public Thing SpawnFixed(
        ThingDef def,
        IntVec3 pos,
        Rot4 rot,
        CellRect interior,
        ThingDef stuff = null,
        QualityCategory? quality = null,
        bool claimForPlayer = false)
    {
        if (!TryReserveRect(def, pos, rot, interior, out CellRect rect))
        {
            Log.Warning($"BetterRimworlds.Stargate: Blueprint failed to place {def?.defName ?? "null"} at {pos} rot={rot}.");
            return null;
        }

        // Never silently destroy existing things with WipeMode.Vanish.
        // Fail the placement instead so caller-owned blueprints/buildings/items
        // on the target cells are preserved.
        if (HasThings(rect))
        {
            Log.Warning($"BetterRimworlds.Stargate: Blueprint blocked at {pos}: target cells already contain things.");
            return null;
        }

        ThingDef actualStuff = stuff;
        if (def.MadeFromStuff && actualStuff == null)
        {
            actualStuff = ThingDefOf.Steel;
        }

        Thing thing = ThingMaker.MakeThing(def, actualStuff);
        GenSpawn.Spawn(thing, pos, map, rot, WipeMode.Vanish);

        if (claimForPlayer && thing.def.CanHaveFaction)
        {
            thing.SetFaction(Faction.OfPlayer);
        }

        if (quality.HasValue)
        {
            CompQuality compQuality = thing.TryGetComp<CompQuality>();
            compQuality?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
        }

        return thing;
    }

    public void SpawnStack(ThingDef def, IntVec3 pos, int count, string roomName = "Room")
    {
        if (def == null || !pos.IsValid || map == null || !pos.InBounds(map)) return;
        if (count <= 0) return;

        GetOrCreateStockpileFor(def, pos, roomName + " " + def.label);

        // Merge before using an empty target cell, so existing items are never wiped.
        foreach (Thing stack in pos.GetThingList(map)
                     .Where(t => t.def == def && t.stackCount < def.stackLimit)
                     .ToList())
        {
            int added = Mathf.Min(def.stackLimit - stack.stackCount, count);
            stack.stackCount += added;
            count -= added;
            if (count == 0) return;
        }

        // Place at most one legal stack in the target or each cardinal neighbor.
        foreach (IntVec3 cell in new[] { pos }.Concat(GenAdj.CardinalDirections.Select(offset => pos + offset)))
        {
            if (!cell.InBounds(map) || cell.GetThingList(map).Count > 0) continue;

            int placed = Mathf.Min(count, def.stackLimit);
            Thing stack = ThingMaker.MakeThing(def);
            stack.stackCount = placed;
            GenSpawn.Spawn(stack, cell, map, WipeMode.Vanish);
            if ((count -= placed) == 0) return;
        }

        Log.Warning($"BetterRimworlds.Stargate: Blueprint failed to stack {def.defName} at {pos}: no empty target or adjacent cell for the remaining {count} items.");
    }

    public void SpawnConduitRing(CellRect secretRoomRect, ThingDef conduitDef)
    {
        if (conduitDef == null) return;

        foreach (IntVec3 cell in secretRoomRect.EdgeCells)
        {
            if (!cell.InBounds(map)) continue;
            if (ContainsThingOfDef(cell, conduitDef)) continue;

            // Don't wipe unrelated things that already occupy the cell.
            if (cell.GetThingList(map).Count > 0) continue;

            GenSpawn.Spawn(ThingMaker.MakeThing(conduitDef), cell, map, WipeMode.Vanish);
        }
    }

    public IntVec3 At(int x, int z)
    {
        int rx;
        int rz;

        if (layoutSide == Rot4.East)
        {
            rx = x;
            rz = z;
        }
        else if (layoutSide == Rot4.North)
        {
            rx = (layoutRefHeight - 1) - z;
            rz = x;
        }
        else if (layoutSide == Rot4.West)
        {
            rx = (layoutRefWidth - 1) - x;
            rz = (layoutRefHeight - 1) - z;
        }
        else
        {
            rx = z;
            rz = (layoutRefWidth - 1) - x;
        }

        return new IntVec3(layoutInterior.minX + rx, 0, layoutInterior.minZ + rz);
    }

    public Rot4 Rot(Rot4 rotation)
    {
        if (layoutSide == Rot4.East) return rotation;
        if (layoutSide == Rot4.North) return rotation.Rotated(RotationDirection.Counterclockwise);
        if (layoutSide == Rot4.West) return rotation.Opposite;

        return rotation.Rotated(RotationDirection.Clockwise);
    }

    public static bool RectFullyInside(CellRect outer, CellRect inner)
    {
        return outer.Contains(new IntVec3(inner.minX, 0, inner.minZ))
            && outer.Contains(new IntVec3(inner.maxX, 0, inner.maxZ));
    }

    public bool RectClear(CellRect rect)
    {
        foreach (CellRect occupied in occupiedRects)
        {
            if (rect.Overlaps(occupied)) return false;
        }

        return true;
    }

    public void ReserveRect(CellRect rect)
    {
        occupiedRects.Add(rect);
    }

    private bool TryReserveRect(ThingDef def, IntVec3 pos, Rot4 rot, CellRect interior, out CellRect rect)
    {
        rect = default;

        if (def == null) return false;
        if (!pos.IsValid) return false;
        if (!pos.InBounds(map)) return false;

        rect = GenAdj.OccupiedRect(pos, rot, def.size);
        if (!BlueprintSpawner.RectFullyInside(interior, rect)) return false;
        if (!RectClear(rect)) return false;

        occupiedRects.Add(rect);
        return true;
    }

    private Zone_Stockpile GetOrCreateStockpileFor(ThingDef def, IntVec3 pos, string roomName = "Room")
    {
        Zone currentZone = map.zoneManager.ZoneAt(pos);
        if (currentZone is Zone_Stockpile currentStockpile &&
            currentStockpile.settings.filter.Allows(def) &&
            currentStockpile.settings.filter.AllowedDefCount == 1)
        {
            return currentStockpile;
        }

        foreach (IntVec3 adjacentCell in GenAdj.CardinalDirections)
        {
            IntVec3 checkPos = pos + adjacentCell;
            Zone adjacentZone = map.zoneManager.ZoneAt(checkPos);

            if (adjacentZone is Zone_Stockpile adjacentStockpile)
            {
                if (adjacentStockpile.settings.filter.Allows(def) &&
                    adjacentStockpile.settings.filter.AllowedDefCount == 1)
                {
                    adjacentStockpile.AddCell(pos);
                    return adjacentStockpile;
                }
            }
        }

        Zone_Stockpile zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        map.zoneManager.RegisterZone(zone);

#if RIMWORLD16
        zone.Hidden = true;
#else
        zone.hidden = true;
#endif

        if (zone.settings == null)
        {
            zone.settings = new StorageSettings(zone);
        }

        zone.settings.Priority = StoragePriority.Critical;
        zone.settings.filter.SetDisallowAll();
        zone.settings.filter.SetAllow(def, true);
        zone.label = roomName;
        zone.AddCell(pos);

        return zone;
    }

    private bool ContainsThingOfDef(IntVec3 cell, ThingDef def)
    {
        return map.thingGrid.ThingsListAt(cell).Any(t => t.def == def);
    }

    private bool HasThings(CellRect rect)
    {
        foreach (IntVec3 cell in rect.Cells)
        {
            if (!cell.InBounds(map)) continue;
            if (cell.GetThingList(map).Count > 0) return true;
        }

        return false;
    }
}
