using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ProgressionPacing
{
    public static class LeviathansCompat
    {
        public const string SandwormKey = "SandWorm_LeviathanQuest";
        public const string HiveLordKey = "HiveLord_SyndicateHunt";

        public static void ApplyPatch(Harmony harmony)
        {
            TryPatch(harmony, "chezhou.creature.sandworm", "SandWormLib.SandWormQuestUtility", "TryCreateLeviathanQuest", nameof(SandwormPrefix));
            TryPatch(harmony, "chezhou.creature.hivelord", "HiveLordLib.HiveLordQuestUtility", "TryOfferQuest", nameof(HiveLordPrefix));
        }

        private static void TryPatch(Harmony harmony, string packageId, string typeName, string methodName, string prefixName)
        {
            if (!ModsConfig.IsActive(packageId)) return;

            Type type = AccessTools.TypeByName(typeName);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, methodName);
            if (method == null || method.ReturnType != typeof(bool))
            {
                Log.Warning("[Pacing Manager] " + typeName + "." + methodName + " not found; its quest era gate will only apply once the quest is generated.");
                return;
            }

            harmony.Patch(method, prefix: new HarmonyMethod(typeof(LeviathansCompat), prefixName));
        }

        public static bool SandwormPrefix(ref bool __result)
        {
            return Allow(SandwormKey, ref __result);
        }

        public static bool HiveLordPrefix(ref bool __result)
        {
            return Allow(HiveLordKey, ref __result);
        }

        private static bool Allow(string key, ref bool __result)
        {
            if (ProgressionPacingModSettings.IsQuestGeneratorAllowed(key)) return true;
            __result = false;
            return false;
        }
    }
}
