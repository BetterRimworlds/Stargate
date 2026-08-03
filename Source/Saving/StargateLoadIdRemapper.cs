// ==== Source/Saving/StargateLoadIdRemapper.cs ====
using System.Collections.Generic;
using Verse;

namespace BetterRimworlds.Stargate.Saving;

/// Remaps load-reference IDs on things imported through the Stargate buffer.
///
/// Cross-world wormholes deep-load pawns from another save's ID space. Hediff
/// loadIDs are preserved from the origin world and are not coordinated with the
/// destination's UniqueIDsManager. That produces collisions such as:
///
///   Cannot register GateTravelerImplant ... id=Hediff_98472
///   Id already used by GateTravelerImplant ...
///
/// Always reassign imported hediff loadIDs from the destination world's
/// sequence so future GetNextHediffID() values cannot reuse them.
public static class StargateLoadIdRemapper
{
    public static void RemapImportedThingLoadIds(IEnumerable<Thing> things)
    {
        if (things == null)
        {
            return;
        }

        if (Find.UniqueIDsManager == null)
        {
            Log.Warning(
                "[Stargate] Cannot remap imported hediff loadIDs: UniqueIDsManager is null."
            );
            return;
        }

        int remappedCount = 0;

        foreach (Thing thing in things)
        {
            remappedCount += RemapThingTree(thing);
        }

        if (remappedCount > 0)
        {
            Log.Message(
                $"[Stargate] Remapped {remappedCount} hediff loadID(s) for imported Stargate cargo."
            );
        }
    }

    public static int RemapPawnHediffLoadIds(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return 0;
        }

        if (Find.UniqueIDsManager == null)
        {
            return 0;
        }

        int remappedCount = 0;

        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff == null)
            {
                continue;
            }

            int previousId = hediff.loadID;
            hediff.loadID = Find.UniqueIDsManager.GetNextHediffID();
            remappedCount++;

            if (Prefs.DevMode && previousId >= 0)
            {
                Log.Message(
                    $"[Stargate] Remapped hediff loadID {previousId} -> {hediff.loadID} " +
                    $"({hediff.def?.defName} on {pawn.LabelShortCap})."
                );
            }
        }

        return remappedCount;
    }

    private static int RemapThingTree(Thing thing)
    {
        if (thing == null)
        {
            return 0;
        }

        int remappedCount = 0;

        if (thing is Pawn pawn)
        {
            remappedCount += RemapPawnHediffLoadIds(pawn);
        }
        else if (thing is Corpse corpse && corpse.InnerPawn != null)
        {
            remappedCount += RemapPawnHediffLoadIds(corpse.InnerPawn);
        }

        return remappedCount;
    }
}
