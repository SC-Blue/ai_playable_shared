using System;
using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;
using Supercent.PlayableAI.Generation.Editor.Pipeline;
using UnityEngine;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public sealed class CompiledPlayablePlanValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();
    }

    public static class CompiledPlayablePlanValidator
    {
        private struct EnvironmentCellCoordinate
        {
            public int X;
            public int Y;

            public EnvironmentCellCoordinate(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private const float ENVIRONMENT_TILE_STEP_FLOOR = 1f;
        private const float ENVIRONMENT_COORD_UNIT = 0.5f;
        private const float ENVIRONMENT_TILE_ALIGNMENT_TOLERANCE_RATIO = 0.05f;
        private const float ENVIRONMENT_TILE_ALIGNMENT_MIN_TOLERANCE = 0.01f;

        public static CompiledPlayablePlanValidationResult Validate(
            CompiledPlayablePlan plan,
            PlayableObjectCatalog catalog,
            LayoutSpecDocument layoutSpec = null)
        {
            var result = new CompiledPlayablePlanValidationResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (plan == null)
            {
                Fail(result, "CompiledPlayablePlan이 null입니다.");
                return FinalizeFailure(result);
            }

            PromptIntentContractRegistry.SetActiveFeatureDescriptors(catalog != null ? catalog.FeatureDescriptors : null);
            PromptIntentCapabilityRegistry.SetActiveFeatureDescriptors(catalog != null ? catalog.FeatureDescriptors : null);
            try
            {
                Dictionary<string, int> objectDesignLookup = BuildObjectDesignLookup(plan.objectDesigns);
                ValidateCatalogFeatureAvailability(plan, catalog, result);
                ValidateObjectDesignSelections(plan.objectDesigns, catalog, result);
                ValidateContentSelections(plan.contentSelections, catalog, result);
                Dictionary<string, CompiledSpawnData> spawnLookup = BuildSpawnLookup(plan.spawns);

                ValidateCurrencies(plan.currencies, result);
                Dictionary<string, int> currencyUnits = BuildCurrencyUnitLookup(plan.currencies);
                int primaryCurrencyUnitValue = ResolvePrimaryCurrencyUnitValue(plan.currencies);
                ValidateUnlocks(plan.unlocks, spawnLookup, currencyUnits, result);
                ValidateItemPrices(plan.itemPrices, catalog, primaryCurrencyUnitValue, result);
                ValidateFeatureAcceptedItems(plan.featureAcceptedItems, spawnLookup, catalog, result);
                ValidateFeatureOutputItems(plan.spawns, plan.featureOutputItems, spawnLookup, catalog, result);
                ValidatePlayerOptions(plan.playerOptions, result);
                ValidateFeatureOptions(plan.featureOptions, spawnLookup, catalog, result);
                ValidateFeatureLayouts(plan.featureLayouts, spawnLookup, catalog, result);
                ValidateCompiledFlowBeats(plan.beats, plan.actions, spawnLookup, catalog, currencyUnits, result);
                HashSet<string> declaredSourceImageIds = ValidateSourceImages(layoutSpec, result);
                ValidateCustomerPaths(layoutSpec, spawnLookup, catalog, result);
                ValidateSourceImageReferences(layoutSpec, declaredSourceImageIds, result);
                ValidateEnvironmentPerimeterThickness(layoutSpec, catalog, result);
                ValidatePlacementSpatialSemantics(layoutSpec, result);
                ValidateRuntimeOwnedDesignSources(plan.spawns, plan.featureAcceptedItems, plan.featureOutputItems, plan.featureOptions, plan.itemPrices, plan.currencies, objectDesignLookup, catalog, result);
                ValidateImageLayoutEnvironmentPresence(layoutSpec, catalog, result);
            }
            finally
            {
                PromptIntentCapabilityRegistry.ClearActiveFeatureDescriptors();
                PromptIntentContractRegistry.ClearActiveFeatureDescriptors();
            }

            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            result.IsValid = true;
            result.Message = "유효합니다.";
            return result;
        }

        private static void ValidateCatalogFeatureAvailability(
            CompiledPlayablePlan plan,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            if (!HasFeatureDescriptorAuthority(catalog))
                return;

            PlayableScenarioFeatureOptionDefinition[] featureOptions = plan.featureOptions ?? new PlayableScenarioFeatureOptionDefinition[0];
            for (int i = 0; i < featureOptions.Length; i++)
            {
                PlayableScenarioFeatureOptionDefinition definition = featureOptions[i];
                string featureType = PlayableFeatureTypeIds.Normalize(definition != null ? definition.featureType : string.Empty);
                if (string.IsNullOrEmpty(featureType))
                    continue;

                if (!catalog.IsSupportedFeatureType(featureType))
                    Fail(result, "featureOptions[" + i + "].featureType '" + featureType + "'는 현재 catalog에 없는 feature입니다.");
            }

        }

        private static bool HasFeatureDescriptorAuthority(PlayableObjectCatalog catalog)
        {
            return catalog != null && (catalog.FeatureDescriptors ?? Array.Empty<FeatureDescriptor>()).Length > 0;
        }

        private static void ValidateCompiledFlowBeats(
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            Dictionary<string, int> currencyUnits,
            CompiledPlayablePlanValidationResult result)
        {
            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            var seenBeatIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < safeBeats.Length; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null)
                {
                    Fail(result, "beats[" + i + "]가 null입니다.");
                    continue;
                }

                string beatId = beat.id != null ? beat.id.Trim() : string.Empty;
                if (string.IsNullOrEmpty(beatId))
                {
                    Fail(result, "beats[" + i + "].id가 필요합니다.");
                }
                else if (!seenBeatIds.Add(beatId))
                {
                    Fail(result, "중복된 beats id입니다: '" + beatId + "'.");
                }

                string enterWhenError = StepConditionRules.Validate(beat.enterWhen, "beats[" + i + "].enterWhen");
                if (!string.IsNullOrEmpty(enterWhenError))
                    Fail(result, enterWhenError);

                string completeWhenError = StepConditionRules.Validate(beat.completeWhen, "beats[" + i + "].completeWhen");
                if (!string.IsNullOrEmpty(completeWhenError))
                    Fail(result, completeWhenError);
            }

            HashSet<string> actionIds = ValidateCompiledFlowActions(
                actions,
                seenBeatIds,
                spawnLookup,
                catalog,
                currencyUnits,
                result);

            for (int i = 0; i < safeBeats.Length; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null)
                    continue;

                ValidateBeatConditionTarget(beat.enterWhen, "beats[" + i + "].enterWhen", seenBeatIds, actionIds, currencyUnits, result);
                ValidateBeatConditionTarget(beat.completeWhen, "beats[" + i + "].completeWhen", seenBeatIds, actionIds, currencyUnits, result);
            }
        }

        private static HashSet<string> ValidateCompiledFlowActions(
            FlowActionDefinition[] actions,
            HashSet<string> beatIds,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            Dictionary<string, int> currencyUnits,
            CompiledPlayablePlanValidationResult result)
        {
            FlowActionDefinition[] safeActions = actions ?? new FlowActionDefinition[0];
            var seenActionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeActions.Length; i++)
            {
                FlowActionDefinition action = safeActions[i];
                string actionLabel = "actions[" + i + "]";
                if (action == null)
                {
                    Fail(result, actionLabel + "가 null입니다.");
                    continue;
                }

                string actionId = action.id != null ? action.id.Trim() : string.Empty;
                if (string.IsNullOrEmpty(actionId))
                {
                    Fail(result, actionLabel + ".id가 필요합니다.");
                }
                else if (!seenActionIds.Add(actionId))
                {
                    Fail(result, "actions에 중복된 action id '" + actionId + "'가 있습니다.");
                }

                string ownerBeatId = action.ownerBeatId != null ? action.ownerBeatId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(ownerBeatId))
                {
                    Fail(result, actionLabel + ".ownerBeatId가 필요합니다.");
                }
                else if (!beatIds.Contains(ownerBeatId))
                {
                    Fail(result, actionLabel + ".ownerBeatId '" + ownerBeatId + "'가 beats[]에 없습니다.");
                }

                string triggerMode = action.triggerMode != null ? action.triggerMode.Trim() : string.Empty;
                if (!string.Equals(triggerMode, FlowActionTriggerModes.ON_BEAT_ENTER, StringComparison.Ordinal) &&
                    !string.Equals(triggerMode, FlowActionTriggerModes.ON_BEAT_COMPLETE, StringComparison.Ordinal) &&
                    !string.Equals(triggerMode, FlowActionTriggerModes.REACTIVE, StringComparison.Ordinal))
                {
                    Fail(result, actionLabel + ".triggerMode '" + action.triggerMode + "'는 지원되지 않습니다.");
                }

                if (string.Equals(triggerMode, FlowActionTriggerModes.REACTIVE, StringComparison.Ordinal))
                {
                    string conditionError = ReactiveConditionRules.ValidateGroup(action.when, actionLabel + ".when");
                    if (!string.IsNullOrEmpty(conditionError))
                        Fail(result, conditionError);
                    ValidateReactiveConditionTargets(action.when, actionLabel + ".when", beatIds, seenActionIds, currencyUnits, result);
                }
                else if (!IsEmptyReactiveGroup(action.when))
                {
                    Fail(result, actionLabel + ".when은 reactive action에서만 사용됩니다.");
                }

                ValidateCompiledFlowActionPayload(
                    action,
                    actionLabel,
                    spawnLookup,
                    catalog,
                    result);
            }

            return seenActionIds;
        }

        private static void ValidateCompiledFlowActionPayload(
            FlowActionDefinition action,
            string label,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            string kind = action.kind != null ? action.kind.Trim() : string.Empty;
            if (string.IsNullOrEmpty(kind))
            {
                Fail(result, label + ".kind가 필요합니다.");
                return;
            }

            FlowActionPayloadDefinition payload = action.payload ?? new FlowActionPayloadDefinition();
            switch (kind)
            {
                case FlowActionKinds.CAMERA_FOCUS:
                    if (IsEmptyCameraFocusPayload(payload.cameraFocus))
                    {
                        Fail(result, label + ".payload.cameraFocus가 필요합니다.");
                        return;
                    }

                    if (!IsEmptyArrowGuidePayload(payload.arrowGuide) ||
                        !IsEmptyRevealPayload(payload.reveal))
                    {
                        Fail(result, label + "에는 camera focus 외 payload가 함께 들어있습니다.");
                    }

                    ValidateCompiledFlowTarget(payload.cameraFocus.targetId, payload.cameraFocus.eventKey, label + ".payload.cameraFocus", spawnLookup, result);
                    break;

                case FlowActionKinds.ARROW_GUIDE:
                    if (IsEmptyArrowGuidePayload(payload.arrowGuide))
                    {
                        Fail(result, label + ".payload.arrowGuide가 필요합니다.");
                        return;
                    }

                    if (!IsEmptyCameraFocusPayload(payload.cameraFocus) ||
                        !IsEmptyRevealPayload(payload.reveal))
                    {
                        Fail(result, label + "에는 arrow guide 외 payload가 함께 들어있습니다.");
                    }

                    ValidateCompiledFlowTarget(payload.arrowGuide.targetId, payload.arrowGuide.eventKey, label + ".payload.arrowGuide", spawnLookup, result);
                    break;

                case FlowActionKinds.REVEAL:
                    if (!IsEmptyCameraFocusPayload(payload.cameraFocus) ||
                        !IsEmptyArrowGuidePayload(payload.arrowGuide))
                    {
                        Fail(result, label + "에는 reveal 외 payload가 함께 들어있습니다.");
                    }

                    ActivationTargetDefinition[] targets = payload.reveal != null ? payload.reveal.targets ?? new ActivationTargetDefinition[0] : new ActivationTargetDefinition[0];
                    if (targets.Length == 0)
                    {
                        Fail(result, label + ".payload.reveal.targets에는 최소 1개의 target이 필요합니다.");
                        return;
                    }

                    var seenTargets = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < targets.Length; i++)
                    {
                        ActivationTargetDefinition target = targets[i];
                        string validationError = ActivationTargetRules.Validate(target, label + ".payload.reveal.targets[" + i + "]");
                        if (!string.IsNullOrEmpty(validationError))
                        {
                            Fail(result, validationError);
                            continue;
                        }

                        string uniqueId = (target.kind != null ? target.kind.Trim() : string.Empty) + ":" + (target.id != null ? target.id.Trim() : string.Empty);
                        if (!seenTargets.Add(uniqueId))
                            Fail(result, label + ".payload.reveal에 중복 target '" + uniqueId + "'가 있습니다.");

                        if (string.Equals(target.kind != null ? target.kind.Trim() : string.Empty, ActivationTargetKinds.SCENE_REF, StringComparison.Ordinal) &&
                            !spawnLookup.ContainsKey(target.id.Trim()))
                        {
                            Fail(result, label + ".payload.reveal.targets[" + i + "].id '" + target.id.Trim() + "'를 compiled spawns에서 찾지 못했습니다.");
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        private static void ValidateBeatConditionTarget(
            StepConditionDefinition condition,
            string label,
            HashSet<string> beatIds,
            HashSet<string> actionIds,
            Dictionary<string, int> currencyUnits,
            CompiledPlayablePlanValidationResult result)
        {
            if (condition == null)
                return;

            string type = condition.type != null ? condition.type.Trim() : string.Empty;
            switch (type)
            {
                case StepConditionRules.CURRENCY_AT_LEAST:
                    int unitValue = ResolveCurrencyUnitValue(currencyUnits, condition.currencyId);
                    if (!IsMoneyMultiple(condition.amount, unitValue))
                        Fail(result, label + ".amount는 currency unitValue의 배수여야 합니다.");
                    break;
                case StepConditionRules.BEAT_COMPLETED:
                    if (!string.IsNullOrWhiteSpace(condition.targetId) && !beatIds.Contains(condition.targetId.Trim()))
                        Fail(result, label + ".targetId '" + condition.targetId + "'가 beats[]에 없습니다.");
                    break;
                case StepConditionRules.ACTION_STARTED:
                case StepConditionRules.ACTION_COMPLETED:
                    if (!string.IsNullOrWhiteSpace(condition.targetId) && !actionIds.Contains(condition.targetId.Trim()))
                        Fail(result, label + ".targetId '" + condition.targetId + "'가 actions[]에 없습니다.");
                    break;
            }
        }

        private static void ValidateReactiveConditionTargets(
            ReactiveConditionGroupDefinition group,
            string label,
            HashSet<string> beatIds,
            HashSet<string> actionIds,
            Dictionary<string, int> currencyUnits,
            CompiledPlayablePlanValidationResult result)
        {
            ReactiveConditionDefinition[] conditions = group != null ? group.conditions ?? new ReactiveConditionDefinition[0] : new ReactiveConditionDefinition[0];
            for (int i = 0; i < conditions.Length; i++)
            {
                ReactiveConditionDefinition condition = conditions[i];
                if (condition == null)
                    continue;

                string type = condition.type != null ? condition.type.Trim() : string.Empty;
                switch (type)
                {
                    case StepConditionRules.CURRENCY_AT_LEAST:
                        int unitValue = ResolveCurrencyUnitValue(currencyUnits, condition.currencyId);
                        if (!IsMoneyMultiple(condition.amount, unitValue))
                            Fail(result, label + ".conditions[" + i + "].amount는 currency unitValue의 배수여야 합니다.");
                        break;
                    case ReactiveConditionRules.BEAT_COMPLETED:
                        if (!string.IsNullOrWhiteSpace(condition.targetId) && !beatIds.Contains(condition.targetId.Trim()))
                            Fail(result, label + ".conditions[" + i + "].targetId '" + condition.targetId + "'가 beats[]에 없습니다.");
                        break;
                    case StepConditionRules.ACTION_STARTED:
                    case StepConditionRules.ACTION_COMPLETED:
                        if (!string.IsNullOrWhiteSpace(condition.targetId) && !actionIds.Contains(condition.targetId.Trim()))
                            Fail(result, label + ".conditions[" + i + "].targetId '" + condition.targetId + "'가 actions[]에 없습니다.");
                        break;
                }
            }
        }

        private static void ValidateCompiledFlowTarget(
            string targetId,
            string eventKey,
            string label,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            CompiledPlayablePlanValidationResult result)
        {
            string normalizedTargetId = targetId != null ? targetId.Trim() : string.Empty;
            string normalizedEventKey = eventKey != null ? eventKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedTargetId))
            {
                Fail(result, label + ".targetId가 필요합니다.");
                return;
            }

            if (!spawnLookup.ContainsKey(normalizedTargetId))
                Fail(result, label + ".targetId '" + normalizedTargetId + "'를 compiled spawns에서 찾지 못했습니다.");
            if (!FlowTargetEventKeys.IsSupported(normalizedEventKey))
                Fail(result, label + ".eventKey '" + eventKey + "'는 지원되지 않습니다.");
        }

        private static Dictionary<string, HashSet<string>> BuildAcceptedItemKeysByTargetId(FeatureAcceptedItemDefinition[] acceptedItems)
        {
            var values = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            FeatureAcceptedItemDefinition[] safeAcceptedItems = acceptedItems ?? new FeatureAcceptedItemDefinition[0];
            for (int i = 0; i < safeAcceptedItems.Length; i++)
            {
                FeatureAcceptedItemDefinition acceptedItem = safeAcceptedItems[i];
                if (acceptedItem == null || string.IsNullOrWhiteSpace(acceptedItem.targetId))
                    continue;

                string targetId = acceptedItem.targetId.Trim();
                string itemKey = ItemRefUtility.ToItemKey(acceptedItem.item);
                if (string.IsNullOrEmpty(itemKey))
                    continue;

                if (!values.TryGetValue(targetId, out HashSet<string> itemKeys))
                {
                    itemKeys = new HashSet<string>(StringComparer.Ordinal);
                    values.Add(targetId, itemKeys);
                }

                itemKeys.Add(itemKey);
            }

            return values;
        }

        private static bool IsEmptyReactiveGroup(ReactiveConditionGroupDefinition group)
        {
            if (group == null)
                return true;

            bool hasMode = !string.IsNullOrWhiteSpace(group.mode);
            bool hasDelay = group.delaySeconds > 0f;
            bool hasConditions = group.conditions != null && group.conditions.Length > 0;
            return !hasMode && !hasDelay && !hasConditions;
        }

        private static bool IsEmptyRevealPayload(RevealActionPayload payload)
        {
            return payload == null || payload.targets == null || payload.targets.Length == 0;
        }

        private static bool IsEmptyCameraFocusPayload(CameraFocusActionPayload payload)
        {
            return payload == null ||
                (string.IsNullOrWhiteSpace(payload.targetId) &&
                 string.IsNullOrWhiteSpace(payload.eventKey) &&
                 payload.movingTime <= 0.5f &&
                 payload.startDelay <= 0f &&
                 payload.returnDelay <= 0f);
        }

        private static bool IsEmptyArrowGuidePayload(ArrowGuideActionPayload payload)
        {
            return payload == null ||
                (string.IsNullOrWhiteSpace(payload.targetId) &&
                 string.IsNullOrWhiteSpace(payload.eventKey) &&
                 payload.autoHideOnBeatExit);
        }

        private static HashSet<string> ValidateSourceImages(
            LayoutSpecDocument layoutSpec,
            CompiledPlayablePlanValidationResult result)
        {
            var declaredSourceImageIds = new HashSet<string>(StringComparer.Ordinal);
            if (layoutSpec == null)
                return declaredSourceImageIds;

            LayoutSpecSourceImageEntry[] sourceImages = layoutSpec.sourceImages ?? new LayoutSpecSourceImageEntry[0];
            for (int i = 0; i < sourceImages.Length; i++)
            {
                LayoutSpecSourceImageEntry entry = sourceImages[i];
                if (entry == null)
                {
                    Fail(result, "layoutSpec.sourceImages[" + i + "]가 null입니다.");
                    continue;
                }

                string sourceImageId = entry.sourceImageId != null ? entry.sourceImageId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(sourceImageId))
                {
                    Fail(result, "layoutSpec.sourceImages[" + i + "].sourceImageId는 필수입니다.");
                    continue;
                }

                if (!declaredSourceImageIds.Add(sourceImageId))
                    Fail(result, "중복된 layoutSpec.sourceImages.sourceImageId '" + sourceImageId + "'입니다.");
            }

            return declaredSourceImageIds;
        }

        private static void ValidateSourceImageReferences(
            LayoutSpecDocument layoutSpec,
            HashSet<string> declaredSourceImageIds,
            CompiledPlayablePlanValidationResult result)
        {
            if (layoutSpec == null)
                return;

            ValidatePlacementSourceImageReferences(layoutSpec.placements, declaredSourceImageIds, result);
            ValidateEnvironmentSourceImageReferences(layoutSpec.environment, declaredSourceImageIds, result);
            ValidateCustomerPathSourceImageReferences(layoutSpec.customerPaths, declaredSourceImageIds, result);
        }

        private static void ValidateCurrencies(CurrencyDefinition[] currencies, CompiledPlayablePlanValidationResult result)
        {
            CurrencyDefinition[] safeCurrencies = currencies ?? new CurrencyDefinition[0];
            if (safeCurrencies.Length == 0)
            {
                Fail(result, "currencies[]에는 최소 1개의 entry가 필요합니다.");
                return;
            }

            var seenCurrencyIds = new HashSet<string>(StringComparer.Ordinal);
            int validCurrencyCount = 0;
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                CurrencyDefinition currency = safeCurrencies[i];
                if (currency == null)
                {
                    Fail(result, "currencies[" + i + "]가 null입니다.");
                    continue;
                }

                string currencyId = string.IsNullOrWhiteSpace(currency.currencyId) ? string.Empty : currency.currencyId.Trim();
                if (string.IsNullOrEmpty(currencyId))
                {
                    Fail(result, "currencies[" + i + "].currencyId가 필요합니다.");
                    continue;
                }

                validCurrencyCount++;
                if (!seenCurrencyIds.Add(currencyId))
                    Fail(result, "중복된 currencies currencyId '" + currencyId + "'입니다.");

                if (currency.unitValue <= 0)
                    Fail(result, "currencies[" + i + "].unitValue는 0보다 커야 합니다.");
                else if (!IsMoneyMultiple(currency.startBalance, currency.unitValue))
                    Fail(result, "currencies[" + i + "].startBalance는 unitValue의 배수여야 합니다.");

                string visualModeError = CurrencyStartVisualRules.Validate(currency.startVisualMode, "currencies[" + i + "].startVisualMode");
                if (!string.IsNullOrEmpty(visualModeError))
                    Fail(result, visualModeError);
            }

            if (validCurrencyCount > 1)
                Fail(result, "현재 editor-based core/hud bake는 currencies[] 1개만 지원합니다.");
        }

        private static void ValidateUnlocks(
            UnlockDefinition[] unlocks,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            Dictionary<string, int> currencyUnits,
            CompiledPlayablePlanValidationResult result)
        {
            UnlockDefinition[] safeUnlocks = unlocks ?? new UnlockDefinition[0];
            var seenUnlockerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeUnlocks.Length; i++)
            {
                UnlockDefinition unlock = safeUnlocks[i];
                if (unlock == null)
                {
                    Fail(result, "unlocks[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(unlock.unlockerId))
                {
                    Fail(result, "unlocks[" + i + "].unlockerId가 필요합니다.");
                }
                else if (!seenUnlockerIds.Add(unlock.unlockerId.Trim()))
                {
                    Fail(result, "중복된 unlockerId입니다: '" + unlock.unlockerId.Trim() + "'.");
                }

                if (string.IsNullOrWhiteSpace(unlock.currencyId))
                    Fail(result, "unlocks[" + i + "].currencyId가 필요합니다.");

                if (unlock.cost <= 0)
                    Fail(result, "unlocks[" + i + "].cost는 0보다 커야 합니다.");
                else if (!IsMoneyMultiple(unlock.cost, ResolveCurrencyUnitValue(currencyUnits, unlock.currencyId)))
                    Fail(result, "unlocks[" + i + "].cost는 currency unitValue의 배수여야 합니다.");

                ActivationTargetDefinition[] targets = unlock.targets ?? new ActivationTargetDefinition[0];
                if (targets.Length == 0)
                {
                    Fail(result, "unlocks[" + i + "]에는 최소 1개의 target이 필요합니다.");
                    continue;
                }

                for (int j = 0; j < targets.Length; j++)
                {
                    ActivationTargetDefinition target = targets[j];
                    string validationError = ActivationTargetRules.Validate(target, "unlocks[" + i + "].targets[" + j + "]");
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        Fail(result, validationError);
                        continue;
                    }

                    if (target == null || !string.Equals(target.kind != null ? target.kind.Trim() : string.Empty, ActivationTargetKinds.SCENE_REF, StringComparison.Ordinal))
                        continue;

                    string targetId = target.id != null ? target.id.Trim() : string.Empty;
                    if (!spawnLookup.ContainsKey(targetId))
                        Fail(result, "unlocks[" + i + "].targets[" + j + "].id '" + targetId + "'를 compiled spawns에서 찾지 못했습니다.");
                }
            }
        }

        private static void ValidateItemPrices(
            ItemPriceDefinition[] itemPrices,
            PlayableObjectCatalog catalog,
            int primaryCurrencyUnitValue,
            CompiledPlayablePlanValidationResult result)
        {
            ItemPriceDefinition[] safeItemPrices = itemPrices ?? new ItemPriceDefinition[0];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeItemPrices.Length; i++)
            {
                ItemPriceDefinition itemPrice = safeItemPrices[i];
                if (itemPrice == null)
                {
                    Fail(result, "itemPrices[" + i + "]가 null입니다.");
                    continue;
                }

                string itemKey = ItemRefUtility.ToItemKey(itemPrice.item);
                if (string.IsNullOrWhiteSpace(itemKey))
                {
                    Fail(result, "itemPrices[" + i + "].item은 필수입니다.");
                    continue;
                }

                if (!seen.Add(itemKey))
                    Fail(result, "중복된 itemPrices item '" + itemKey + "'입니다.");

                if (itemPrice.price <= 0)
                    Fail(result, "itemPrices[" + i + "].price는 0보다 커야 합니다.");
                else if (!IsMoneyMultiple(itemPrice.price, primaryCurrencyUnitValue))
                    Fail(result, "itemPrices[" + i + "].price는 primary currency unitValue의 배수여야 합니다.");

                if (!ItemRefUtility.IsValid(itemPrice.item))
                    Fail(result, "itemPrices[" + i + "].item은 familyId와 variantId가 모두 필요합니다.");
            }
        }

        private static void ValidateFeatureAcceptedItems(FeatureAcceptedItemDefinition[] definitions, Dictionary<string, CompiledSpawnData> spawnLookup, PlayableObjectCatalog catalog, CompiledPlayablePlanValidationResult result)
        {
            FeatureAcceptedItemDefinition[] safeDefinitions = definitions ?? new FeatureAcceptedItemDefinition[0];
            var seenLaneKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FeatureAcceptedItemDefinition definition = safeDefinitions[i];
                if (definition == null)
                {
                    Fail(result, "featureAcceptedItems[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.targetId))
                {
                    Fail(result, "featureAcceptedItems[" + i + "].targetId는 필수입니다.");
                    continue;
                }

                string targetId = definition.targetId.Trim();
                if (!spawnLookup.ContainsKey(targetId))
                    Fail(result, "featureAcceptedItems[" + i + "].targetId '" + targetId + "'를 compiled spawns에서 찾지 못했습니다.");

                string itemKey = ItemRefUtility.ToItemKey(definition.item);
                if (string.IsNullOrWhiteSpace(itemKey))
                {
                    Fail(result, "featureAcceptedItems[" + i + "].item은 필수입니다.");
                    continue;
                }

                if (!ItemRefUtility.IsValid(definition.item))
                    Fail(result, "featureAcceptedItems[" + i + "].item은 familyId와 variantId가 모두 필요합니다.");

                if (definition.laneIndex < 0)
                {
                    Fail(result, "featureAcceptedItems[" + i + "].laneIndex는 0 이상이어야 합니다.");
                    continue;
                }

                string laneKey = targetId + "::" + definition.laneIndex;
                if (!seenLaneKeys.Add(laneKey))
                    Fail(result, "중복된 featureAcceptedItems lane '" + laneKey + "'입니다.");
            }
        }

        private static Dictionary<string, int> BuildCurrencyUnitLookup(CurrencyDefinition[] currencies)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            CurrencyDefinition[] safeCurrencies = currencies ?? new CurrencyDefinition[0];
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                CurrencyDefinition currency = safeCurrencies[i];
                if (currency == null || string.IsNullOrWhiteSpace(currency.currencyId))
                    continue;

                lookup[currency.currencyId.Trim()] = Math.Max(1, currency.unitValue);
            }

            return lookup;
        }

        private static int ResolvePrimaryCurrencyUnitValue(CurrencyDefinition[] currencies)
        {
            CurrencyDefinition[] safeCurrencies = currencies ?? new CurrencyDefinition[0];
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                CurrencyDefinition currency = safeCurrencies[i];
                if (currency == null || string.IsNullOrWhiteSpace(currency.currencyId))
                    continue;

                return Math.Max(1, currency.unitValue);
            }

            return 1;
        }

        private static int ResolveCurrencyUnitValue(Dictionary<string, int> currencyUnits, string currencyId)
        {
            string normalizedCurrencyId = string.IsNullOrWhiteSpace(currencyId) ? string.Empty : currencyId.Trim();
            if (!string.IsNullOrEmpty(normalizedCurrencyId) &&
                currencyUnits != null &&
                currencyUnits.TryGetValue(normalizedCurrencyId, out int unitValue))
            {
                return Math.Max(1, unitValue);
            }

            return 1;
        }

        private static bool IsMoneyMultiple(int value, int unitValue)
        {
            int safeUnitValue = Math.Max(1, unitValue);
            return value >= 0 && value % safeUnitValue == 0;
        }

        private static void ValidateCustomerPaths(
            LayoutSpecDocument layoutSpec,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            if (layoutSpec == null)
                return;

            LayoutSpecCustomerPathEntry[] customerPaths = layoutSpec.customerPaths ?? new LayoutSpecCustomerPathEntry[0];
            HashSet<string> requiredTargets = CollectRequiredCustomerPathTargets(layoutSpec, spawnLookup, catalog);
            var seenTargets = new HashSet<string>(StringComparer.Ordinal);
            if (requiredTargets.Count > 0 && customerPaths.Length == 0)
            {
                Fail(
                    result,
                    "MissingCustomerPaths: Step 3 layoutSpec.customerPaths[]가 비어 있습니다. customer-facing feature targetId: " +
                    string.Join(", ", requiredTargets));
                return;
            }

            for (int i = 0; i < customerPaths.Length; i++)
            {
                LayoutSpecCustomerPathEntry entry = customerPaths[i];
                if (entry == null)
                {
                    Fail(result, "layoutSpec.customerPaths[" + i + "]가 null입니다.");
                    continue;
                }

                string targetId = entry.targetId != null ? entry.targetId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(targetId))
                {
                    Fail(result, "layoutSpec.customerPaths[" + i + "].targetId는 필수입니다.");
                    continue;
                }

                if (!seenTargets.Add(targetId))
                    Fail(result, "중복된 layoutSpec.customerPaths.targetId '" + targetId + "'입니다.");

                if (!spawnLookup.TryGetValue(targetId, out CompiledSpawnData targetSpawn) || targetSpawn == null)
                {
                    Fail(result, "layoutSpec.customerPaths[" + i + "].targetId '" + targetId + "'를 compiled spawn으로 해석하지 못했습니다.");
                    continue;
                }

                if (!TryResolveGameplayPrefab(targetId, spawnLookup, catalog, out GameObject prefab) || prefab == null)
                {
                    Fail(result, "layoutSpec.customerPaths[" + i + "].targetId '" + targetId + "'를 baked gameplay prefab으로 해석하지 못했습니다.");
                    continue;
                }

                if (!SupportsCustomerPathAuthoring(catalog, targetSpawn, prefab))
                    Fail(result, "layoutSpec.customerPaths[" + i + "].targetId '" + targetId + "'는 customer line이 가능한 feature가 아닙니다.");

                ValidateCustomerPathPoint(entry.spawnPoint, "layoutSpec.customerPaths[" + i + "].spawnPoint", result);
                ValidateCustomerPathPoint(entry.leavePoint, "layoutSpec.customerPaths[" + i + "].leavePoint", result);
                ValidateCustomerPathPointArray(entry.queuePoints, "layoutSpec.customerPaths[" + i + "].queuePoints", 1, result);
                ValidateCustomerPathPointArray(entry.entryWaypoints, "layoutSpec.customerPaths[" + i + "].entryWaypoints", 1, result);
                ValidateCustomerPathPointArray(entry.exitWaypoints, "layoutSpec.customerPaths[" + i + "].exitWaypoints", 1, result);
            }

            if (requiredTargets.Count > 0)
            {
                var missingTargets = new List<string>();
                foreach (string requiredTarget in requiredTargets)
                {
                    if (!seenTargets.Contains(requiredTarget))
                        missingTargets.Add(requiredTarget);
                }

                var unexpectedTargets = new List<string>();
                foreach (string seenTarget in seenTargets)
                {
                    if (!requiredTargets.Contains(seenTarget))
                        unexpectedTargets.Add(seenTarget);
                }

                if (missingTargets.Count > 0 || unexpectedTargets.Count > 0)
                {
                    var segments = new List<string>();
                    if (missingTargets.Count > 0)
                        segments.Add("누락 targetId: " + string.Join(", ", missingTargets));
                    if (unexpectedTargets.Count > 0)
                        segments.Add("초과 targetId: " + string.Join(", ", unexpectedTargets));
                    Fail(result, "CustomerPathCoverageMismatch: Step 3 customerPaths coverage가 customer-facing feature와 일치하지 않습니다. " + string.Join(" / ", segments));
                }
            }
        }

        private static void ValidateFeatureOutputItems(
            CompiledSpawnData[] spawns,
            FeatureOutputItemDefinition[] definitions,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            FeatureOutputItemDefinition[] safeDefinitions = definitions ?? new FeatureOutputItemDefinition[0];
            var outputItemByTargetId = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FeatureOutputItemDefinition definition = safeDefinitions[i];
                if (definition == null)
                {
                    Fail(result, "featureOutputItems[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.targetId))
                {
                    Fail(result, "featureOutputItems[" + i + "].targetId는 필수입니다.");
                    continue;
                }

                string targetId = definition.targetId.Trim();
                if (!spawnLookup.ContainsKey(targetId))
                    Fail(result, "featureOutputItems[" + i + "].targetId '" + targetId + "'를 compiled spawns에서 찾지 못했습니다.");

                string itemKey = ItemRefUtility.ToItemKey(definition.item);
                if (string.IsNullOrWhiteSpace(itemKey))
                {
                    Fail(result, "featureOutputItems[" + i + "].item은 필수입니다.");
                    continue;
                }

                if (!ItemRefUtility.IsValid(definition.item))
                    Fail(result, "featureOutputItems[" + i + "].item은 familyId와 variantId가 모두 필요합니다.");

                if (outputItemByTargetId.TryGetValue(targetId, out string existingItemKey) &&
                    !string.Equals(existingItemKey, itemKey, StringComparison.Ordinal))
                {
                    Fail(result, "featureOutputItems[" + i + "]는 targetId '" + targetId + "'에 대해 단일 output item만 허용합니다.");
                    continue;
                }

                outputItemByTargetId[targetId] = itemKey;
            }

        }

        private static void ValidateCustomerPathPoint(
            LayoutSpecCustomerPathPoint point,
            string label,
            CompiledPlayablePlanValidationResult result)
        {
            if (point == null)
            {
                Fail(result, label + "가 null입니다.");
                return;
            }

            float worldX = point.hasWorldPosition ? point.worldX : point.gridX * IntentAuthoringUtility.LAYOUT_SPACING;
            float worldZ = point.hasWorldPosition ? point.worldZ : point.gridZ * IntentAuthoringUtility.LAYOUT_SPACING;
            if (float.IsNaN(worldX) || float.IsInfinity(worldX) ||
                float.IsNaN(worldZ) || float.IsInfinity(worldZ))
            {
                Fail(result, label + " 좌표가 유효하지 않습니다.");
            }
        }

        private static void ValidateCustomerPathPointArray(
            LayoutSpecCustomerPathPoint[] points,
            string label,
            int minCount,
            CompiledPlayablePlanValidationResult result)
        {
            LayoutSpecCustomerPathPoint[] safePoints = points ?? new LayoutSpecCustomerPathPoint[0];
            if (safePoints.Length < minCount)
            {
                Fail(result, label + "에는 최소 " + minCount + "개의 좌표가 필요합니다.");
                return;
            }

            for (int i = 0; i < safePoints.Length; i++)
                ValidateCustomerPathPoint(safePoints[i], label + "[" + i + "]", result);
        }

        private static HashSet<string> CollectRequiredCustomerPathTargets(
            LayoutSpecDocument layoutSpec,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog)
        {
            var requiredTargets = new HashSet<string>(StringComparer.Ordinal);
            if (layoutSpec == null || spawnLookup == null || spawnLookup.Count == 0 || catalog == null)
                return requiredTargets;

            foreach (KeyValuePair<string, CompiledSpawnData> pair in spawnLookup)
            {
                CompiledSpawnData spawn = pair.Value;
                if (spawn == null ||
                    string.IsNullOrWhiteSpace(spawn.spawnKey) ||
                    string.IsNullOrWhiteSpace(spawn.objectId))
                {
                    continue;
                }

                if (!catalog.TryResolveGameplayPrefab(spawn.objectId.Trim(), spawn.designIndex, out GameObject prefab, out _) || prefab == null)
                    continue;

                if (SupportsCustomerPathAuthoring(catalog, spawn, prefab))
                    requiredTargets.Add(ResolveCustomerPathAuthoringId(spawn));
            }

            return requiredTargets;
        }

        private static string ResolveCustomerPathAuthoringId(CompiledSpawnData spawn)
        {
            string sceneObjectId = ResolveSceneObjectIdFromSpawnKey(spawn);
            if (!string.IsNullOrEmpty(sceneObjectId))
                return sceneObjectId;

            return ResolveSpawnLabel(spawn);
        }

        private static bool SupportsCustomerPathAuthoring(PlayableObjectCatalog catalog, CompiledSpawnData spawn, GameObject prefab)
        {
            if (prefab != null &&
                PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata) &&
                CatalogPrefabMetadataCapabilityUtility.HasCapability(metadata, CatalogPrefabCapabilityIds.CUSTOMER_FEATURE))
            {
                return true;
            }

            string objectId = spawn != null && !string.IsNullOrWhiteSpace(spawn.objectId) ? spawn.objectId.Trim() : string.Empty;
            if (catalog == null || string.IsNullOrEmpty(objectId))
                return false;

            FeatureDescriptor[] descriptors = catalog.FeatureDescriptors ?? Array.Empty<FeatureDescriptor>();
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                FeatureDescriptor descriptor = descriptors[descriptorIndex];
                if (descriptor == null ||
                    descriptor.inputOutputSemantics == null ||
                    !descriptor.inputOutputSemantics.supportsCustomerFeature)
                {
                    continue;
                }

                FeatureCompiledGameplayRoleDescriptor[] mappings =
                    descriptor.compiledGameplayRoleMappings ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
                for (int mappingIndex = 0; mappingIndex < mappings.Length; mappingIndex++)
                {
                    FeatureCompiledGameplayRoleDescriptor mapping = mappings[mappingIndex];
                    if (mapping != null &&
                        string.Equals(
                            mapping.gameplayObjectId != null ? mapping.gameplayObjectId.Trim() : string.Empty,
                            objectId,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsImageLayoutMode(LayoutSpecDocument layoutSpec)
        {
            if (layoutSpec == null)
                return false;

            if (layoutSpec.sourceImages != null && layoutSpec.sourceImages.Length > 0)
                return true;

            return HasImagePlacement(layoutSpec.placements) ||
                   HasPlacementEnvironmentSourceImageReference(layoutSpec.environment) ||
                   HasCustomerPathSourceImageReference(layoutSpec.customerPaths);
        }

        private static bool HasImagePlacement(LayoutSpecPlacementEntry[] placements)
        {
            LayoutSpecPlacementEntry[] safePlacements = placements ?? new LayoutSpecPlacementEntry[0];
            for (int i = 0; i < safePlacements.Length; i++)
            {
                LayoutSpecPlacementEntry entry = safePlacements[i];
                if (entry != null && entry.hasImageBounds)
                    return true;
            }

            return false;
        }

        private static bool HasPlacementEnvironmentSourceImageReference(LayoutSpecEnvironmentEntry[] environmentEntries)
        {
            LayoutSpecEnvironmentEntry[] safeEntries = environmentEntries ?? new LayoutSpecEnvironmentEntry[0];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = safeEntries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.sourceImageId))
                    return true;
            }

            return false;
        }

        private static bool HasCustomerPathSourceImageReference(LayoutSpecCustomerPathEntry[] customerPaths)
        {
            LayoutSpecCustomerPathEntry[] safeEntries = customerPaths ?? new LayoutSpecCustomerPathEntry[0];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                LayoutSpecCustomerPathEntry entry = safeEntries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.sourceImageId))
                    return true;
            }

            return false;
        }

