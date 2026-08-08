using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.Stargate;

[StaticConstructorOnStartup]
public static class ResearchProjectVisibilityPatches
{
    private static readonly string[] VisibilityMemberNames =
    {
        "Visible",
        "visible",
        "IsVisible",
        "isVisible",
        "Hidden",
        "hidden",
    };

    static ResearchProjectVisibilityPatches()
    {
        var harmony = new Harmony("com.betterrimworlds.stargate.research-visibility");

        MethodInfo visibleGetter = FindVisibilityGetter(out bool invertResult);

        if (visibleGetter != null)
        {
            HarmonyMethod postfix = invertResult
                ? new HarmonyMethod(typeof(ResearchProjectVisibilityPatches), nameof(HiddenPostfix))
                : new HarmonyMethod(typeof(ResearchProjectVisibilityPatches), nameof(VisiblePostfix));

            harmony.Patch(visibleGetter, postfix: postfix);
            Log.Message("BetterRimworlds.Stargate: Harmony research visibility patch applied.");
        }
        else
        {
            Log.Message("BetterRimworlds.Stargate: Research visibility hook not found; leaving vanilla behavior unchanged.");
        }
    }

    private static MethodInfo FindVisibilityGetter(out bool invertResult)
    {
        invertResult = false;

        foreach (string memberName in VisibilityMemberNames)
        {
            // AccessTools.PropertyGetter already returns the getter method for the
            // property, so no get_<name> method fallback is needed here.
            MethodInfo getter = AccessTools.PropertyGetter(typeof(ResearchProjectDef), memberName);

            if (getter != null)
            {
                if (string.Equals(memberName, "Hidden", System.StringComparison.OrdinalIgnoreCase))
                {
                    invertResult = true;
                }

                return getter;
            }
        }

        foreach (MethodInfo method in typeof(ResearchProjectDef).GetMethods(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                     BindingFlags.DeclaredOnly))
        {
            if (method.ReturnType != typeof(bool) || method.GetParameters().Length != 0)
            {
                continue;
            }

            string methodName = method.Name;
            if (methodName.IndexOf("vis", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return method;
            }

            if (methodName.IndexOf("hidden", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                invertResult = true;
                return method;
            }
        }

        return null;
    }

    public static void VisiblePostfix(ResearchProjectDef __instance, ref bool __result)
    {
        if (__instance == null)
        {
            return;
        }

        if (__instance.GetModExtension<RequiresOceanBase>() == null)
        {
            return;
        }

        if (!IsOceanBaseRun())
        {
            __result = false;
        }
    }

    public static void HiddenPostfix(ResearchProjectDef __instance, ref bool __result)
    {
        if (__instance == null)
        {
            return;
        }

        if (__instance.GetModExtension<RequiresOceanBase>() == null)
        {
            return;
        }

        if (!IsOceanBaseRun())
        {
            __result = true;
        }
    }

    private static bool IsOceanBaseRun()
    {
        Map map = Find.CurrentMap;
        if (map != null)
        {
            return map.TileInfo != null && map.TileInfo.WaterCovered;
        }

        if (Find.GameInitData != null && Find.GameInitData.startingTile >= 0 && Find.WorldGrid != null)
        {
            Tile tile = GetTile(Find.GameInitData.startingTile);
            return tile != null && tile.WaterCovered;
        }

        return false;
    }

    private static Tile GetTile(int tileId)
    {
#if RIMWORLD16
        return Find.WorldGrid[tileId];
#else
        return Find.WorldGrid.tiles[tileId];
#endif
    }
}
