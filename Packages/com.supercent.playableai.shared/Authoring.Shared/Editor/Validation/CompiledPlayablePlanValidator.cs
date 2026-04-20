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
        private sealed class GameplaySpawnFootprintBounds
        {
            public string ReferenceId = string.Empty;
            public string SceneObjectId = string.Empty;
            public string SpawnKey = string.Empty;
            public string ObjectId = string.Empty;
            public string Role = string.Empty;
            public string SinkEndpointTargetObjectId = string.Empty;
            public string LaneId = string.Empty;
            public bool HasLaneOrder;
            public int LaneOrder;
            public string SharedSlotId = string.Empty;
            public bool HasMinGapToNextCells;
            public float MinGapToNextCells;
            public float CenterX;
            public float CenterZ;
            public bool HasResolvedYaw;
            public float ResolvedYawDegrees;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        private sealed class EnvironmentOccupiedCellSet
        {
            public int EntryIndex;
            public float TileStep;
            public HashSet<EnvironmentCellCoordinate> OccupiedCells = new HashSet<EnvironmentCellCoordinate>();
        }

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
        private const float MAX_IMAGE_LAYOUT_PADDING_PER_SIDE = 4f;

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

            Dictionary<string, int> objectDesignLookup = BuildObjectDesignLookup(plan.objectDesigns);
            Dictionary<string, CompiledSpawnData> spawnLookup = BuildSpawnLookup(plan.spawns, plan.physicsAreas);

            ValidateCurrencies(plan.currencies, result);
            Dictionary<string, int> currencyUnits = BuildCurrencyUnitLookup(plan.currencies);
            int primaryCurrencyUnitValue = ResolvePrimaryCurrencyUnitValue(plan.currencies);
            ValidateUnlocks(plan.unlocks, spawnLookup, currencyUnits, result);
            ValidateItemPrices(plan.itemPrices, catalog, primaryCurrencyUnitValue, result);
            ValidateFacilityAcceptedItems(plan.facilityAcceptedItems, spawnLookup, catalog, result);
            ValidateFacilityOutputItems(plan.spawns, plan.facilityOutputItems, spawnLookup, catalog, result);
            ValidatePlayerOptions(plan.playerOptions, result);
            ValidateFacilityOptions(plan.facilityOptions, spawnLookup, result);
            ValidateCompiledFlowBeats(plan.beats, plan.actions, plan.facilityAcceptedItems, spawnLookup, catalog, currencyUnits, result);
            HashSet<string> declaredSourceImageIds = ValidateSourceImages(layoutSpec, result);
            ValidateCustomerPaths(layoutSpec, spawnLookup, catalog, result);
            ValidateSourceImageReferences(layoutSpec, declaredSourceImageIds, result);
            ValidatePlacementSpatialSemantics(layoutSpec, result);
            HashSet<string> physicsAreaObjectIds = ValidatePhysicsAreas(plan.physicsAreas, catalog, result);
            ValidateRails(plan.rails, spawnLookup, physicsAreaObjectIds, catalog, result);
            ValidateRuntimeOwnedDesignSources(plan.spawns, plan.facilityAcceptedItems, plan.facilityOutputItems, plan.itemPrices, objectDesignLookup, catalog, result);
            ValidateImageLayoutEnvironmentPresence(layoutSpec, catalog, result);
            ValidateOuterRoadFloorClearance(layoutSpec, catalog, result);
            ValidateImageLayoutPadding(plan.spawns, plan.physicsAreas, plan.rails, catalog, layoutSpec, result);
            ValidateGameplaySpawnFootprintOverlaps(plan.spawns, plan.physicsAreas, plan.rails, plan.unlocks, catalog, layoutSpec, result);
            ValidateDeclaredLaneRelationships(plan.spawns, plan.physicsAreas, plan.rails, plan.unlocks, catalog, layoutSpec, result);
            ValidateGameplaySpawnLayoutContainment(plan.spawns, plan.physicsAreas, plan.rails, plan.unlocks, catalog, layoutSpec, result);
            ValidateEnvironmentOccupiedCellConflicts(plan.spawns, plan.physicsAreas, plan.rails, plan.unlocks, catalog, layoutSpec, result);

            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            result.IsValid = true;
            result.Message = "유효합니다.";
            return result;
        }

        private static void ValidateCompiledFlowBeats(
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            FacilityAcceptedItemDefinition[] acceptedItems,
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

            var acceptedItemKeysByFacilityId = BuildAcceptedItemKeysByFacilityId(acceptedItems);
            HashSet<string> actionIds = ValidateCompiledFlowActions(
                actions,
                seenBeatIds,
                acceptedItemKeysByFacilityId,
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
            Dictionary<string, HashSet<string>> acceptedItemKeysByFacilityId,
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
                    acceptedItemKeysByFacilityId,
                    spawnLookup,
                    catalog,
                    result);
            }

            return seenActionIds;
        }

        private static void ValidateCompiledFlowActionPayload(
            FlowActionDefinition action,
            string label,
            Dictionary<string, HashSet<string>> acceptedItemKeysByFacilityId,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            string kind = action.kind != null ? action.kind.Trim() : string.Empty;
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
                        !IsEmptyRevealPayload(payload.reveal) ||
                        !IsEmptyCustomerSpawnPayload(payload.customerSpawn) ||
                        !IsEmptySellerRequestPayload(payload.sellerRequest))
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
                        !IsEmptyRevealPayload(payload.reveal) ||
                        !IsEmptyCustomerSpawnPayload(payload.customerSpawn) ||
                        !IsEmptySellerRequestPayload(payload.sellerRequest))
                    {
                        Fail(result, label + "에는 arrow guide 외 payload가 함께 들어있습니다.");
                    }

                    ValidateCompiledFlowTarget(payload.arrowGuide.targetId, payload.arrowGuide.eventKey, label + ".payload.arrowGuide", spawnLookup, result);
                    break;

                case FlowActionKinds.REVEAL:
                    if (!IsEmptyCameraFocusPayload(payload.cameraFocus) ||
                        !IsEmptyArrowGuidePayload(payload.arrowGuide) ||
                        !IsEmptyCustomerSpawnPayload(payload.customerSpawn) ||
                        !IsEmptySellerRequestPayload(payload.sellerRequest))
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

                case FlowActionKinds.CUSTOMER_SPAWN:
                    if (!IsEmptyCameraFocusPayload(payload.cameraFocus) ||
                        !IsEmptyArrowGuidePayload(payload.arrowGuide) ||
                        !IsEmptyRevealPayload(payload.reveal) ||
                        !IsEmptySellerRequestPayload(payload.sellerRequest))
                    {
                        Fail(result, label + "에는 customer spawn 외 payload가 함께 들어있습니다.");
                    }

                    if (payload.customerSpawn == null || payload.customerSpawn.customerDesignIndex < 0)
                    {
                        Fail(result, label + ".payload.customerSpawn.customerDesignIndex는 0 이상이어야 합니다.");
                        return;
                    }

                    if (!TryResolveGameplayPrefab(payload.customerSpawn.targetId, spawnLookup, catalog, out GameObject customerPrefab) || customerPrefab == null)
                    {
                        Fail(result, label + ".payload.customerSpawn.targetId '" + payload.customerSpawn.targetId + "'를 baked gameplay prefab으로 해석하지 못했습니다.");
                        return;
                    }

                    if (!PortablePrefabMetadataUtility.TryGetMetadata(customerPrefab, out CatalogPrefabMetadata customerMetadata) || !customerMetadata.supportsCustomerFacility)
                        Fail(result, label + ".payload.customerSpawn.targetId '" + payload.customerSpawn.targetId + "'는 customer spawn이 가능한 facility가 아닙니다.");
                    else if (catalog == null || !catalog.IsValidGameplayDesignIndex("customer", payload.customerSpawn.customerDesignIndex))
                        Fail(result, label + ".payload.customerSpawn.customerDesignIndex '" + payload.customerSpawn.customerDesignIndex + "'는 customer catalog design으로 해석되지 않습니다.");
                    break;

                case FlowActionKinds.SELLER_REQUEST:
                    if (!IsEmptyCameraFocusPayload(payload.cameraFocus) ||
                        !IsEmptyArrowGuidePayload(payload.arrowGuide) ||
                        !IsEmptyRevealPayload(payload.reveal) ||
                        !IsEmptyCustomerSpawnPayload(payload.customerSpawn))
                    {
                        Fail(result, label + "에는 seller request 외 payload가 함께 들어있습니다.");
                    }

                    string targetId = payload.sellerRequest != null && payload.sellerRequest.targetId != null ? payload.sellerRequest.targetId.Trim() : string.Empty;
                    string itemKey = ItemRefUtility.ToStableKey(payload.sellerRequest != null ? payload.sellerRequest.item : null);
                    if (string.IsNullOrEmpty(targetId))
                    {
                        Fail(result, label + ".payload.sellerRequest.targetId가 필요합니다.");
                        return;
                    }

                    if (!TryResolveGameplayPrefab(targetId, spawnLookup, catalog, out GameObject sellerPrefab) || sellerPrefab == null)
                    {
                        Fail(result, label + ".payload.sellerRequest.targetId '" + targetId + "'를 baked gameplay prefab으로 해석하지 못했습니다.");
                        return;
                    }

                    if (string.IsNullOrEmpty(itemKey))
                    {
                        Fail(result, label + ".payload.sellerRequest.item은 필수입니다.");
                        return;
                    }

                    if (!ItemRefUtility.IsValid(payload.sellerRequest.item))
                        Fail(result, label + ".payload.sellerRequest.item은 familyId와 variantId가 모두 필요합니다.");

                    if (!acceptedItemKeysByFacilityId.TryGetValue(targetId, out HashSet<string> acceptedItemKeys) || !acceptedItemKeys.Contains(itemKey))
                        Fail(result, label + ".payload.sellerRequest.item '" + itemKey + "'은(는) target seller accepted item에 포함되어야 합니다.");
                    break;

                default:
                    Fail(result, label + ".kind '" + action.kind + "'는 지원되지 않습니다.");
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

        private static Dictionary<string, HashSet<string>> BuildAcceptedItemKeysByFacilityId(FacilityAcceptedItemDefinition[] acceptedItems)
        {
            var values = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            FacilityAcceptedItemDefinition[] safeAcceptedItems = acceptedItems ?? new FacilityAcceptedItemDefinition[0];
            for (int i = 0; i < safeAcceptedItems.Length; i++)
            {
                FacilityAcceptedItemDefinition acceptedItem = safeAcceptedItems[i];
                if (acceptedItem == null || string.IsNullOrWhiteSpace(acceptedItem.facilityId))
                    continue;

                string facilityId = acceptedItem.facilityId.Trim();
                string itemKey = ItemRefUtility.ToStableKey(acceptedItem.item);
                if (string.IsNullOrEmpty(itemKey))
                    continue;

                if (!values.TryGetValue(facilityId, out HashSet<string> itemKeys))
                {
                    itemKeys = new HashSet<string>(StringComparer.Ordinal);
                    values.Add(facilityId, itemKeys);
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

        private static bool IsEmptyCustomerSpawnPayload(CustomerSpawnActionPayload payload)
        {
            return payload == null ||
                (string.IsNullOrWhiteSpace(payload.targetId) &&
                 payload.customerDesignIndex < 0);
        }

        private static bool IsEmptySellerRequestPayload(SellerRequestActionPayload payload)
        {
            return payload == null ||
                (string.IsNullOrWhiteSpace(payload.targetId) &&
                 string.IsNullOrWhiteSpace(ItemRefUtility.ToStableKey(payload.item)));
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

                string itemKey = ItemRefUtility.ToStableKey(itemPrice.item);
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

        private static void ValidateFacilityAcceptedItems(FacilityAcceptedItemDefinition[] definitions, Dictionary<string, CompiledSpawnData> spawnLookup, PlayableObjectCatalog catalog, CompiledPlayablePlanValidationResult result)
        {
            FacilityAcceptedItemDefinition[] safeDefinitions = definitions ?? new FacilityAcceptedItemDefinition[0];
            var seenLaneKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FacilityAcceptedItemDefinition definition = safeDefinitions[i];
                if (definition == null)
                {
                    Fail(result, "facilityAcceptedItems[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.facilityId))
                {
                    Fail(result, "facilityAcceptedItems[" + i + "].facilityId는 필수입니다.");
                    continue;
                }

                string facilityId = definition.facilityId.Trim();
                if (!spawnLookup.ContainsKey(facilityId))
                    Fail(result, "facilityAcceptedItems[" + i + "].facilityId '" + facilityId + "'를 compiled spawns에서 찾지 못했습니다.");

                string itemKey = ItemRefUtility.ToStableKey(definition.item);
                if (string.IsNullOrWhiteSpace(itemKey))
                {
                    Fail(result, "facilityAcceptedItems[" + i + "].item은 필수입니다.");
                    continue;
                }

                if (!ItemRefUtility.IsValid(definition.item))
                    Fail(result, "facilityAcceptedItems[" + i + "].item은 familyId와 variantId가 모두 필요합니다.");

                if (definition.laneIndex < 0)
                {
                    Fail(result, "facilityAcceptedItems[" + i + "].laneIndex는 0 이상이어야 합니다.");
                    continue;
                }

                string laneKey = facilityId + "::" + definition.laneIndex;
                if (!seenLaneKeys.Add(laneKey))
                    Fail(result, "중복된 facilityAcceptedItems lane '" + laneKey + "'입니다.");
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
                    "MissingCustomerPaths: Step 3 layoutSpec.customerPaths[]가 비어 있습니다. customer-facing facility targetId: " +
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

                if (!SupportsCustomerPathAuthoring(prefab))
                    Fail(result, "layoutSpec.customerPaths[" + i + "].targetId '" + targetId + "'는 customer line이 가능한 facility가 아닙니다.");

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
                    Fail(result, "CustomerPathCoverageMismatch: Step 3 customerPaths coverage가 customer-facing facility와 일치하지 않습니다. " + string.Join(" / ", segments));
                }
            }
        }

        private static void ValidateFacilityOutputItems(
            CompiledSpawnData[] spawns,
            FacilityOutputItemDefinition[] definitions,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            FacilityOutputItemDefinition[] safeDefinitions = definitions ?? new FacilityOutputItemDefinition[0];
            var outputItemByFacilityId = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FacilityOutputItemDefinition definition = safeDefinitions[i];
                if (definition == null)
                {
                    Fail(result, "facilityOutputItems[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.facilityId))
                {
                    Fail(result, "facilityOutputItems[" + i + "].facilityId는 필수입니다.");
                    continue;
                }

                string facilityId = definition.facilityId.Trim();
                if (!spawnLookup.ContainsKey(facilityId))
                    Fail(result, "facilityOutputItems[" + i + "].facilityId '" + facilityId + "'를 compiled spawns에서 찾지 못했습니다.");

                string itemKey = ItemRefUtility.ToStableKey(definition.item);
                if (string.IsNullOrWhiteSpace(itemKey))
                {
                    Fail(result, "facilityOutputItems[" + i + "].item은 필수입니다.");
                    continue;
                }

                if (!ItemRefUtility.IsValid(definition.item))
                    Fail(result, "facilityOutputItems[" + i + "].item은 familyId와 variantId가 모두 필요합니다.");

                if (outputItemByFacilityId.TryGetValue(facilityId, out string existingItemKey) &&
                    !string.Equals(existingItemKey, itemKey, StringComparison.Ordinal))
                {
                    Fail(result, "facilityOutputItems[" + i + "]는 facilityId '" + facilityId + "'에 대해 단일 output item만 허용합니다.");
                    continue;
                }

                outputItemByFacilityId[facilityId] = itemKey;
            }

            CompiledSpawnData[] safeSpawns = spawns ?? new CompiledSpawnData[0];
            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData spawn = safeSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.spawnKey))
                    continue;

                if (!string.Equals(spawn.objectId != null ? spawn.objectId.Trim() : string.Empty, "converter", StringComparison.Ordinal))
                    continue;

                string facilityId = spawn.spawnKey.Trim();
                if (!outputItemByFacilityId.ContainsKey(facilityId))
                    Fail(result, "processor spawn '" + facilityId + "'에는 facilityOutputItems entry가 필요합니다.");
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

                if (SupportsCustomerPathAuthoring(prefab))
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

        private static bool SupportsCustomerPathAuthoring(GameObject prefab)
        {
            return prefab != null &&
                   PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata) &&
                   metadata.supportsCustomerFacility;
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
        string sharedSlotId = entry.sharedSlotId != null ? entry.sharedSlotId.Trim() : string.Empty;

        if (!string.IsNullOrEmpty(laneId) && !entry.hasLaneOrder)
        {
            string message = "RelationshipOrderViolation: layoutSpec.placements[" + i + "]는 laneId를 쓰면 laneOrder도 함께 제공해야 합니다.";
            Warn(result, message);
        }
        if (entry.hasLaneOrder && string.IsNullOrEmpty(laneId))
        {
            string message = "RelationshipOrderViolation: layoutSpec.placements[" + i + "]는 laneOrder를 쓰면 laneId도 함께 제공해야 합니다.";
            Warn(result, message);
        }
        if (entry.hasMinGapToNextCells && string.IsNullOrEmpty(laneId))
        {
            string message = "LaneGapInsufficient: layoutSpec.placements[" + i + "]는 minGapToNextCells를 쓰면 laneId도 함께 제공해야 합니다.";
            Warn(result, message);
        }
        if (entry.hasMinGapToNextCells && entry.minGapToNextCells < 0f)
        {
            string message = "LaneGapInsufficient: layoutSpec.placements[" + i + "].minGapToNextCells는 0 이상이어야 합니다.";
            Warn(result, message);
        }
        if (!string.IsNullOrEmpty(sharedSlotId) && string.IsNullOrEmpty(laneId))
        {
            string message = "AmbiguousSharedSlot: layoutSpec.placements[" + i + "]는 sharedSlotId를 쓰면 laneId도 함께 제공해야 합니다.";
            Warn(result, message);
        }

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

        private static void ValidateFacilityOptions(PlayableScenarioFacilityOptionDefinition[] definitions, Dictionary<string, CompiledSpawnData> spawnLookup, CompiledPlayablePlanValidationResult result)
        {
            PlayableScenarioFacilityOptionDefinition[] safeDefinitions = definitions ?? new PlayableScenarioFacilityOptionDefinition[0];
            var seenFacilityIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                PlayableScenarioFacilityOptionDefinition definition = safeDefinitions[i];
                if (definition == null)
                {
                    Fail(result, "facilityOptions[" + i + "]가 null입니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.facilityId))
                {
                    Fail(result, "facilityOptions[" + i + "].facilityId는 필수입니다.");
                    continue;
                }

                string facilityId = definition.facilityId.Trim();
                if (!seenFacilityIds.Add(facilityId))
                    Fail(result, "중복된 facilityOptions facilityId '" + facilityId + "'입니다.");

                if (!spawnLookup.ContainsKey(facilityId))
                    Fail(result, "facilityOptions[" + i + "].facilityId '" + facilityId + "'를 compiled spawns에서 찾지 못했습니다.");

                ValidateFacilityOptionValues(definition.options, "facilityOptions[" + i + "].options", result);

                if (spawnLookup.TryGetValue(facilityId, out CompiledSpawnData spawn) &&
                    spawn != null &&
                    string.Equals(spawn.objectId != null ? spawn.objectId.Trim() : string.Empty, PromptIntentObjectRoles.SELLER, StringComparison.Ordinal))
                {
                    ValidateSellerCustomerRequestRange(definition.options, "facilityOptions[" + i + "].options", result);
                }
            }
        }

        private static void ValidateFacilityOptionValues(PlayableScenarioFacilityOptions options, string label, CompiledPlayablePlanValidationResult result)
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

        private static void ValidateSellerCustomerRequestRange(
            PlayableScenarioFacilityOptions options,
            string label,
            CompiledPlayablePlanValidationResult result)
        {
            if (options.customerReqMin <= 0 || options.customerReqMax <= 0)
            {
                Fail(result, label + ".customerReqMin/Max는 seller에서 둘 다 1 이상이어야 합니다.");
                return;
            }

            if (options.customerReqMin > options.customerReqMax)
                Fail(result, label + ".customerReqMin은 customerReqMax보다 클 수 없습니다.");
        }

private static void ValidateRuntimeOwnedDesignSources(CompiledSpawnData[] spawns, FacilityAcceptedItemDefinition[] facilityAcceptedItems, FacilityOutputItemDefinition[] facilityOutputItems, ItemPriceDefinition[] itemPrices, Dictionary<string, int> objectDesignLookup, PlayableObjectCatalog catalog, CompiledPlayablePlanValidationResult result)
{
    RuntimeOwnedObjectDesignResolution resolution = RuntimeOwnedObjectDesignResolver.Resolve(spawns, facilityAcceptedItems, facilityOutputItems, itemPrices, catalog);
    for (int i = 0; i < resolution.Errors.Count; i++)
        Fail(result, resolution.Errors[i]);

            for (int i = 0; i < resolution.RequiredObjectIds.Count; i++)
            {
                string objectId = resolution.RequiredObjectIds[i];
        if (!objectDesignLookup.ContainsKey(objectId))
            Fail(result, "Compiled plan에는 runtime-owned objectId '" + objectId + "'에 대한 objectDesigns[] entry가 필요합니다.");
    }
}

private static HashSet<string> ValidatePhysicsAreas(
    CompiledPhysicsAreaDefinition[] physicsAreas,
    PlayableObjectCatalog catalog,
    CompiledPlayablePlanValidationResult result)
{
    var objectIds = new HashSet<string>(StringComparer.Ordinal);
    var spawnKeys = new HashSet<string>(StringComparer.Ordinal);
    CompiledPhysicsAreaDefinition[] safeAreas = physicsAreas ?? new CompiledPhysicsAreaDefinition[0];
    for (int i = 0; i < safeAreas.Length; i++)
    {
        CompiledPhysicsAreaDefinition area = safeAreas[i];
        if (area == null)
        {
            Fail(result, "physicsAreas[" + i + "]가 null입니다.");
            continue;
        }

        string objectId = area.objectId != null ? area.objectId.Trim() : string.Empty;
        if (string.IsNullOrEmpty(objectId))
        {
            Fail(result, "physicsAreas[" + i + "].objectId가 필요합니다.");
        }
        else if (!objectIds.Add(objectId))
        {
            Fail(result, "중복된 physicsAreas objectId '" + objectId + "'입니다.");
        }

        string spawnKey = area.spawnKey != null ? area.spawnKey.Trim() : string.Empty;
        if (string.IsNullOrEmpty(spawnKey))
        {
            Fail(result, "physicsAreas[" + i + "].spawnKey가 필요합니다.");
        }
        else if (!spawnKeys.Add(spawnKey))
        {
            Fail(result, "중복된 physicsAreas spawnKey '" + spawnKey + "'입니다.");
        }

        if (area.options == null || !ItemRefUtility.IsValid(area.options.item))
        {
            Fail(result, "physicsAreas[" + i + "].options.item이 필요합니다.");
        }

        if (!TryResolvePhysicsAreaFootprintBounds(area, null, out _, out string error) && !string.IsNullOrEmpty(error))
            Fail(result, error);
    }

    return objectIds;
}

private static void ValidateRails(
    CompiledRailDefinition[] rails,
    Dictionary<string, CompiledSpawnData> spawnLookup,
    HashSet<string> physicsAreaObjectIds,
    PlayableObjectCatalog catalog,
    CompiledPlayablePlanValidationResult result)
{
    var seenObjectIds = new HashSet<string>(StringComparer.Ordinal);
    var seenSpawnKeys = new HashSet<string>(StringComparer.Ordinal);
    CompiledRailDefinition[] safeRails = rails ?? new CompiledRailDefinition[0];
    for (int i = 0; i < safeRails.Length; i++)
    {
        CompiledRailDefinition rail = safeRails[i];
        if (rail == null)
        {
            Fail(result, "rails[" + i + "]가 null입니다.");
            continue;
        }

        string objectId = rail.objectId != null ? rail.objectId.Trim() : string.Empty;
        if (string.IsNullOrEmpty(objectId))
        {
            Fail(result, "rails[" + i + "].objectId가 필요합니다.");
        }
        else if (!seenObjectIds.Add(objectId))
        {
            Fail(result, "중복된 rails objectId '" + objectId + "'입니다.");
        }

        string spawnKey = rail.spawnKey != null ? rail.spawnKey.Trim() : string.Empty;
        if (string.IsNullOrEmpty(spawnKey))
        {
            Fail(result, "rails[" + i + "].spawnKey가 필요합니다.");
        }
        else if (!seenSpawnKeys.Add(spawnKey))
        {
            Fail(result, "중복된 rails spawnKey '" + spawnKey + "'입니다.");
        }
        else if (!spawnLookup.ContainsKey(spawnKey))
        {
            Fail(result, "rails[" + i + "].spawnKey '" + spawnKey + "'에 대응하는 compiled spawn을 찾지 못했습니다.");
        }

        if (rail.options == null)
        {
            Fail(result, "rails[" + i + "].options가 필요합니다.");
        }
        else
        {
            if (!ItemRefUtility.IsValid(rail.options.item))
            {
                Fail(result, "rails[" + i + "].options.item이 필요합니다.");
            }

            if (rail.options.spawnIntervalSeconds <= 0f)
                Fail(result, "rails[" + i + "].options.spawnIntervalSeconds는 0보다 커야 합니다.");
            if (rail.options.travelDurationSeconds <= 0f)
                Fail(result, "rails[" + i + "].options.travelDurationSeconds는 0보다 커야 합니다.");

            string sinkEndpointTargetObjectId = rail.options.sinkEndpointTargetObjectId != null ? rail.options.sinkEndpointTargetObjectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(sinkEndpointTargetObjectId))
            {
                Fail(result, "rails[" + i + "].options.sinkEndpointTargetObjectId가 필요합니다.");
            }
            else if (!IsSupportedRailSinkTargetObjectId(sinkEndpointTargetObjectId, spawnLookup))
            {
                Fail(result, "rails[" + i + "].options.sinkEndpointTargetObjectId '" + sinkEndpointTargetObjectId + "'에 대응하는 processor 또는 seller를 찾지 못했습니다.");
            }
        }

        if (rail.layout == null || rail.layout.pathCells == null || rail.layout.pathCells.Length == 0)
            Fail(result, "rails[" + i + "].layout.pathCells가 필요합니다.");
    }
}

private static void ValidateGameplaySpawnFootprintOverlaps(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    UnlockDefinition[] unlocks,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    CompiledPlayablePlanValidationResult result)
{
    if (!TryCollectGameplayFootprintBounds(spawns, physicsAreas, rails, unlocks, catalog, layoutSpec, out List<GameplaySpawnFootprintBounds> boundsList, out string error))
    {
        if (!string.IsNullOrEmpty(error))
            Fail(result, error);
        return;
    }

    if (boundsList.Count <= 1)
        return;

            for (int i = 0; i < boundsList.Count; i++)
            {
                GameplaySpawnFootprintBounds left = boundsList[i];
                for (int j = i + 1; j < boundsList.Count; j++)
                {
                    GameplaySpawnFootprintBounds right = boundsList[j];
                    if (IsDeclaredSharedSlotPair(left, right))
                    {
                        continue;
                    }

                    if (!AreWorldBoundsOverlapping(
                            left.MinX,
                            left.MaxX,
                            left.MinZ,
                            left.MaxZ,
                            right.MinX,
                            right.MaxX,
                            right.MinZ,
                            right.MaxZ))
                    {
                        continue;
                    }

                    Warn(
                        result,
                        "gameplay spawn footprint가 겹칩니다: '" + left.SpawnKey + "'(objectId='" + left.ObjectId + "') vs '" +
                        right.SpawnKey + "'(objectId='" + right.ObjectId + "').");
                }
            }
        }

private static void ValidateDeclaredLaneRelationships(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    UnlockDefinition[] unlocks,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    CompiledPlayablePlanValidationResult result)
{
    if (layoutSpec == null)
        return;

    if (!TryCollectGameplayFootprintBounds(spawns, physicsAreas, rails, unlocks, catalog, layoutSpec, out List<GameplaySpawnFootprintBounds> boundsList, out string error))
    {
        if (!string.IsNullOrEmpty(error))
            Fail(result, error);
        return;
    }

    var lanes = new Dictionary<string, List<GameplaySpawnFootprintBounds>>(StringComparer.Ordinal);
    for (int i = 0; i < boundsList.Count; i++)
    {
        GameplaySpawnFootprintBounds bounds = boundsList[i];
        string laneId = bounds != null && bounds.LaneId != null ? bounds.LaneId.Trim() : string.Empty;
        if (string.IsNullOrEmpty(laneId) || !bounds.HasLaneOrder)
            continue;

        if (!lanes.TryGetValue(laneId, out List<GameplaySpawnFootprintBounds> laneBounds))
        {
            laneBounds = new List<GameplaySpawnFootprintBounds>();
            lanes.Add(laneId, laneBounds);
        }

        laneBounds.Add(bounds);
    }

    foreach (KeyValuePair<string, List<GameplaySpawnFootprintBounds>> pair in lanes)
    {
        string laneId = pair.Key;
        List<GameplaySpawnFootprintBounds> laneBounds = pair.Value ?? new List<GameplaySpawnFootprintBounds>();
        laneBounds.Sort(
            delegate (GameplaySpawnFootprintBounds left, GameplaySpawnFootprintBounds right)
            {
                int orderCompare = left.LaneOrder.CompareTo(right.LaneOrder);
                if (orderCompare != 0)
                    return orderCompare;
                return string.CompareOrdinal(left.ObjectId, right.ObjectId);
            });

        for (int i = 0; i < laneBounds.Count;)
        {
            int laneOrder = laneBounds[i].LaneOrder;
            var sameOrderGroup = new List<GameplaySpawnFootprintBounds>();
            int cursor = i;
            while (cursor < laneBounds.Count && laneBounds[cursor].LaneOrder == laneOrder)
            {
                sameOrderGroup.Add(laneBounds[cursor]);
                cursor++;
            }

            if (sameOrderGroup.Count > 1 && !TryResolveSharedSlotGroupId(sameOrderGroup, out _))
            {
                Warn(result, "AmbiguousSharedSlot: laneId '" + laneId + "'의 laneOrder " + laneOrder + "에 동일 슬롯으로 해석할 sharedSlotId가 일치하지 않는 placement가 있습니다.");
                return;
            }

            if (cursor < laneBounds.Count)
            {
                int nextLaneOrder = laneBounds[cursor].LaneOrder;
                var nextOrderGroup = new List<GameplaySpawnFootprintBounds>();
                int nextCursor = cursor;
                while (nextCursor < laneBounds.Count && laneBounds[nextCursor].LaneOrder == nextLaneOrder)
                {
                    nextOrderGroup.Add(laneBounds[nextCursor]);
                    nextCursor++;
                }

                float upperAverageWorldZ = ComputeAverageWorldZ(sameOrderGroup);
                float lowerAverageWorldZ = ComputeAverageWorldZ(nextOrderGroup);
                if (upperAverageWorldZ <= lowerAverageWorldZ + 0.0001f)
                {
                    Warn(result, "RelationshipOrderViolation: laneId '" + laneId + "'에서 laneOrder " + laneOrder + " group이 laneOrder " + nextLaneOrder + " group보다 뒤/위쪽(worldZ가 더 커야 함)에 있지 않습니다.");
                    return;
                }

                float actualGap = ComputeLaneGroupGap(sameOrderGroup, nextOrderGroup);
                float requiredGap = ResolveDeclaredLaneRequiredGap(sameOrderGroup, nextOrderGroup);
                if (actualGap <= 0.0001f)
                {
                    Warn(result, "AmbiguousSharedSlot: laneId '" + laneId + "'에서 '" + ResolveRepresentativePlacementId(sameOrderGroup) + "'와 '" + ResolveRepresentativePlacementId(nextOrderGroup) + "'가 same-lane zero-gap인데 sharedSlotId가 없습니다.");
                    return;
                }

                if (requiredGap > 0f && actualGap + 0.0001f < requiredGap)
                {
                    Warn(result, "LaneGapInsufficient: laneId '" + laneId + "'에서 '" + ResolveRepresentativePlacementId(sameOrderGroup) + "' -> '" + ResolveRepresentativePlacementId(nextOrderGroup) + "' gap이 부족합니다. actualGap=" + actualGap + ", requiredGap=" + requiredGap + ".");
                    return;
                }
            }

            i = cursor;
        }
    }
}

private static void ValidateGameplaySpawnLayoutContainment(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    UnlockDefinition[] unlocks,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    CompiledPlayablePlanValidationResult result)
{
    if (!TryResolveExplicitLayoutWorldBounds(layoutSpec, catalog, out float minWorldX, out float maxWorldX, out float minWorldZ, out float maxWorldZ))
        return;

    if (!TryCollectGameplayFootprintBounds(spawns, physicsAreas, rails, unlocks, catalog, layoutSpec, out List<GameplaySpawnFootprintBounds> boundsList, out string error))
    {
        if (!string.IsNullOrEmpty(error))
            Fail(result, error);
        return;
    }

    const float EPSILON = 0.0001f;
    for (int i = 0; i < boundsList.Count; i++)
    {
        GameplaySpawnFootprintBounds bounds = boundsList[i];

        if (bounds.MinX < minWorldX - EPSILON ||
            bounds.MaxX > maxWorldX + EPSILON ||
                    bounds.MinZ < minWorldZ - EPSILON ||
                    bounds.MaxZ > maxWorldZ + EPSILON)
                {
                    Warn(
                        result,
                        "gameplay spawn footprint가 layout 경계를 벗어났습니다: '" + bounds.SpawnKey + "'(objectId='" + bounds.ObjectId + "'). " +
                        "spawnBounds=(" + bounds.MinX + ", " + bounds.MaxX + ", " + bounds.MinZ + ", " + bounds.MaxZ + "), " +
                        "layoutBounds=(" + minWorldX + ", " + maxWorldX + ", " + minWorldZ + ", " + maxWorldZ + ").");
                    return;
                }
            }
        }

private static void ValidateEnvironmentOccupiedCellConflicts(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    UnlockDefinition[] unlocks,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    CompiledPlayablePlanValidationResult result)
{
    LayoutSpecEnvironmentEntry[] entries = layoutSpec != null ? layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0] : new LayoutSpecEnvironmentEntry[0];
    if (catalog == null || entries.Length == 0)
        return;

    if (!TryCollectGameplayFootprintBounds(spawns, physicsAreas, rails, unlocks, catalog, layoutSpec, out List<GameplaySpawnFootprintBounds> gameplayBounds, out _))
        return;

            for (int i = 0; i < entries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = entries[i];
                if (entry == null)
                    continue;

                if (!TryResolveEnvironmentOccupiedCellSet(catalog, entry, i, out EnvironmentOccupiedCellSet occupiedCellSet, out string error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Fail(result, error);
                        return;
                    }

                    continue;
                }

                if (occupiedCellSet == null || occupiedCellSet.OccupiedCells == null || occupiedCellSet.OccupiedCells.Count == 0)
                    continue;

                if (IsPerimeterEnvironmentPlacement(catalog, entry))
                    TrimEnvironmentOccupiedCellsAgainstGameplayBounds(occupiedCellSet.OccupiedCells, occupiedCellSet.TileStep, gameplayBounds);

                if (!TryValidateGameplaySpawnsAgainstEnvironmentOccupiedCells(gameplayBounds, occupiedCellSet, out error))
                {
                    Warn(result, error + " HTML에서 검토된 geometry는 bake 시 그대로 유지합니다.");
                }
            }
        }

        private static void ValidateOuterRoadFloorClearance(
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            LayoutSpecFloorBounds floorBounds = layoutSpec != null ? layoutSpec.floorBounds : null;
            if (floorBounds == null || !floorBounds.hasWorldBounds)
                return;

            float floorMinX = floorBounds.worldX - floorBounds.worldWidth * 0.5f;
            float floorMaxX = floorBounds.worldX + floorBounds.worldWidth * 0.5f;
            float floorMinZ = floorBounds.worldZ - floorBounds.worldDepth * 0.5f;
            float floorMaxZ = floorBounds.worldZ + floorBounds.worldDepth * 0.5f;
            LayoutSpecEnvironmentEntry[] environmentEntries = layoutSpec != null
                ? layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0]
                : new LayoutSpecEnvironmentEntry[0];

            for (int i = 0; i < environmentEntries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = environmentEntries[i];
                if (entry == null || !entry.hasWorldBounds)
                    continue;

                string objectId = entry.objectId != null ? entry.objectId.Trim() : string.Empty;
                if (!IsRoadEnvironmentEntry(entry, catalog))
                    continue;

                float rectMinX = entry.worldX - entry.worldWidth * 0.5f;
                float rectMaxX = entry.worldX + entry.worldWidth * 0.5f;
                float rectMinZ = entry.worldZ - entry.worldDepth * 0.5f;
                float rectMaxZ = entry.worldZ + entry.worldDepth * 0.5f;
                if (!TryResolveOuterRoadFloorClearance(
                        rectMinX,
                        rectMaxX,
                        rectMinZ,
                        rectMaxZ,
                        floorMinX,
                        floorMaxX,
                        floorMinZ,
                        floorMaxZ,
                        out string sideLabel,
                        out float clearance))
                {
                    continue;
                }

                if (clearance >= AuthoringLayoutRules.OUTER_ROAD_MIN_FLOOR_CLEARANCE_CELLS - 0.0001f)
                    continue;

                if (clearance < 0f)
                {
                    Warn(
                        result,
                        "layout_spec.environment[" + i + "] road가 floor envelope와 겹칩니다. " +
                        "road는 floor 바깥에 있고 최소 1 cell 이상 떨어져 있어야 합니다. " +
                        "(objectId='" + objectId + "').");
                    continue;
                }

                Warn(
                    result,
                    "layout_spec.environment[" + i + "] road의 " + sideLabel + " clearance가 부족합니다. " +
                    "road inner edge는 floor envelope와 최소 1 cell 이상 떨어져 있어야 합니다. " +
                    "(objectId='" + objectId + "', clearance=" + clearance.ToString("0.###") + ").");
            }
        }

        private static bool TryResolveOuterRoadFloorClearance(
            float rectMinX,
            float rectMaxX,
            float rectMinZ,
            float rectMaxZ,
            float floorMinX,
            float floorMaxX,
            float floorMinZ,
            float floorMaxZ,
            out string sideLabel,
            out float clearance)
        {
            sideLabel = string.Empty;
            clearance = 0f;

            float overlapX = Math.Min(rectMaxX, floorMaxX) - Math.Max(rectMinX, floorMinX);
            float overlapZ = Math.Min(rectMaxZ, floorMaxZ) - Math.Max(rectMinZ, floorMinZ);
            if (overlapX > 0.0001f && overlapZ > 0.0001f)
            {
                sideLabel = "interior";
                clearance = -Math.Min(overlapX, overlapZ);
                return true;
            }

            if (rectMaxX <= floorMinX + 0.0001f)
            {
                sideLabel = "left";
                clearance = floorMinX - rectMaxX;
                return true;
            }

            if (rectMinX >= floorMaxX - 0.0001f)
            {
                sideLabel = "right";
                clearance = rectMinX - floorMaxX;
                return true;
            }

            if (rectMaxZ <= floorMinZ + 0.0001f)
            {
                sideLabel = "bottom";
                clearance = floorMinZ - rectMaxZ;
                return true;
            }

            if (rectMinZ >= floorMaxZ - 0.0001f)
            {
                sideLabel = "top";
                clearance = rectMinZ - floorMaxZ;
                return true;
            }

            return false;
        }

        private static bool TryResolveExplicitLayoutWorldBounds(
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            out float minWorldX,
            out float maxWorldX,
            out float minWorldZ,
            out float maxWorldZ)
        {
            minWorldX = 0f;
            maxWorldX = 0f;
            minWorldZ = 0f;
            maxWorldZ = 0f;

            LayoutSpecFloorBounds floorBounds = layoutSpec != null ? layoutSpec.floorBounds : null;
            if (floorBounds != null && floorBounds.hasWorldBounds)
            {
                float worldWidth = floorBounds.worldWidth > 0f ? floorBounds.worldWidth : IntentAuthoringUtility.LAYOUT_SPACING;
                float worldDepth = floorBounds.worldDepth > 0f ? floorBounds.worldDepth : IntentAuthoringUtility.LAYOUT_SPACING;
                minWorldX = floorBounds.worldX - worldWidth * 0.5f;
                maxWorldX = floorBounds.worldX + worldWidth * 0.5f;
                minWorldZ = floorBounds.worldZ - worldDepth * 0.5f;
                maxWorldZ = floorBounds.worldZ + worldDepth * 0.5f;
                return true;
            }

            return TryResolveExplicitEnvironmentWorldBounds(layoutSpec != null ? layoutSpec.environment : null, catalog, out minWorldX, out maxWorldX, out minWorldZ, out maxWorldZ);
        }

        private static bool TryResolveExplicitEnvironmentWorldBounds(
            LayoutSpecEnvironmentEntry[] entries,
            PlayableObjectCatalog catalog,
            out float minWorldX,
            out float maxWorldX,
            out float minWorldZ,
            out float maxWorldZ)
        {
            minWorldX = 0f;
            maxWorldX = 0f;
            minWorldZ = 0f;
            maxWorldZ = 0f;
            LayoutSpecEnvironmentEntry[] safeEntries = entries ?? new LayoutSpecEnvironmentEntry[0];
            var includeCandidates = new List<LayoutSpecEnvironmentEntry>();
            var nonRoadCandidates = new List<LayoutSpecEnvironmentEntry>();
            for (int i = 0; i < safeEntries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = safeEntries[i];
                if (entry == null || !entry.includeInBounds)
                    continue;

                includeCandidates.Add(entry);
                if (!IsRoadEnvironmentEntry(entry, catalog))
                    nonRoadCandidates.Add(entry);
            }

            LayoutSpecEnvironmentEntry[] boundsEntries = nonRoadCandidates.Count > 0
                ? nonRoadCandidates.ToArray()
                : includeCandidates.ToArray();
            if (boundsEntries.Length == 0)
                return false;

            bool hasAny = false;
            for (int i = 0; i < boundsEntries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = boundsEntries[i];
                if (entry == null)
                    continue;

                float worldWidth = entry.worldWidth > 0f ? entry.worldWidth : IntentAuthoringUtility.LAYOUT_SPACING;
                float worldDepth = entry.worldDepth > 0f ? entry.worldDepth : IntentAuthoringUtility.LAYOUT_SPACING;
                float entryMinX = entry.worldX - worldWidth * 0.5f;
                float entryMaxX = entry.worldX + worldWidth * 0.5f;
                float entryMinZ = entry.worldZ - worldDepth * 0.5f;
                float entryMaxZ = entry.worldZ + worldDepth * 0.5f;
                if (!hasAny)
                {
                    minWorldX = entryMinX;
                    maxWorldX = entryMaxX;
                    minWorldZ = entryMinZ;
                    maxWorldZ = entryMaxZ;
                    hasAny = true;
                    continue;
                }

                if (entryMinX < minWorldX)
                    minWorldX = entryMinX;
                if (entryMaxX > maxWorldX)
                    maxWorldX = entryMaxX;
                if (entryMinZ < minWorldZ)
                    minWorldZ = entryMinZ;
                if (entryMaxZ > maxWorldZ)
                    maxWorldZ = entryMaxZ;
            }

            return hasAny;
        }

        private static bool IsRoadEnvironmentEntry(LayoutSpecEnvironmentEntry entry, PlayableObjectCatalog catalog)
        {
            string objectId = entry != null && !string.IsNullOrWhiteSpace(entry.objectId) ? entry.objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(objectId))
                return false;

            if (catalog != null && catalog.TryGetEnvironmentEntry(objectId, out EnvironmentCatalogEntry environmentEntry) && environmentEntry != null)
            {
                return string.Equals(environmentEntry.category != null ? environmentEntry.category.Trim() : string.Empty, EnvironmentCatalog.ROAD_CATEGORY, StringComparison.Ordinal);
            }

            return string.Equals(objectId, "road", StringComparison.Ordinal);
        }

        private static bool IsFloorEnvironmentEntry(LayoutSpecEnvironmentEntry entry, PlayableObjectCatalog catalog)
        {
            string objectId = entry != null && !string.IsNullOrWhiteSpace(entry.objectId) ? entry.objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(objectId))
                return false;

            if (catalog != null && catalog.TryGetEnvironmentEntry(objectId, out EnvironmentCatalogEntry environmentEntry) && environmentEntry != null)
            {
                return string.Equals(environmentEntry.category != null ? environmentEntry.category.Trim() : string.Empty, EnvironmentCatalog.FLOOR_CATEGORY, StringComparison.Ordinal);
            }

            return string.Equals(objectId, "floor", StringComparison.Ordinal);
        }

        private static string ResolveEnvironmentCategory(PlayableObjectCatalog catalog, string objectId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(objectId))
                return string.Empty;

            if (!catalog.TryGetEnvironmentEntry(objectId.Trim(), out EnvironmentCatalogEntry entry) || entry == null)
                return string.Empty;

            return entry.category != null ? entry.category.Trim() : string.Empty;
        }

        private static bool TryResolveEnvironmentOccupiedCellSet(
            PlayableObjectCatalog catalog,
            LayoutSpecEnvironmentEntry entry,
            int entryIndex,
            out EnvironmentOccupiedCellSet occupiedCellSet,
            out string error)
        {
            occupiedCellSet = null;
            error = string.Empty;
            if (catalog == null || entry == null || string.IsNullOrWhiteSpace(entry.objectId))
                return false;

            // Floor is the walkable base and should not reserve occupancy against gameplay spawns.
            if (IsFloorEnvironmentEntry(entry, catalog))
                return false;

            string designId = !string.IsNullOrWhiteSpace(entry.designId) ? entry.designId.Trim() : PlayableObjectCatalogContractValidator.DEFAULT_DESIGN_ID;
            if (!catalog.TryGetEnvironmentDesign(entry.objectId.Trim(), designId, out EnvironmentDesignVariantEntry design, out string placementMode, out string variationMode, out _))
            {
                error = "layout_spec.environment[" + entryIndex + "]의 catalog environment design을 해석하지 못했습니다.";
                return false;
            }

            if (!TryValidateEnvironmentDesignFootprintRules(design, variationMode, out float tileStep, out error))
            {
                error = "layout_spec.environment[" + entryIndex + "] footprint 검증 실패: " + error;
                return false;
            }

            if (!TryValidatePerimeterThicknessRule(placementMode, entry, tileStep, entryIndex, out error))
                return false;

            if (!TryResolveEnvironmentOccupiedCells(placementMode, entry, tileStep, out HashSet<EnvironmentCellCoordinate> occupiedCells))
            {
                error = "layout_spec.environment[" + entryIndex + "] 점유 셀을 계산하지 못했습니다.";
                return false;
            }

            occupiedCellSet = new EnvironmentOccupiedCellSet
            {
                EntryIndex = entryIndex,
                TileStep = tileStep,
                OccupiedCells = occupiedCells,
            };
            return true;
        }

        private static bool TryValidateEnvironmentDesignFootprintRules(
            EnvironmentDesignVariantEntry design,
            string variationMode,
            out float tileStep,
            out string error)
        {
            tileStep = 0f;
            error = string.Empty;
            var uniquePrefabs = new HashSet<GameObject>();
            TryAddEnvironmentPrefab(uniquePrefabs, design != null ? design.prefab : null);
            TryAddEnvironmentPrefab(uniquePrefabs, design != null ? design.straightPrefab : null);
            TryAddEnvironmentPrefab(uniquePrefabs, design != null ? design.cornerPrefab : null);
            TryAddEnvironmentPrefab(uniquePrefabs, design != null ? design.tJunctionPrefab : null);
            TryAddEnvironmentPrefab(uniquePrefabs, design != null ? design.crossPrefab : null);
            if (uniquePrefabs.Count == 0)
            {
                error = "environment prefab이 비어 있습니다.";
                return false;
            }

            int expectedSquareSizeCells = 0;
            foreach (GameObject prefab in uniquePrefabs)
            {
                if (!PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata))
                {
                    error = "environment prefab metadata를 읽지 못했습니다.";
                    return false;
                }

                int widthCells = metadata.placementFootprintWidthCells > 0 ? metadata.placementFootprintWidthCells : 1;
                int depthCells = metadata.placementFootprintDepthCells > 0 ? metadata.placementFootprintDepthCells : 1;
                if (widthCells != depthCells)
                {
                    error = "environment prefab footprint는 정사각형이어야 합니다.";
                    return false;
                }

                if (expectedSquareSizeCells == 0)
                {
                    expectedSquareSizeCells = widthCells;
                    continue;
                }

                if (expectedSquareSizeCells != widthCells)
                {
                    error = "environment prefab 세트의 모든 variant는 동일한 정사각 footprint를 가져야 합니다.";
                    return false;
                }
            }

            if (expectedSquareSizeCells < 1)
            {
                error = "environment footprint size를 해석하지 못했습니다.";
                return false;
            }

            tileStep = expectedSquareSizeCells * IntentAuthoringUtility.LAYOUT_SPACING;
            return true;
        }

        private static void TryAddEnvironmentPrefab(HashSet<GameObject> prefabs, GameObject prefab)
        {
            if (prefabs == null || prefab == null)
                return;

            prefabs.Add(prefab);
        }

        private static bool IsPerimeterEnvironmentPlacement(PlayableObjectCatalog catalog, LayoutSpecEnvironmentEntry entry)
        {
            if (catalog == null || entry == null || string.IsNullOrWhiteSpace(entry.objectId))
                return false;

            return catalog.TryResolveEnvironmentPlacementMode(entry.objectId.Trim(), out string placementMode) &&
                   string.Equals(placementMode, EnvironmentCatalog.PLACEMENT_MODE_PERIMETER, StringComparison.Ordinal);
        }

        private static void TrimEnvironmentOccupiedCellsAgainstGameplayBounds(
            HashSet<EnvironmentCellCoordinate> occupiedCells,
            float tileStep,
            List<GameplaySpawnFootprintBounds> gameplayBounds)
        {
            if (occupiedCells == null || occupiedCells.Count == 0 || gameplayBounds == null || gameplayBounds.Count == 0)
                return;

            float safeTileStep = NormalizeEnvironmentTileStep(tileStep);
            float halfTile = safeTileStep * 0.5f;
            const float EPSILON = 0.0001f;
            var overlappingCells = new List<EnvironmentCellCoordinate>();
            foreach (EnvironmentCellCoordinate cell in occupiedCells)
            {
                Vector3 cellCenter = ResolveEnvironmentCellWorldPosition(cell);
                float envMinX = cellCenter.x - halfTile;
                float envMaxX = cellCenter.x + halfTile;
                float envMinZ = cellCenter.z - halfTile;
                float envMaxZ = cellCenter.z + halfTile;

                for (int i = 0; i < gameplayBounds.Count; i++)
                {
                    GameplaySpawnFootprintBounds spawnBounds = gameplayBounds[i];
                    bool overlaps =
                        spawnBounds.MinX < envMaxX - EPSILON &&
                        spawnBounds.MaxX > envMinX + EPSILON &&
                        spawnBounds.MinZ < envMaxZ - EPSILON &&
                        spawnBounds.MaxZ > envMinZ + EPSILON;
                    if (!overlaps)
                        continue;

                    overlappingCells.Add(cell);
                    break;
                }
            }

            for (int i = 0; i < overlappingCells.Count; i++)
                occupiedCells.Remove(overlappingCells[i]);
        }

        private static bool TryValidateGameplaySpawnsAgainstEnvironmentOccupiedCells(
            List<GameplaySpawnFootprintBounds> gameplayBounds,
            EnvironmentOccupiedCellSet occupiedCellSet,
            out string error)
        {
            error = string.Empty;
            if (gameplayBounds == null || occupiedCellSet == null || occupiedCellSet.OccupiedCells == null)
                return true;

            float tileStep = NormalizeEnvironmentTileStep(occupiedCellSet.TileStep);
            float halfTile = tileStep * 0.5f;
            const float EPSILON = 0.0001f;
            foreach (GameplaySpawnFootprintBounds spawnBounds in gameplayBounds)
            {
                if (spawnBounds == null)
                    continue;

                foreach (EnvironmentCellCoordinate cell in occupiedCellSet.OccupiedCells)
                {
                    Vector3 cellCenter = ResolveEnvironmentCellWorldPosition(cell);
                    float envMinX = cellCenter.x - halfTile;
                    float envMaxX = cellCenter.x + halfTile;
                    float envMinZ = cellCenter.z - halfTile;
                    float envMaxZ = cellCenter.z + halfTile;
                    bool overlaps =
                        spawnBounds.MinX < envMaxX - EPSILON &&
                        spawnBounds.MaxX > envMinX + EPSILON &&
                        spawnBounds.MinZ < envMaxZ - EPSILON &&
                        spawnBounds.MaxZ > envMinZ + EPSILON;
                    if (!overlaps)
                        continue;

                    error =
                        "gameplay spawn footprint가 environment 점유 셀과 충돌합니다: '" + spawnBounds.SpawnKey +
                        "'(objectId='" + spawnBounds.ObjectId + "', environmentEntryIndex=" + occupiedCellSet.EntryIndex + ").";
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveEnvironmentOccupiedCells(
            string placementMode,
            LayoutSpecEnvironmentEntry entry,
            float tileStep,
            out HashSet<EnvironmentCellCoordinate> occupiedCells)
        {
            occupiedCells = new HashSet<EnvironmentCellCoordinate>();
            if (entry == null)
                return false;

            float safeTileStep = NormalizeEnvironmentTileStep(tileStep);
            int coordinateStride = ResolveEnvironmentCoordinateStride(safeTileStep);
            bool useFill = string.Equals(placementMode, EnvironmentCatalog.PLACEMENT_MODE_FILL, StringComparison.Ordinal);
            bool enforceSingleLayer = entry.singleLayer;

            if (entry.hasWorldBounds)
            {
                float worldWidth = entry.worldWidth > 0f ? entry.worldWidth : safeTileStep;
                float worldDepth = entry.worldDepth > 0f ? entry.worldDepth : safeTileStep;
                int widthTileCount = ResolveEnvironmentTileCount(worldWidth, safeTileStep);
                int depthTileCount = ResolveEnvironmentTileCount(worldDepth, safeTileStep);
                int footprintCells = System.Math.Max(1, (int)System.MathF.Round(safeTileStep / IntentAuthoringUtility.LAYOUT_SPACING));
                int firstCoordinateX = ResolveEnvironmentFirstCoordinate(entry.worldX, worldWidth, widthTileCount, safeTileStep, coordinateStride, footprintCells);
                int firstCoordinateZ = ResolveEnvironmentFirstCoordinate(entry.worldZ, worldDepth, depthTileCount, safeTileStep, coordinateStride, footprintCells);

                if (useFill)
                {
                    if (enforceSingleLayer && widthTileCount > 1 && depthTileCount > 1)
                    {
                        if (widthTileCount >= depthTileCount)
                        {
                            int laneCoordinateZ = firstCoordinateZ + ((depthTileCount - 1) / 2) * coordinateStride;
                            for (int x = 0; x < widthTileCount; x++)
                                occupiedCells.Add(new EnvironmentCellCoordinate(firstCoordinateX + x * coordinateStride, laneCoordinateZ));
                        }
                        else
                        {
                            int laneCoordinateX = firstCoordinateX + ((widthTileCount - 1) / 2) * coordinateStride;
                            for (int z = 0; z < depthTileCount; z++)
                                occupiedCells.Add(new EnvironmentCellCoordinate(laneCoordinateX, firstCoordinateZ + z * coordinateStride));
                        }

                        return true;
                    }

                    for (int x = 0; x < widthTileCount; x++)
                    {
                        for (int z = 0; z < depthTileCount; z++)
                            occupiedCells.Add(new EnvironmentCellCoordinate(firstCoordinateX + x * coordinateStride, firstCoordinateZ + z * coordinateStride));
                    }

                    return true;
                }

                int lastCoordinateX = firstCoordinateX + (widthTileCount - 1) * coordinateStride;
                int lastCoordinateZ = firstCoordinateZ + (depthTileCount - 1) * coordinateStride;
                if (widthTileCount == 1 || depthTileCount == 1)
                {
                    for (int x = 0; x < widthTileCount; x++)
                    {
                        for (int z = 0; z < depthTileCount; z++)
                            occupiedCells.Add(new EnvironmentCellCoordinate(firstCoordinateX + x * coordinateStride, firstCoordinateZ + z * coordinateStride));
                    }

                    return true;
                }

                for (int x = 0; x < widthTileCount; x++)
                {
                    int coordinateX = firstCoordinateX + x * coordinateStride;
                    occupiedCells.Add(new EnvironmentCellCoordinate(coordinateX, firstCoordinateZ));
                    occupiedCells.Add(new EnvironmentCellCoordinate(coordinateX, lastCoordinateZ));
                }

                for (int z = 0; z < depthTileCount; z++)
                {
                    int coordinateZ = firstCoordinateZ + z * coordinateStride;
                    occupiedCells.Add(new EnvironmentCellCoordinate(firstCoordinateX, coordinateZ));
                    occupiedCells.Add(new EnvironmentCellCoordinate(lastCoordinateX, coordinateZ));
                }

                return true;
            }

            return false;
        }

        private static bool TryValidatePerimeterThicknessRule(
            string placementMode,
            LayoutSpecEnvironmentEntry entry,
            float tileStep,
            int entryIndex,
            out string error)
        {
            error = string.Empty;
            if (entry == null || !entry.hasWorldBounds)
                return true;

            if (!string.Equals(placementMode, EnvironmentCatalog.PLACEMENT_MODE_PERIMETER, StringComparison.Ordinal))
                return true;

            float safeTileStep = NormalizeEnvironmentTileStep(tileStep);
            float worldWidth = entry.worldWidth > 0f ? entry.worldWidth : safeTileStep;
            float worldDepth = entry.worldDepth > 0f ? entry.worldDepth : safeTileStep;
            if (!TryResolveAlignedTileCount(worldWidth, safeTileStep, out int widthTileCount))
            {
                error =
                    "layout_spec.environment[" + entryIndex + "]의 worldWidth가 footprint tileStep 배수가 아닙니다. " +
                    "(objectId='" + (entry.objectId ?? string.Empty) + "', designId='" + (entry.designId ?? string.Empty) +
                    "', worldWidth=" + worldWidth.ToString("0.###") + ", tileStep=" + safeTileStep.ToString("0.###") + ").";
                return false;
            }

            if (!TryResolveAlignedTileCount(worldDepth, safeTileStep, out int depthTileCount))
            {
                error =
                    "layout_spec.environment[" + entryIndex + "]의 worldDepth가 footprint tileStep 배수가 아닙니다. " +
                    "(objectId='" + (entry.objectId ?? string.Empty) + "', designId='" + (entry.designId ?? string.Empty) +
                    "', worldDepth=" + worldDepth.ToString("0.###") + ", tileStep=" + safeTileStep.ToString("0.###") + ").";
                return false;
            }

            if (Math.Min(widthTileCount, depthTileCount) != 1)
            {
                int footprintCells = Math.Max(1, (int)MathF.Round(safeTileStep / IntentAuthoringUtility.LAYOUT_SPACING));
                error =
                    "layout_spec.environment[" + entryIndex + "]는 perimeter 두께 규칙을 위반했습니다. " +
                    "(objectId='" + (entry.objectId ?? string.Empty) + "', designId='" + (entry.designId ?? string.Empty) +
                    "', footprint=" + footprintCells + "x" + footprintCells +
                    ", tileStep=" + safeTileStep.ToString("0.###") +
                    ", worldWidth=" + worldWidth.ToString("0.###") +
                    ", worldDepth=" + worldDepth.ToString("0.###") +
                    ", tileCount=" + widthTileCount + "x" + depthTileCount + "). " +
                    "perimeter는 짧은 축 두께가 정확히 1 tile이어야 합니다.";
                return false;
            }

            return true;
        }

        private static bool TryResolveAlignedTileCount(float worldSize, float tileStep, out int tileCount)
        {
            tileCount = 0;
            if (worldSize <= 0f || tileStep <= 0f)
                return false;

            float ratio = worldSize / tileStep;
            if (float.IsNaN(ratio) || float.IsInfinity(ratio))
                return false;

            int rounded = Math.Max(1, (int)MathF.Round(ratio));
            float snappedSize = rounded * tileStep;
            float tolerance = Math.Max(ENVIRONMENT_TILE_ALIGNMENT_MIN_TOLERANCE, tileStep * ENVIRONMENT_TILE_ALIGNMENT_TOLERANCE_RATIO);
            if (MathF.Abs(snappedSize - worldSize) > tolerance)
                return false;

            tileCount = rounded;
            return true;
        }

        private static float NormalizeEnvironmentTileStep(float tileStep)
        {
            return tileStep > 0f ? tileStep : ENVIRONMENT_TILE_STEP_FLOOR;
        }

        private static int ResolveEnvironmentCoordinateStride(float tileStep)
        {
            float safeStep = NormalizeEnvironmentTileStep(tileStep);
            return System.Math.Max(1, (int)System.MathF.Round(safeStep / ENVIRONMENT_COORD_UNIT));
        }

        private static Vector3 ResolveEnvironmentCellWorldPosition(EnvironmentCellCoordinate coordinate)
        {
            return new Vector3(coordinate.X * ENVIRONMENT_COORD_UNIT, 0f, coordinate.Y * ENVIRONMENT_COORD_UNIT);
        }

        private static int ResolveEnvironmentTileCount(float worldSize, float tileStep)
        {
            float safeStep = NormalizeEnvironmentTileStep(tileStep);
            int floorCount = System.Math.Max(1, (int)System.MathF.Floor((worldSize / safeStep) + 0.0001f));
            int ceilCount = System.Math.Max(1, (int)System.MathF.Ceiling((worldSize / safeStep) - 0.0001f));
            float floorError = System.MathF.Abs(floorCount * safeStep - worldSize);
            float ceilError = System.MathF.Abs(ceilCount * safeStep - worldSize);
            return floorError <= ceilError ? floorCount : ceilCount;
        }

        private static int ResolveEnvironmentFirstCoordinate(
            float worldCenter,
            float worldSize,
            int tileCount,
            float tileStep,
            int coordinateStride,
            int footprintCells)
        {
            int safeTileCount = tileCount > 0 ? tileCount : 1;
            if ((footprintCells & 1) == 0)
            {
                int snappedCenterCoordinate = QuantizeEnvironmentIntegerCenterCoordinate(worldCenter);
                return snappedCenterCoordinate - ((safeTileCount - 1) * coordinateStride) / 2;
            }

            float firstTileCenterWorld = worldCenter - worldSize * 0.5f + NormalizeEnvironmentTileStep(tileStep) * 0.5f;
            return QuantizeEnvironmentCoordinate(firstTileCenterWorld);
        }

        private static int QuantizeEnvironmentCoordinate(float worldCenter)
        {
            return (int)System.MathF.Round(worldCenter / ENVIRONMENT_COORD_UNIT);
        }

        private static int QuantizeEnvironmentIntegerCenterCoordinate(float worldCenter)
        {
            return (int)System.MathF.Round(worldCenter / IntentAuthoringUtility.LAYOUT_SPACING) * 2;
        }

private static bool TryResolveGameplaySpawnFootprintBounds(
    CompiledSpawnData spawn,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    Dictionary<string, LayoutSpecPlacementEntry> placementLookup,
    out GameplaySpawnFootprintBounds bounds,
            out string error)
        {
            bounds = null;
            error = string.Empty;
            if (spawn == null || catalog == null || string.IsNullOrWhiteSpace(spawn.objectId))
                return false;

            if (!TryResolveSpawnFootprintFromCatalog(catalog, spawn, out int widthCells, out int depthCells))
            {
                error = "compiled spawn '" + ResolveSpawnLabel(spawn) + "'의 gameplay footprint metadata를 해석하지 못했습니다.";
                return false;
            }

            if (ShouldSwapGameplaySpawnFootprintAxes(catalog, layoutSpec, spawn, widthCells, depthCells))
            {
                int temp = widthCells;
                widthCells = depthCells;
                depthCells = temp;
            }

            SerializableVector3 position = spawn.localPosition;
            float halfWidth = widthCells * IntentAuthoringUtility.LAYOUT_SPACING * 0.5f;
            float halfDepth = depthCells * IntentAuthoringUtility.LAYOUT_SPACING * 0.5f;
            bounds = new GameplaySpawnFootprintBounds
            {
                ReferenceId = ResolveSpawnLabel(spawn),
                SceneObjectId = ResolveSceneObjectIdFromSpawnKey(spawn),
                SpawnKey = ResolveSpawnLabel(spawn),
                ObjectId = spawn.objectId != null ? spawn.objectId.Trim() : string.Empty,
                Role = GameplayOverlapAllowanceRules.ResolveCompiledGameplayRole(spawn.objectId),
                CenterX = position.x,
                CenterZ = position.z,
                HasResolvedYaw = spawn.hasResolvedYaw,
                ResolvedYawDegrees = spawn.resolvedYawDegrees,
                MinX = position.x - halfWidth,
                MaxX = position.x + halfWidth,
                MinZ = position.z - halfDepth,
                MaxZ = position.z + halfDepth,
    };
    ApplyPlacementSemantics(
        bounds,
        ResolveLayoutPlacementEntry(
            placementLookup,
            ResolveSpawnLabel(spawn),
            ResolveSceneObjectIdFromSpawnKey(spawn)));
    return true;
}

private static bool TryResolveRailFootprintBounds(
    CompiledRailDefinition rail,
    Dictionary<string, LayoutSpecPlacementEntry> placementLookup,
    out GameplaySpawnFootprintBounds bounds,
    out string error)
{
    bounds = null;
    error = string.Empty;
    if (rail == null)
        return false;

    string objectId = rail.objectId != null ? rail.objectId.Trim() : string.Empty;
    string spawnKey = rail.spawnKey != null ? rail.spawnKey.Trim() : string.Empty;
    if (string.IsNullOrEmpty(objectId))
    {
        error = "rail objectId가 비어 있습니다.";
        return false;
    }

    if (!TryResolveRailLayoutBounds(rail.layout, out WorldBoundsDefinition trackBounds, out string trackBoundsError))
    {
        error = "rail '" + (string.IsNullOrEmpty(spawnKey) ? objectId : spawnKey) + "'의 layout.pathCells가 유효하지 않습니다. " + trackBoundsError;
        return false;
    }

    float halfWidth = trackBounds.worldWidth * 0.5f;
    float halfDepth = trackBounds.worldDepth * 0.5f;
    string referenceId = string.IsNullOrEmpty(spawnKey) ? objectId : spawnKey;
    bounds = new GameplaySpawnFootprintBounds
    {
        ReferenceId = referenceId,
        SceneObjectId = objectId,
        SpawnKey = referenceId,
        ObjectId = objectId,
        Role = PromptIntentObjectRoles.RAIL,
        CenterX = trackBounds.worldX,
        CenterZ = trackBounds.worldZ,
        SinkEndpointTargetObjectId = rail.options != null && rail.options.sinkEndpointTargetObjectId != null
            ? rail.options.sinkEndpointTargetObjectId.Trim()
            : string.Empty,
        MinX = trackBounds.worldX - halfWidth,
        MaxX = trackBounds.worldX + halfWidth,
        MinZ = trackBounds.worldZ - halfDepth,
        MaxZ = trackBounds.worldZ + halfDepth,
    };
    ApplyPlacementSemantics(bounds, ResolveLayoutPlacementEntry(placementLookup, referenceId, objectId));
    return true;
}

private static bool TryResolvePhysicsAreaFootprintBounds(
    CompiledPhysicsAreaDefinition physicsArea,
    Dictionary<string, LayoutSpecPlacementEntry> placementLookup,
    out GameplaySpawnFootprintBounds bounds,
    out string error)
{
    bounds = null;
    error = string.Empty;
    if (physicsArea == null)
        return false;

    string objectId = physicsArea.objectId != null ? physicsArea.objectId.Trim() : string.Empty;
    string spawnKey = physicsArea.spawnKey != null ? physicsArea.spawnKey.Trim() : string.Empty;
    if (string.IsNullOrEmpty(objectId))
    {
        error = "physics_area objectId가 비어 있습니다.";
        return false;
    }

    if (!TryResolvePhysicsAreaUnionBounds(
            physicsArea.layout,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ,
            out error))
    {
        return false;
    }

    bounds = new GameplaySpawnFootprintBounds
    {
        ReferenceId = string.IsNullOrEmpty(spawnKey) ? objectId : spawnKey,
        SceneObjectId = objectId,
        SpawnKey = string.IsNullOrEmpty(spawnKey) ? objectId : spawnKey,
        ObjectId = objectId,
        Role = PromptIntentObjectRoles.PHYSICS_AREA,
        CenterX = (minX + maxX) * 0.5f,
        CenterZ = (minZ + maxZ) * 0.5f,
        MinX = minX,
        MaxX = maxX,
        MinZ = minZ,
        MaxZ = maxZ,
    };
    ApplyPlacementSemantics(
        bounds,
        ResolveLayoutPlacementEntry(
            placementLookup,
            string.IsNullOrEmpty(spawnKey) ? objectId : spawnKey,
            objectId));
    return true;
}

private static bool TryResolvePhysicsAreaUnionBounds(
    PhysicsAreaLayoutDefinition layout,
    out float minX,
    out float maxX,
    out float minZ,
    out float maxZ,
    out string error)
{
    minX = 0f;
    maxX = 0f;
    minZ = 0f;
    maxZ = 0f;
    error = string.Empty;
    if (layout == null || !HasWorldBounds(layout.realPhysicsZoneBounds) || !HasWorldBounds(layout.fakeSpriteZoneBounds))
    {
        error = "physics_area layout에는 real/fake zone world bounds가 모두 필요합니다.";
        return false;
    }

    minX = Math.Min(
        layout.realPhysicsZoneBounds.worldX - layout.realPhysicsZoneBounds.worldWidth * 0.5f,
        layout.fakeSpriteZoneBounds.worldX - layout.fakeSpriteZoneBounds.worldWidth * 0.5f);
    maxX = Math.Max(
        layout.realPhysicsZoneBounds.worldX + layout.realPhysicsZoneBounds.worldWidth * 0.5f,
        layout.fakeSpriteZoneBounds.worldX + layout.fakeSpriteZoneBounds.worldWidth * 0.5f);
    minZ = Math.Min(
        layout.realPhysicsZoneBounds.worldZ - layout.realPhysicsZoneBounds.worldDepth * 0.5f,
        layout.fakeSpriteZoneBounds.worldZ - layout.fakeSpriteZoneBounds.worldDepth * 0.5f);
    maxZ = Math.Max(
        layout.realPhysicsZoneBounds.worldZ + layout.realPhysicsZoneBounds.worldDepth * 0.5f,
        layout.fakeSpriteZoneBounds.worldZ + layout.fakeSpriteZoneBounds.worldDepth * 0.5f);
    return true;
}

private static bool HasWorldBounds(WorldBoundsDefinition value)
{
    return value != null &&
           value.hasWorldBounds &&
           value.worldWidth > 0f &&
           value.worldDepth > 0f;
}

private static bool UsesFloorBoundaryBackFacingRule(string objectId)
{
    return AuthoringLayoutRules.UsesFloorBoundaryInwardFacingRuleForObjectId(objectId);
}

        private static bool ShouldSwapGameplaySpawnFootprintAxes(
            PlayableObjectCatalog catalog,
            LayoutSpecDocument layoutSpec,
            CompiledSpawnData spawn,
            int widthCells,
            int depthCells)
        {
            if (catalog == null || spawn == null || string.IsNullOrWhiteSpace(spawn.objectId))
                return false;

            if (UsesFloorBoundaryBackFacingRule(spawn.objectId) &&
                TryResolveExplicitLayoutWorldBounds(layoutSpec, catalog, out float minWorldX, out float maxWorldX, out float minWorldZ, out float maxWorldZ))
            {
                SerializableVector3 position = spawn.localPosition;
                float yaw = ResolveFloorBoundaryBackFacingYaw(position.x, position.z, minWorldX, maxWorldX, minWorldZ, maxWorldZ);
                return IntentAuthoringUtility.IsQuarterTurnOddYaw(yaw);
            }

            if (spawn.hasResolvedYaw)
                return IntentAuthoringUtility.IsQuarterTurnOddYaw(spawn.resolvedYawDegrees);

            if (!catalog.TryGetGameplayEntry(spawn.objectId.Trim(), out GameplayCatalogEntry entry) || entry == null)
                return false;

            string category = entry.category != null ? entry.category.Trim() : string.Empty;
            if (string.Equals(category, GameplayCatalog.UNLOCKER_CATEGORY, StringComparison.Ordinal))
                return IntentAuthoringUtility.IsQuarterTurnOddYaw(0f);
            return false;
        }

        private static float ResolveFloorBoundaryBackFacingYaw(
            float positionX,
            float positionZ,
            float minWorldX,
            float maxWorldX,
            float minWorldZ,
            float maxWorldZ)
        {
            return AuthoringLayoutRules.ResolveFloorBoundaryInwardFacingYaw(
                positionX,
                positionZ,
                minWorldX,
                maxWorldX,
                minWorldZ,
                maxWorldZ);
        }

        private static void ValidateImageLayoutEnvironmentPresence(
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            CompiledPlayablePlanValidationResult result)
        {
            if (layoutSpec == null)
                return;

            LayoutSpecPlacementEntry[] placements = layoutSpec.placements ?? new LayoutSpecPlacementEntry[0];
            bool hasImagePlacement = false;
            for (int i = 0; i < placements.Length; i++)
            {
                LayoutSpecPlacementEntry placement = placements[i];
                if (placement != null && placement.hasImageBounds)
                {
                    hasImagePlacement = true;
                    break;
                }
            }

            if (!hasImagePlacement)
                return;

            LayoutSpecEnvironmentEntry[] environmentEntries = layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0];
            if (environmentEntries.Length == 0)
            {
                Fail(result, "이미지 기반 Step 3에서는 environment[]를 비울 수 없습니다.");
                return;
            }

            if (!HasRequiredImageLayoutEnvironmentStructure(layoutSpec, catalog))
                Fail(result, "이미지 기반 Step 3에서는 floorBounds 또는 floor entry와 함께 wall/fence/road 경계가 필요합니다.");
        }

private static void ValidateImageLayoutPadding(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    CompiledPlayablePlanValidationResult result)
{
            if (!HasAnyImagePlacement(layoutSpec) ||
                catalog == null ||
                !TryResolveExplicitLayoutWorldBounds(layoutSpec, catalog, out float layoutMinX, out float layoutMaxX, out float layoutMinZ, out float layoutMaxZ))
            {
                return;
            }

    if (!TryResolveRequiredGameplayBounds(
            spawns,
            physicsAreas,
            rails,
            null,
            catalog,
            layoutSpec,
            out float gameplayMinX,
                    out float gameplayMaxX,
                    out float gameplayMinZ,
                    out float gameplayMaxZ,
                    out string error))
            {
                if (!string.IsNullOrEmpty(error))
                    Fail(result, error);
                return;
            }

            float leftPadding = gameplayMinX - layoutMinX;
            float rightPadding = layoutMaxX - gameplayMaxX;
            float bottomPadding = gameplayMinZ - layoutMinZ;
            float topPadding = layoutMaxZ - gameplayMaxZ;
            const float EPSILON = 0.0001f;

            if (leftPadding <= MAX_IMAGE_LAYOUT_PADDING_PER_SIDE + EPSILON &&
                rightPadding <= MAX_IMAGE_LAYOUT_PADDING_PER_SIDE + EPSILON &&
                bottomPadding <= MAX_IMAGE_LAYOUT_PADDING_PER_SIDE + EPSILON &&
                topPadding <= MAX_IMAGE_LAYOUT_PADDING_PER_SIDE + EPSILON)
            {
                return;
            }

            Fail(
                result,
                "이미지 기반 Step 3 layout bounds 여유가 과도합니다. " +
                "gameplayBounds=(" + gameplayMinX + ", " + gameplayMaxX + ", " + gameplayMinZ + ", " + gameplayMaxZ + "), " +
                "layoutBounds=(" + layoutMinX + ", " + layoutMaxX + ", " + layoutMinZ + ", " + layoutMaxZ + "), " +
                "maxPaddingPerSide=" + MAX_IMAGE_LAYOUT_PADDING_PER_SIDE + ".");
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

private static bool TryResolveRequiredGameplayBounds(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    UnlockDefinition[] unlocks,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    out float minX,
    out float maxX,
    out float minZ,
            out float maxZ,
            out string error)
        {
            minX = 0f;
    maxX = 0f;
    minZ = 0f;
    maxZ = 0f;
    error = string.Empty;

    if (!TryCollectGameplayFootprintBounds(spawns, physicsAreas, rails, unlocks, catalog, layoutSpec, out List<GameplaySpawnFootprintBounds> boundsList, out error))
        return false;

    bool hasAny = false;
    for (int i = 0; i < boundsList.Count; i++)
    {
        GameplaySpawnFootprintBounds bounds = boundsList[i];

        if (!hasAny)
        {
            minX = bounds.MinX;
                    maxX = bounds.MaxX;
                    minZ = bounds.MinZ;
                    maxZ = bounds.MaxZ;
                    hasAny = true;
                    continue;
                }

                if (bounds.MinX < minX)
                    minX = bounds.MinX;
                if (bounds.MaxX > maxX)
                    maxX = bounds.MaxX;
                if (bounds.MinZ < minZ)
                    minZ = bounds.MinZ;
                if (bounds.MaxZ > maxZ)
                    maxZ = bounds.MaxZ;
    }

    return hasAny;
}

private static bool TryCollectGameplayFootprintBounds(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas,
    CompiledRailDefinition[] rails,
    UnlockDefinition[] unlocks,
    PlayableObjectCatalog catalog,
    LayoutSpecDocument layoutSpec,
    out List<GameplaySpawnFootprintBounds> boundsList,
    out string error)
{
    boundsList = new List<GameplaySpawnFootprintBounds>();
    error = string.Empty;
    if (catalog == null)
        return false;

    Dictionary<string, LayoutSpecPlacementEntry> placementLookup = BuildLayoutPlacementLookup(layoutSpec);
    Dictionary<string, CompiledRailDefinition> railBySpawnKey = BuildRailLookupBySpawnKey(rails);

    CompiledSpawnData[] safeSpawns = spawns ?? new CompiledSpawnData[0];
    for (int i = 0; i < safeSpawns.Length; i++)
    {
        CompiledSpawnData spawn = safeSpawns[i];
        string spawnKey = ResolveSpawnLabel(spawn);
        if (spawn == null || (!string.IsNullOrEmpty(spawnKey) && railBySpawnKey.ContainsKey(spawnKey)))
            continue;

        if (!TryResolveGameplaySpawnFootprintBounds(
                spawn,
                catalog,
                layoutSpec,
                placementLookup,
                out GameplaySpawnFootprintBounds bounds,
                out error))
        {
            return false;
        }

        boundsList.Add(bounds);
    }

    foreach (KeyValuePair<string, CompiledRailDefinition> pair in railBySpawnKey)
    {
        if (!TryResolveRailFootprintBounds(pair.Value, placementLookup, out GameplaySpawnFootprintBounds railBounds, out error))
            return false;

        boundsList.Add(railBounds);
    }

    CompiledPhysicsAreaDefinition[] safeAreas = physicsAreas ?? new CompiledPhysicsAreaDefinition[0];
    for (int i = 0; i < safeAreas.Length; i++)
    {
        CompiledPhysicsAreaDefinition area = safeAreas[i];
        if (area == null)
            continue;

        if (!TryResolvePhysicsAreaFootprintBounds(area, placementLookup, out GameplaySpawnFootprintBounds bounds, out error))
            return false;

        boundsList.Add(bounds);
    }

    return true;
}

private static Dictionary<string, CompiledRailDefinition> BuildRailLookupBySpawnKey(CompiledRailDefinition[] rails)
{
    var lookup = new Dictionary<string, CompiledRailDefinition>(StringComparer.Ordinal);
    CompiledRailDefinition[] safeRails = rails ?? new CompiledRailDefinition[0];
    for (int i = 0; i < safeRails.Length; i++)
    {
        CompiledRailDefinition rail = safeRails[i];
        string spawnKey = rail != null && rail.spawnKey != null ? rail.spawnKey.Trim() : string.Empty;
        if (string.IsNullOrEmpty(spawnKey))
            continue;

        lookup[spawnKey] = rail;
    }

    return lookup;
}

private static Dictionary<string, LayoutSpecPlacementEntry> BuildLayoutPlacementLookup(LayoutSpecDocument layoutSpec)
{
    var lookup = new Dictionary<string, LayoutSpecPlacementEntry>(StringComparer.Ordinal);
    LayoutSpecPlacementEntry[] safePlacements = layoutSpec != null ? layoutSpec.placements ?? new LayoutSpecPlacementEntry[0] : new LayoutSpecPlacementEntry[0];
    for (int i = 0; i < safePlacements.Length; i++)
    {
        LayoutSpecPlacementEntry entry = safePlacements[i];
        string objectId = entry != null && entry.objectId != null ? entry.objectId.Trim() : string.Empty;
        if (string.IsNullOrEmpty(objectId))
            continue;

        lookup[objectId] = entry;
    }

    return lookup;
}

private static LayoutSpecPlacementEntry ResolveLayoutPlacementEntry(
    Dictionary<string, LayoutSpecPlacementEntry> placementLookup,
    params string[] referenceIds)
{
    if (placementLookup == null || referenceIds == null)
        return null;

    for (int i = 0; i < referenceIds.Length; i++)
    {
        string normalizedReferenceId = referenceIds[i] != null ? referenceIds[i].Trim() : string.Empty;
        if (string.IsNullOrEmpty(normalizedReferenceId))
            continue;

        if (placementLookup.TryGetValue(normalizedReferenceId, out LayoutSpecPlacementEntry entry) && entry != null)
            return entry;
    }

    return null;
}

private static void ApplyPlacementSemantics(
    GameplaySpawnFootprintBounds bounds,
    LayoutSpecPlacementEntry placementEntry)
{
    if (bounds == null || placementEntry == null)
        return;

    bounds.LaneId = placementEntry.laneId != null ? placementEntry.laneId.Trim() : string.Empty;
    bounds.HasLaneOrder = placementEntry.hasLaneOrder;
    bounds.LaneOrder = placementEntry.laneOrder;
    bounds.SharedSlotId = placementEntry.sharedSlotId != null ? placementEntry.sharedSlotId.Trim() : string.Empty;
    bounds.HasMinGapToNextCells = placementEntry.hasMinGapToNextCells;
    bounds.MinGapToNextCells = placementEntry.minGapToNextCells;
}

private static string ResolveSceneObjectIdFromSpawnKey(CompiledSpawnData spawn)
{
    string spawnKey = ResolveSpawnLabel(spawn);
    if (spawnKey.StartsWith("spawn_", StringComparison.Ordinal))
        return spawnKey.Substring("spawn_".Length);
    return string.Empty;
}

private static bool TryResolveSharedSlotGroupId(
    List<GameplaySpawnFootprintBounds> boundsGroup,
    out string sharedSlotId)
{
    sharedSlotId = string.Empty;
    List<GameplaySpawnFootprintBounds> safeBoundsGroup = boundsGroup ?? new List<GameplaySpawnFootprintBounds>();
    for (int i = 0; i < safeBoundsGroup.Count; i++)
    {
        string candidate = safeBoundsGroup[i] != null && safeBoundsGroup[i].SharedSlotId != null
            ? safeBoundsGroup[i].SharedSlotId.Trim()
            : string.Empty;
        if (string.IsNullOrEmpty(candidate))
            return false;

        if (string.IsNullOrEmpty(sharedSlotId))
        {
            sharedSlotId = candidate;
            continue;
        }

        if (!string.Equals(sharedSlotId, candidate, StringComparison.Ordinal))
            return false;
    }

    return !string.IsNullOrEmpty(sharedSlotId);
}

private static float ComputeAverageWorldZ(List<GameplaySpawnFootprintBounds> boundsGroup)
{
    List<GameplaySpawnFootprintBounds> safeBoundsGroup = boundsGroup ?? new List<GameplaySpawnFootprintBounds>();
    if (safeBoundsGroup.Count == 0)
        return 0f;

    float sum = 0f;
    int count = 0;
    for (int i = 0; i < safeBoundsGroup.Count; i++)
    {
        GameplaySpawnFootprintBounds bounds = safeBoundsGroup[i];
        if (bounds == null)
            continue;

        sum += bounds.CenterZ;
        count++;
    }

    return count == 0 ? 0f : sum / count;
}

private static float ComputeLaneGroupGap(
    List<GameplaySpawnFootprintBounds> upperGroup,
    List<GameplaySpawnFootprintBounds> lowerGroup)
{
    float gap = float.MaxValue;
    List<GameplaySpawnFootprintBounds> safeUpperGroup = upperGroup ?? new List<GameplaySpawnFootprintBounds>();
    List<GameplaySpawnFootprintBounds> safeLowerGroup = lowerGroup ?? new List<GameplaySpawnFootprintBounds>();
    for (int upperIndex = 0; upperIndex < safeUpperGroup.Count; upperIndex++)
    {
        GameplaySpawnFootprintBounds upper = safeUpperGroup[upperIndex];
        if (upper == null)
            continue;

        for (int lowerIndex = 0; lowerIndex < safeLowerGroup.Count; lowerIndex++)
        {
            GameplaySpawnFootprintBounds lower = safeLowerGroup[lowerIndex];
            if (lower == null)
                continue;

            float candidateGap = ComputeAxisGap(lower.MinZ, lower.MaxZ, upper.MinZ, upper.MaxZ);
            if (candidateGap < gap)
                gap = candidateGap;
        }
    }

    return gap == float.MaxValue ? 0f : gap;
}

private static float ResolveDeclaredLaneRequiredGap(
    List<GameplaySpawnFootprintBounds> upperGroup,
    List<GameplaySpawnFootprintBounds> lowerGroup)
{
    float requiredGap = 0f;
    List<GameplaySpawnFootprintBounds> safeUpperGroup = upperGroup ?? new List<GameplaySpawnFootprintBounds>();
    List<GameplaySpawnFootprintBounds> safeLowerGroup = lowerGroup ?? new List<GameplaySpawnFootprintBounds>();
    for (int upperIndex = 0; upperIndex < safeUpperGroup.Count; upperIndex++)
    {
        GameplaySpawnFootprintBounds upper = safeUpperGroup[upperIndex];
        if (upper == null)
            continue;

        if (upper.HasMinGapToNextCells)
            requiredGap = Math.Max(requiredGap, upper.MinGapToNextCells);

        for (int lowerIndex = 0; lowerIndex < safeLowerGroup.Count; lowerIndex++)
        {
            GameplaySpawnFootprintBounds lower = safeLowerGroup[lowerIndex];
            if (lower == null)
                continue;

            requiredGap = Math.Max(requiredGap, ResolveRequiredLaneGapBetween(upper, lower));
        }
    }

    return requiredGap;
}

private static float ResolveRequiredLaneGapBetween(
    GameplaySpawnFootprintBounds left,
    GameplaySpawnFootprintBounds right)
{
    if (left == null || right == null)
        return 0f;

    if (!string.Equals(left.LaneId, right.LaneId, StringComparison.Ordinal))
        return 0f;

    if (!left.HasLaneOrder || !right.HasLaneOrder || left.LaneOrder == right.LaneOrder)
        return 0f;

    if (IsDeclaredSharedSlotPair(left, right))
        return 0f;

    float requiredGap = 0f;
    if (left.LaneOrder < right.LaneOrder && left.HasMinGapToNextCells)
        requiredGap = Math.Max(requiredGap, left.MinGapToNextCells);
    if (right.LaneOrder < left.LaneOrder && right.HasMinGapToNextCells)
        requiredGap = Math.Max(requiredGap, right.MinGapToNextCells);
    return requiredGap;
}

private static bool IsDeclaredSharedSlotPair(
    GameplaySpawnFootprintBounds left,
    GameplaySpawnFootprintBounds right)
{
    string leftSharedSlotId = left != null && left.SharedSlotId != null ? left.SharedSlotId.Trim() : string.Empty;
    string rightSharedSlotId = right != null && right.SharedSlotId != null ? right.SharedSlotId.Trim() : string.Empty;
    return
        !string.IsNullOrEmpty(leftSharedSlotId) &&
        string.Equals(leftSharedSlotId, rightSharedSlotId, StringComparison.Ordinal);
}

private static string ResolveRepresentativePlacementId(List<GameplaySpawnFootprintBounds> boundsGroup)
{
    List<GameplaySpawnFootprintBounds> safeBoundsGroup = boundsGroup ?? new List<GameplaySpawnFootprintBounds>();
    string bestObjectId = string.Empty;
    for (int i = 0; i < safeBoundsGroup.Count; i++)
    {
        string objectId = safeBoundsGroup[i] != null && safeBoundsGroup[i].ObjectId != null
            ? safeBoundsGroup[i].ObjectId.Trim()
            : string.Empty;
        if (string.IsNullOrEmpty(objectId))
            continue;

        if (string.IsNullOrEmpty(bestObjectId) || string.CompareOrdinal(objectId, bestObjectId) < 0)
            bestObjectId = objectId;
    }

    return bestObjectId;
}

private static float ComputeAxisGap(float firstMin, float firstMax, float secondMin, float secondMax)
{
    if (firstMax < secondMin)
        return secondMin - firstMax;
    if (secondMax < firstMin)
        return firstMin - secondMax;
    return 0f;
}


        private static bool HasRequiredImageLayoutEnvironmentStructure(
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog)
        {
            bool hasFloorEnvelope = layoutSpec != null &&
                                    layoutSpec.floorBounds != null &&
                                    layoutSpec.floorBounds.hasWorldBounds;
            bool hasBoundaryStructure = false;
            LayoutSpecEnvironmentEntry[] environmentEntries = layoutSpec != null
                ? layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0]
                : new LayoutSpecEnvironmentEntry[0];
            for (int i = 0; i < environmentEntries.Length; i++)
            {
                LayoutSpecEnvironmentEntry entry = environmentEntries[i];
                if (entry == null)
                    continue;

                string category = ResolveEnvironmentCategory(catalog, entry.objectId != null ? entry.objectId.Trim() : string.Empty);
                if (string.Equals(category, EnvironmentCatalog.FLOOR_CATEGORY, StringComparison.Ordinal))
                    hasFloorEnvelope = true;

                if (string.Equals(category, EnvironmentCatalog.WALL_CATEGORY, StringComparison.Ordinal) ||
                    string.Equals(category, EnvironmentCatalog.FENCE_CATEGORY, StringComparison.Ordinal) ||
                    string.Equals(category, EnvironmentCatalog.ROAD_CATEGORY, StringComparison.Ordinal))
                {
                    hasBoundaryStructure = true;
                }
            }

            return hasFloorEnvelope && hasBoundaryStructure;
        }

        private static bool TryResolveSpawnFootprintFromCatalog(
            PlayableObjectCatalog catalog,
            CompiledSpawnData spawn,
            out int widthCells,
            out int depthCells)
        {
            widthCells = 1;
            depthCells = 1;
            if (catalog == null || spawn == null || string.IsNullOrWhiteSpace(spawn.objectId))
                return false;

            if (!catalog.TryResolveGameplayPrefab(spawn.objectId.Trim(), spawn.designIndex, out GameObject prefab, out _) || prefab == null)
                return false;

            if (!PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata))
                return false;

            widthCells = metadata.placementFootprintWidthCells > 0 ? metadata.placementFootprintWidthCells : 1;
            depthCells = metadata.placementFootprintDepthCells > 0 ? metadata.placementFootprintDepthCells : 1;
            return widthCells > 0 && depthCells > 0;
        }


        private static bool AreWorldBoundsOverlapping(
            float minAX,
            float maxAX,
            float minAZ,
            float maxAZ,
            float minBX,
            float maxBX,
            float minBZ,
            float maxBZ)
        {
            return minAX < maxBX &&
                   maxAX > minBX &&
                   minAZ < maxBZ &&
                   maxAZ > minBZ;
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
                if (selection == null || string.IsNullOrWhiteSpace(selection.objectId) || selection.designIndex < 0)
                    continue;

                lookup[selection.objectId.Trim()] = selection.designIndex;
            }

            return lookup;
        }