private static void ValidatePlacementSourceImageReferences(
    LayoutSpecPlacementEntry[] placements,
    HashSet<string> declaredSourceImageIds,
    CompiledPlayablePlanValidationResult result)
        {
            LayoutSpecPlacementEntry[] safePlacements = placements ?? new LayoutSpecPlacementEntry[0];
            for (int i = 0; i < safePlacements.Length; i++)
            {
                LayoutSpecPlacementEntry entry = safePlacements[i];
                if (entry == null)
                    continue;

                ValidateSourceImageReference(
                    entry.sourceImageId != null ? entry.sourceImageId.Trim() : string.Empty,
                    declaredSourceImageIds,
                    "layoutSpec.placements[" + i + "].sourceImageId",
            result);
    }
}

private static void ValidatePlacementSpatialSemantics(
    LayoutSpecDocument layoutSpec,
    CompiledPlayablePlanValidationResult result)
{
    if (layoutSpec == null)
        return;

    LayoutSpecPlacementEntry[] safePlacements = layoutSpec.placements ?? new LayoutSpecPlacementEntry[0];
    bool imageMode = IsImageLayoutMode(layoutSpec);
    bool multiImageMode = (layoutSpec.sourceImages ?? new LayoutSpecSourceImageEntry[0]).Length > 1;
    int bboxPlacementCount = 0;

    for (int i = 0; i < safePlacements.Length; i++)
    {
        LayoutSpecPlacementEntry entry = safePlacements[i];
        if (entry == null)
            continue;

        string laneId = entry.laneId != null ? entry.laneId.Trim() : string.Empty;
        if (entry.hasImageBounds)
            bboxPlacementCount++;

        if (multiImageMode &&
            !IsPlayerPlacement(entry) &&
            string.IsNullOrWhiteSpace(entry.sourceImageId))
        {
            Fail(result, "MissingSourceImageProvenance: multi-image layoutSpec.placements[" + i + "]는 sourceImageId가 필요합니다.");
        }
    }

    if (layoutSpec.playerStart != null &&
        !layoutSpec.playerStart.hasWorldPosition &&
        bboxPlacementCount < 2)
    {
        Fail(result, "MissingPxScaleSamples: layoutSpec.playerStart를 px 기준으로 world 환산하려면 non-player bbox placement가 최소 2개 필요합니다.");
    }

}

