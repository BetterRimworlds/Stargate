// ==== Source/Scenario/StargateDestinationMapGen.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

public static partial class StargateDestinationMapGen
{
   // Must match ScenPart_StargateFacility constants exactly.
   private const int RoomSize = 15;

   public static void Apply(Map map, string tileDescription)
   {
       // Always lay rich soil under the inner room footprint.
       //
       // Ocean:      removing concrete floors reveals farmable soil — Atlantis gardens.
       // Impassable: underground growing under artificial light.
       //
       // This must run BEFORE GenerateRoomStructure, because SetTerrain stores the
       // current terrain as underterrain when a floor type is placed on top.
       // The sequence is:  SetTerrain(SoilRich) → SetTerrain(Concrete)
       // Removing the concrete later restores the SoilRich underterrain.
       SetFacilityUnderlayTerrain(map);

       switch (tileDescription)
       {
           case "Ocean":
               GenerateOceanSurroundings(map);
               break;

           case "Impassable":
               GenerateImpassableSurroundings(map);
               break;
       }
   }

   // -------------------------------------------------------------------------

   private static void SetFacilityUnderlayTerrain(Map map)
   {
       IntVec3 center = map.Center;
       int halfSize   = RoomSize / 2;

       CellRect roomRect = new CellRect(
           center.x - halfSize,
           center.z - halfSize,
           RoomSize,
           RoomSize
       );

       TerrainDef richSoil;
       #if RIMWORLD12
       richSoil = DefDatabase<TerrainDef>.GetNamed("SoilRich");
       #else
       richSoil = TerrainDefOf.SoilRich;
       #endif

       foreach (IntVec3 cell in roomRect.Cells)
       {
           if (!cell.InBounds(map)) continue;
           map.terrainGrid.SetTerrain(cell, richSoil);
       }
   }
}