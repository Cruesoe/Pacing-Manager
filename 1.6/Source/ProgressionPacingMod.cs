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
        private readonly Dictionary<TechLevel, string> roundingBuffers = new Dictionary<TechLevel, string>();
        private readonly Dictionary<string, string> questBuffers = new Dictionary<string, string>();
        private string powerOutputRoundingBuffer;

        private const float NumericFieldHeight = 30f;
        private const float NumericFieldPadding = 16f;
        private const float ControlGap = 12f;
        private const float MinSliderWidth = 80f;
        private const float ScrollbarWidth = 20f;
        private const float ContentRightPadding = 16f;
        private const int AddonMaxValue = 99999999;

        private static readonly Color ResetButtonColor = new Color(0.48f, 0.12f, 0.12f);
        internal static readonly Color CloseButtonColor = new Color(0.50f, 0.62f, 0.78f);
        internal static bool StyleCloseButton;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            StyleCloseButton = true;
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

            researchSectionExpanded = DrawSectionHeader(listing, "PP_ResearchSection".Translate(), "PP_ResearchSectionTip".Translate(), "PP_ResearchResetTip".Translate(), researchSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetResearchSettings();
                addonBuffers.Clear();
                roundingBuffers.Clear();
            });
            if (researchSectionExpanded)
            {
                DrawResearchSection(listing);
            }

            listing.Gap();
            powerSectionExpanded = DrawSectionHeader(listing, "PP_PowerSection".Translate(), "PP_PowerSectionTip".Translate(), "PP_PowerResetTip".Translate(), powerSectionExpanded, () =>
            {
                ProgressionPacingModSettings.ResetPowerSettings();
                powerOutputRoundingBuffer = null;
            });
            if (powerSectionExpanded)
            {
                DrawPowerSection(listing);
            }

            listing.Gap();
            questSectionExpanded = DrawSectionHeader(listing, "PP_QuestSection".Translate(), "PP_QuestSectionTip".Translate(), "PP_QuestResetTip".Translate(), questSectionExpanded, () =>
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

        private static bool DrawSectionHeader(Listing_Standard listing, string label, string headerTip, string resetTip, bool expanded, Action onReset)
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
            TooltipHandler.TipRegion(toggleRect, headerTip);
            if (Widgets.ButtonInvisible(toggleRect))
            {
                expanded = !expanded;
            }

            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(resetRect, resetTip);
            if (DrawColoredButton(resetRect, "Reset".Translate(), ResetButtonColor, Color.white))
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
                listing.CheckboxLabeled("PP_ExcludeGravdata".Translate(), ref ProgressionPacingModSettings.excludeGravdata, "PP_ExcludeGravdataTip".Translate());
            }

            listing.GapLine();
            ProgressionPacingModSettings.GetResearchTotals(out int totalTechs, out float totalPoints);
            Text.Font = GameFont.Small;
            Rect totalRow = listing.GetRect(Text.LineHeight + 4f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(totalRow, "PP_ResearchAllTotals".Translate(totalTechs.ToString("N0"), totalPoints.ToString("N0")));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(totalRow, "PP_ResearchAllTotalsTip".Translate());
        }

        private void DrawEraRow(Listing_Standard listing, TechLevel techLevel)
        {
            ProgressionPacingModSettings.GetEraTotals(techLevel, out int totalTechs, out float totalPoints);
            if (totalTechs == 0) return;

            string eraName = techLevel.ToString();
            string totalsText = "PP_ResearchTotalTechs".Translate() + ": " + totalTechs + "    " + "PP_ResearchTotalPoints".Translate() + ": " + totalPoints.ToString("N0");
            string addonLabel = "PP_ResearchAddon".Translate() + ":";
            string roundingLabel = "PP_ResearchRounding".Translate() + ":";
            float fieldWidth = NumericFieldWidth();
            float rightLabelWidth = Mathf.Max(Text.CalcSize(addonLabel).x, Text.CalcSize(roundingLabel).x) + 6f;
            float rightBlockWidth = rightLabelWidth + fieldWidth;

            int addon = ProgressionPacingModSettings.GetAddonForTechLevel(techLevel);
            if (!addonBuffers.TryGetValue(techLevel, out string addonBuffer) || addonBuffer == null)
            {
                addonBuffer = addon.ToString();
            }
            int rounding = ProgressionPacingModSettings.GetRoundingMultipleForTechLevel(techLevel);
            if (!roundingBuffers.TryGetValue(techLevel, out string roundingBuffer) || roundingBuffer == null)
            {
                roundingBuffer = rounding.ToString();
            }

            Text.Font = GameFont.Medium;
            Rect titleRow = listing.GetRect(NumericFieldHeight);
            float titleWidth = Text.CalcSize(eraName).x + 16f;
            Rect titleRect = new Rect(titleRow.x, titleRow.y, titleWidth, titleRow.height);
            Rect addonBlock = new Rect(titleRow.xMax - rightBlockWidth, titleRow.y, rightBlockWidth, titleRow.height);
            Rect totalsRect = new Rect(titleRect.xMax, titleRow.y, Mathf.Max(0f, addonBlock.x - titleRect.xMax - ControlGap), titleRow.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, eraName);
            Text.Font = GameFont.Small;
            Widgets.Label(totalsRect, totalsText);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(titleRect, "PP_ResearchEraTip".Translate(eraName));
            TooltipHandler.TipRegion(totalsRect, "PP_ResearchTotalsTip".Translate());
            if (IsRectVisible(listing, titleRow))
            {
                DrawRightLabeledNumeric(addonBlock, addonLabel, rightLabelWidth, fieldWidth, ref addon, ref addonBuffer, 0, AddonMaxValue, "PP_ResearchAddonTip".Translate());
            }

            float multiplier = ProgressionPacingModSettings.techLevelMultipliers[techLevel];
            string multiplierLabel = "PP_ResearchMultiplier".Translate() + ": " + multiplier.ToStringPercent();
            float multiplierLabelWidth = Text.CalcSize(multiplierLabel).x + 8f;

            Rect controlRow = listing.GetRect(NumericFieldHeight);
            Rect roundingBlock = new Rect(controlRow.xMax - rightBlockWidth, controlRow.y, rightBlockWidth, controlRow.height);
            Rect sliderArea = new Rect(controlRow.x, controlRow.y, Mathf.Max(MinSliderWidth, roundingBlock.x - controlRow.x - ControlGap), controlRow.height);
            if (IsRectVisible(listing, controlRow))
            {
                Rect multiplierLabelRect = new Rect(sliderArea.x, sliderArea.y, multiplierLabelWidth, sliderArea.height);
                Rect sliderRect = new Rect(multiplierLabelRect.xMax, sliderArea.y + (sliderArea.height - 22f) / 2f, Mathf.Max(MinSliderWidth, sliderArea.width - multiplierLabelWidth), 22f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(multiplierLabelRect, multiplierLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                multiplier = Widgets.HorizontalSlider(sliderRect, multiplier, 0.01f, 10f);
                TooltipHandler.TipRegion(sliderArea, "PP_ResearchMultiplierTip".Translate());
                DrawRightLabeledNumeric(roundingBlock, roundingLabel, rightLabelWidth, fieldWidth, ref rounding, ref roundingBuffer, 1, 10000, "PP_ResearchRoundingTip".Translate());
            }

            addonBuffers[techLevel] = addonBuffer;
            roundingBuffers[techLevel] = roundingBuffer;
            ProgressionPacingModSettings.techLevelMultipliers[techLevel] = multiplier;
            ProgressionPacingModSettings.techLevelAddons[techLevel] = addon;
            ProgressionPacingModSettings.techLevelRoundingMultiples[techLevel] = rounding;
            listing.GapLine();
        }

        private static void DrawRightLabeledNumeric(Rect block, string label, float labelWidth, float fieldWidth, ref int value, ref string buffer, float min, float max, string tooltip)
        {
            Rect labelRect = new Rect(block.x, block.y, labelWidth, block.height);
            Rect fieldRect = new Rect(block.xMax - fieldWidth, block.y, fieldWidth, block.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
            TooltipHandler.TipRegion(block, tooltip);
        }

        private void DrawPowerSection(Listing_Standard listing)
        {
            listing.Indent();
            float powerSliderY = listing.CurHeight;
            string powerOutputLabel = "PP_PowerOutputMultiplier".Translate() + ": " + ProgressionPacingModSettings.powerOutputMultiplier.ToStringPercent();
            ProgressionPacingModSettings.powerOutputMultiplier = listing.SliderLabeled(powerOutputLabel, ProgressionPacingModSettings.powerOutputMultiplier, 0.01f, 10f, labelPct: 0.30f);
            TooltipHandler.TipRegion(new Rect(0f, powerSliderY, listing.ColumnWidth, listing.CurHeight - powerSliderY), "PP_PowerOutputMultiplierTip".Translate());
            listing.Gap(listing.verticalSpacing);
            int powerOutputRoundingValue = ProgressionPacingModSettings.powerOutputRoundingMultiple;
            if (powerOutputRoundingBuffer == null)
            {
                powerOutputRoundingBuffer = powerOutputRoundingValue.ToString();
            }
            DrawLabeledNumeric(listing, "PP_PowerOutputRoundingMultiple".Translate(), NumericFieldWidth(), ref powerOutputRoundingValue, ref powerOutputRoundingBuffer, 1, 10000, "PP_PowerOutputRoundingTip".Translate());
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
            Rect storytellerRect = listing.GetRect(Text.LineHeight + 4f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(storytellerRect, "PP_CurrentStorytellerNamed".Translate(storyteller.LabelCap));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(storytellerRect, "PP_CurrentStorytellerTip".Translate());

            foreach (var entry in entries)
            {
                if (entry.cycleCount > 1)
                {
                    listing.Label("PP_QuestCycleOnly".Translate(entry.cycleNumber));
                }
                QuestPacingValues values = ProgressionPacingModSettings.GetQuestPacingValues(entry.key);
                string daysLabel = "PP_QuestCycleDays".Translate() + ":";
                string spacingLabel = "PP_QuestMinDaysBetween".Translate() + ":";
                string countLabel = "PP_QuestCount".Translate() + ":";
                float questLabelColumn = Mathf.Max(Text.CalcSize(daysLabel).x, Text.CalcSize(spacingLabel).x, Text.CalcSize(countLabel).x) + 8f;
                DrawQuestFloatField(listing, entry.key + ".onDays", daysLabel, ref values.onDays, 0.1f, 1000f, "PP_QuestCycleDaysTip".Translate(), questLabelColumn);
                DrawQuestFloatField(listing, entry.key + ".minSpacingDays", spacingLabel, ref values.minSpacingDays, 0f, 1000f, "PP_QuestMinDaysBetweenTip".Translate(), questLabelColumn);
                DrawQuestFloatField(listing, entry.key + ".questsEachCycle", countLabel, ref values.questsEachCycle, 0f, 100f, "PP_QuestCountTip".Translate(), questLabelColumn);
            }
        }

        private void DrawQuestFloatField(Listing_Standard listing, string bufferKey, string label, ref float value, float min, float max, string tooltip, float labelColumnWidth)
        {
            if (!questBuffers.TryGetValue(bufferKey, out string buffer) || buffer == null)
            {
                buffer = value.ToString();
            }
            DrawLabeledNumeric(listing, label, NumericFieldWidth(), ref value, ref buffer, min, max, tooltip, labelColumnWidth);
            questBuffers[bufferKey] = buffer;
        }

        internal static bool DrawColoredButton(Rect rect, string label, Color background, Color textColor)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color fill = background;
                Texture2D atlas = Widgets.ButtonBGAtlas;
                if (Mouse.IsOver(rect))
                {
                    atlas = Input.GetMouseButton(0) ? Widgets.ButtonBGAtlasClick : Widgets.ButtonBGAtlasMouseover;
                    fill = Input.GetMouseButton(0)
                        ? Color.Lerp(background, Color.black, 0.10f)
                        : Color.Lerp(background, Color.white, 0.12f);
                }

                Color previous = GUI.color;
                GUI.color = Color.white;
                Widgets.DrawAtlas(rect, atlas);
                Widgets.DrawBoxSolid(rect, new Color(fill.r, fill.g, fill.b, 0.78f));

                TextAnchor previousAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = textColor;
                Widgets.Label(rect, label);
                GUI.color = previous;
                Text.Anchor = previousAnchor;
            }

            return ColoredButtonClicked(rect);
        }

        // MouseDown only. ButtonInvisible/GUI.Button can desync control IDs between Layout and
        // Repaint and make Unity retry OnGUI until FPS collapses.
        private static bool ColoredButtonClicked(Rect rect)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
            {
                return false;
            }
            if (!Mouse.IsOver(rect))
            {
                return false;
            }
            Event.current.Use();
            return true;
        }

        private static float NumericFieldWidth()
        {
            return Mathf.Ceil(Text.CalcSize("88888888").x) + NumericFieldPadding;
        }

        private static bool IsRectVisible(Listing_Standard listing, Rect rect)
        {
            return !listing.BoundingRectCached.HasValue || rect.Overlaps(listing.BoundingRectCached.Value);
        }

        private static void DrawLabeledNumeric(Listing_Standard listing, string label, float fieldWidth, ref int value, ref string buffer, float min, float max, string tooltip)
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
                TooltipHandler.TipRegion(row, tooltip);
            }
            listing.Gap(listing.verticalSpacing);
        }

        private static void DrawLabeledNumeric(Listing_Standard listing, string label, float fieldWidth, ref float value, ref string buffer, float min, float max, string tooltip, float labelColumnWidth = -1f)
        {
            Rect row = listing.GetRect(NumericFieldHeight);
            if (IsRectVisible(listing, row))
            {
                float labelWidth = labelColumnWidth > 0f ? labelColumnWidth : Text.CalcSize(label).x + 8f;
                Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
                Rect fieldRect = new Rect(labelRect.xMax, row.y, fieldWidth, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
                TooltipHandler.TipRegion(row, tooltip);
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
