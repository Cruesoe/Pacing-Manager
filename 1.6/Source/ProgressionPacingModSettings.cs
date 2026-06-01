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
        public static Dictionary<TechLevel, float> techLevelMultipliers = new Dictionary<TechLevel, float>
        {
            { TechLevel.Animal, 1f },
            { TechLevel.Neolithic, 1f },
            { TechLevel.Medieval, 1f },
            { TechLevel.Industrial, 1f },
            { TechLevel.Spacer, 1f },
            { TechLevel.Ultra, 1f },
            { TechLevel.Archotech, 1f }
        };
        public static Dictionary<TechLevel, int> techLevelRoundingMultiples = new Dictionary<TechLevel, int>
        {
            { TechLevel.Animal, 1 },
            { TechLevel.Neolithic, 1 },
            { TechLevel.Medieval, 1 },
            { TechLevel.Industrial, 1 },
            { TechLevel.Spacer, 1 },
            { TechLevel.Ultra, 1 },
            { TechLevel.Archotech, 1 }
        };

        public static float powerOutputMultiplier = 1f;
        public static int powerOutputRoundingMultiple = 1;
        public static bool excludeGravdata;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref techLevelMultipliers, "techLevelMultipliers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref techLevelRoundingMultiples, "techLevelRoundingMultiples", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref powerOutputMultiplier, "powerOutputMultiplier", 1f);
            Scribe_Values.Look(ref powerOutputRoundingMultiple, "powerOutputRoundingMultiple", 1);
            Scribe_Values.Look(ref excludeGravdata, "excludeGravdata");
        }

        public static void ResetTechLevelMultipliers()
        {
            techLevelMultipliers = new Dictionary<TechLevel, float>
                {
                    { TechLevel.Animal, 1f },
                    { TechLevel.Neolithic, 1f },
                    { TechLevel.Medieval, 1f },
                    { TechLevel.Industrial, 1f },
                    { TechLevel.Spacer, 1f },
                    { TechLevel.Ultra, 1f },
                    { TechLevel.Archotech, 1f }
                };
            techLevelRoundingMultiples = new Dictionary<TechLevel, int>
                {
                    { TechLevel.Animal, 1 },
                    { TechLevel.Neolithic, 1 },
                    { TechLevel.Medieval, 1 },
                    { TechLevel.Industrial, 1 },
                    { TechLevel.Spacer, 1 },
                    { TechLevel.Ultra, 1 },
                    { TechLevel.Archotech, 1 }
                };
            powerOutputMultiplier = 1f;
            powerOutputRoundingMultiple = 1;
        }
        
        public static Dictionary<ResearchProjectDef, float> originalResearchCosts = null;

        public static void UpdateResearchProjectCosts()
        {
            if (originalResearchCosts == null)
            {
                originalResearchCosts = new Dictionary<ResearchProjectDef, float>();
                foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
                {
                    originalResearchCosts[def] = def.baseCost;
                }
            }

            Dictionary<ResearchProjectDef, float> oldCosts = new Dictionary<ResearchProjectDef, float>();
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                oldCosts[def] = def.baseCost;
            }

            foreach (var kvp in originalResearchCosts)
            {
                kvp.Key.baseCost = kvp.Value;
            }
            
            if (techLevelMultipliers == null || techLevelRoundingMultiples == null)
            {
                ResetTechLevelMultipliers();
            }

            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (def.knowledgeCost > 0) continue;
                if (def.defName.StartsWith("BRM_Emergence_")) continue;
                if (ModsConfig.IsActive("vanillaexpanded.gravship") && excludeGravdata && def.tab?.defName == "VGE_Gravtech") continue;
                float multiplier = GetMultiplierForTechLevel(def.techLevel);
                float newCost = def.baseCost * multiplier;
                int roundingMultiple = GetRoundingMultipleForTechLevel(def.techLevel);
                if (roundingMultiple > 1)
                {
                    def.baseCost = Mathf.RoundToInt(newCost / roundingMultiple) * roundingMultiple;
                }
                else
                {
                    def.baseCost = Mathf.RoundToInt(newCost);
                }
                if (def.baseCost < roundingMultiple)
                {
                    def.baseCost = roundingMultiple;
                }
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
            if (techLevelMultipliers.TryGetValue(techLevel, out float multiplier))
            {
                return multiplier;
            }
            return 1f;
        }

        public static int GetRoundingMultipleForTechLevel(TechLevel techLevel)
        {
            if (techLevelRoundingMultiples.TryGetValue(techLevel, out int roundingMultiple))
            {
                return roundingMultiple;
            }
            return 1;
        }
    }
}