private static void ValidateEnvironmentPerimeterThickness(
    LayoutSpecDocument layoutSpec,
    PlayableObjectCatalog catalog,
    CompiledPlayablePlanValidationResult result)
{
    if (layoutSpec == null || catalog == null)
        return;

    LayoutSpecEnvironmentEntry[] environmentEntries = layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0];
    for (int i = 0; i < environmentEntries.Length; i++)
    {
        LayoutSpecEnvironmentEntry entry = environmentEntries[i];
        if (entry == null || !entry.hasWorldBounds)
            continue;

        string objectId = entry.objectId != null ? entry.objectId.Trim() : string.Empty;
        string designId = entry.designId != null ? entry.designId.Trim() : string.Empty;
        if (string.IsNullOrEmpty(objectId))
            continue;

        if (!catalog.TryGetEnvironmentDesign(
                objectId,
                designId,
                out EnvironmentDesignVariantEntry design,
                out string placementMode,
                out _,
                out _) ||
            !string.Equals(placementMode != null ? placementMode.Trim() : string.Empty, EnvironmentCatalog.PLACEMENT_MODE_PERIMETER, StringComparison.Ordinal))
        {
            continue;
        }

        if (!TryResolveEnvironmentFootprintCells(design, out int footprintCells))
        {
            Fail(result, "EnvironmentPerimeterThickness: layoutSpec.environment[" + i + "](" + objectId + "/" + designId + ") perimeter 두께를 검증할 catalog footprint metadata가 없습니다.");
            continue;
        }

        float worldWidth = Math.Abs(entry.worldWidth);
        float worldDepth = Math.Abs(entry.worldDepth);
        if (worldWidth <= 0f || worldDepth <= 0f)
            continue;

        float maxThickness = Math.Max(1, footprintCells) * IntentAuthoringUtility.LAYOUT_SPACING;
        float actualThickness = Math.Min(worldWidth, worldDepth);
        if (actualThickness > maxThickness + ENVIRONMENT_TILE_ALIGNMENT_MIN_TOLERANCE)
        {
            Fail(result, "EnvironmentPerimeterThickness: layoutSpec.environment[" + i + "](" + objectId + "/" + designId + ") perimeter 두께가 catalog footprint " +
                footprintCells + "x" + footprintCells + "를 초과했습니다. thickness=" +
                actualThickness.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ", max=" + maxThickness.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ". perimeter는 긴 축만 늘리고 짧은 축 두께는 footprint 이하로 유지해야 합니다.");
        }
    }
}

