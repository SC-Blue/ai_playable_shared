using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public sealed class ScenarioModelValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public List<string> Errors = new List<string>();
        public List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();
    }

    public static class ScenarioModelValidator
    {
        private struct EconomyLoopState
        {
            // collect_item 이후 바로 sell_item으로 닫을 수 있는 수익 경로 후보인지 추적한다.
            public bool CanDirectSell;
            // convert_item을 거친 뒤 sell_item으로 닫을 수 있는 수익 경로 후보인지 추적한다.
            public bool CanProcessedSell;
            // 현재 sale -> collect_currency가 반복 수익 구성을 완성할 수 있는지 표시한다.
            public bool SaleCanCompleteLoop;
            // 직전 sale이 어느 currency로 정산되었는지 기억해서 collect_currency와 연결한다.
            public string SoldCurrencyId;
        }

        public static ScenarioModelValidationResult Validate(PlayableScenarioModel model)
        {
            var result = new ScenarioModelValidationResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (model == null)
                return Fail(result, "PlayableScenarioModel이 null입니다.");

            if (model.objects == null)
            {
                Fail(result, "objects[]가 필요합니다.");
                return FinalizeResult(result);
            }

            if (model.objects.Length == 0)
            {
                Fail(result, "objects[]에는 최소 1개의 entry가 필요합니다.");
                return FinalizeResult(result);
            }

            if (model.stages == null)
            {
                Fail(result, "stages[]가 필요합니다.");
                return FinalizeResult(result);
            }

            if (model.stages.Length == 0)
            {
                Fail(result, "stages[]에는 최소 1개의 entry가 필요합니다.");
                return FinalizeResult(result);
            }

            ValidatePlayerOptions(model.playerOptions, result);
            ValidateContentSelections(model.contentSelections, result);
            ValidateObjects(model.objects, result);
            ValidateStages(model, result);
            ValidateSellerRequestableItems(model, result);
            ValidateEconomyReachability(model, result);
            return FinalizeResult(result);
        }

        private static void ValidateContentSelections(ContentSelectionDefinition[] selections, ScenarioModelValidationResult result)
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

                string objectId = selection.objectId != null ? selection.objectId.Trim() : string.Empty;
                string designId = selection.designId != null ? selection.designId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(objectId))
                    Fail(result, label + ".objectId는 비어 있을 수 없습니다.");
                else if (!seenObjectIds.Add(objectId))
                    Fail(result, "중복된 contentSelections objectId '" + objectId + "'입니다.");

                if (string.IsNullOrEmpty(designId))
                    Fail(result, label + ".designId는 비어 있을 수 없습니다.");
                else if (ContentSelectionRules.IsUnsetDesignId(designId))
                    Fail(result, label + ".designId는 '" + ContentSelectionRules.DESIGN_ID_NOT_SET + "'일 수 없습니다. 실제 카탈로그 design을 선택해야 합니다.");
            }

            for (int i = 0; i < ContentSelectionRules.REQUIRED_OBJECT_IDS.Length; i++)
            {
                string requiredObjectId = ContentSelectionRules.REQUIRED_OBJECT_IDS[i];
                if (!seenObjectIds.Contains(requiredObjectId))
                    Fail(result, "필수 UI content '" + requiredObjectId + "'에 대한 contentSelections entry가 필요합니다.");
            }
        }

        private static void ValidatePlayerOptions(PlayableScenarioPlayerOptions options, ScenarioModelValidationResult result)
        {
            if (options.itemStacker.maxCount < 0)
                Fail(result, "playerOptions.itemStacker.maxCount는 0 이상이어야 합니다.");
        }

        private static void ValidateFeatureOptions(
            PlayableScenarioFeatureOptions options,
            string label,
            ScenarioModelValidationResult result)
        {
            if (options.customerReqMin < 0 || options.customerReqMax < 0)
                Fail(result, label + ".customerReqMin/Max는 0 이상이어야 합니다.");

            if (options.customerReqMin > 0 || options.customerReqMax > 0)
            {
                if (options.customerReqMin == 0 || options.customerReqMax == 0)
                    Fail(result, label + ".customerReqMin/Max는 둘 다 0이거나 둘 다 1 이상이어야 합니다.");
                else if (options.customerReqMin > options.customerReqMax)
                    Fail(result, label + ".customerReqMin은 customerReqMax보다 클 수 없습니다.");
            }

            if (options.inputCountPerConversion < 0)
                Fail(result, label + ".inputCountPerConversion은 0 이상이어야 합니다.");
            if (options.conversionInterval < 0f)
                Fail(result, label + ".conversionInterval은 0 이상이어야 합니다.");
            if (options.inputItemMoveInterval < 0f)
                Fail(result, label + ".inputItemMoveInterval은 0 이상이어야 합니다.");
            if (options.spawnInterval < 0f)
                Fail(result, label + ".spawnInterval은 0 이상이어야 합니다.");
        }

        private static void ValidateObjects(ScenarioModelObjectDefinition[] objects, ScenarioModelValidationResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            Dictionary<string, ScenarioModelObjectDefinition> objectById = BuildObjectLookup(safeObjects);
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string firstPresentingStageId = value.firstPresentingStageId != null ? value.firstPresentingStageId.Trim() : string.Empty;
                string firstActivatingStageId = value.firstActivatingStageId != null ? value.firstActivatingStageId.Trim() : string.Empty;
                if (!string.Equals(firstPresentingStageId, firstActivatingStageId, StringComparison.Ordinal))
                {
                    Fail(result, "objects[" + i + "]는 reveal_object와 activate_object를 서로 다른 stage에 나누어 visible but non-interactive lifecycle을 요구합니다.");
                }

                if (value.startsPresent && !value.startsActive)
                    Fail(result, "objects[" + i + "]는 visible but non-interactive 상태를 요구합니다. 이번 runtime contract에서는 지원하지 않습니다.");

                if (!value.startsPresent && value.startsActive)
                    Fail(result, "objects[" + i + "]는 present later인데 active at start로 표시되었습니다.");

                string label = "objects[" + i + "].featureOptions";
                ValidateFeatureOptions(value.featureOptions, label, result);
                ValidateObjectFeatureOptionsByRole(value.role, value.featureOptions, label, result);
                ValidateSellerRequestableItemDefinitions(value, "objects[" + i + "]", objectById, result);
                ValidateRoleSpecificOptions(value, "objects[" + i + "]", objectById, result);
            }
        }

        private static void ValidateSellerRequestableItemDefinitions(
            ScenarioModelObjectDefinition value,
            string label,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            ScenarioModelValidationResult result)
        {
            if (value == null)
                return;

            string role = value.role != null ? value.role.Trim() : string.Empty;
            ScenarioModelSellerRequestableItemDefinition[] requestableItems = value.sellerRequestableItems ?? new ScenarioModelSellerRequestableItemDefinition[0];
            if (requestableItems.Length == 0)
                return;

            if (!string.Equals(role, PromptIntentObjectRoles.SELLER, StringComparison.Ordinal))
            {
                Fail(result, label + ".sellerRequestableItems는 seller role에서만 사용할 수 있습니다.");
                return;
            }

            var seenItemKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requestableItems.Length; i++)
            {
                ScenarioModelSellerRequestableItemDefinition requestableItem = requestableItems[i];
                string requestableLabel = label + ".sellerRequestableItems[" + i + "]";
                if (requestableItem == null)
                {
                    Fail(result, requestableLabel + "가 null입니다.");
                    continue;
                }

                string itemKey = ItemRefUtility.ToStableKey(requestableItem.item);
                if (!ItemRefUtility.IsValid(requestableItem.item))
                    Fail(result, requestableLabel + ".item은 familyId와 variantId가 모두 필요합니다.");
                else if (!seenItemKeys.Add(itemKey))
                    Fail(result, requestableLabel + ".item '" + itemKey + "'이 중복되었습니다.");

                ValidateSellerRequestableItemStartCondition(
                    requestableItem.startCondition,
                    requestableLabel + ".startCondition",
                    objectById,
                    result);
            }
        }

        private static void ValidateSellerRequestableItemStartCondition(
            ScenarioModelConditionDefinition condition,
            string label,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            ScenarioModelValidationResult result)
        {
            if (condition == null)
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            string kind = condition.kind != null ? condition.kind.Trim() : string.Empty;
            string[] supportedTargetRoles = PromptIntentCapabilityRegistry.GetConditionSupportedTargetRoles(kind);
            bool allowAnyTargetRole = PromptIntentCapabilityRegistry.ConditionAllowsAnyTargetRole(kind);
            if (allowAnyTargetRole || supportedTargetRoles.Length > 0)
            {
                ValidateScenarioObjectReferenceByRolePolicy(
                    condition.targetObjectId,
                    objectById,
                    supportedTargetRoles,
                    allowAnyTargetRole,
                    label + ".targetObjectId",
                    result);
            }

            if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
            {
                Fail(result, label + ".kind는 requestableItems에서 stage_completed를 지원하지 않습니다.");
                return;
            }

            if (PromptIntentContractRegistry.ConditionRequiresCurrencyId(kind))
            {
                if (string.IsNullOrWhiteSpace(condition.currencyId))
                    Fail(result, label + ".currencyId가 필요합니다.");
            }

            if (PromptIntentContractRegistry.ConditionRequiresItem(kind))
            {
                if (!ItemRefUtility.IsValid(condition.item))
                    Fail(result, label + ".item은 familyId와 variantId가 모두 필요합니다.");
            }
            else if (!ItemRefUtility.IsEmpty(condition.item) && !PromptIntentContractRegistry.ConditionSupportsItem(kind))
            {
                Fail(result, label + ".item은 kind '" + kind + "'에서 지원되지 않습니다.");
            }

            if (!PromptIntentConditionKinds.IsSupported(kind))
            {
                Fail(result, label + ".kind '" + kind + "'은(는) 지원되지 않습니다.");
            }
        }

        private static void ValidateObjectFeatureOptionsByRole(
            string role,
            PlayableScenarioFeatureOptions options,
            string label,
            ScenarioModelValidationResult result)
        {
            string normalizedRole = role != null ? role.Trim() : string.Empty;
            bool supportsCustomerRequestCount = FeatureScenarioOptionRules.SupportsCustomerRequestCount(normalizedRole);
            bool supportsInputCountPerConversion = FeatureScenarioOptionRules.SupportsInputCountPerConversion(normalizedRole);
            bool supportsConversionInterval = FeatureScenarioOptionRules.SupportsConversionIntervalSeconds(normalizedRole);
            bool supportsInputItemMoveInterval = FeatureScenarioOptionRules.SupportsInputItemMoveIntervalSeconds(normalizedRole);
            bool supportsSpawnInterval = FeatureScenarioOptionRules.SupportsSpawnIntervalSeconds(normalizedRole);

            switch (normalizedRole)
            {
                case PromptIntentObjectRoles.SELLER:
                    if (supportsCustomerRequestCount && (options.customerReqMin <= 0 || options.customerReqMax <= 0))
                    {
                        Fail(result, label + "는 seller role에서 customerReqMin/Max가 둘 다 1 이상이어야 합니다.");
                        break;
                    }

                    if (supportsCustomerRequestCount && options.customerReqMin > options.customerReqMax)
                    {
                        Fail(result, label + ".customerReqMin은 customerReqMax보다 클 수 없습니다.");
                        break;
                    }

                    if ((!supportsInputCountPerConversion && options.inputCountPerConversion > 0) ||
                        (!supportsConversionInterval && options.conversionInterval > 0f) ||
                        (!supportsInputItemMoveInterval && options.inputItemMoveInterval > 0f) ||
                        (!supportsSpawnInterval && options.spawnInterval > 0f))
                        Fail(result, label + "는 seller role에서 customerReqMin/Max만 지원합니다.");
                    break;
                case PromptIntentObjectRoles.PROCESSOR:
                    if ((!supportsCustomerRequestCount && (options.customerReqMin > 0 || options.customerReqMax > 0)) ||
                        (!supportsSpawnInterval && options.spawnInterval > 0f))
                        Fail(result, label + "는 processor role에서 conversion 관련 옵션만 지원합니다.");
                    break;
                case PromptIntentObjectRoles.GENERATOR:
                    if ((!supportsCustomerRequestCount && (options.customerReqMin > 0 || options.customerReqMax > 0)) ||
                        (!supportsInputCountPerConversion && options.inputCountPerConversion > 0) ||
                        (!supportsConversionInterval && options.conversionInterval > 0f) ||
                        (!supportsInputItemMoveInterval && options.inputItemMoveInterval > 0f))
                        Fail(result, label + "는 generator role에서 spawnInterval만 지원합니다.");
                    break;
                default:
                    if ((options.customerReqMin > 0 || options.customerReqMax > 0) ||
                        options.inputCountPerConversion > 0 ||
                        options.conversionInterval > 0f ||
                        options.inputItemMoveInterval > 0f ||
                        options.spawnInterval > 0f)
                        Fail(result, label + "는 seller, processor, generator role에서만 지원합니다.");
                    break;
            }
        }

        private static void ValidateRoleSpecificOptions(
            ScenarioModelObjectDefinition value,
            string label,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            ScenarioModelValidationResult result)
        {
            if (value == null)
                return;

            string role = value.role != null ? value.role.Trim() : string.Empty;
            if (string.Equals(role, PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
            {
                if (value.physicsAreaOptions == null || !ItemRefUtility.IsValid(value.physicsAreaOptions.item))
                    Fail(result, label + ".physicsAreaOptions.item이 필요합니다.");
                return;
            }

            if (string.Equals(role, PromptIntentObjectRoles.RAIL, StringComparison.Ordinal))
            {
                if (value.railOptions == null)
                {
                    Fail(result, label + ".railOptions가 필요합니다.");
                    return;
                }

                if (!ItemRefUtility.IsValid(value.railOptions.item))
                    Fail(result, label + ".railOptions.item이 필요합니다.");
                if (string.IsNullOrWhiteSpace(value.railOptions.sinkEndpointTargetObjectId))
                    Fail(result, label + ".railOptions.sinkEndpointTargetObjectId가 필요합니다.");
                else
                    ValidateRailEndpointTargetRole(
                        value.railOptions.sinkEndpointTargetObjectId,
                        objectById,
                        PromptIntentObjectRoles.IsRailSinkTargetRoleSupported,
                        label + ".railOptions.sinkEndpointTargetObjectId",
                        "processor/seller",
                        result);
                if (value.railOptions.spawnIntervalSeconds <= 0f)
                    Fail(result, label + ".railOptions.spawnIntervalSeconds는 0보다 커야 합니다.");
                if (value.railOptions.travelDurationSeconds <= 0f)
                    Fail(result, label + ".railOptions.travelDurationSeconds는 0보다 커야 합니다.");
            }
        }

        private static void ValidateWorldBounds(
            WorldBoundsDefinition bounds,
            string label,
            ScenarioModelValidationResult result)
        {
            if (bounds == null || !bounds.hasWorldBounds)
                return;

            if (bounds.worldWidth <= 0f)
                Fail(result, label + ".worldWidth는 0보다 커야 합니다.");
            if (bounds.worldDepth <= 0f)
                Fail(result, label + ".worldDepth는 0보다 커야 합니다.");
        }

        private static void ValidateStages(PlayableScenarioModel model, ScenarioModelValidationResult result)
        {
            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];
            Dictionary<string, ScenarioModelObjectDefinition> objectById = BuildObjectLookup(model.objects);
            for (int i = 0; i < stages.Length; i++)
            {
                ScenarioModelStageDefinition stage = stages[i];
                if (stage == null)
                    continue;

                ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
                ScenarioModelEffectDefinition[] entryEffects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];
                ScenarioModelEffectDefinition[] completionEffects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];

                int absorbableCount = 0;
                bool hasAbsorbedArrow = false;
                for (int j = 0; j < objectives.Length; j++)
                {
                    ScenarioModelObjectiveDefinition objective = objectives[j];
                    if (objective == null)
                        continue;

                    if (PromptIntentObjectiveKinds.CanAbsorbArrow(objective.kind))
                        absorbableCount++;
                    if (objective.absorbsArrow)
                        hasAbsorbedArrow = true;

                    ValidateObjectiveContract(objective, objectById, "stages[" + i + "].objectives[" + j + "]", result);
                }

                ValidateEffectContract(entryEffects, objectById, "stages[" + i + "].entryEffects", result);
                ValidateEffectContract(completionEffects, objectById, "stages[" + i + "].completionEffects", result);

                bool hasResidualArrowEffect = HasEffect(entryEffects, PromptIntentEffectKinds.SHOW_ARROW);
                if (hasResidualArrowEffect)
                    Fail(result, "stages[" + i + "]는 arrow를 흡수할 수 있는 beat가 없습니다.");

                if (hasAbsorbedArrow && absorbableCount == 0)
                    Fail(result, "stages[" + i + "]는 absorbable objective 없이 arrow 흡수를 요청했습니다.");

                bool hasBlockingBeat = HasFocusCamera(entryEffects) || objectives.Length > 0;
                bool hasEntryArtifact = HasLowerableReactiveEntryEffect(entryEffects);
                bool hasCompletionArtifact = completionEffects.Length > 0;
                if (!hasBlockingBeat && !hasEntryArtifact && !hasCompletionArtifact)
                    Fail(result, "stages[" + i + "]는 executable artifact를 하나도 생성하지 못하는 empty stage입니다.");

                if (!hasBlockingBeat && completionEffects.Length > 0)
                    Fail(result, "stages[" + i + "]는 completion trigger를 만들 beat가 없는데 onComplete effect를 가지고 있습니다.");

                if (HasFocusCamera(entryEffects) &&
                    objectives.Length == 0 &&
                    completionEffects.Length == 0 &&
                    !HasEffect(entryEffects, PromptIntentEffectKinds.SHOW_GUIDE_ARROW))
                {
                    Fail(result, "stages[" + i + "]는 focus_camera만 있고 이후 progression을 만들 objective/effect가 없습니다.");
                }

                int unlockObjectiveCount = CountObjectives(objectives, PromptIntentObjectiveKinds.UNLOCK_OBJECT);
                if (unlockObjectiveCount > 1)
                    Fail(result, "stages[" + i + "]는 unlock_object objective를 1개까지만 지원합니다.");

                ValidateSpawnCustomerTiming(stage, i, stages, result);
                ValidateShowArrowTiming(stage, i, result);
            }
        }

        private static void ValidateRailEndpointTargetRole(
            string objectId,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            Func<string, bool> isSupportedRole,
            string label,
            string expectedRoles,
            ScenarioModelValidationResult result)
        {
            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
                return;

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            string role = target.role != null ? target.role.Trim() : string.Empty;
            if (isSupportedRole == null || !isSupportedRole(role))
                Fail(result, label + " '" + normalizedObjectId + "'는 " + expectedRoles + "여야 합니다.");
        }

        private static Dictionary<string, ScenarioModelObjectDefinition> BuildObjectLookup(ScenarioModelObjectDefinition[] objects)
        {
            var objectById = new Dictionary<string, ScenarioModelObjectDefinition>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = value != null && value.id != null ? value.id.Trim() : string.Empty;
                if (string.IsNullOrEmpty(objectId) || objectById.ContainsKey(objectId))
                    continue;

                objectById.Add(objectId, value);
            }

            return objectById;
        }

        private static void ValidateScenarioObjectReference(
            string objectId,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string expectedRole,
            string label,
            ScenarioModelValidationResult result,
            bool allowAnyRole = false)
        {
            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            if (allowAnyRole)
                return;

            string normalizedExpectedRole = expectedRole != null ? expectedRole.Trim() : string.Empty;
            string actualRole = target.role != null ? target.role.Trim() : string.Empty;
            if (!string.Equals(actualRole, normalizedExpectedRole, StringComparison.Ordinal))
                Fail(result, label + " '" + normalizedObjectId + "'는 role '" + normalizedExpectedRole + "'이어야 합니다.");
        }

        private static void ValidateScenarioObjectReferenceByRolePolicy(
            string objectId,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string[] supportedRoles,
            bool allowAnyRole,
            string label,
            ScenarioModelValidationResult result)
        {
            string[] safeSupportedRoles = supportedRoles ?? new string[0];
            if (allowAnyRole || safeSupportedRoles.Length == 0)
            {
                ValidateScenarioObjectReference(objectId, objectById, string.Empty, label, result, allowAnyRole: true);
                return;
            }

            if (safeSupportedRoles.Length == 1)
            {
                ValidateScenarioObjectReference(objectId, objectById, safeSupportedRoles[0], label, result);
                return;
            }

            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            string actualRole = target.role != null ? target.role.Trim() : string.Empty;
            for (int i = 0; i < safeSupportedRoles.Length; i++)
            {
                if (string.Equals(actualRole, safeSupportedRoles[i] != null ? safeSupportedRoles[i].Trim() : string.Empty, StringComparison.Ordinal))
                    return;
            }

            Fail(result, label + " '" + normalizedObjectId + "'는 role '" + JoinRoles(safeSupportedRoles) + "' 중 하나여야 합니다.");
        }

        private static void ValidateSellerRequestableItems(PlayableScenarioModel model, ScenarioModelValidationResult result)
        {
            if (model == null)
                return;

            var acceptedItemKeysBySellerId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                ScenarioModelStageDefinition stage = stages[stageIndex];
                ScenarioModelObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new ScenarioModelObjectiveDefinition[0] : new ScenarioModelObjectiveDefinition[0];
                for (int objectiveIndex = 0; objectiveIndex < objectives.Length; objectiveIndex++)
                {
                    ScenarioModelObjectiveDefinition objective = objectives[objectiveIndex];
                    if (objective == null ||
                        !string.Equals(objective.kind != null ? objective.kind.Trim() : string.Empty, PromptIntentObjectiveKinds.SELL_ITEM, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string sellerId = objective.targetObjectId != null ? objective.targetObjectId.Trim() : string.Empty;
                    string itemKey = ItemRefUtility.ToStableKey(objective.item);
                    if (string.IsNullOrEmpty(sellerId) || string.IsNullOrEmpty(itemKey))
                        continue;

                    if (!acceptedItemKeysBySellerId.TryGetValue(sellerId, out HashSet<string> itemKeys))
                    {
                        itemKeys = new HashSet<string>(StringComparer.Ordinal);
                        acceptedItemKeysBySellerId.Add(sellerId, itemKeys);
                    }

                    itemKeys.Add(itemKey);
                }
            }

            ScenarioModelObjectDefinition[] objects = model.objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < objects.Length; i++)
            {
                ScenarioModelObjectDefinition value = objects[i];
                if (value == null)
                    continue;

                string sellerId = value.id != null ? value.id.Trim() : string.Empty;
                ScenarioModelSellerRequestableItemDefinition[] requestableItems = value.sellerRequestableItems ?? new ScenarioModelSellerRequestableItemDefinition[0];
                for (int requestIndex = 0; requestIndex < requestableItems.Length; requestIndex++)
                {
                    ScenarioModelSellerRequestableItemDefinition requestableItem = requestableItems[requestIndex];
                    if (requestableItem == null)
                        continue;

                    string itemKey = ItemRefUtility.ToStableKey(requestableItem.item);
                    if (string.IsNullOrEmpty(itemKey))
                        continue;

                    if (!acceptedItemKeysBySellerId.TryGetValue(sellerId, out HashSet<string> acceptedItems) || !acceptedItems.Contains(itemKey))
                    {
                        Fail(result, "objects[" + i + "].sellerRequestableItems[" + requestIndex + "].item '" + itemKey + "'은(는) 해당 seller의 sell_item objective로 선언되어야 합니다.");
                    }
                }
            }
        }

        private static void ValidateObjectiveContract(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string label,
            ScenarioModelValidationResult result)
        {
            if (objective == null)
                return;

            string kind = objective.kind != null ? objective.kind.Trim() : string.Empty;
            string targetEventKey = PromptIntentCapabilityRegistry.GetObjectiveTargetEventKey(kind);
            if (!string.IsNullOrWhiteSpace(targetEventKey) && !string.IsNullOrWhiteSpace(objective.targetObjectId))
            {
                ValidateTargetEventKeyCapability(objective.targetObjectId, targetEventKey, objectById, label + ".targetObjectId", kind, result);
            }

            if (string.Equals(kind, PromptIntentObjectiveKinds.CONVERT_ITEM, StringComparison.Ordinal))
            {
                if (!ItemRefUtility.IsValid(objective.inputItem))
                    Fail(result, label + ".inputItem이 필요합니다.");
            }

            if (PromptIntentCapabilityRegistry.ObjectiveRequiresAbsorbedArrow(kind) && !objective.absorbsArrow)
            {
                string missingArrowEventKey = PromptIntentCapabilityRegistry.GetObjectiveRequiredArrowEventKey(kind);
                Fail(result, label + "에는 explicit show_arrow(eventKey='" + missingArrowEventKey + "')가 필요합니다.");
                return;
            }

            if (!objective.absorbsArrow)
                return;

            string arrowTargetObjectId = !string.IsNullOrWhiteSpace(objective.arrowTargetObjectId)
                ? objective.arrowTargetObjectId.Trim()
                : (objective.targetObjectId != null ? objective.targetObjectId.Trim() : string.Empty);
            string arrowEventKey = objective.arrowEventKey != null ? objective.arrowEventKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(arrowEventKey))
            {
                Fail(result, label + ".arrowEventKey가 필요합니다.");
                return;
            }

            string requiredArrowEventKey = PromptIntentCapabilityRegistry.GetObjectiveRequiredArrowEventKey(kind);
            if (!string.IsNullOrEmpty(requiredArrowEventKey) &&
                !string.Equals(arrowEventKey, requiredArrowEventKey, StringComparison.Ordinal))
            {
                Fail(result, label + "의 absorbed arrow는 eventKey '" + requiredArrowEventKey + "'이어야 합니다.");
            }

            ValidateTargetEventKeyCapability(arrowTargetObjectId, arrowEventKey, objectById, label, "show_arrow", result);
        }

        private static void ValidateEffectContract(
            ScenarioModelEffectDefinition[] effects,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string label,
            ScenarioModelValidationResult result)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    !string.Equals(effect.kind != null ? effect.kind.Trim() : string.Empty, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal))
                {
                    continue;
                }

                string eventKey = effect.eventKey != null ? effect.eventKey.Trim() : string.Empty;
                if (string.IsNullOrEmpty(eventKey))
                {
                    Fail(result, label + "[" + i + "].eventKey가 필요합니다.");
                    continue;
                }

                ValidateTargetEventKeyCapability(effect.targetObjectId, eventKey, objectById, label + "[" + i + "]", "show_arrow", result);
            }
        }

        private static void ValidateTargetEventKeyCapability(
            string objectId,
            string eventKey,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string label,
            string usageLabel,
            ScenarioModelValidationResult result)
        {
            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            string normalizedEventKey = eventKey != null ? eventKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId) || string.IsNullOrEmpty(normalizedEventKey))
                return;

            if (!objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition value) || value == null)
                return;

            string role = value.role != null ? value.role.Trim() : string.Empty;
            if (PromptIntentCapabilityRegistry.RoleSupportsTargetEventKey(role, normalizedEventKey))
                return;

            Fail(result, label + "는 object '" + normalizedObjectId + "'(role='" + role + "')에서 " + usageLabel + " eventKey '" + normalizedEventKey + "'를 지원하지 않습니다." + BuildTargetEventKeyGuidance(normalizedObjectId, role, usageLabel));
        }

        private static void ValidateEconomyReachability(PlayableScenarioModel model, ScenarioModelValidationResult result)
        {
            ScenarioModelCurrencyDefinition[] currencies = model.currencies ?? new ScenarioModelCurrencyDefinition[0];
            ScenarioModelSaleValueDefinition[] saleValues = model.saleValues ?? new ScenarioModelSaleValueDefinition[0];
            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];

            var spendableBalanceByCurrency = new Dictionary<string, int>(StringComparer.Ordinal);
            var pendingCollectedByCurrency = new Dictionary<string, int>(StringComparer.Ordinal);
            var repeatableIncomeCurrencies = new HashSet<string>(StringComparer.Ordinal);
            var loopState = new EconomyLoopState();
            for (int i = 0; i < currencies.Length; i++)
            {
                ScenarioModelCurrencyDefinition currency = currencies[i];
                if (currency == null || string.IsNullOrWhiteSpace(currency.currencyId))
                    continue;

                string currencyId = currency.currencyId.Trim();
                spendableBalanceByCurrency[currencyId] = Math.Max(0, currency.startingAmount);
                pendingCollectedByCurrency[currencyId] = 0;
            }

            var saleValuesByItemKey = new Dictionary<string, ScenarioModelSaleValueDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < saleValues.Length; i++)
            {
                ScenarioModelSaleValueDefinition saleValue = saleValues[i];
                string itemKey = ItemRefUtility.ToStableKey(saleValue != null ? saleValue.item : null);
                if (saleValue == null || string.IsNullOrWhiteSpace(itemKey))
                    continue;

                saleValuesByItemKey[itemKey] = saleValue;
            }

            for (int i = 0; i < stages.Length; i++)
            {
                ScenarioModelStageDefinition stage = stages[i];
                if (stage == null || stage.enterCondition == null)
                    continue;

                ScenarioModelConditionDefinition enterCondition = stage.enterCondition;
                if (string.Equals(enterCondition.kind, PromptIntentConditionKinds.BALANCE_AT_LEAST, StringComparison.Ordinal))
                {
                    string currencyId = enterCondition.currencyId != null ? enterCondition.currencyId.Trim() : string.Empty;
                    int currentUpperBound = GetCurrencyAmount(spendableBalanceByCurrency, currencyId);
                    if (currentUpperBound < enterCondition.amount && !repeatableIncomeCurrencies.Contains(currencyId))
                    {
                        Fail(result, "stages[" + i + "].enterCondition은 currency '" + currencyId + "'의 threshold '" + enterCondition.amountValue + "'에 도달할 수 없습니다.");
                    }
                }

                ApplyStageEconomyEffects(
                    stage.objectives,
                    saleValuesByItemKey,
                    spendableBalanceByCurrency,
                    pendingCollectedByCurrency,
                    repeatableIncomeCurrencies,
                    ref loopState);
            }
        }

        private static void ApplyStageEconomyEffects(
            ScenarioModelObjectiveDefinition[] objectives,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByItemKey,
            Dictionary<string, int> spendableBalanceByCurrency,
            Dictionary<string, int> pendingCollectedByCurrency,
            HashSet<string> repeatableIncomeCurrencies,
            ref EconomyLoopState loopState)
        {
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];

            for (int i = 0; i < safeObjectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = safeObjectives[i];
                if (objective == null)
                    continue;

                string kind = objective.kind != null ? objective.kind.Trim() : string.Empty;
                switch (kind)
                {
                    case PromptIntentObjectiveKinds.COLLECT_ITEM:
                        loopState.CanDirectSell = true;
                        loopState.CanProcessedSell = false;
                        loopState.SaleCanCompleteLoop = false;
                        loopState.SoldCurrencyId = string.Empty;
                        break;
                    case PromptIntentObjectiveKinds.CONVERT_ITEM:
                        if (loopState.CanDirectSell)
                            loopState.CanProcessedSell = true;
                        loopState.SaleCanCompleteLoop = false;
                        loopState.SoldCurrencyId = string.Empty;
                        break;
                    case PromptIntentObjectiveKinds.SELL_ITEM:
                        ApplySaleObjective(
                            objective,
                            saleValuesByItemKey,
                            pendingCollectedByCurrency,
                            ref loopState.SaleCanCompleteLoop,
                            ref loopState.SoldCurrencyId,
                            loopState.CanDirectSell,
                            loopState.CanProcessedSell);
                        loopState.CanDirectSell = false;
                        loopState.CanProcessedSell = false;
                        break;
                    case PromptIntentObjectiveKinds.COLLECT_CURRENCY:
                        ApplyCollectCurrencyObjective(
                            objective,
                            spendableBalanceByCurrency,
                            pendingCollectedByCurrency,
                            repeatableIncomeCurrencies,
                            ref loopState.SaleCanCompleteLoop,
                            ref loopState.SoldCurrencyId);
                        break;
                    case PromptIntentObjectiveKinds.UNLOCK_OBJECT:
                        ApplyUnlockObjective(objective, spendableBalanceByCurrency);
                        loopState.SaleCanCompleteLoop = false;
                        loopState.SoldCurrencyId = string.Empty;
                        loopState.CanDirectSell = false;
                        loopState.CanProcessedSell = false;
                        break;
                    default:
                        break;
                }
            }
        }

        private static void ApplySaleObjective(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByItemKey,
            Dictionary<string, int> pendingCollectedByCurrency,
            ref bool saleCanCompleteLoop,
            ref string soldCurrencyId,
            bool canDirectSell,
            bool canProcessedSell)
        {
            string itemKey = ItemRefUtility.ToStableKey(objective != null ? objective.item : null);
            if (!saleValuesByItemKey.TryGetValue(itemKey, out ScenarioModelSaleValueDefinition saleValue) || saleValue == null)
            {
                saleCanCompleteLoop = false;
                soldCurrencyId = string.Empty;
                return;
            }

            string currencyId = saleValue.currencyId != null ? saleValue.currencyId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(currencyId))
            {
                saleCanCompleteLoop = false;
                soldCurrencyId = string.Empty;
                return;
            }

            int currentPending = GetCurrencyAmount(pendingCollectedByCurrency, currencyId);
            pendingCollectedByCurrency[currencyId] = currentPending + Math.Max(0, saleValue.amount);
            saleCanCompleteLoop = canDirectSell || canProcessedSell;
            soldCurrencyId = currencyId;
        }

        private static void ApplyCollectCurrencyObjective(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, int> spendableBalanceByCurrency,
            Dictionary<string, int> pendingCollectedByCurrency,
            HashSet<string> repeatableIncomeCurrencies,
            ref bool saleCanCompleteLoop,
            ref string soldCurrencyId)
        {
            string currencyId = objective.currencyId != null ? objective.currencyId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(currencyId))
            {
                saleCanCompleteLoop = false;
                soldCurrencyId = string.Empty;
                return;
            }

            int pendingAmount = GetCurrencyAmount(pendingCollectedByCurrency, currencyId);
            if (pendingAmount > 0)
            {
                int spendableAmount = GetCurrencyAmount(spendableBalanceByCurrency, currencyId);
                spendableBalanceByCurrency[currencyId] = spendableAmount + pendingAmount;
                pendingCollectedByCurrency[currencyId] = 0;
            }

            if (saleCanCompleteLoop && string.Equals(soldCurrencyId, currencyId, StringComparison.Ordinal) && pendingAmount > 0)
                repeatableIncomeCurrencies.Add(currencyId);

            saleCanCompleteLoop = false;
            soldCurrencyId = string.Empty;
        }

        private static void ApplyUnlockObjective(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, int> spendableBalanceByCurrency)
        {
            string currencyId = objective.currencyId != null ? objective.currencyId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(currencyId))
                return;

            int currentBalance = GetCurrencyAmount(spendableBalanceByCurrency, currencyId);
            spendableBalanceByCurrency[currencyId] = Math.Max(0, currentBalance - Math.Max(0, objective.amount));
        }

        private static int GetCurrencyAmount(Dictionary<string, int> amountsByCurrency, string currencyId)
        {
            if (string.IsNullOrEmpty(currencyId))
                return 0;

            return amountsByCurrency.TryGetValue(currencyId, out int amount) ? amount : 0;
        }

        private static bool HasFocusCamera(ScenarioModelEffectDefinition[] effects)
        {
            return HasEffect(effects, PromptIntentEffectKinds.FOCUS_CAMERA);
        }

        private static bool HasLowerableReactiveEntryEffect(ScenarioModelEffectDefinition[] effects)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                string kind = safeEffects[i] != null ? safeEffects[i].kind : string.Empty;
                if (string.Equals(kind, PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                    continue;
                if (string.Equals(kind, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal))
                    continue;
                return true;
            }

            return false;
        }

        private static void ValidateSpawnCustomerTiming(
            ScenarioModelStageDefinition stage,
            int stageIndex,
            ScenarioModelStageDefinition[] allStages,
            ScenarioModelValidationResult result)
        {
            if (stage == null)
                return;

            bool completionFocusAsLastBeat = HasCompletionFocusAsLastBeat(stage);
            bool previousStageFocusAsLastBeat = HasPreviousStageFocusAsLastBeat(stageIndex, stage, allStages);

            ValidateSpawnCustomerTimingGroup(
                stage.entryEffects,
                "stages[" + stageIndex + "].entryEffects",
                requireExplicitTiming: previousStageFocusAsLastBeat,
                previousStageFocusAsLastBeat,
                result);

            ValidateSpawnCustomerTimingGroup(
                stage.completionEffects,
                "stages[" + stageIndex + "].completionEffects",
                requireExplicitTiming: completionFocusAsLastBeat,
                completionFocusAsLastBeat,
                result);
        }

        private static void ValidateSpawnCustomerTimingGroup(
            ScenarioModelEffectDefinition[] effects,
            string label,
            bool requireExplicitTiming,
            bool allowArrivalTiming,
            ScenarioModelValidationResult result)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    !string.Equals(effect.kind != null ? effect.kind.Trim() : string.Empty, PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                {
                    continue;
                }

                string timing = effect.timing != null ? effect.timing.Trim() : string.Empty;
                if (requireExplicitTiming && string.IsNullOrEmpty(timing))
                {
                    Fail(result, label + "[" + i + "].timing이 필요합니다. focus camera와 직접 연결되는 spawn_customer는 arrival/completed를 명시해야 합니다.");
                    continue;
                }

                if (!string.IsNullOrEmpty(timing) &&
                    !PromptIntentEffectTimingKinds.IsSupported(timing))
                {
                    Fail(result, label + "[" + i + "].timing '" + timing + "'은(는) 지원되지 않습니다.");
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
            ScenarioModelStageDefinition stage,
            int stageIndex,
            ScenarioModelValidationResult result)
        {
            if (stage == null)
                return;

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            bool hasArrivalArrow = false;
            for (int i = 0; i < objectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = objectives[i];
                if (objective == null)
                    continue;

                string arrowTiming = PromptIntentCapabilityRegistry.ResolveArrowTiming(objective.arrowTiming, objective.arrowOnFocusArrival);
                if (!string.IsNullOrEmpty(arrowTiming) && !PromptIntentEffectTimingKinds.IsSupported(arrowTiming))
                {
                    Fail(result, "stages[" + stageIndex + "].objectives[" + i + "].arrowTiming '" + arrowTiming + "'은(는) 지원되지 않습니다.");
                    continue;
                }

                if (!string.IsNullOrEmpty(arrowTiming) && !objective.absorbsArrow)
                {
                    Fail(result, "stages[" + stageIndex + "].objectives[" + i + "]는 absorbed arrow 없이 arrow timing을 가질 수 없습니다.");
                    continue;
                }

                if (objective.absorbsArrow &&
                    (string.Equals(arrowTiming, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal) ||
                     string.Equals(arrowTiming, PromptIntentEffectTimingKinds.COMPLETED, StringComparison.Ordinal)))
                {
                    if (!HasFocusCamera(stage.entryEffects))
                    {
                        Fail(result, "stages[" + stageIndex + "]의 explicit show_arrow timing은 same-stage entry focus_camera가 있을 때만 사용할 수 있습니다.");
                        continue;
                    }
                }

                if (objective.absorbsArrow &&
                    string.Equals(arrowTiming, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
                    hasArrivalArrow = true;
            }

            if (hasArrivalArrow && !HasFocusCamera(stage.entryEffects))
                Fail(result, "stages[" + stageIndex + "]의 show_arrow arrival timing은 same-stage entry focus_camera가 있을 때만 사용할 수 있습니다.");
        }

        private static bool HasPreviousStageFocusAsLastBeat(
            int stageIndex,
            ScenarioModelStageDefinition stage,
            ScenarioModelStageDefinition[] allStages)
        {
            if (stageIndex <= 0)
                return false;

            ScenarioModelConditionDefinition enterCondition = stage != null ? stage.enterCondition : null;
            string kind = enterCondition != null && enterCondition.kind != null ? enterCondition.kind.Trim() : string.Empty;
            if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
            {
                string referencedStageId = enterCondition.stageId != null ? enterCondition.stageId.Trim() : string.Empty;
                ScenarioModelStageDefinition referencedStage = FindStageById(allStages, referencedStageId);
                return HasFocusAsLastBeat(referencedStage);
            }

            return HasFocusAsLastBeat(allStages[stageIndex - 1]);
        }

        private static bool HasFocusAsLastBeat(ScenarioModelStageDefinition stage)
        {
            if (stage == null)
                return false;

            if (HasCompletionFocusAsLastBeat(stage))
                return true;

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            if (objectives.Length > 0)
                return false;

            return HasFocusCamera(stage.entryEffects);
        }

        private static bool HasCompletionFocusAsLastBeat(ScenarioModelStageDefinition stage)
        {
            ScenarioModelEffectDefinition[] effects = stage != null ? stage.completionEffects ?? new ScenarioModelEffectDefinition[0] : new ScenarioModelEffectDefinition[0];
            return HasFocusCamera(effects);
        }

        private static ScenarioModelStageDefinition FindStageById(ScenarioModelStageDefinition[] stages, string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                return null;

            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage != null && string.Equals(stage.id != null ? stage.id.Trim() : string.Empty, stageId, StringComparison.Ordinal))
                    return stage;
            }

            return null;
        }

        private static int CountObjectives(ScenarioModelObjectiveDefinition[] objectives, string kind)
        {
            int count = 0;
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                if (string.Equals(safeObjectives[i] != null ? safeObjectives[i].kind : string.Empty, kind, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static bool HasEffect(ScenarioModelEffectDefinition[] effects, string kind)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                if (string.Equals(safeEffects[i] != null ? safeEffects[i].kind : string.Empty, kind, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static ScenarioModelValidationResult FinalizeResult(ScenarioModelValidationResult result)
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
                    result.FailureCode = PlayableFailureCode.ModelValidationFailed;
                result.Message = result.Errors[0];
            }

            return result;
        }

        private static ScenarioModelValidationResult Fail(ScenarioModelValidationResult result, string message)
        {
            return Fail(result, new ValidationIssueRecord(
                ValidationRuleId.SCENARIO_MODEL_GENERIC,
                ValidationSeverity.Blocker,
                message,
                "ScenarioModel"));
        }

        private static ScenarioModelValidationResult Fail(ScenarioModelValidationResult result, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return result;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.ModelValidationFailed;
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

            string text = safeRoles[0] != null ? safeRoles[0].Trim() : string.Empty;
            for (int i = 1; i < safeRoles.Length; i++)
                text += ", " + (safeRoles[i] != null ? safeRoles[i].Trim() : string.Empty);
            return text;
        }

        private static string BuildTargetEventKeyGuidance(string objectId, string role, string usageLabel)
        {
            string[] supportedEventKeys = PromptIntentCapabilityRegistry.GetSupportedFlowTargetEventKeys(role);
            if (supportedEventKeys == null || supportedEventKeys.Length == 0)
                return " -> 수정 가이드: 현재 계약에서 role '" + role + "'의 허용 eventKey를 확인하세요.";

            var segments = new List<string>
            {
                "현재 model objects[] 기준 object '" + objectId + "'의 role은 '" + role + "'입니다",
                "제작 계약상 role '" + role + "'의 허용 eventKey: [" + string.Join(", ", supportedEventKeys) + "]"
            };

            if (supportedEventKeys.Length == 1)
                segments.Add("추천 수정: " + usageLabel + ".eventKey를 '" + supportedEventKeys[0] + "'로 바꾸세요");

            return " -> 수정 가이드: " + string.Join("; ", segments) + ".";
        }
    }
}
