using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace ProgressionPacing
{
    [HarmonyPatch(typeof(CompProperties_Power), nameof(CompProperties_Power.PowerConsumption), MethodType.Getter)]
    public static class CompProperties_Power_PowerConsumption_Patch
    {
        public static void Postfix(ref float __result)
        {
            if (__result < 0f && ProgressionPacingModSettings.powerOutputMultiplier != 1f)
            {
                __result *= ProgressionPacingModSettings.powerOutputMultiplier;
                if (ProgressionPacingModSettings.powerOutputRoundingMultiple > 1)
                {
                    __result = Mathf.RoundToInt(__result / ProgressionPacingModSettings.powerOutputRoundingMultiple) * ProgressionPacingModSettings.powerOutputRoundingMultiple;
                }
                else
                {
                    __result = Mathf.RoundToInt(__result);
                }
                if (Mathf.Abs(__result) < ProgressionPacingModSettings.powerOutputRoundingMultiple)
                {
                    __result = -ProgressionPacingModSettings.powerOutputRoundingMultiple;
                }
            }
        }
    }
}
