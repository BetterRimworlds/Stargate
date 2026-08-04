// ==== Source/Research/GateTravelerResearchTabInjector.cs ====
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate;

[StaticConstructorOnStartup]
public static class GateTravelerResearchTabInjector
{
    static GateTravelerResearchTabInjector()
    {
        ThingDef humanDef = DefDatabase<ThingDef>.GetNamedSilentFail("Human");

        if (humanDef == null)
        {
            Log.Warning("[Stargate] Could not add Gate Traveler research tab: Human ThingDef not found.");
            return;
        }

        Type tabType = typeof(ITab_Pawn_GateTravelerResearch);

        humanDef.inspectorTabs ??= new List<Type>();

        if (humanDef.inspectorTabs.Contains(tabType))
        {
            return;
        }

        // Add to end first, then reorder
        humanDef.inspectorTabs.Add(tabType);

        // Reorder: move to position 2 (0=Health, 1=Bio, 2=our tab)
        int currentIndex = humanDef.inspectorTabs.Count - 1;
        int targetIndex = 2;

        if (currentIndex >= 0 && targetIndex < humanDef.inspectorTabs.Count)
        {
            Type item = humanDef.inspectorTabs[currentIndex];
            humanDef.inspectorTabs.RemoveAt(currentIndex);
            if (targetIndex > humanDef.inspectorTabs.Count)
            {
                targetIndex = humanDef.inspectorTabs.Count;
            }
            humanDef.inspectorTabs.Insert(targetIndex, item);
        }

        // Same for resolved tabs - add then reorder
        humanDef.inspectorTabsResolved ??= new List<InspectTabBase>();

        // Remove if already exists
        for (int i = humanDef.inspectorTabsResolved.Count - 1; i >= 0; i--)
        {
            if (humanDef.inspectorTabsResolved[i]?.GetType() == tabType)
            {
                humanDef.inspectorTabsResolved.RemoveAt(i);
            }
        }

        InspectTabBase resolvedTab = InspectTabManager.GetSharedInstance(tabType);

        if (resolvedTab == null)
        {
            Log.Warning("[Stargate] Could not resolve Gate Traveler research inspect tab instance.");
            return;
        }

        // Add to end
        humanDef.inspectorTabsResolved.Add(resolvedTab);

        // Reorder to position 2
        int resolvedCurrent = humanDef.inspectorTabsResolved.Count - 1;
        int resolvedTarget = 2;

        if (resolvedCurrent >= 0 && resolvedTarget < humanDef.inspectorTabsResolved.Count)
        {
            InspectTabBase resolvedItem = humanDef.inspectorTabsResolved[resolvedCurrent];
            humanDef.inspectorTabsResolved.RemoveAt(resolvedCurrent);
            if (resolvedTarget > humanDef.inspectorTabsResolved.Count)
            {
                resolvedTarget = humanDef.inspectorTabsResolved.Count;
            }
            humanDef.inspectorTabsResolved.Insert(resolvedTarget, resolvedItem);
        }

        Log.Message("[Stargate] Added Gate Traveler research inspect tab to Human ThingDef (position: index 2, right of Bio).");
    }
}
