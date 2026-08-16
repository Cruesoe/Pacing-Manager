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

        private float scrollHeight = 0f;
        private Vector2 scrollPosition = Vector2.zero;
        private bool researchSectionExpanded;
        private bool powerSectionExpanded;
        private bool questSectionExpanded;
        private int lastSettingsFrame = -100;
        private readonly Dictionary<TechLevel, string> addonBuffers = new Dictionary<TechLevel, string>();
        private readonly Dictionary<string, string> questBuffers = new Dictionary<string, string>();
        private string powerOutputRoundingBuffer;

        private const float NumericFieldHeight = 30f;
        private const float NumericFieldPadding = 16f;
        private const float ControlGap = 12f;
        private const float MinSliderWidth = 80f;
        private const float ScrollbarWidth = 20f;
        private const float ContentRightPadding = 16f;
        private const int AddonMaxValue = 99999999;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            ProgressionPacingModSettings.EnsureDictionaries();
            if (Time.frameCount > lastSettingsFrame + 1)
            {
                researchSectionExpanded = false;
                powerSectionExpanded = false;
                questSectionExpanded = false;
                scrollPosition = Vector2.zero;
            }
            lastSettingsFrame = Time.frameCount;

            float viewWidth = inRect.width - ScrollbarWidth;
            float listingWidth = viewWidth - ContentRightPadding;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(scrollHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(new Rect(0f, 0f, listingWidth, 99999f));
            listing.ColumnWidth = listingWidth;

            researchSectionExpanded = DrawSectionHeader(listing, "PP_ResearchSection".Translate(), researchSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetResearchSettings();
                addonBuffers.Clear();
            });
            if (researchSectionExpanded)
            {
                DrawResearchSection(listing);
            }

            listing.Gap();
            powerSectionExpanded = DrawSectionHeader(listing, "PP_PowerSection".Translate(), powerSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetPowerSettings();
                powerOutputRoundingBuffer = null;
            });
            if (powerSectionExpanded)
            {
                DrawPowerSection(listing);
            }

            listing.Gap();
            questSectionExpanded = DrawSectionHeader(listing, "PP_QuestSection".Translate(), questSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetQuestPacing();
                questBuffers.Clear();
            });
            if (questSectionExpanded)
            {
                DrawQuestSection(listing);
            }

            scrollHeight = listing.CurHeight + 24f;
            listing.End();
            Widgets.EndScrollView();
        }

        private static bool DrawSectionHeader(Listing_Standard listing, string label, bool expanded, Action onReset)
        {
            Text.Font = GameFont.Medium;
            float height = Text.LineHeight + 8f;
            Rect row = listing.GetRect(height);
            Rect resetRect = new Rect(row.xMax - 110f, row.y + (row.height - 30f) / 2f, 110f, 30f);
            Rect toggleRect = new Rect(row.x, row.y, resetRect.x - row.x - 8f, row.height);

            Widgets.DrawHighlightIfMouseover(toggleRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(toggleRect, (expanded ? "▼  " : "▶  ") + label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(toggleRect))
            {
                expanded = !expanded;
            }

            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(resetRect, "Reset".Translate()))
            {
                onReset?.Invoke();
            }
            listing.GapLine();
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

            string eraName = techLevel.ToString();
            string totalsText = "PP_ResearchTotalTechs".Translate() + ": " + totalTechs + "    " + "PP_ResearchTotalPoints".Translate() + ": " + totalPoints.ToString("N0");

            Text.Font = GameFont.Medium;
            Rect titleRow = listing.GetRect(Text.LineHeight);
            float titleWidth = Text.CalcSize(eraName).x + 16f;
            Rect titleRect = new Rect(titleRow.x, titleRow.y, titleWidth, titleRow.height);
            Rect totalsRect = new Rect(titleRect.xMax, titleRow.y, titleRow.width - titleWidth, titleRow.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, eraName);
            Text.Font = GameFont.Small;
            Widgets.Label(totalsRect, totalsText);
            Text.Anchor = TextAnchor.UpperLeft;

            int addon = ProgressionPacingModSettings.GetAddonForTechLevel(techLevel);
            if (!addonBuffers.TryGetValue(techLevel, out string buffer) || buffer == null)
            {
                buffer = addon.ToString();
            }

            float multiplier = ProgressionPacingModSettings.techLevelMultipliers[techLevel];
            string multiplierLabel = "PP_ResearchMultiplier".Translate() + ": " + multiplier.ToStringPercent();
            string addonLabel = "PP_ResearchAddon".Translate() + ":";
            float fieldWidth = NumericFieldWidth();
            float addonLabelWidth = Text.CalcSize(addonLabel).x + 6f;
            float multiplierLabelWidth = Text.CalcSize(multiplierLabel).x + 8f;

            Rect controlRow = listing.GetRect(NumericFieldHeight);
            if (IsRectVisible(listing, controlRow))
            {
                Rect addonFieldRect = new Rect(controlRow.xMax - fieldWidth, controlRow.y, fieldWidth, controlRow.height);
                Rect addonLabelRect = new Rect(addonFieldRect.x - addonLabelWidth, controlRow.y, addonLabelWidth, controlRow.height);
                Rect sliderArea = new Rect(controlRow.x, controlRow.y, Mathf.Max(MinSliderWidth, addonLabelRect.x - controlRow.x - ControlGap), controlRow.height);
                Rect multiplierLabelRect = new Rect(sliderArea.x, sliderArea.y, multiplierLabelWidth, sliderArea.height);
                Rect sliderRect = new Rect(multiplierLabelRect.xMax, sliderArea.y + (sliderArea.height - 22f) / 2f, Mathf.Max(MinSliderWidth, sliderArea.width - multiplierLabelWidth), 22f);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(multiplierLabelRect, multiplierLabel);
                Widgets.Label(addonLabelRect, addonLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                multiplier = Widgets.HorizontalSlider(sliderRect, multiplier, 0.01f, 10f);
                Widgets.TextFieldNumeric(addonFieldRect, ref addon, ref buffer, 0, AddonMaxValue);
            }

            addonBuffers[techLevel] = buffer;
            ProgressionPacingModSettings.techLevelMultipliers[techLevel] = multiplier;
            ProgressionPacingModSettings.techLevelAddons[techLevel] = addon;
            listing.GapLine();
        }

        private void DrawPowerSection(Listing_Standard listing)
        {
            listing.Indent();
            string powerOutputLabel = "PP_PowerOutputMultiplier".Translate() + ": " + ProgressionPacingModSettings.powerOutputMultiplier.ToStringPercent();
            ProgressionPacingModSettings.powerOutputMultiplier = listing.SliderLabeled(powerOutputLabel, ProgressionPacingModSettings.powerOutputMultiplier, 0.01f, 10f, labelPct: 0.30f);
            listing.Gap(listing.verticalSpacing);
            int powerOutputRoundingValue = ProgressionPacingModSettings.powerOutputRoundingMultiple;
            if (powerOutputRoundingBuffer == null)
            {
                powerOutputRoundingBuffer = powerOutputRoundingValue.ToString();
            }
            DrawLabeledNumeric(listing, "PP_PowerOutputRoundingMultiple".Translate(), NumericFieldWidth(), ref powerOutputRoundingValue, ref powerOutputRoundingBuffer, 1, 10000);
            ProgressionPacingModSettings.powerOutputRoundingMultiple = powerOutputRoundingValue;
            listing.Outdent();
        }

        private void DrawQuestSection(Listing_Standard listing)
        {
            ProgressionPacingModSettings.EnsureDictionaries();
            StorytellerDef storyteller = ProgressionPacingModSettings.CurrentStorytellerDef();
            if (storyteller == null)
            {
                listing.Label("PP_QuestNeedsColony".Translate());
                return;
            }

            var entries = ProgressionPacingModSettings.CurrentStorytellerQuestComps().ToList();
            if (entries.Count == 0)
            {
                listing.Label("PP_QuestNoRandomQuests".Translate(storyteller.LabelCap));
                return;
            }

            Text.Font = GameFont.Medium;
            listing.Label(storyteller.LabelCap);
            Text.Font = GameFont.Small;

            foreach (var entry in entries)
            {
                if (entry.cycleCount > 1)
                {
                    listing.Label("PP_QuestCycleOnly".Translate(entry.cycleNumber));
                }
                QuestPacingValues values = ProgressionPacingModSettings.GetQuestPacingValues(entry.key);
                DrawQuestFloatField(listing, entry.key + ".onDays", "PP_QuestCycleDays".Translate(), ref values.onDays, 0.1f, 1000f);
                DrawQuestFloatField(listing, entry.key + ".minSpacingDays", "PP_QuestMinDaysBetween".Translate(), ref values.minSpacingDays, 0f, 1000f);
                DrawQuestFloatField(listing, entry.key + ".questsEachCycle", "PP_QuestCount".Translate(), ref values.questsEachCycle, 0f, 100f);
            }
        }

        private void DrawQuestFloatField(Listing_Standard listing, string bufferKey, string label, ref float value, float min, float max)
        {
            if (!questBuffers.TryGetValue(bufferKey, out string buffer) || buffer == null)
            {
                buffer = value.ToString();
            }
            DrawLabeledNumeric(listing, label + ":", NumericFieldWidth(), ref value, ref buffer, min, max);
            questBuffers[bufferKey] = buffer;
        }

        private static float NumericFieldWidth()
        {
            return Mathf.Ceil(Text.CalcSize("88888888").x) + NumericFieldPadding;
        }

        private static bool IsRectVisible(Listing_Standard listing, Rect rect)
        {
            return !listing.BoundingRectCached.HasValue || rect.Overlaps(listing.BoundingRectCached.Value);
        }

        private static void DrawLabeledNumeric(Listing_Standard listing, string label, float fieldWidth, ref int value, ref string buffer, float min, float max)
        {
            Rect row = listing.GetRect(NumericFieldHeight);
            if (IsRectVisible(listing, row))
            {
                float labelWidth = Text.CalcSize(label).x + 8f;
                Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
                Rect fieldRect = new Rect(labelRect.xMax, row.y, fieldWidth, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
            }
            listing.Gap(listing.verticalSpacing);
        }

        private static void DrawLabeledNumeric(Listing_Standard listing, string label, float fieldWidth, ref float value, ref string buffer, float min, float max)
        {
            Rect row = listing.GetRect(NumericFieldHeight);
            if (IsRectVisible(listing, row))
            {
                float labelWidth = Text.CalcSize(label).x + 8f;
                Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
                Rect fieldRect = new Rect(labelRect.xMax, row.y, fieldWidth, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
            }
            listing.Gap(listing.verticalSpacing);
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
                ProgressionPacingModSettings.UpdateQuestPacing();
            }
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            UpdateSavedMultipliers();
            ProgressionPacingModSettings.UpdateQuestPacing();
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
