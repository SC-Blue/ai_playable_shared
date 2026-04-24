using System;
using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public sealed class PromptIntentSemanticValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public List<string> Errors = new List<string>();
        public List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();
    }

    public static class PromptIntentSemanticValidator
    {
        public static PromptIntentSemanticValidationResult Validate(
            PlayablePromptIntent intent,
            PlayableObjectCatalog catalog)
        {
            var result = new PromptIntentSemanticValidationResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (intent == null)
                return Fail(result, "PlayablePromptIntent가 null입니다.");
            if (catalog == null)
                return Fail(result, "PlayableObjectCatalog가 필요합니다.");
            if (!ValidateCatalogContract(catalog, result))
                return FinalizeResult(result);

            string normalizedIntentThemeId = Normalize(intent.themeId);
            string normalizedCatalogThemeId = Normalize(catalog.ThemeId);
            if (string.IsNullOrEmpty(normalizedIntentThemeId))
            {
                Fail(result, "themeId는 필수이며 비워둘 수 없습니다.");
            }
            else if (string.IsNullOrEmpty(normalizedCatalogThemeId))
            {
                Fail(result, "catalog.ThemeId가 비어 있어 themeId를 검증할 수 없습니다.");
            }
            else if (!string.Equals(normalizedIntentThemeId, normalizedCatalogThemeId, StringComparison.Ordinal))
            {
                Fail(result, "themeId '" + intent.themeId + "'가 catalog.ThemeId '" + catalog.ThemeId + "'와 일치하지 않습니다.");
            }

            var objectById = BuildObjectLookup(intent.objects, result);
            var currencyById = BuildCurrencyLookup(intent.currencies, result);
            var saleValueByItemKey = BuildSaleValueLookup(intent.saleValues, result);
            ValidateCurrencies(intent.currencies, result);
            ValidateContentSelections(intent.contentSelections, catalog, result);
            ValidateObjects(intent.objects, objectById, currencyById, catalog, result);
            if (result.Errors.Count > 0 && result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.IntentValidationFailed;
            ValidateSaleValues(intent.saleValues, currencyById, catalog, result);
            ValidateStages(intent.stages, objectById, currencyById, saleValueByItemKey, catalog, result);
            ValidateFeatureItemConsistency(intent.stages, result);
            return FinalizeResult(result);
        }

        private static void ValidateContentSelections(
            ContentSelectionDefinition[] selections,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
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

                string objectId = Normalize(selection.objectId);
                string designId = Normalize(selection.designId);
                if (string.IsNullOrEmpty(objectId))
                {
                    Fail(result, label + ".objectId는 필수입니다.");
                    continue;
                }

                if (!seenObjectIds.Add(objectId))
                {
                    Fail(result, "중복된 contentSelections objectId '" + objectId + "'입니다.");
                    continue;
                }

                if (!catalog.IsSupportedContentSelectionObject(objectId))
                {
                    Fail(result, label + ".objectId '" + objectId + "'는 selectable content catalog에 없습니다.");
                    continue;
                }

                if (ContentSelectionRules.IsUnsetDesignId(designId))
                {
                    Fail(result, label + ".designId는 실제 카탈로그 design을 선택해야 합니다. '" + ContentSelectionRules.DESIGN_ID_NOT_SET + "' 상태로 둘 수 없습니다.");
                    continue;
                }

                if (!catalog.IsValidContentSelectionDesignId(objectId, designId))
                    Fail(result, label + ".designId '" + designId + "'는 objectId '" + objectId + "'의 selectable content design이 아닙니다.");
            }

            for (int i = 0; i < ContentSelectionRules.REQUIRED_OBJECT_IDS.Length; i++)
            {
                string objectId = ContentSelectionRules.REQUIRED_OBJECT_IDS[i];
                if (!catalog.IsSupportedContentSelectionObject(objectId))
                {
                    Fail(result, "필수 UI content '" + objectId + "'가 selectable content catalog에 없습니다.");
                    continue;
                }

                if (!seenObjectIds.Contains(objectId))
                    Fail(result, "필수 UI content '" + objectId + "'에 대한 contentSelections entry가 필요합니다.");
            }
        }

        private static Dictionary<string, PromptIntentObjectDefinition> BuildObjectLookup(
            PromptIntentObjectDefinition[] objects,
            PromptIntentSemanticValidationResult result)
        {
            var objectById = new Dictionary<string, PromptIntentObjectDefinition>(StringComparer.Ordinal);
            PromptIntentObjectDefinition[] safeObjects = objects ?? new PromptIntentObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                PromptIntentObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId))
                    continue;

                if (objectById.ContainsKey(objectId))
                {
                    Fail(result, "중복된 objects id '" + objectId + "'입니다.");
                    continue;
                }

                objectById.Add(objectId, value);
            }

            return objectById;
        }

        private static Dictionary<string, PromptIntentCurrencyDefinition> BuildCurrencyLookup(
            PromptIntentCurrencyDefinition[] currencies,
            PromptIntentSemanticValidationResult result)
        {
            var currencyById = new Dictionary<string, PromptIntentCurrencyDefinition>(StringComparer.Ordinal);
            PromptIntentCurrencyDefinition[] safeCurrencies = currencies ?? new PromptIntentCurrencyDefinition[0];
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                PromptIntentCurrencyDefinition value = safeCurrencies[i];
                string currencyId = Normalize(value != null ? value.currencyId : string.Empty);
                if (string.IsNullOrEmpty(currencyId))
                    continue;

                if (currencyById.ContainsKey(currencyId))
                {
                    Fail(result, "중복된 currencies currencyId '" + currencyId + "'입니다.");
                    continue;
                }

                currencyById.Add(currencyId, value);
            }

            return currencyById;
        }

        private static void ValidateCurrencies(
            PromptIntentCurrencyDefinition[] currencies,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentCurrencyDefinition[] safeCurrencies = currencies ?? new PromptIntentCurrencyDefinition[0];
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                PromptIntentCurrencyDefinition value = safeCurrencies[i];
                if (value == null)
                    continue;

                string error = CurrencyStartVisualRules.Validate(value.startVisualMode, "currencies[" + i + "].startVisualMode");
                if (!string.IsNullOrEmpty(error))
                    Fail(result, error);
            }
        }

        private static Dictionary<string, PromptIntentSaleValueDefinition> BuildSaleValueLookup(
            PromptIntentSaleValueDefinition[] saleValues,
            PromptIntentSemanticValidationResult result)
        {
            var saleValueByItemKey = new Dictionary<string, PromptIntentSaleValueDefinition>(StringComparer.Ordinal);
            PromptIntentSaleValueDefinition[] safeSaleValues = saleValues ?? new PromptIntentSaleValueDefinition[0];
            for (int i = 0; i < safeSaleValues.Length; i++)
            {
                PromptIntentSaleValueDefinition value = safeSaleValues[i];
                string itemKey = ItemRefUtility.ToStableKey(value != null ? value.item : null);
                if (string.IsNullOrEmpty(itemKey))
                    continue;

                if (saleValueByItemKey.ContainsKey(itemKey))
                {
                    Fail(result, "중복된 saleValues item '" + itemKey + "'입니다.");
                    continue;
                }

                saleValueByItemKey.Add(itemKey, value);
            }

            return saleValueByItemKey;
        }

        private static void ValidateObjects(
            PromptIntentObjectDefinition[] objects,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentObjectDefinition[] safeObjects = objects ?? new PromptIntentObjectDefinition[0];
            int playerCount = 0;
            for (int i = 0; i < safeObjects.Length; i++)
            {
                PromptIntentObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string role = Normalize(value.role);
                string objectId = Normalize(value.id);
                if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(objectId))
                    continue;

                if (string.Equals(role, PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
                    playerCount++;

                if (string.Equals(role, PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(Normalize(value.designId)))
                        Fail(result, "objects[" + i + "].designId는 physics_area에서 사용할 수 없습니다.");

                    ValidatePhysicsAreaObject(value, objectById, catalog, "objects[" + i + "]", result);
                }
                else
                {
                    if (!IntentAuthoringUtility.TryResolveCatalogObjectId(catalog, role, out string gameplayObjectId, out string error))
                    {
                        Fail(result, "objects[" + i + "]를 catalog objectId로 해석하지 못했습니다: " + error);
                        continue;
                    }

                    int resolvedDesignIndex = IntentAuthoringUtility.ResolveGameplayDesignIndex(
                        catalog,
                        gameplayObjectId,
                        value.designId,
                        result.Errors,
                        "objects[" + i + "]");
                    if (resolvedDesignIndex < 0 && result.FailureCode == PlayableFailureCode.None)
                        result.FailureCode = PlayableFailureCode.IntentValidationFailed;

                    if (string.Equals(role, PromptIntentObjectRoles.RAIL, StringComparison.Ordinal))
                        ValidateRailObject(value, objectById, catalog, "objects[" + i + "]", result);
                }

                ValidateObjectScenarioOptions(
                    value.scenarioOptions,
                    role,
                    "objects[" + i + "].scenarioOptions",
                    objectById,
                    currencyById,
                    catalog,
                    result);
            }

            if (playerCount == 0)
            {
                Fail(result, "objects[]에는 최소 1개의 player role이 필요합니다.");
            }
        }

        private static void ValidateSaleValues(
            PromptIntentSaleValueDefinition[] saleValues,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentSaleValueDefinition[] safeSaleValues = saleValues ?? new PromptIntentSaleValueDefinition[0];
            for (int i = 0; i < safeSaleValues.Length; i++)
            {
                PromptIntentSaleValueDefinition value = safeSaleValues[i];
                if (value == null)
                    continue;

                string itemKey = ItemRefUtility.ToStableKey(value.item);
                string currencyId = Normalize(value.currencyId);
                if (!string.IsNullOrEmpty(currencyId) && !currencyById.ContainsKey(currencyId))
                    Fail(result, "saleValues[" + i + "].currencyId '" + currencyId + "'가 currencies[]에 존재하지 않습니다.");

                if (!string.IsNullOrEmpty(itemKey))
                    ValidateItemReference(value.item, "saleValues[" + i + "].item", catalog, result);
            }
        }

        private static void ValidateStages(
            PromptIntentStageDefinition[] stages,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            Dictionary<string, PromptIntentSaleValueDefinition> saleValueByItemKey,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentStageDefinition[] safeStages = stages ?? new PromptIntentStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                PromptIntentStageDefinition stage = safeStages[i];
                if (stage == null)
                    continue;

                ValidateCondition(stage.enterWhen, "stages[" + i + "].enterWhen", i, safeStages, objectById, currencyById, catalog, result);
                ValidateEffects(stage.onEnter, "stages[" + i + "].onEnter", objectById, result);
                ValidateObjectives(stage.objectives, "stages[" + i + "].objectives", objectById, currencyById, saleValueByItemKey, catalog, result);
                ValidateEffects(stage.onComplete, "stages[" + i + "].onComplete", objectById, result);
                ValidateSpawnCustomerTiming(stage, i, safeStages, result);
                ValidateShowArrowTiming(stage, i, result);

                int focusCameraCount = CountEffects(stage.onEnter, PromptIntentEffectKinds.FOCUS_CAMERA);
                if (focusCameraCount > 1)
                    Fail(result, "stages[" + i + "].onEnter에는 focus_camera를 1개만 둘 수 있습니다.");

                if (CountObjectives(stage.objectives, PromptIntentObjectiveKinds.UNLOCK_OBJECT) > 1)
                    Fail(result, "stages[" + i + "]는 unlock_object objective를 1개까지만 지원합니다.");

                ValidateStageArrowObjectiveMapping(stage, i, result);
                ValidateTerminalEndGameUsage(stage, i, safeStages, result);
            }
        }

        private static void ValidateCondition(
            PromptIntentConditionDefinition value,
            string label,
            int stageIndex,
            PromptIntentStageDefinition[] allStages,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            if (value == null)
                return;

            string kind = Normalize(value.kind);
            string[] supportedTargetRoles = PromptIntentCapabilityRegistry.GetConditionSupportedTargetRoles(kind);
            bool allowAnyTargetRole = PromptIntentCapabilityRegistry.ConditionAllowsAnyTargetRole(kind);
            if (allowAnyTargetRole || supportedTargetRoles.Length > 0)
            {
                ValidateObjectReferenceByRolePolicy(
                    value.targetObjectId,
                    label + ".targetObjectId",
                    objectById,
                    supportedTargetRoles,
                    allowAnyTargetRole,
                    result);
            }

            if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
            {
                string stageId = Normalize(value.stageId);
                if (!ExistsPreviousStage(stageId, stageIndex, allStages))
                    Fail(result, label + ".stageId '" + stageId + "'는 반드시 이전 stage를 참조해야 합니다.");
            }

            if (PromptIntentContractRegistry.ConditionRequiresCurrencyId(kind) ||
                (!string.IsNullOrWhiteSpace(value.currencyId) && PromptIntentContractRegistry.ConditionSupportsCurrencyId(kind)))
            {
                ValidateCurrencyReference(value.currencyId, label + ".currencyId", currencyById, result);
            }

            if (PromptIntentContractRegistry.ConditionRequiresItem(kind) ||
                (!ItemRefUtility.IsEmpty(value.item) && PromptIntentContractRegistry.ConditionSupportsItem(kind)))
            {
                ValidateItemReference(value.item, label + ".item", catalog, result);
            }
        }

        private static void ValidateEffects(
            PromptIntentEffectDefinition[] effects,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentEffectDefinition[] safeEffects = effects ?? new PromptIntentEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                PromptIntentEffectDefinition effect = safeEffects[i];
                if (effect == null)
                    continue;

                string kind = Normalize(effect.kind);
                string[] supportedTargetRoles = PromptIntentCapabilityRegistry.GetEffectSupportedTargetRoles(kind);
                bool allowAnyTargetRole = PromptIntentCapabilityRegistry.EffectAllowsAnyTargetRole(kind);
                if (PromptIntentEffectKinds.RequiresTargetObjectId(kind) &&
                    (allowAnyTargetRole || supportedTargetRoles.Length > 0))
                {
                    ValidateObjectReferenceByRolePolicy(
                        effect.targetObjectId,
                        label + "[" + i + "].targetObjectId",
                        objectById,
                        supportedTargetRoles,
                        allowAnyTargetRole,
                        result);
                }

                if (IsArrowGuideEffectKind(kind))
                {
                    ValidateTargetEventKeyCompatibility(
                        effect.targetObjectId,
                        effect.eventKey,
                        objectById,
                        label + "[" + i + "]",
                        kind,
                        result);
                }

                string timing = Normalize(effect.timing);
                if (!string.IsNullOrEmpty(timing) && !PromptIntentEffectKinds.SupportsExplicitTiming(kind))
                    Fail(result, label + "[" + i + "].timing은 spawn_customer, show_arrow 또는 show_guide_arrow에서만 사용할 수 있습니다.");
            }
        }

        private static void ValidateSpawnCustomerTiming(
            PromptIntentStageDefinition stage,
            int stageIndex,
            PromptIntentStageDefinition[] allStages,
            PromptIntentSemanticValidationResult result)
        {
            if (stage == null)
                return;

            bool completionFocusAsLastBeat = HasCompletionFocusAsLastBeat(stage);
            bool previousStageFocusAsLastBeat = HasPreviousStageFocusAsLastBeat(stageIndex, stage, allStages);

            ValidateSpawnCustomerTimingGroup(
                stage.onEnter,
                "stages[" + stageIndex + "].onEnter",
                requireExplicitTiming: previousStageFocusAsLastBeat,
                allowArrivalTiming: previousStageFocusAsLastBeat,
                result: result);

            ValidateSpawnCustomerTimingGroup(
                stage.onComplete,
                "stages[" + stageIndex + "].onComplete",
                requireExplicitTiming: completionFocusAsLastBeat,
                allowArrivalTiming: completionFocusAsLastBeat,
                result: result);
        }

        private static void ValidateSpawnCustomerTimingGroup(
            PromptIntentEffectDefinition[] effects,
            string label,
            bool requireExplicitTiming,
            bool allowArrivalTiming,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentEffectDefinition[] safeEffects = effects ?? new PromptIntentEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                PromptIntentEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    !string.Equals(Normalize(effect.kind), PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                {
                    continue;
                }

                string timing = Normalize(effect.timing);
                if (requireExplicitTiming && string.IsNullOrEmpty(timing))
                {
                    Fail(result, label + "[" + i + "].timing이 필요합니다. camera focus와 이어지는 spawn_customer는 arrival/completed를 명시해야 합니다.");
                    continue;
                }

                if (string.Equals(timing, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal) &&
                    !allowArrivalTiming &&
                    !IntentAuthoringUtility.HasEntryFocusBeforeIndex(safeEffects, i))
                {
                    Fail(result, label + "[" + i + "].timing 'arrival'은 focus camera arrival context가 있을 때만 사용할 수 있습니다.");
                }
            }
        }

        private static void ValidateShowArrowTiming(
            PromptIntentStageDefinition stage,
            int stageIndex,
            PromptIntentSemanticValidationResult result)
        {
            if (stage == null)
                return;

            bool hasEntryFocus = HasEntryFocus(stage.onEnter);
            ValidateShowArrowTimingGroup(
                stage.onEnter,
                "stages[" + stageIndex + "].onEnter",
                allowExplicitTiming: hasEntryFocus,
                result: result);

            ValidateShowArrowTimingGroup(
                stage.onComplete,
                "stages[" + stageIndex + "].onComplete",
                allowExplicitTiming: false,
                result: result);
        }

        private static void ValidateShowArrowTimingGroup(
            PromptIntentEffectDefinition[] effects,
            string label,
            bool allowExplicitTiming,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentEffectDefinition[] safeEffects = effects ?? new PromptIntentEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                PromptIntentEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    !IsArrowGuideEffectKind(Normalize(effect.kind)))
                {
                    continue;
                }

                string timing = Normalize(effect.timing);
                if (string.IsNullOrEmpty(timing))
                    continue;

                if (!PromptIntentEffectTimingKinds.IsSupported(timing))
                {
                    Fail(result, label + "[" + i + "].timing '" + timing + "'은(는) 지원되지 않습니다.");
                    continue;
                }

                if (!allowExplicitTiming)
                {
                    Fail(result, label + "[" + i + "].timing은 same-stage onEnter focus_camera와 직접 연결되는 show_arrow/show_guide_arrow에서만 사용할 수 있습니다. 생략하면 completed 기본 동작을 사용합니다.");
                }
            }
        }

        private static void ValidateStageArrowObjectiveMapping(
            PromptIntentStageDefinition stage,
            int stageIndex,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentEffectDefinition[] entryEffects = stage != null ? stage.onEnter ?? new PromptIntentEffectDefinition[0] : new PromptIntentEffectDefinition[0];
            PromptIntentObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new PromptIntentObjectiveDefinition[0] : new PromptIntentObjectiveDefinition[0];
            var stageArrows = new List<PromptIntentEffectDefinition>();
            int guideArrowCount = 0;
            var absorbableObjectives = new List<PromptIntentObjectiveDefinition>();
            var requiredArrowObjectiveIndices = new List<int>();
            var requiredArrowAbsorbableIndices = new List<int>();
            var requiredArrowEventKeys = new List<string>();

            for (int i = 0; i < entryEffects.Length; i++)
            {
                PromptIntentEffectDefinition effect = entryEffects[i];
                if (effect == null)
                    continue;

                string kind = Normalize(effect.kind);
                if (string.Equals(kind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal))
                    guideArrowCount++;

                if (!string.Equals(kind, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal))
                {
                    continue;
                }

                stageArrows.Add(effect);
            }

            if (guideArrowCount > 0 && objectives.Length > 0)
            {
                Fail(result, "stages[" + stageIndex + "]는 show_guide_arrow와 objective를 같은 stage에 둘 수 없습니다. show_guide_arrow는 presentation-only stage에서만 사용하고, objective가 필요하면 show_arrow를 쓰거나 stage를 분리하세요.");
            }

            for (int i = 0; i < objectives.Length; i++)
            {
                PromptIntentObjectiveDefinition objective = objectives[i];
                if (objective == null || !PromptIntentObjectiveKinds.CanAbsorbArrow(Normalize(objective.kind)))
                    continue;

                int absorbableIndex = absorbableObjectives.Count;
                absorbableObjectives.Add(objective);
                string requiredArrowEventKey = PromptIntentCapabilityRegistry.GetObjectiveRequiredArrowEventKey(Normalize(objective.kind));
                if (!string.IsNullOrEmpty(requiredArrowEventKey))
                {
                    requiredArrowObjectiveIndices.Add(i);
                    requiredArrowAbsorbableIndices.Add(absorbableIndex);
                    requiredArrowEventKeys.Add(requiredArrowEventKey);
                }
            }

            if (stageArrows.Count > absorbableObjectives.Count)
            {
                Fail(result, "stages[" + stageIndex + "].onEnter의 show_arrow 개수가 absorbable objective 수보다 많습니다.");
            }

            for (int i = 0; i < requiredArrowObjectiveIndices.Count; i++)
            {
                int objectiveIndex = requiredArrowObjectiveIndices[i];
                int absorbableIndex = requiredArrowAbsorbableIndices[i];
                string requiredArrowEventKey = requiredArrowEventKeys[i];
                if (absorbableIndex >= stageArrows.Count)
                {
                    Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "]에는 explicit show_arrow(eventKey='" + requiredArrowEventKey + "')가 필요합니다.");
                    continue;
                }

                string eventKey = Normalize(stageArrows[absorbableIndex] != null ? stageArrows[absorbableIndex].eventKey : string.Empty);
                if (!string.Equals(eventKey, requiredArrowEventKey, StringComparison.Ordinal))
                {
                    Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "]에는 show_arrow(eventKey='" + requiredArrowEventKey + "')를 사용해야 합니다.");
                }
            }

            if (HasEntryFocus(entryEffects) && stageArrows.Count > 0 && absorbableObjectives.Count > 0)
            {
                string firstArrowTiming = Normalize(stageArrows[0] != null ? stageArrows[0].timing : string.Empty);
                if (!string.Equals(firstArrowTiming, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
                {
                    Fail(result, "stages[" + stageIndex + "].onEnter의 첫 absorbed show_arrow는 same-stage focus_camera가 있으면 timing 'arrival'을 명시해야 합니다.");
                }
            }
        }

        private static bool IsArrowGuideEffectKind(string kind)
        {
            string normalizedKind = Normalize(kind);
            return string.Equals(normalizedKind, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal) ||
                string.Equals(normalizedKind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal);
        }

        private static void ValidateTerminalEndGameUsage(
            PromptIntentStageDefinition stage,
            int stageIndex,
            PromptIntentStageDefinition[] allStages,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentEffectDefinition[] entryEffects = stage != null ? stage.onEnter ?? new PromptIntentEffectDefinition[0] : new PromptIntentEffectDefinition[0];
            PromptIntentEffectDefinition[] completionEffects = stage != null ? stage.onComplete ?? new PromptIntentEffectDefinition[0] : new PromptIntentEffectDefinition[0];
            PromptIntentObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new PromptIntentObjectiveDefinition[0] : new PromptIntentObjectiveDefinition[0];

            int onEnterEndGameCount = CountEffects(entryEffects, PromptIntentEffectKinds.END_GAME);
            int onCompleteEndGameCount = CountEffects(completionEffects, PromptIntentEffectKinds.END_GAME);
            if (onEnterEndGameCount == 0 && onCompleteEndGameCount == 0)
                return;

            bool isLastStage = allStages != null && stageIndex == allStages.Length - 1;
            if (!isLastStage)
                Fail(result, "stages[" + stageIndex + "]의 end_game은 마지막 stage에서만 사용할 수 있습니다.");

            if (onEnterEndGameCount > 0 && onCompleteEndGameCount > 0)
                Fail(result, "stages[" + stageIndex + "]는 onEnter와 onComplete에 end_game을 동시에 둘 수 없습니다.");

            if (onCompleteEndGameCount > 0)
                Fail(result, "stages[" + stageIndex + "]는 end_game을 onComplete에 둘 수 없습니다. 종료가 필요하면 마지막 stage의 onEnter에만 두고, 연출이 필요하면 이전 stage에서 끝낸 뒤 별도 마지막 stage에서 end_game을 호출하세요.");

            if (onEnterEndGameCount > 0 && completionEffects.Length > 0)
                Fail(result, "stages[" + stageIndex + "]는 onEnter end_game을 쓰면 onComplete effect를 둘 수 없습니다.");

            if (onEnterEndGameCount > 0 && objectives.Length > 0)
                Fail(result, "stages[" + stageIndex + "]는 onEnter end_game을 쓰면 objective를 둘 수 없습니다.");

            if (onEnterEndGameCount > 0 && entryEffects.Length != onEnterEndGameCount)
                Fail(result, "stages[" + stageIndex + "]는 onEnter end_game을 쓸 때 다른 onEnter effect를 함께 둘 수 없습니다. 연출이 필요하면 이전 stage에서 끝내고 마지막 stage는 end_game만 두세요.");
        }

        private static bool HasPreviousStageFocusAsLastBeat(
            int stageIndex,
            PromptIntentStageDefinition stage,
            PromptIntentStageDefinition[] allStages)
        {
            if (stageIndex <= 0)
                return false;

            PromptIntentConditionDefinition enterWhen = stage != null ? stage.enterWhen : null;
            string kind = Normalize(enterWhen != null ? enterWhen.kind : string.Empty);
            if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
            {
                string referencedStageId = Normalize(enterWhen != null ? enterWhen.stageId : string.Empty);
                PromptIntentStageDefinition referencedStage = FindStageById(allStages, referencedStageId);
                return HasFocusAsLastBeat(referencedStage);
            }

            return HasFocusAsLastBeat(allStages[stageIndex - 1]);
        }

        private static bool HasFocusAsLastBeat(PromptIntentStageDefinition stage)
        {
            if (stage == null)
                return false;

            if (HasCompletionFocusAsLastBeat(stage))
                return true;

            PromptIntentObjectiveDefinition[] objectives = stage.objectives ?? new PromptIntentObjectiveDefinition[0];
            if (objectives.Length > 0)
                return false;

            return HasEntryFocus(stage.onEnter);
        }

        private static bool HasCompletionFocusAsLastBeat(PromptIntentStageDefinition stage)
        {
            PromptIntentEffectDefinition[] effects = stage != null ? stage.onComplete ?? new PromptIntentEffectDefinition[0] : new PromptIntentEffectDefinition[0];
            for (int i = 0; i < effects.Length; i++)
            {
                if (string.Equals(Normalize(effects[i] != null ? effects[i].kind : string.Empty), PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasEntryFocus(PromptIntentEffectDefinition[] effects)
        {
            PromptIntentEffectDefinition[] safeEffects = effects ?? new PromptIntentEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                if (string.Equals(Normalize(safeEffects[i] != null ? safeEffects[i].kind : string.Empty), PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static PromptIntentStageDefinition FindStageById(PromptIntentStageDefinition[] stages, string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                return null;

            PromptIntentStageDefinition[] safeStages = stages ?? new PromptIntentStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                PromptIntentStageDefinition stage = safeStages[i];
                if (stage != null && string.Equals(Normalize(stage.id), stageId, StringComparison.Ordinal))
                    return stage;
            }

            return null;
        }

        private static void ValidateObjectives(
            PromptIntentObjectiveDefinition[] objectives,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            Dictionary<string, PromptIntentSaleValueDefinition> saleValueByItemKey,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            PromptIntentObjectiveDefinition[] safeObjectives = objectives ?? new PromptIntentObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                PromptIntentObjectiveDefinition value = safeObjectives[i];
                if (value == null)
                    continue;

            string objectiveLabel = label + "[" + i + "]";
            string kind = Normalize(value.kind);
            string[] supportedTargetRoles = PromptIntentCapabilityRegistry.GetObjectiveSupportedTargetRoles(kind);
            bool allowAnyTargetRole = PromptIntentCapabilityRegistry.ObjectiveAllowsAnyTargetRole(kind);
            if (PromptIntentObjectiveKinds.IsSupported(kind) &&
                (allowAnyTargetRole || supportedTargetRoles.Length > 0))
            {
                ValidateObjectReferenceByRolePolicy(
                    value.targetObjectId,
                    objectiveLabel + ".targetObjectId",
                    objectById,
                    supportedTargetRoles,
                    allowAnyTargetRole,
                    result);
            }

            string targetEventKey = PromptIntentCapabilityRegistry.GetObjectiveTargetEventKey(kind);
            if (!string.IsNullOrEmpty(targetEventKey) && !string.IsNullOrWhiteSpace(value.targetObjectId))
            {
                ValidateTargetEventKeyCompatibility(
                    value.targetObjectId,
                    targetEventKey,
                    objectById,
                    objectiveLabel + ".targetObjectId",
                    kind,
                    result);
            }

            switch (kind)
            {
                case PromptIntentObjectiveKinds.UNLOCK_OBJECT:
                    ValidateCurrencyReference(value.currencyId, objectiveLabel + ".currencyId", currencyById, result);
                    break;
                case PromptIntentObjectiveKinds.COLLECT_ITEM:
                    ValidateItemReference(value.item, objectiveLabel + ".item", catalog, result);
                    break;
                case PromptIntentObjectiveKinds.CONVERT_ITEM:
                    ValidateItemReference(value.inputItem, objectiveLabel + ".inputItem", catalog, result);
                    break;
                case PromptIntentObjectiveKinds.SELL_ITEM:
                    ValidateItemReference(value.item, objectiveLabel + ".item", catalog, result);
                    string itemKey = ItemRefUtility.ToStableKey(value.item);
                    if (!string.IsNullOrEmpty(itemKey) && !saleValueByItemKey.ContainsKey(itemKey))
                        Fail(result, objectiveLabel + ".item '" + itemKey + "'에 대한 saleValues[] entry가 필요합니다.");
                    break;
                case PromptIntentObjectiveKinds.COLLECT_CURRENCY:
                    ValidateCurrencyReference(value.currencyId, objectiveLabel + ".currencyId", currencyById, result);
                    break;
            }
        }
        }

        private static void ValidateFeatureItemConsistency(
            PromptIntentStageDefinition[] stages,
            PromptIntentSemanticValidationResult result)
        {
            var processorInputItemByTargetObjectId = new Dictionary<string, string>(StringComparer.Ordinal);
            PromptIntentStageDefinition[] safeStages = stages ?? new PromptIntentStageDefinition[0];
            for (int stageIndex = 0; stageIndex < safeStages.Length; stageIndex++)
            {
                PromptIntentStageDefinition stage = safeStages[stageIndex];
                PromptIntentObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new PromptIntentObjectiveDefinition[0] : new PromptIntentObjectiveDefinition[0];
                for (int objectiveIndex = 0; objectiveIndex < objectives.Length; objectiveIndex++)
                {
                    PromptIntentObjectiveDefinition objective = objectives[objectiveIndex];
                    if (objective == null)
                        continue;

                    string kind = Normalize(objective.kind);
                    if (string.Equals(kind, PromptIntentObjectiveKinds.SELL_ITEM, StringComparison.Ordinal))
                        continue;

                    if (!string.Equals(kind, PromptIntentObjectiveKinds.CONVERT_ITEM, StringComparison.Ordinal))
                        continue;

                    ItemRef inputItem = objective.inputItem;
                    string inputItemKey = ItemRefUtility.ToStableKey(inputItem);
                    if (string.IsNullOrEmpty(inputItemKey))
                    {
                        Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "]의 convert_item에는 inputItem이 필요합니다.");
                        continue;
                    }

                    RegisterFeatureItemConsistency(
                        processorInputItemByTargetObjectId,
                        Normalize(objective.targetObjectId),
                        inputItemKey,
                        "stages[" + stageIndex + "].objectives[" + objectiveIndex + "]",
                        "processor",
                        result);
                }
            }
        }

        private static void RegisterFeatureItemConsistency(
            Dictionary<string, string> itemByTargetObjectId,
            string targetObjectId,
            string itemId,
            string label,
            string roleLabel,
            PromptIntentSemanticValidationResult result)
        {
            if (string.IsNullOrEmpty(targetObjectId) || string.IsNullOrEmpty(itemId))
                return;

            if (itemByTargetObjectId.TryGetValue(targetObjectId, out string existingItemId))
            {
                if (!string.Equals(existingItemId, itemId, StringComparison.Ordinal))
                {
                    Fail(result, label + "는 같은 " + roleLabel + " '" + targetObjectId + "'에 대해 서로 다른 itemId('" + existingItemId + "', '" + itemId + "')를 사용할 수 없습니다.");
                }
                return;
            }

            itemByTargetObjectId.Add(targetObjectId, itemId);
        }

        private static void ValidateObjectScenarioOptions(
            PromptIntentObjectScenarioOptions value,
            string role,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            if (value == null)
                return;

            bool hasCustomerRequestCount = value.customerRequestCount != null;
            bool hasRequestableItems = value.requestableItems != null && value.requestableItems.Length > 0;
            bool hasInputCountPerConversion = value.inputCountPerConversion > 0;
            bool hasConversionInterval = value.conversionIntervalSeconds > 0f;
            bool hasInputItemMoveInterval = value.inputItemMoveIntervalSeconds > 0f;
            bool hasSpawnInterval = value.spawnIntervalSeconds > 0f;

            if (hasCustomerRequestCount && !FeatureScenarioOptionRules.SupportsCustomerRequestCount(role))
                Fail(result, label + "는 " + Normalize(role) + " role에서 " + FeatureScenarioOptionRules.DescribeSupportedPromptScenarioOptions(role));

            if (hasRequestableItems && !string.Equals(Normalize(role), PromptIntentObjectRoles.SELLER, StringComparison.Ordinal))
                Fail(result, label + "는 " + Normalize(role) + " role에서 " + FeatureScenarioOptionRules.DescribeSupportedPromptScenarioOptions(role));

            if (hasInputCountPerConversion && !FeatureScenarioOptionRules.SupportsInputCountPerConversion(role))
                Fail(result, label + "는 " + Normalize(role) + " role에서 " + FeatureScenarioOptionRules.DescribeSupportedPromptScenarioOptions(role));

            if (hasConversionInterval && !FeatureScenarioOptionRules.SupportsConversionIntervalSeconds(role))
                Fail(result, label + "는 " + Normalize(role) + " role에서 " + FeatureScenarioOptionRules.DescribeSupportedPromptScenarioOptions(role));

            if (hasInputItemMoveInterval && !FeatureScenarioOptionRules.SupportsInputItemMoveIntervalSeconds(role))
                Fail(result, label + "는 " + Normalize(role) + " role에서 " + FeatureScenarioOptionRules.DescribeSupportedPromptScenarioOptions(role));

            if (hasSpawnInterval && !FeatureScenarioOptionRules.SupportsSpawnIntervalSeconds(role))
                Fail(result, label + "는 " + Normalize(role) + " role에서 " + FeatureScenarioOptionRules.DescribeSupportedPromptScenarioOptions(role));

            PromptIntentSellerRequestableItemDefinition[] requestableItems = value.requestableItems ?? new PromptIntentSellerRequestableItemDefinition[0];
            for (int i = 0; i < requestableItems.Length; i++)
            {
                PromptIntentSellerRequestableItemDefinition requestableItem = requestableItems[i];
                if (requestableItem == null)
                    continue;

                string requestableLabel = label + ".requestableItems[" + i + "]";
                ValidateItemReference(requestableItem.item, requestableLabel + ".item", catalog, result);
                ValidateSellerRequestableItemCondition(
                    requestableItem.startWhen,
                    requestableLabel + ".startWhen",
                    objectById,
                    currencyById,
                    catalog,
                    result);
            }
        }

        private static void ValidateSellerRequestableItemCondition(
            PromptIntentConditionDefinition value,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            if (value == null)
            {
                Fail(result, label + "가 필요합니다. 항상 주문 가능 항목도 startWhen.kind = 'start'로 명시해야 합니다.");
                return;
            }

            string kind = Normalize(value.kind);
            if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
            {
                Fail(result, label + ".kind는 requestableItems에서 stage_completed를 지원하지 않습니다.");
                return;
            }

            ValidateCondition(
                value,
                label,
                -1,
                new PromptIntentStageDefinition[0],
                objectById,
                currencyById,
                catalog,
                result);
        }

        private static void ValidatePhysicsAreaObject(
            PromptIntentObjectDefinition value,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            PlayableObjectCatalog catalog,
            string label,
            PromptIntentSemanticValidationResult result)
        {
            _ = objectById;
            if (value == null)
                return;

            if (value.physicsAreaOptions == null)
            {
                Fail(result, label + ".physicsAreaOptions가 필요합니다.");
                return;
            }

            ValidateItemReference(value.physicsAreaOptions.item, label + ".physicsAreaOptions.item", catalog, result);
        }

        private static void ValidateRailObject(
            PromptIntentObjectDefinition value,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            PlayableObjectCatalog catalog,
            string label,
            PromptIntentSemanticValidationResult result)
        {
            if (value == null)
                return;

            if (value.railOptions == null)
            {
                Fail(result, label + ".railOptions가 필요합니다.");
                return;
            }

            ValidateItemReference(value.railOptions.item, label + ".railOptions.item", catalog, result);
            ValidateRailSinkEndpointTargetReference(
                value.railOptions.sinkEndpointTargetObjectId,
                label + ".railOptions.sinkEndpointTargetObjectId",
                objectById,
                result);
        }

        private static void ValidateRailSinkEndpointTargetReference(
            string objectId,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            PromptIntentSemanticValidationResult result)
        {
            string normalizedObjectId = Normalize(objectId);
            if (string.IsNullOrEmpty(normalizedObjectId))
                return;

            if (!objectById.TryGetValue(normalizedObjectId, out PromptIntentObjectDefinition value) || value == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            string role = Normalize(value.role);
            if (!PromptIntentObjectRoles.IsRailSinkTargetRoleSupported(role))
            {
                Fail(result, label + " '" + normalizedObjectId + "'는 processor/seller여야 합니다.");
            }
        }

        private static void ValidateRequiredWorldBounds(
            WorldBoundsDefinition bounds,
            string label,
            PromptIntentSemanticValidationResult result)
        {
            if (bounds == null)
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            if (!bounds.hasWorldBounds)
                return;

            if (bounds.worldWidth <= 0f)
                Fail(result, label + ".worldWidth는 0보다 커야 합니다.");
            if (bounds.worldDepth <= 0f)
                Fail(result, label + ".worldDepth는 0보다 커야 합니다.");
        }

        private static void ValidateObjectReference(
            string objectId,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            string expectedRole,
            PromptIntentSemanticValidationResult result,
            bool allowAnyRole = false)
        {
            string normalizedObjectId = Normalize(objectId);
            if (string.IsNullOrEmpty(normalizedObjectId))
                return;

            if (!objectById.TryGetValue(normalizedObjectId, out PromptIntentObjectDefinition value) || value == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            if (allowAnyRole || string.IsNullOrEmpty(expectedRole))
                return;

            string actualRole = Normalize(value.role);
            if (!string.Equals(actualRole, Normalize(expectedRole), StringComparison.Ordinal))
                Fail(result, label + " '" + normalizedObjectId + "'는 role '" + expectedRole + "'이어야 합니다.");
        }

        private static void ValidateObjectReferenceByRolePolicy(
            string objectId,
            string label,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            string[] supportedRoles,
            bool allowAnyRole,
            PromptIntentSemanticValidationResult result)
        {
            string[] safeSupportedRoles = supportedRoles ?? new string[0];
            if (allowAnyRole || safeSupportedRoles.Length == 0)
            {
                ValidateObjectReference(objectId, label, objectById, string.Empty, result, allowAnyRole: true);
                return;
            }

            if (safeSupportedRoles.Length == 1)
            {
                ValidateObjectReference(objectId, label, objectById, safeSupportedRoles[0], result);
                return;
            }

            string normalizedObjectId = Normalize(objectId);
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out PromptIntentObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            string actualRole = Normalize(target.role);
            for (int i = 0; i < safeSupportedRoles.Length; i++)
            {
                if (string.Equals(actualRole, Normalize(safeSupportedRoles[i]), StringComparison.Ordinal))
                    return;
            }

            Fail(result, label + " '" + normalizedObjectId + "'는 role '" + JoinRoles(safeSupportedRoles) + "' 중 하나여야 합니다.");
        }

        private static void ValidateTargetEventKeyCompatibility(
            string objectId,
            string eventKey,
            Dictionary<string, PromptIntentObjectDefinition> objectById,
            string label,
            string usageLabel,
            PromptIntentSemanticValidationResult result)
        {
            string normalizedObjectId = Normalize(objectId);
            string normalizedEventKey = Normalize(eventKey);
            if (string.IsNullOrEmpty(normalizedObjectId) || string.IsNullOrEmpty(normalizedEventKey))
                return;

            if (!objectById.TryGetValue(normalizedObjectId, out PromptIntentObjectDefinition value) || value == null)
                return;

            string role = Normalize(value.role);
            if (PromptIntentCapabilityRegistry.RoleSupportsTargetEventKey(role, normalizedEventKey))
                return;

            Fail(result, label + "는 object '" + normalizedObjectId + "'(role='" + role + "')에서 " + usageLabel + " eventKey '" + normalizedEventKey + "'를 지원하지 않습니다." + BuildTargetEventKeyGuidance(normalizedObjectId, role, usageLabel));
        }

        private static void ValidateCurrencyReference(
            string currencyId,
            string label,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            PromptIntentSemanticValidationResult result)
        {
            string normalizedCurrencyId = Normalize(currencyId);
            if (string.IsNullOrEmpty(normalizedCurrencyId))
                return;

            if (!currencyById.ContainsKey(normalizedCurrencyId))
                Fail(result, label + " '" + normalizedCurrencyId + "'가 currencies[]에 존재하지 않습니다.");
        }

        private static void ValidateItemReference(
            ItemRef item,
            string label,
            PlayableObjectCatalog catalog,
            PromptIntentSemanticValidationResult result)
        {
            if (ItemRefUtility.IsEmpty(item))
                return;

            if (!ItemRefUtility.IsValid(item))
            {
                Fail(result, label + "는 familyId와 variantId가 모두 필요합니다.");
                return;
            }

            if (catalog == null)
                return;
        }

        private static bool ExistsPreviousStage(string stageId, int currentStageIndex, PromptIntentStageDefinition[] stages)
        {
            string normalizedStageId = Normalize(stageId);
            if (string.IsNullOrEmpty(normalizedStageId))
                return false;

            PromptIntentStageDefinition[] safeStages = stages ?? new PromptIntentStageDefinition[0];
            for (int i = 0; i < currentStageIndex && i < safeStages.Length; i++)
            {
                if (string.Equals(Normalize(safeStages[i] != null ? safeStages[i].id : string.Empty), normalizedStageId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static int CountEffects(PromptIntentEffectDefinition[] effects, string kind)
        {
            int count = 0;
            PromptIntentEffectDefinition[] safeEffects = effects ?? new PromptIntentEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                if (string.Equals(Normalize(safeEffects[i] != null ? safeEffects[i].kind : string.Empty), Normalize(kind), StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static int CountObjectives(PromptIntentObjectiveDefinition[] objectives, string kind)
        {
            int count = 0;
            PromptIntentObjectiveDefinition[] safeObjectives = objectives ?? new PromptIntentObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                if (string.Equals(Normalize(safeObjectives[i] != null ? safeObjectives[i].kind : string.Empty), Normalize(kind), StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static bool ValidateCatalogContract(PlayableObjectCatalog catalog, PromptIntentSemanticValidationResult result)
        {
            PlayableObjectCatalogValidationResult validation = PlayableObjectCatalogContractValidator.Validate(catalog);
            if (validation.IsValid)
                return true;

            for (int i = 0; i < validation.Errors.Count; i++)
                Fail(result, "catalog contract 오류: " + validation.Errors[i].message);

            return false;
        }

        private static PromptIntentSemanticValidationResult FinalizeResult(PromptIntentSemanticValidationResult result)
        {
            result.IsValid = result.Errors.Count == 0;
            if (result.IsValid)
            {
                result.FailureCode = PlayableFailureCode.None;
                result.Message = "유효합니다.";
            }
            else
            {
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.IntentValidationFailed;
                result.Message = result.Errors[0];
            }

            return result;
        }

        private static PromptIntentSemanticValidationResult Fail(PromptIntentSemanticValidationResult result, string message)
        {
            return Fail(result, new ValidationIssueRecord(
                ValidationRuleId.INTENT_SEMANTIC_GENERIC,
                ValidationSeverity.Blocker,
                message,
                "PromptIntentSemantic"));
        }

        private static PromptIntentSemanticValidationResult Fail(PromptIntentSemanticValidationResult result, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return result;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.IntentValidationFailed;
            result.Errors.Add(issue.message ?? string.Empty);
            result.Issues ??= new List<ValidationIssueRecord>();
            result.Issues.Add(issue);
            return result;
        }

        private static string JoinRoles(string[] roles)
        {
            string[] safeRoles = roles ?? new string[0];
            if (safeRoles.Length == 0)
                return string.Empty;

            string text = Normalize(safeRoles[0]);
            for (int i = 1; i < safeRoles.Length; i++)
                text += ", " + Normalize(safeRoles[i]);
            return text;
        }

        private static string BuildTargetEventKeyGuidance(string objectId, string role, string usageLabel)
        {
            string[] supportedEventKeys = PromptIntentCapabilityRegistry.GetSupportedFlowTargetEventKeys(role);
            if (supportedEventKeys == null || supportedEventKeys.Length == 0)
                return " -> 수정 가이드: 현재 계약에서 role '" + role + "'의 허용 eventKey를 확인하세요.";

            var segments = new List<string>
            {
                "현재 objects[] 기준 object '" + objectId + "'의 role은 '" + role + "'입니다",
                "제작 계약상 role '" + role + "'의 허용 eventKey: [" + string.Join(", ", supportedEventKeys) + "]"
            };

            if (supportedEventKeys.Length == 1)
                segments.Add("추천 수정: " + usageLabel + ".eventKey를 '" + supportedEventKeys[0] + "'로 바꾸세요");

            return " -> 수정 가이드: " + string.Join("; ", segments) + ".";
        }

        private static string Normalize(string value)
        {
            return IntentAuthoringUtility.Normalize(value);
        }
    }
}
