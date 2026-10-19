// ==== Source/Saving/DuplicateThingIdGuardPatch.cs ====
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace BetterRimworlds.Stargate.Saving
{
    /// Keeps LoadedObjectDirectory.RegisterLoaded from failing when an imported
    /// thing reuses an ID that already exists in the destination world.
    ///
    /// Stargate cargo comes from another save's ID space, so a pawn can arrive
    /// as Thing_Human679 while an unrelated item here is Apparel_FlakVest679.
    /// RimWorld 1.4+ indexes things by the bare thingIDNumber as well as the
    /// full load ID, and the second registration throws
    ///
    ///   Exception registering Verse.Pawn ... with unique thing ID 679:
    ///   System.ArgumentException: An item with the same key has already been added.
    ///
    /// The full load IDs differ, so the newcomer is still resolvable; only the
    /// numeric fallback index is ambiguous. Keep the existing numeric mapping
    /// and register the newcomer by load ID instead of changing its ID (which
    /// would invalidate every saved reference to it). If the full load ID is
    /// genuinely duplicated too, move the newcomer to a fresh ID.
    [HarmonyPatch(typeof(LoadedObjectDirectory), nameof(LoadedObjectDirectory.RegisterLoaded))]
    public static class DuplicateThingIdGuardPatch
    {
        // Introduced in RimWorld 1.4; absent in 1.2 and 1.3, where things are
        // registered by full load ID only.
        private static readonly FieldInfo AllThingsByThingIdField =
            AccessTools.Field(typeof(LoadedObjectDirectory), "allThingsByThingID");

        public static bool Prefix(
            LoadedObjectDirectory __instance,
            ILoadReferenceable reffable,
            Dictionary<string, ILoadReferenceable> ___allObjectsByLoadID
        )
        {
            if (reffable is not Thing thing || ___allObjectsByLoadID == null)
            {
                return true;
            }

            string uniqueId = thing.GetUniqueLoadID();

            bool uniqueIdTaken =
                ___allObjectsByLoadID.TryGetValue(uniqueId, out var existing)
                && !ReferenceEquals(existing, thing);

            if (uniqueIdTaken)
            {
                RemapThingToFreshId(thing);
                return true;
            }

            var allThingsByThingId =
                AllThingsByThingIdField?.GetValue(__instance)
                    as Dictionary<int, ILoadReferenceable>;

            if (allThingsByThingId == null
                || !allThingsByThingId.TryGetValue(thing.thingIDNumber, out var existingThing)
                || ReferenceEquals(existingThing, thing))
            {
                return true;
            }

            // The full load ID is unique but the numeric ID belongs to another
            // thing. Register the former ourselves and skip vanilla's failing
            // numeric add.
            ___allObjectsByLoadID.Add(uniqueId, thing);

            Log.Warning(
                $"[Stargate] Thing ID {thing.thingIDNumber} is shared by "
                + $"{thing.ToStringSafe()} and {existingThing.ToStringSafe()}. "
                + $"Kept the existing numeric mapping and registered the newcomer "
                + $"as {uniqueId}."
            );

            return false;
        }

        private static void RemapThingToFreshId(Thing thing)
        {
            if (Find.UniqueIDsManager == null)
            {
                return;
            }

            int previousId = thing.thingIDNumber;
            int newId;

            do
            {
                newId = Find.UniqueIDsManager.GetNextThingID();
            }
            while (newId == previousId);

            thing.thingIDNumber = newId;

            Log.Warning(
                $"[Stargate] Remapped duplicate thing ID {previousId} -> {newId} "
                + $"({thing.ToStringSafe()}) while registering loaded objects."
            );
        }
    }
}
