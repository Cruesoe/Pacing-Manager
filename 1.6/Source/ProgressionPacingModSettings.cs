using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ProgressionPacing
{
    public class ProgressionPacingModSettings : ModSettings
    {
        public static Dictionary<TechLevel, float> techLevelMultipliers = CreateDefaultMultipliers();
        public static Dictionary<TechLevel, int> techLevelRoundingMultiples = CreateDefaultRoundingMultiples();
        public static Dictionary<TechLevel, int> techLevelAddons = CreateDefaultAddons();

        public static float powerOutputMultiplier = 1f;
        public static int powerOutputRoundingMultiple = 1;
        public static bool excludeGravdata;

        public static Dictionary<ResearchProjectDef, float> originalResearchCosts = null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref techLevelMultipliers, "techLevelMultipliers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref techLevelRoundingMultiples, "techLevelRoundingMultiples", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref techLevelAddons, "techLevelAddons", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref powerOutputMultiplier, "powerOutputMultiplier", 1f);
            Scribe_Values.Look(ref powerOutputRoundingMultiple, "powerOutputRoundingMultiple", 1);
            Scribe_Values.Look(ref excludeGravdata, "excludeGravdata");
            EnsureDictionaries();
        }

        public static void EnsureDictionaries()
        {
            if (techLevelMultipliers == null) techLevelMultipliers = CreateDefaultMultipliers();
            if (techLevelRoundingMultiples == null) techLevelRoundingMultiples = CreateDefaultRoundingMultiples();
            if (techLevelAddons == null) techLevelAddons = CreateDefaultAddons();
            foreach (TechLevel level in Enum.GetValues(typeof(TechLevel)))
            {
                if (level == TechLevel.Undefined) continue;
                if (!techLevelMultipliers.ContainsKey(level)) techLevelMultipliers[level] = 1f;
                if (!techLevelRoundingMultiples.ContainsKey(level)) techLevelRoundingMultiples[level] = 1;
                if (!techLevelAddons.ContainsKey(level)) techLevelAddons[level] = 0;
            }
        }

        public static void ResetResearchSettings()
        {
            techLevelMultipliers = CreateDefaultMultipliers();
            techLevelRoundingMultiples = CreateDefaultRoundingMultiples();
            techLevelAddons = CreateDefaultAddons();
            excludeGravdata = false;
        }

        public static void ResetPowerSettings()
        {
            powerOutputMultiplier = 1f;
            powerOutputRoundingMultiple = 1;
        }

        public static void ResetTechLevelMultipliers()
        {
            ResetResearchSettings();
            ResetPowerSettings();
        }

        private static Dictionary<TechLevel, float> CreateDefaultMultipliers()
        {
            return new Dictionary<TechLevel, float>
            {
                { TechLevel.Animal, 1f },
                { TechLevel.Neolithic, 1f },
                { TechLevel.Medieval, 1f },
                { TechLevel.Industrial, 1f },
                { TechLevel.Spacer, 1f },
                { TechLevel.Ultra, 1f },
                { TechLevel.Archotech, 1f }
            };
        }

        private static Dictionary<TechLevel, int> CreateDefaultRoundingMultiples()
        {
            return new Dictionary<TechLevel, int>
            {
                { TechLevel.Animal, 1 },
                { TechLevel.Neolithic, 1 },
                { TechLevel.Medieval, 1 },
                { TechLevel.Industrial, 1 },
                { TechLevel.Spacer, 1 },
                { TechLevel.Ultra, 1 },
                { TechLevel.Archotech, 1 }
            };
        }

        private static Dictionary<TechLevel, int> CreateDefaultAddons()
        {
            return new Dictionary<TechLevel, int>
            {
                { TechLevel.Animal, 0 },
                { TechLevel.Neolithic, 0 },
                { TechLevel.Medieval, 0 },
                { TechLevel.Industrial, 0 },
                { TechLevel.Spacer, 0 },
                { TechLevel.Ultra, 0 },
                { TechLevel.Archotech, 0 }
            };
        }

        public static bool ShouldSkipResearchProject(ResearchProjectDef def)
        {
            if (def == null) return true;
            if (def.knowledgeCost > 0) return true;
            if (def.defName.StartsWith("BRM_Emergence_")) return true;
            if (ModsConfig.IsActive("vanillaexpanded.gravship") && excludeGravdata && def.tab?.defName == "VGE_Gravtech") return true;
            return false;
        }

        public static float ComputeAdjustedCost(float originalCost, TechLevel techLevel)
        {
            return ComputeAdjustedCost(originalCost, GetMultiplierForTechLevel(techLevel), GetAddonForTechLevel(techLevel), GetRoundingMultipleForTechLevel(techLevel));
        }

        public static float ComputeAdjustedCost(float originalCost, float multiplier, int addon, int roundingMultiple)
        {
            float newCost = originalCost * multiplier + addon;
            if (roundingMultiple > 1)
            {
                newCost = Mathf.RoundToInt(newCost / roundingMultiple) * roundingMultiple;
            }
            else
            {
                newCost = Mathf.RoundToInt(newCost);
            }
            if (newCost < 1f)
            {
                newCost = 1f;
            }
            if (roundingMultiple > 1 && newCost < roundingMultiple)
            {
                newCost = roundingMultiple;
            }
            return newCost;
        }

        public static void EnsureOriginalResearchCosts()
        {
            if (originalResearchCosts != null) return;
            originalResearchCosts = new Dictionary<ResearchProjectDef, float>();
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                originalResearchCosts[def] = def.baseCost;
            }
        }

        public static void GetEraTotals(TechLevel techLevel, out int totalTechs, out float totalPoints)
        {
            totalTechs = 0;
            totalPoints = 0f;
            EnsureOriginalResearchCosts();
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (def.techLevel != techLevel) continue;
                if (ShouldSkipResearchProject(def)) continue;
                float original = originalResearchCosts.TryGetValue(def, out float orig) ? orig : def.baseCost;
                totalTechs++;
                totalPoints += ComputeAdjustedCost(original, techLevel);
            }
        }

        public static void UpdateResearchProjectCosts()
        {
            EnsureDictionaries();
            EnsureOriginalResearchCosts();

            Dictionary<ResearchProjectDef, float> oldCosts = new Dictionary<ResearchProjectDef, float>();
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                oldCosts[def] = def.baseCost;
            }

            foreach (var kvp in originalResearchCosts)
            {
                kvp.Key.baseCost = kvp.Value;
            }

            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (ShouldSkipResearchProject(def)) continue;
                float original = originalResearchCosts.TryGetValue(def, out float orig) ? orig : def.baseCost;
                def.baseCost = ComputeAdjustedCost(original, def.techLevel);
            }

            if (Current.ProgramState == ProgramState.Playing && Find.ResearchManager != null)
            {
                var progressDict = Find.ResearchManager.progress;
                if (progressDict != null)
                {
                    foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
                    {
                        if (progressDict.TryGetValue(def, out float currentProgress) && currentProgress > 0)
                        {
                            float oldCost = oldCosts[def];
                            float newCost = def.baseCost;

                            if (oldCost > 0 && newCost > 0 && Math.Abs(oldCost - newCost) > 0.1f)
                            {
                                float ratio = newCost / oldCost;
                                float newProgress = currentProgress * ratio;

                                if (currentProgress >= oldCost - 0.01f)
                                {
                                    newProgress = Mathf.Max(newProgress, newCost);
                                }

                                progressDict[def] = newProgress;
                            }
                        }
                    }
                }

                var comp = Current.Game?.GetComponent<ProgressionPacingGameComponent>();
                if (comp != null)
                {
                    comp.UpdateSavedMultipliers();
                }
            }
        }

        public static float GetMultiplierForTechLevel(TechLevel techLevel)
        {
            if (techLevelMultipliers != null && techLevelMultipliers.TryGetValue(techLevel, out float multiplier))
            {
                return multiplier;
            }
            return 1f;
        }

        public static int GetRoundingMultipleForTechLevel(TechLevel techLevel)
        {
            if (techLevelRoundingMultiples != null && techLevelRoundingMultiples.TryGetValue(techLevel, out int roundingMultiple))
            {
                return roundingMultiple;
            }
            return 1;
        }

        public static int GetAddonForTechLevel(TechLevel techLevel)
        {
            if (techLevelAddons != null && techLevelAddons.TryGetValue(techLevel, out int addon))
            {
                return addon;
            }
            return 0;
        }
    }
}
