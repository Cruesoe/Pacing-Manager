using HarmonyLib;
using Verse;
using UnityEngine;
using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ProgressionPacing
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }
    [HotSwappable]
    public class ProgressionPacingMod : Mod
    {
        public ProgressionPacingMod(ModContentPack pack) : base(pack)
        {
            GetSettings<ProgressionPacingModSettings>();
            new Harmony("ProgressionPacingMod").PatchAll();
        }

        private static float scrollHeight = 999999f;
        private Vector2 scrollPosition = Vector2.zero;
        private readonly Dictionary<TechLevel, string> addonBuffers = new Dictionary<TechLevel, string>();
        private readonly Dictionary<string, string> questBuffers = new Dictionary<string, string>();
        private string powerOutputRoundingBuffer;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            ProgressionPacingModSettings.EnsureDictionaries();
            Rect viewRect = new Rect(0, 0, inRect.width - 16f, scrollHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            ProgressionPacingModSettings.researchSectionExpanded = DrawSectionHeader(listing, "PP_ResearchSection".Translate(), ProgressionPacingModSettings.researchSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetResearchSettings();
                addonBuffers.Clear();
            });
            if (ProgressionPacingModSettings.researchSectionExpanded)
            {
                DrawResearchSection(listing);
            }

            listing.Gap();
            ProgressionPacingModSettings.powerSectionExpanded = DrawSectionHeader(listing, "PP_PowerSection".Translate(), ProgressionPacingModSettings.powerSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetPowerSettings();
                powerOutputRoundingBuffer = null;
            });
            if (ProgressionPacingModSettings.powerSectionExpanded)
            {
                DrawPowerSection(listing);
            }

            listing.Gap();
            ProgressionPacingModSettings.questSectionExpanded = DrawSectionHeader(listing, "PP_QuestSection".Translate(), ProgressionPacingModSettings.questSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetQuestPacing();
                questBuffers.Clear();
            });
            if (ProgressionPacingModSettings.questSectionExpanded)
            {
                DrawQuestSection(listing);
            }

            scrollHeight = listing.CurHeight + 20f;
            listing.End();

            Widgets.EndScrollView();
        }

        private static bool DrawSectionHeader(Listing_Standard listing, string label, bool expanded, Action onReset)
        {
            Rect row = listing.GetRect(30f);
            Rect buttonRect = new Rect(row.xMax - 100f, row.y, 100f, row.height);
            Rect toggleRect = new Rect(row.x, row.y, buttonRect.x - row.x - 8f, row.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(toggleRect, (expanded ? "▼ " : "▶ ") + label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(toggleRect))
            {
                expanded = !expanded;
            }
            if (Widgets.ButtonText(buttonRect, "Reset".Translate()))
            {
                onReset?.Invoke();
            }
            listing.Gap(listing.verticalSpacing);
            return expanded;
        }

        private void DrawResearchSection(Listing_Standard listing)
        {
            foreach (var techLevel in Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>())
            {
                if (techLevel == TechLevel.Undefined) continue;
                DrawEraRow(listing, techLevel);
            }
            if (ModsConfig.IsActive("vanillaexpanded.gravship"))
            {
                listing.CheckboxLabeled("PP_ExcludeGravdata".Translate(), ref ProgressionPacingModSettings.excludeGravdata);
            }
        }

        private void DrawEraRow(Listing_Standard listing, TechLevel techLevel)
        {
            ProgressionPacingModSettings.GetEraTotals(techLevel, out int totalTechs, out float totalPoints);
            if (totalTechs == 0) return;

            Rect header = listing.GetRect(Text.LineHeight);
            Rect nameRect = header.LeftHalf().Rounded();
            Rect totalsRect = header.RightHalf().Rounded();
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, techLevel.ToString());
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(totalsRect, "PP_ResearchTotalTechs".Translate() + ": " + totalTechs + "    " + "PP_ResearchTotalPoints".Translate() + ": " + totalPoints.ToString("N0"));
            Text.Anchor = TextAnchor.UpperLeft;

            string multiplierLabel = "PP_ResearchMultiplier".Translate() + ": " + ProgressionPacingModSettings.techLevelMultipliers[techLevel].ToStringPercent();
            ProgressionPacingModSettings.techLevelMultipliers[techLevel] = listing.SliderLabeled(multiplierLabel, ProgressionPacingModSettings.techLevelMultipliers[techLevel], 0.01f, 10f, labelPct: 0.30f);

            int addon = ProgressionPacingModSettings.GetAddonForTechLevel(techLevel);
            if (!addonBuffers.TryGetValue(techLevel, out string buffer) || buffer == null)
            {
                buffer = addon.ToString();
            }
            Rect addonRect = listing.GetRect(Text.LineHeight);
            if (!listing.BoundingRectCached.HasValue || addonRect.Overlaps(listing.BoundingRectCached.Value))
            {
                Rect labelRect = addonRect.LeftHalf().Rounded();
                Rect fieldRect = addonRect.RightHalf().Rounded();
                fieldRect.height -= 6f;
                fieldRect.y += 3f;
                TextAnchor anchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(labelRect, "PP_ResearchAddon".Translate() + ": ");
                Text.Anchor = anchor;
                Widgets.TextFieldNumeric(fieldRect, ref addon, ref buffer, 0, 100000);
            }
            listing.Gap(listing.verticalSpacing);
            addonBuffers[techLevel] = buffer;
            ProgressionPacingModSettings.techLevelAddons[techLevel] = addon;
            listing.GapLine();
        }

        private void DrawPowerSection(Listing_Standard listing)
        {
            string powerOutputLabel = "PP_PowerOutputMultiplier".Translate() + ": " + ProgressionPacingModSettings.powerOutputMultiplier.ToStringPercent();
            ProgressionPacingModSettings.powerOutputMultiplier = listing.SliderLabeled(powerOutputLabel, ProgressionPacingModSettings.powerOutputMultiplier, 0.01f, 10f, labelPct: 0.30f);
            listing.Gap();
            Text.Anchor = TextAnchor.MiddleCenter;
            listing.Label("PP_PowerOutputRoundingMultiple".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            int powerOutputRoundingValue = ProgressionPacingModSettings.powerOutputRoundingMultiple;
            if (powerOutputRoundingBuffer == null)
            {
                powerOutputRoundingBuffer = powerOutputRoundingValue.ToString();
            }
            Rect powerOutputRoundingRect = listing.GetRect(Text.LineHeight);
            if (!listing.BoundingRectCached.HasValue || powerOutputRoundingRect.Overlaps(listing.BoundingRectCached.Value))
            {
                Rect fieldRect = powerOutputRoundingRect.RightHalf().Rounded();
                fieldRect.height -= 6f;
                fieldRect.y += 3f;
                Widgets.TextFieldNumeric(fieldRect, ref powerOutputRoundingValue, ref powerOutputRoundingBuffer, 1, 10000);
            }
            listing.Gap(listing.verticalSpacing);
            ProgressionPacingModSettings.powerOutputRoundingMultiple = powerOutputRoundingValue;
        }

        private void DrawQuestSection(Listing_Standard listing)
        {
            ProgressionPacingModSettings.EnsureDictionaries();
            var entries = ProgressionPacingModSettings.AllRandomQuestComps()
                .OrderBy(e => e.def.label)
                .ThenBy(e => e.cycleNumber)
                .ToList();
            foreach (var entry in entries)
            {
                listing.Label(ProgressionPacingModSettings.QuestCycleLabel(entry.def, entry.cycleNumber, entry.cycleCount));
                QuestPacingValues values = ProgressionPacingModSettings.GetQuestPacingValues(entry.key);
                DrawQuestFloatField(listing, entry.key + ".onDays", "PP_QuestCycleDays".Translate(), ref values.onDays, 0.1f, 1000f);
                DrawQuestFloatField(listing, entry.key + ".minSpacingDays", "PP_QuestMinDaysBetween".Translate(), ref values.minSpacingDays, 0f, 1000f);
                DrawQuestFloatField(listing, entry.key + ".questsEachCycle", "PP_QuestCount".Translate(), ref values.questsEachCycle, 0f, 100f);
                listing.GapLine();
            }
        }

        private void DrawQuestFloatField(Listing_Standard listing, string bufferKey, string label, ref float value, float min, float max)
        {
            if (!questBuffers.TryGetValue(bufferKey, out string buffer) || buffer == null)
            {
                buffer = value.ToString();
            }
            Rect rect = listing.GetRect(Text.LineHeight);
            if (!listing.BoundingRectCached.HasValue || rect.Overlaps(listing.BoundingRectCached.Value))
            {
                Rect labelRect = rect.LeftHalf().Rounded();
                Rect fieldRect = rect.RightHalf().Rounded();
                fieldRect.height -= 6f;
                fieldRect.y += 3f;
                TextAnchor anchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(labelRect, label + ": ");
                Text.Anchor = anchor;
                Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
            }
            listing.Gap(listing.verticalSpacing);
            questBuffers[bufferKey] = buffer;
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ProgressionPacingModSettings.UpdateResearchProjectCosts();
            ProgressionPacingModSettings.UpdateQuestPacing();
        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }

    public class ProgressionPacingGameComponent : GameComponent
    {
        public Dictionary<TechLevel, float> savedMultipliers = new Dictionary<TechLevel, float>();
        public Dictionary<TechLevel, int> savedRoundingMultiples = new Dictionary<TechLevel, int>();
        public Dictionary<TechLevel, int> savedAddons = new Dictionary<TechLevel, int>();

        public ProgressionPacingGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref savedMultipliers, "savedMultipliers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref savedRoundingMultiples, "savedRoundingMultiples", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref savedAddons, "savedAddons", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (savedMultipliers == null) savedMultipliers = new Dictionary<TechLevel, float>();
                if (savedRoundingMultiples == null) savedRoundingMultiples = new Dictionary<TechLevel, int>();
                if (savedAddons == null) savedAddons = new Dictionary<TechLevel, int>();

                FixResearchProgress();
                UpdateSavedMultipliers();
            }
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            UpdateSavedMultipliers();
        }

        public void UpdateSavedMultipliers()
        {
            savedMultipliers.Clear();
            savedRoundingMultiples.Clear();
            savedAddons.Clear();
            foreach (TechLevel level in Enum.GetValues(typeof(TechLevel)))
            {
                if (level != TechLevel.Undefined)
                {
                    savedMultipliers[level] = ProgressionPacingModSettings.GetMultiplierForTechLevel(level);
                    savedRoundingMultiples[level] = ProgressionPacingModSettings.GetRoundingMultipleForTechLevel(level);
                    savedAddons[level] = ProgressionPacingModSettings.GetAddonForTechLevel(level);
                }
            }
        }

        private void FixResearchProgress()
        {
            if (Find.ResearchManager == null) return;
            var progressDict = Find.ResearchManager.progress;
            if (progressDict == null) return;

            bool wasEmpty = savedMultipliers.Count == 0;

            foreach (var def in progressDict.Keys.ToList())
            {
                if (def.knowledgeCost > 0) continue;
                if (ProgressionPacingModSettings.excludeGravdata && ModsConfig.IsActive("vanillaexpanded.gravship") && def.tab?.defName == "VGE_Gravtech") continue;

                if (progressDict.TryGetValue(def, out float currentProgress) && currentProgress > 0)
                {
                    float oldMultiplier = wasEmpty ? 1f : (savedMultipliers.TryGetValue(def.techLevel, out float m) ? m : 1f);
                    int oldRounding = wasEmpty ? 1 : (savedRoundingMultiples.TryGetValue(def.techLevel, out int r) ? r : 1);
                    int oldAddon = wasEmpty ? 0 : (savedAddons.TryGetValue(def.techLevel, out int a) ? a : 0);

                    float originalVanillaCost = ProgressionPacingModSettings.originalResearchCosts != null && ProgressionPacingModSettings.originalResearchCosts.TryGetValue(def, out float orig) ? orig : def.baseCost;

                    float savedCost = ProgressionPacingModSettings.ComputeAdjustedCost(originalVanillaCost, oldMultiplier, oldAddon, oldRounding);
                    float currentCost = def.baseCost;

                    if (savedCost > 0 && currentCost > 0 && Math.Abs(savedCost - currentCost) > 0.1f)
                    {
                        float ratio = currentCost / savedCost;
                        float newProgress = currentProgress * ratio;

                        if (currentProgress >= savedCost - 0.01f)
                        {
                            newProgress = Mathf.Max(newProgress, currentCost);
                        }

                        progressDict[def] = newProgress;
                    }
                }
            }
        }
    }
}