private static bool TryResolveEnvironmentFootprintCells(EnvironmentDesignVariantEntry design, out int footprintCells)
{
    footprintCells = 0;
    if (TryReadEnvironmentPrefabFootprintCells(design != null ? design.prefab : null, out footprintCells) ||
        TryReadEnvironmentPrefabFootprintCells(design != null ? design.straightPrefab : null, out footprintCells) ||
        TryReadEnvironmentPrefabFootprintCells(design != null ? design.cornerPrefab : null, out footprintCells) ||
        TryReadEnvironmentPrefabFootprintCells(design != null ? design.tJunctionPrefab : null, out footprintCells) ||
        TryReadEnvironmentPrefabFootprintCells(design != null ? design.crossPrefab : null, out footprintCells))
    {
        return footprintCells > 0;
    }

    return false;
}

private static bool TryReadEnvironmentPrefabFootprintCells(GameObject prefab, out int footprintCells)
{
    footprintCells = 0;
    if (prefab == null)
        return false;

    if (!PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata))
        return false;

    int widthCells = metadata.placementFootprintWidthCells > 0 ? metadata.placementFootprintWidthCells : 1;
    int depthCells = metadata.placementFootprintDepthCells > 0 ? metadata.placementFootprintDepthCells : 1;
    if (widthCells != depthCells)
        return false;

    footprintCells = widthCells;
    return true;
}

