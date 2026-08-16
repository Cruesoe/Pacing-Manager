using HarmonyLib;
using UnityEngine;
using Verse;

namespace ProgressionPacing
{
    [HarmonyPatch(typeof(DefaultWindowDrawing), nameof(DefaultWindowDrawing.DoCloseButton))]
    public static class DefaultWindowDrawing_DoCloseButton_Patch
    {
        public static bool Prefix(Rect rect, string text, ref bool __result)
        {
            if (!ProgressionPacingMod.StyleCloseButton)
            {
                return true;
            }

            ProgressionPacingMod.StyleCloseButton = false;
            __result = ProgressionPacingMod.DrawColoredButton(rect, text, ProgressionPacingMod.CloseButtonColor, Color.white);
            return false;
        }
    }
}
