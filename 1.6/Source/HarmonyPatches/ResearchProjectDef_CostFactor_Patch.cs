using HarmonyLib;
using RimWorld;
using Verse;

namespace ProgressionPacing
{
    [HarmonyPatch(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CostFactor))]
    public static class ResearchProjectDef_CostFactor_Patch
    {
        public static bool Prefix(ref float __result)
        {
            if (!ProgressionPacingModSettings.disableTechLevelCostFactor) return true;
            __result = 1f;
            return false;
        }
    }
}