private static Dictionary<string, CompiledSpawnData> BuildSpawnLookup(
    CompiledSpawnData[] spawns,
    CompiledPhysicsAreaDefinition[] physicsAreas)
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

    CompiledPhysicsAreaDefinition[] safeAreas = physicsAreas ?? new CompiledPhysicsAreaDefinition[0];
    for (int i = 0; i < safeAreas.Length; i++)
    {
        CompiledPhysicsAreaDefinition area = safeAreas[i];
        if (area == null)
            continue;

        var sceneRef = new CompiledSpawnData
        {
            spawnKey = area.spawnKey,
            objectId = area.objectId,
            localPosition = area.localPosition,
        };

        if (!string.IsNullOrWhiteSpace(sceneRef.spawnKey))
            lookup[sceneRef.spawnKey.Trim()] = sceneRef;
        if (!string.IsNullOrWhiteSpace(sceneRef.objectId))
            lookup[sceneRef.objectId.Trim()] = sceneRef;
    }

    return lookup;
}

private static bool IsSupportedRailSinkTargetObjectId(
    string sinkEndpointTargetObjectId,
    Dictionary<string, CompiledSpawnData> spawnLookup)
{
    string normalizedTargetObjectId = string.IsNullOrWhiteSpace(sinkEndpointTargetObjectId)
        ? string.Empty
        : sinkEndpointTargetObjectId.Trim();
    if (string.IsNullOrEmpty(normalizedTargetObjectId))
        return false;

    if (spawnLookup == null || !spawnLookup.TryGetValue(normalizedTargetObjectId, out CompiledSpawnData spawn) || spawn == null)
        return false;

    string compiledGameplayObjectId = !string.IsNullOrWhiteSpace(spawn.objectId)
        ? spawn.objectId.Trim()
        : string.Empty;
    string compiledRole = GameplayOverlapAllowanceRules.ResolveCompiledGameplayRole(compiledGameplayObjectId);
    return PromptIntentObjectRoles.IsRailSinkTargetRoleSupported(compiledRole);
}

private static bool TryResolveRailLayoutBounds(
    RailLayoutDefinition layout,
    out WorldBoundsDefinition bounds,
    out string error)
{
    bounds = new WorldBoundsDefinition();
    error = string.Empty;
    RailPathAnchorDefinition[] pathCells = layout != null ? layout.pathCells ?? new RailPathAnchorDefinition[0] : new RailPathAnchorDefinition[0];
    return RailPathAuthoringUtility.TryBuildTrackBounds(pathCells, out bounds, out error);
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

        private static void Warn(CompiledPlayablePlanValidationResult result, string message)
        {
            if (result == null || string.IsNullOrWhiteSpace(message))
                return;

            Warn(result, new ValidationIssueRecord(
                ValidationRuleId.COMPILED_PLAN_WARNING_GENERIC,
                ValidationSeverity.Warning,
                message,
                "CompiledPlayablePlan"));
        }

        private static void Warn(CompiledPlayablePlanValidationResult result, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return;

            result.Warnings.Add(issue.message ?? string.Empty);
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