private static bool IsPlayerPlacement(LayoutSpecPlacementEntry entry)
{
    string objectId = entry != null && entry.objectId != null ? entry.objectId.Trim() : string.Empty;
    string imageLabel = entry != null && entry.imageLabel != null ? entry.imageLabel.Trim() : string.Empty;
    return
        string.Equals(objectId, "player_main", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(objectId, "player", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(imageLabel, "player_main", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(imageLabel, "player", StringComparison.OrdinalIgnoreCase);
}

private static void ValidateEnvironmentSourceImageReferences(
    LayoutSpecEnvironmentEntry[] environmentEntries,
    HashSet<string> declaredSourceImageIds,
            CompiledPlayablePlanValidationResult result)
        {
            LayoutSpecEnvironmentEntry[] safeEntries = environmentEntries ?? new LayoutSpecEnvironmentEntry[0];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = safeEntries[i];
                if (entry == null)
                    continue;

                ValidateSourceImageReference(
                    entry.sourceImageId != null ? entry.sourceImageId.Trim() : string.Empty,
                    declaredSourceImageIds,
                    "layoutSpec.environment[" + i + "].sourceImageId",
                    result);
            }
        }

        private static void ValidateCustomerPathSourceImageReferences(
            LayoutSpecCustomerPathEntry[] customerPaths,
            HashSet<string> declaredSourceImageIds,
            CompiledPlayablePlanValidationResult result)
        {
            LayoutSpecCustomerPathEntry[] safeEntries = customerPaths ?? new LayoutSpecCustomerPathEntry[0];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                LayoutSpecCustomerPathEntry entry = safeEntries[i];
                if (entry == null)
                    continue;

                ValidateSourceImageReference(
                    entry.sourceImageId != null ? entry.sourceImageId.Trim() : string.Empty,
                    declaredSourceImageIds,
                    "layoutSpec.customerPaths[" + i + "].sourceImageId",
                    result);
            }
        }

        private static void ValidateSourceImageReference(
            string sourceImageId,
            HashSet<string> declaredSourceImageIds,
            string label,
            CompiledPlayablePlanValidationResult result)
        {
            if (string.IsNullOrEmpty(sourceImageId))
                return;

            if (declaredSourceImageIds == null || declaredSourceImageIds.Count == 0)
            {
                Fail(result, label + " '" + sourceImageId + "'를 사용하려면 layoutSpec.sourceImages[]가 필요합니다.");
                return;
            }

            if (!declaredSourceImageIds.Contains(sourceImageId))
                Fail(result, label + " '" + sourceImageId + "'는 layoutSpec.sourceImages[]에 선언되지 않았습니다.");
        }
        private static void ValidatePlayerOptions(PlayableScenarioPlayerOptions options, CompiledPlayablePlanValidationResult result)
        {
            if (options.itemStacker.maxCount < 0)
                Fail(result, "playerOptions.itemStacker.maxCount는 0 이상이어야 합니다.");
        }

        private static void ValidateFeatureOptions(
            PlayableScenarioFeatureOptionDefinition[] definitions,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            PlayableScenarioFeatureOptionDefinition[] safeDefinitions = definitions ?? new PlayableScenarioFeatureOptionDefinition[0];
            var seenFeatureIds = new HashSet<string>(StringComparer.Ordinal);
            var seenTypedTargets = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                PlayableScenarioFeatureOptionDefinition definition = safeDefinitions[i];
                if (definition == null)
                {
                    Fail(result, "featureOptions[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.featureId))
                {
                    Fail(result, "featureOptions[" + i + "].featureId는 필수입니다.");
                    continue;
                }

                string featureId = definition.featureId.Trim();
                if (!seenFeatureIds.Add(featureId))
                    Fail(result, "중복된 featureOptions featureId '" + featureId + "'입니다.");

                string featureType = PlayableFeatureTypeIds.Normalize(definition.featureType);
                if (string.IsNullOrEmpty(featureType))
                {
                    Fail(result, "featureOptions[" + i + "].featureType는 필수입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.targetId))
                {
                    Fail(result, "featureOptions[" + i + "].targetId는 필수입니다.");
                    continue;
                }

                string targetId = definition.targetId.Trim();
                if (!seenTypedTargets.Add(featureType + "::" + targetId))
                    Fail(result, "중복된 featureOptions(featureType, targetId) '" + featureType + "', '" + targetId + "'입니다.");

                if (!spawnLookup.ContainsKey(targetId))
                    Fail(result, "featureOptions[" + i + "].targetId '" + targetId + "'를 compiled spawns에서 찾지 못했습니다.");

                PlayableScenarioFeatureOptions options = definition.options;
                if (!string.Equals(PlayableFeatureTypeIds.Normalize(options.featureType), featureType, StringComparison.Ordinal))
                    Fail(result, "featureOptions[" + i + "].options.featureType은 featureOptions[" + i + "].featureType과 같아야 합니다.");
                if (!string.Equals(PlayableFeatureTypeIds.Normalize(options.targetId), targetId, StringComparison.Ordinal))
                    Fail(result, "featureOptions[" + i + "].options.targetId는 featureOptions[" + i + "].targetId와 같아야 합니다.");
                if (!LooksLikeJsonObject(options.optionsJson))
                {
                    Fail(result, "featureOptions[" + i + "].options.optionsJson은 JSON object 문자열이어야 합니다.");
                }
                else if (catalog != null && catalog.TryGetFeatureDescriptor(featureType, out FeatureDescriptor descriptor))
                {
                    var schemaErrors = new List<string>();
                    FeatureOptionsSchemaValidator.ValidateCompiledOptions(
                        definition,
                        descriptor,
                        spawnLookup,
                        "featureOptions[" + i + "]",
                        schemaErrors);
                    for (int schemaErrorIndex = 0; schemaErrorIndex < schemaErrors.Count; schemaErrorIndex++)
                        Fail(result, schemaErrors[schemaErrorIndex]);
                }

            }
        }

        private static void ValidateFeatureLayouts(
            FeatureJsonPayload[] featureLayouts,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            FeatureJsonPayload[] safeLayouts = featureLayouts ?? Array.Empty<FeatureJsonPayload>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeLayouts.Length; i++)
            {
                FeatureJsonPayload layout = safeLayouts[i];
                if (layout == null)
                {
                    Fail(result, "featureLayouts[" + i + "]가 null입니다.");
                    continue;
                }

                string featureType = PlayableFeatureTypeIds.Normalize(layout.featureType);
                string targetId = PlayableFeatureTypeIds.Normalize(layout.targetId);
                if (string.IsNullOrEmpty(featureType))
                    Fail(result, "featureLayouts[" + i + "].featureType는 필수입니다.");
                if (string.IsNullOrEmpty(targetId))
                    Fail(result, "featureLayouts[" + i + "].targetId는 필수입니다.");
                if (!string.IsNullOrEmpty(featureType) && catalog != null && !catalog.IsSupportedFeatureType(featureType))
                    Fail(result, "featureLayouts[" + i + "].featureType '" + featureType + "'는 현재 catalog에 없는 feature입니다.");
                if (!string.IsNullOrEmpty(targetId) && !spawnLookup.ContainsKey(targetId))
                    Fail(result, "featureLayouts[" + i + "].targetId '" + targetId + "'를 compiled spawns에서 찾지 못했습니다.");
                if (!string.IsNullOrWhiteSpace(layout.json) && !LooksLikeJsonObject(layout.json))
                    Fail(result, "featureLayouts[" + i + "].json은 JSON object 문자열이어야 합니다.");

                string key = featureType + "::" + targetId;
                if (!string.IsNullOrEmpty(featureType) && !string.IsNullOrEmpty(targetId) && !seen.Add(key))
                    Fail(result, "중복된 featureLayouts(featureType, targetId) '" + featureType + "', '" + targetId + "'입니다.");
            }
        }

        private static bool LooksLikeJsonObject(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
        }

private static void ValidateRuntimeOwnedDesignSources(CompiledSpawnData[] spawns, FeatureAcceptedItemDefinition[] featureAcceptedItems, FeatureOutputItemDefinition[] featureOutputItems, PlayableScenarioFeatureOptionDefinition[] featureOptions, ItemPriceDefinition[] itemPrices, CurrencyDefinition[] currencies, Dictionary<string, int> objectDesignLookup, PlayableObjectCatalog catalog, CompiledPlayablePlanValidationResult result)
{
    RuntimeOwnedObjectDesignResolution resolution = RuntimeOwnedObjectDesignResolver.Resolve(spawns, featureAcceptedItems, featureOutputItems, featureOptions, itemPrices, currencies, catalog);
    for (int i = 0; i < resolution.Errors.Count; i++)
        Fail(result, resolution.Errors[i]);

            for (int i = 0; i < resolution.RequiredObjectDesigns.Count; i++)
            {
                RuntimeOwnedObjectDesignSelection selection = resolution.RequiredObjectDesigns[i];
                if (selection == null)
                    continue;

                string objectDesignKey = ContentCatalogTokenUtility.BuildObjectDesignSelectionKey(selection.objectId, selection.designId);
                if (!objectDesignLookup.ContainsKey(objectDesignKey))
                    Fail(result, "Compiled plan에는 runtime-owned object '" + objectDesignKey + "'에 대한 objectDesigns[] entry가 필요합니다.");
    }
}

        private static void ValidateObjectDesignSelections(
            ObjectDesignSelectionDefinition[] selections,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            ObjectDesignSelectionDefinition[] safeSelections = selections ?? new ObjectDesignSelectionDefinition[0];
            for (int i = 0; i < safeSelections.Length; i++)
            {
                ObjectDesignSelectionDefinition selection = safeSelections[i];
                string label = "objectDesigns[" + i + "]";
                if (selection == null)
                {
                    Fail(result, label + "가 null입니다.");
                    continue;
                }

                if (!ContentCatalogTokenUtility.ValidateObjectId(selection.objectId, out string objectIdError))
                {
                    Fail(result, label + ".objectId 오류: " + objectIdError);
                    continue;
                }

                if (!ContentCatalogTokenUtility.ValidateDesignId(selection.designId, out string designIdError))
                {
                    Fail(result, label + ".designId 오류: " + designIdError);
                    continue;
                }

                if (selection.designIndex < 0)
                {
                    Fail(result, label + ".designIndex는 0 이상이어야 합니다.");
                    continue;
                }

                string objectDesignKey = ContentCatalogTokenUtility.BuildObjectDesignSelectionKey(selection.objectId, selection.designId);
                if (!seenKeys.Add(objectDesignKey))
                {
                    Fail(result, "중복된 objectDesigns identity '" + objectDesignKey + "'입니다.");
                    continue;
                }

                if (catalog != null &&
                    catalog.TryResolveGameplayDesignIndex(selection.objectId.Trim(), selection.designId.Trim(), out int resolvedDesignIndex) &&
                    resolvedDesignIndex != selection.designIndex)
                {
                    Fail(result, label + "의 designIndex '" + selection.designIndex + "'가 catalog resolved designIndex '" + resolvedDesignIndex + "'와 일치하지 않습니다.");
                }
            }
        }

        private static void ValidateContentSelections(
            ContentSelectionDefinition[] selections,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            var seenObjectIds = new HashSet<string>(StringComparer.Ordinal);
            ContentSelectionDefinition[] safeSelections = selections ?? new ContentSelectionDefinition[0];
            for (int i = 0; i < safeSelections.Length; i++)
            {
                ContentSelectionDefinition selection = safeSelections[i];
                string label = "contentSelections[" + i + "]";
                if (selection == null)
                {
                    Fail(result, label + "가 null입니다.");
                    continue;
                }

                if (!ContentCatalogTokenUtility.ValidateObjectId(selection.objectId, out string objectIdError))
                {
                    Fail(result, label + ".objectId 오류: " + objectIdError);
                    continue;
                }

                if (!ContentCatalogTokenUtility.ValidateDesignId(selection.designId, out string designIdError))
                {
                    Fail(result, label + ".designId 오류: " + designIdError);
                    continue;
                }

                if (selection.designIndex < 0)
                {
                    Fail(result, label + ".designIndex는 0 이상이어야 합니다.");
                    continue;
                }

                string objectId = selection.objectId.Trim();
                string designId = selection.designId.Trim();
                if (!seenObjectIds.Add(objectId))
                {
                    Fail(result, "중복된 contentSelections objectId '" + objectId + "'입니다.");
                    continue;
                }

                if (catalog != null && !catalog.IsSupportedContentSelectionObject(objectId))
                {
                    Fail(result, label + ".objectId '" + objectId + "'는 selectable content catalog에 없습니다.");
                    continue;
                }

                if (ContentSelectionRules.IsUnsetDesignId(designId))
                {
                    Fail(result, label + ".designId는 실제 카탈로그 design이어야 합니다. '" + ContentSelectionRules.DESIGN_ID_NOT_SET + "'는 허용되지 않습니다.");
                    continue;
                }

                if (catalog != null &&
                    catalog.TryResolveContentSelectionDesignIndex(objectId, designId, out int resolvedDesignIndex) &&
                    resolvedDesignIndex != selection.designIndex)
                {
                    Fail(result, label + "의 designIndex '" + selection.designIndex + "'가 catalog resolved designIndex '" + resolvedDesignIndex + "'와 일치하지 않습니다.");
                }
            }

            if (catalog == null)
                return;

            for (int i = 0; i < ContentSelectionRules.REQUIRED_OBJECT_IDS.Length; i++)
            {
                string objectId = ContentSelectionRules.REQUIRED_OBJECT_IDS[i];
                if (!catalog.IsSupportedContentSelectionObject(objectId))
                {
                    Fail(result, "필수 UI content '" + objectId + "'가 selectable content catalog에 없습니다.");
                    continue;
                }

                if (!seenObjectIds.Contains(objectId))
                    Fail(result, "Compiled plan에는 필수 UI content '" + objectId + "'에 대한 contentSelections[] entry가 필요합니다.");
            }
        }

        private static void ValidateImageLayoutEnvironmentPresence(
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            _ = catalog;
            if (!HasAnyImagePlacement(layoutSpec))
                return;

            LayoutSpecEnvironmentEntry[] environmentEntries = layoutSpec != null
                ? layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0]
                : new LayoutSpecEnvironmentEntry[0];
            if (environmentEntries.Length == 0)
                Fail(result, "이미지 기반 Step 3에서는 environment[]를 비울 수 없습니다.");
        }

        private static bool HasAnyImagePlacement(LayoutSpecDocument layoutSpec)
        {
            LayoutSpecPlacementEntry[] placements = layoutSpec != null
                ? layoutSpec.placements ?? new LayoutSpecPlacementEntry[0]
                : new LayoutSpecPlacementEntry[0];
            for (int i = 0; i < placements.Length; i++)
            {
                LayoutSpecPlacementEntry placement = placements[i];
                if (placement != null && placement.hasImageBounds)
                    return true;
            }

            return false;
        }

        private static string ResolveSceneObjectIdFromSpawnKey(CompiledSpawnData spawn)
        {
            string spawnKey = ResolveSpawnLabel(spawn);
            if (spawnKey.StartsWith("spawn_", StringComparison.Ordinal))
                return spawnKey.Substring("spawn_".Length);
            return string.Empty;
        }

        private static string ResolveSpawnLabel(CompiledSpawnData spawn)
        {
            if (spawn == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(spawn.spawnKey))
                return spawn.spawnKey.Trim();

            return !string.IsNullOrWhiteSpace(spawn.objectId) ? spawn.objectId.Trim() : string.Empty;
        }

        private static Dictionary<string, int> BuildObjectDesignLookup(ObjectDesignSelectionDefinition[] selections)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            ObjectDesignSelectionDefinition[] safeSelections = selections ?? new ObjectDesignSelectionDefinition[0];
            for (int i = 0; i < safeSelections.Length; i++)
            {
                ObjectDesignSelectionDefinition selection = safeSelections[i];
                if (selection == null ||
                    string.IsNullOrWhiteSpace(selection.objectId) ||
                    string.IsNullOrWhiteSpace(selection.designId) ||
                    selection.designIndex < 0)
                {
                    continue;
                }

                lookup[ContentCatalogTokenUtility.BuildObjectDesignSelectionKey(selection.objectId.Trim(), selection.designId.Trim())] = selection.designIndex;
            }

            return lookup;
        }

        private static Dictionary<string, CompiledSpawnData> BuildSpawnLookup(CompiledSpawnData[] spawns)
        {
            var lookup = new Dictionary<string, CompiledSpawnData>(StringComparer.Ordinal);
            var objectIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            CompiledSpawnData[] safeSpawns = spawns ?? new CompiledSpawnData[0];

            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData spawn = safeSpawns[i];
                if (spawn == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(spawn.spawnKey))
                    lookup[spawn.spawnKey.Trim()] = spawn;

                if (!string.IsNullOrWhiteSpace(spawn.objectId))
                {
                    string objectId = spawn.objectId.Trim();
                    objectIdCounts[objectId] = objectIdCounts.TryGetValue(objectId, out int count) ? count + 1 : 1;
                }
            }

            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData spawn = safeSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.objectId))
                    continue;

                string objectId = spawn.objectId.Trim();
                if (objectIdCounts.TryGetValue(objectId, out int count) && count == 1)
                    lookup[objectId] = spawn;

                string sceneObjectId = ResolveSceneObjectIdFromSpawnKey(spawn);
                if (!string.IsNullOrEmpty(sceneObjectId))
                    lookup[sceneObjectId] = spawn;
            }

            return lookup;
        }

        private static bool TryResolveGameplayPrefab(string sceneRef, Dictionary<string, CompiledSpawnData> spawnLookup, PlayableObjectCatalog catalog, out GameObject prefab)
        {
            prefab = null;
            if (catalog == null || string.IsNullOrWhiteSpace(sceneRef))
                return false;

            if (!spawnLookup.TryGetValue(sceneRef.Trim(), out CompiledSpawnData spawn) || spawn == null)
                return false;

            return catalog.TryResolveGameplayPrefab(spawn.objectId.Trim(), spawn.designIndex, out prefab, out _);
        }

        private static void Fail(CompiledPlayablePlanValidationResult result, string message)
        {
            Fail(result, new ValidationIssueRecord(
                ValidationRuleId.COMPILED_PLAN_GENERIC,
                ValidationSeverity.Blocker,
                message,
                "CompiledPlayablePlan"));
        }

        private static void Fail(CompiledPlayablePlanValidationResult result, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.CompiledPlanValidationFailed;

            result.Errors.Add(issue.message ?? string.Empty);
            result.Issues ??= new List<ValidationIssueRecord>();
            result.Issues.Add(issue);
        }

        private static CompiledPlayablePlanValidationResult FinalizeFailure(CompiledPlayablePlanValidationResult result)
        {
            result.IsValid = false;
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.CompiledPlanValidationFailed;
            result.Message = result.Errors.Count > 0 ? result.Errors[0] : "Compiled plan 검증에 실패했습니다.";
            return result;
        }
    }
}
