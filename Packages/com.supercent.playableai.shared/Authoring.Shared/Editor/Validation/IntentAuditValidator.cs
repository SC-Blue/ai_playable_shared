using System;
using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public sealed class IntentAuditValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public List<string> Errors = new List<string>();
        public List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();
    }

    public static class IntentAuditValidator
    {
        public static IntentAuditValidationResult Validate(PlayablePromptIntent intent, PlayableScenarioModel model, CompiledPlayablePlan plan, PlayableObjectCatalog catalog)
        {
            return Validate(intent, model, plan, catalog, null);
        }

        public static IntentAuditValidationResult Validate(
            PlayablePromptIntent intent,
            PlayableScenarioModel model,
            CompiledPlayablePlan plan,
            PlayableObjectCatalog catalog,
            LayoutSpecDocument layoutSpec)
        {
            var result = new IntentAuditValidationResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (intent == null)
                return Fail(result, "intent audit를 위해 PlayablePromptIntent가 필요합니다.");
            if (model == null)
                return Fail(result, "intent audit를 위해 PlayableScenarioModel이 필요합니다.");
            if (plan == null)
                return Fail(result, "intent audit를 위해 CompiledPlayablePlan이 필요합니다.");
            if (catalog == null)
                return Fail(result, "intent audit를 위해 PlayableObjectCatalog가 필요합니다.");

            ValidateItemPrices(model.saleValues, plan.itemPrices, result);
            ValidateFacilityAcceptedItems(model, plan.facilityAcceptedItems, result);
            ValidateFacilityOutputItems(model, plan.facilityOutputItems, result);
            ValidatePlayerOptions(model.playerOptions, plan.playerOptions, result);
            ValidateFacilityOptions(model.objects, plan.facilityOptions, result);
            ValidateSpawnStartState(model.objects, plan.spawns, result);
            if (layoutSpec != null)
                ValidateSpawnPositions(model.objects, plan.spawns, layoutSpec, result);
            ValidatePhysicsAreaStartState(model.objects, plan.physicsAreas, result);
            if (layoutSpec != null)
                ValidatePhysicsAreaPositions(model.objects, plan.physicsAreas, layoutSpec, result);
            ValidateArrowAbsorption(model.stages, plan.beats, plan.actions, result);
            ValidateFirstBeatGating(model.stages, plan.beats, result);
            ValidateIntroBeatSequencing(model.stages, plan.beats, plan.actions, result);
            ValidateStageInternalBeatSequencing(model.stages, plan.beats, plan.actions, result);
            ValidateStageCompletedEntryRules(model.stages, plan.beats, plan.actions, plan.stageFirstBeatIds, result);
            ValidateUnlockStageCompletionRules(model.stages, plan.unlocks, plan.beats, plan.actions, result);
            return FinalizeResult(result);
        }

        private static void ValidateArrowAbsorption(
            ScenarioModelStageDefinition[] stages,
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            IntentAuditValidationResult result)
        {
            Dictionary<string, FlowBeatDefinition> beatById = BuildBeatLookup(beats);
            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int stageIndex = 0; stageIndex < safeStages.Length; stageIndex++)
            {
                ScenarioModelStageDefinition stage = safeStages[stageIndex];
                if (stage == null)
                    continue;

                ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
                for (int objectiveIndex = 0; objectiveIndex < objectives.Length; objectiveIndex++)
                {
                    ScenarioModelObjectiveDefinition objective = objectives[objectiveIndex];
                    if (objective == null)
                        continue;

                    string objectiveKind = objective.kind != null ? objective.kind.Trim() : string.Empty;
                    string requiredObjectiveArrowEventKey = PromptIntentCapabilityRegistry.GetObjectiveRequiredArrowEventKey(objectiveKind);
                    if (!string.IsNullOrEmpty(requiredObjectiveArrowEventKey))
                    {
                        if (!objective.absorbsArrow)
                        {
                            Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "]에는 explicit show_arrow(eventKey='" + requiredObjectiveArrowEventKey + "')가 필요합니다.");
                            continue;
                        }

                        string expectedConvertEventKey = objective.arrowEventKey != null ? objective.arrowEventKey.Trim() : string.Empty;
                        if (!string.Equals(expectedConvertEventKey, requiredObjectiveArrowEventKey, StringComparison.Ordinal))
                            Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "]의 absorbed arrow는 eventKey '" + requiredObjectiveArrowEventKey + "'이어야 합니다.");
                    }

                    if (!objective.absorbsArrow)
                        continue;

                    string beatId = (stage.id ?? string.Empty).Trim() + "__objective_" + objectiveIndex.ToString("00");
                    if (!beatById.TryGetValue(beatId, out FlowBeatDefinition beat) ||
                        !TryGetOwnedAction(actions, beatId, FlowActionKinds.ARROW_GUIDE, out FlowActionDefinition arrowAction))
                    {
                        Fail(result, "stages[" + stageIndex + "]의 absorbed arrow가 objective beat에 arrow_guide action으로 lowering되지 않았습니다.");
                        continue;
                    }

                    string expectedEventKey = objective.arrowEventKey != null ? objective.arrowEventKey.Trim() : string.Empty;
                    string actualEventKey = arrowAction.payload != null && arrowAction.payload.arrowGuide != null
                        ? (arrowAction.payload.arrowGuide.eventKey ?? string.Empty).Trim()
                        : string.Empty;
                    if (!string.Equals(expectedEventKey, actualEventKey, StringComparison.Ordinal))
                        Fail(result, "stages[" + stageIndex + "]의 absorbed arrow eventKey가 objective와 같은 값으로 lowering되지 않았습니다.");

                    if (!string.IsNullOrEmpty(requiredObjectiveArrowEventKey) &&
                        !string.Equals(actualEventKey, requiredObjectiveArrowEventKey, StringComparison.Ordinal))
                    {
                        Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "] beat는 eventKey '" + requiredObjectiveArrowEventKey + "'으로 lowering되어야 합니다.");
                    }

                    string requiredSignalEventKey =
                        beat.completeWhen != null &&
                        string.Equals(beat.completeWhen.type, StepConditionRules.GAMEPLAY_SIGNAL, StringComparison.Ordinal)
                            ? PromptIntentCapabilityRegistry.GetGameplaySignalRequiredTargetEventKey(beat.completeWhen.signalId)
                            : string.Empty;
                    if (beat.completeWhen != null &&
                        string.Equals(beat.completeWhen.type, StepConditionRules.GAMEPLAY_SIGNAL, StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(requiredSignalEventKey) &&
                        !string.Equals(actualEventKey, requiredSignalEventKey, StringComparison.Ordinal))
                    {
                        Fail(result, "stages[" + stageIndex + "].objectives[" + objectiveIndex + "] signal '" + beat.completeWhen.signalId + "' beat는 arrow_guide.eventKey '" + requiredSignalEventKey + "'이어야 합니다.");
                    }
                }
            }
        }

        private static void ValidateFirstBeatGating(
            ScenarioModelStageDefinition[] stages,
            FlowBeatDefinition[] beats,
            IntentAuditValidationResult result)
        {
            var firstBeatByStageId = new Dictionary<string, FlowBeatDefinition>(StringComparer.Ordinal);
            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            for (int i = 0; i < safeBeats.Length; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null || string.IsNullOrWhiteSpace(beat.id))
                    continue;

                string beatId = beat.id.Trim();
                int separatorIndex = beatId.IndexOf("__", StringComparison.Ordinal);
                if (separatorIndex <= 0)
                    continue;

                string stageId = beatId.Substring(0, separatorIndex);
                if (!firstBeatByStageId.ContainsKey(stageId))
                    firstBeatByStageId.Add(stageId, beat);
            }

            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                string stageId = stage != null && stage.id != null ? stage.id.Trim() : string.Empty;
                if (string.IsNullOrEmpty(stageId))
                    continue;
                if (!firstBeatByStageId.TryGetValue(stageId, out FlowBeatDefinition beat) || beat == null || beat.enterWhen == null)
                    continue;

                string kind = stage.enterCondition != null ? (stage.enterCondition.kind ?? string.Empty).Trim() : string.Empty;
                if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
                {
                    string referencedStageId = stage.enterCondition.stageId != null ? stage.enterCondition.stageId.Trim() : string.Empty;
                    ScenarioModelStageDefinition referencedStage = FindStageById(safeStages, referencedStageId);
                    string expectedLastBeatId = ResolveLastBeatId(referencedStage);
                    if (!string.Equals(beat.enterWhen.type, StepConditionRules.BEAT_COMPLETED, StringComparison.Ordinal) ||
                        !string.Equals(beat.enterWhen.targetId, expectedLastBeatId, StringComparison.Ordinal))
                    {
                        Fail(result, "stages[" + i + "]의 first beat gating은 referenced stage의 마지막 beat_completed로 lowering되어야 합니다.");
                    }
                    continue;
                }

                if (string.Equals(kind, PromptIntentConditionKinds.BALANCE_AT_LEAST, StringComparison.Ordinal) &&
                    !string.Equals(beat.enterWhen.type, StepConditionRules.CURRENCY_AT_LEAST, StringComparison.Ordinal))
                {
                    Fail(result, "stages[" + i + "]의 first beat gating이 currency_at_least로 lowering되지 않았습니다.");
                }
            }
        }

        private static void ValidateIntroBeatSequencing(
            ScenarioModelStageDefinition[] stages,
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            IntentAuditValidationResult result)
        {
            Dictionary<string, FlowBeatDefinition> beatById = BuildBeatLookup(beats);
            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                    continue;

                List<string> introBeatIds = BuildEntryFocusBeatIds(stage);
                List<string> entryGuideBeatIds = BuildEntryGuideBeatIds(stage);
                for (int introIndex = 1; introIndex < introBeatIds.Count; introIndex++)
                {
                    string prerequisiteActionId = GetPrimaryOwnedActionId(actions, introBeatIds[introIndex - 1], FlowActionKinds.CAMERA_FOCUS);
                    if (string.IsNullOrEmpty(prerequisiteActionId) ||
                        !HasActionCompletedEnterWhen(beatById, introBeatIds[introIndex], prerequisiteActionId))
                    {
                        Fail(result, "stages[" + i + "]의 entry focus beat는 이전 focus camera action 완료 후에만 시작되어야 합니다.");
                    }
                }

                if (entryGuideBeatIds.Count > 0)
                {
                    string expectedFirstGuidePrerequisite = introBeatIds.Count > 0
                        ? introBeatIds[introBeatIds.Count - 1]
                        : string.Empty;
                    if (!string.IsNullOrEmpty(expectedFirstGuidePrerequisite) &&
                        !HasOneOfEnterWhen(
                            beatById,
                            entryGuideBeatIds[0],
                            BuildActionCompletedTargetId(expectedFirstGuidePrerequisite, actions),
                            expectedFirstGuidePrerequisite))
                    {
                        Fail(result, "stages[" + i + "]의 첫 entry guide beat는 직전 focus camera 이후에만 시작되어야 합니다.");
                    }

                    for (int guideIndex = 1; guideIndex < entryGuideBeatIds.Count; guideIndex++)
                    {
                        if (!HasBeatCompletedEnterWhen(beatById, entryGuideBeatIds[guideIndex], entryGuideBeatIds[guideIndex - 1]))
                        {
                            Fail(result, "stages[" + i + "]의 entry guide beat는 이전 guide beat 완료 후에만 시작되어야 합니다.");
                        }
                    }
                }

                string lastEntrySetupBeatId = entryGuideBeatIds.Count > 0
                    ? entryGuideBeatIds[entryGuideBeatIds.Count - 1]
                    : (introBeatIds.Count > 0 ? introBeatIds[introBeatIds.Count - 1] : string.Empty);

                if (string.IsNullOrEmpty(lastEntrySetupBeatId))
                    continue;

                string firstObjectiveBeatId = ResolveFirstObjectiveBeatId(stage);
                if (string.IsNullOrEmpty(firstObjectiveBeatId))
                    continue;

                string introPrerequisiteActionId = GetPrimaryOwnedActionId(actions, lastEntrySetupBeatId, FlowActionKinds.CAMERA_FOCUS);
                bool expectsArrivalTiming = FirstObjectiveUsesArrivalTiming(stage);
                bool objectiveStartsAfterExpectedPrerequisite = expectsArrivalTiming && !string.IsNullOrEmpty(introPrerequisiteActionId)
                    ? HasActionCompletedEnterWhen(beatById, firstObjectiveBeatId, introPrerequisiteActionId)
                    : HasBeatCompletedEnterWhen(beatById, firstObjectiveBeatId, lastEntrySetupBeatId);
                if (!objectiveStartsAfterExpectedPrerequisite)
                {
                    Fail(result, expectsArrivalTiming
                        ? "stages[" + i + "]의 첫 objective beat는 마지막 entry focus camera action 완료 후에 시작되어야 합니다."
                        : "stages[" + i + "]의 첫 objective beat는 마지막 entry setup beat 완료 후에만 시작되어야 합니다.");
                }
            }
        }

        private static void ValidateStageInternalBeatSequencing(
            ScenarioModelStageDefinition[] stages,
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            IntentAuditValidationResult result)
        {
            Dictionary<string, FlowBeatDefinition> beatById = BuildBeatLookup(beats);
            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                    continue;

                List<string> objectiveBeatIds = BuildObjectiveBeatIds(stage);
                for (int objectiveIndex = 1; objectiveIndex < objectiveBeatIds.Count; objectiveIndex++)
                {
                    if (!HasBeatCompletedEnterWhen(beatById, objectiveBeatIds[objectiveIndex], objectiveBeatIds[objectiveIndex - 1]))
                    {
                        Fail(result, "stages[" + i + "]의 objective beat는 이전 objective beat 완료 후에만 시작되어야 합니다.");
                    }
                }

                List<string> completionFocusBeatIds = BuildCompletionFocusBeatIds(stage);
                List<string> completionGuideBeatIds = BuildCompletionGuideBeatIds(stage);

                string expectedFirstCompletionPrerequisite = objectiveBeatIds.Count > 0
                    ? objectiveBeatIds[objectiveBeatIds.Count - 1]
                    : ResolveLastEntrySetupBeatId(stage);

                if (completionFocusBeatIds.Count > 0 &&
                    !string.IsNullOrEmpty(expectedFirstCompletionPrerequisite) &&
                    !HasBeatCompletedEnterWhen(beatById, completionFocusBeatIds[0], expectedFirstCompletionPrerequisite))
                {
                    Fail(result, "stages[" + i + "]의 첫 completion focus beat는 직전 stage beat 완료 후에만 시작되어야 합니다.");
                }

                for (int focusIndex = 1; focusIndex < completionFocusBeatIds.Count; focusIndex++)
                {
                    if (!HasBeatCompletedEnterWhen(beatById, completionFocusBeatIds[focusIndex], completionFocusBeatIds[focusIndex - 1]))
                    {
                        Fail(result, "stages[" + i + "]의 completion focus beat는 이전 completion focus beat 완료 후에만 시작되어야 합니다.");
                    }
                }

                if (completionGuideBeatIds.Count == 0)
                    continue;

                string expectedFirstCompletionGuidePrerequisite = completionFocusBeatIds.Count > 0
                    ? completionFocusBeatIds[completionFocusBeatIds.Count - 1]
                    : expectedFirstCompletionPrerequisite;
                if (!string.IsNullOrEmpty(expectedFirstCompletionGuidePrerequisite) &&
                    !HasOneOfEnterWhen(
                        beatById,
                        completionGuideBeatIds[0],
                        BuildActionCompletedTargetId(expectedFirstCompletionGuidePrerequisite, actions),
                        expectedFirstCompletionGuidePrerequisite))
                {
                    Fail(result, "stages[" + i + "]의 첫 completion guide beat는 직전 completion setup 이후에만 시작되어야 합니다.");
                }

                for (int guideIndex = 1; guideIndex < completionGuideBeatIds.Count; guideIndex++)
                {
                    if (!HasBeatCompletedEnterWhen(beatById, completionGuideBeatIds[guideIndex], completionGuideBeatIds[guideIndex - 1]))
                    {
                        Fail(result, "stages[" + i + "]의 completion guide beat는 이전 guide beat 완료 후에만 시작되어야 합니다.");
                    }
                }
            }
        }

        private static void ValidateStageCompletedEntryRules(
            ScenarioModelStageDefinition[] stages,
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            string[] stageFirstBeatIds,
            IntentAuditValidationResult result)
        {
            var previousLastBeatByStageId = new Dictionary<string, string>(StringComparer.Ordinal);
            string previousLastBeatId = string.Empty;
            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                    continue;

                string stageId = stage.id.Trim();
                previousLastBeatByStageId[stageId] = previousLastBeatId;

                string resolvedLastBeatId = ResolveLastBeatId(stage);
                if (!string.IsNullOrEmpty(resolvedLastBeatId))
                    previousLastBeatId = resolvedLastBeatId;
            }

            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null || stage.enterCondition == null)
                    continue;
                if (!string.Equals(stage.enterCondition.kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
                    continue;

                string previousBeatId = previousLastBeatByStageId.TryGetValue(stage.id.Trim(), out string resolved) ? resolved : string.Empty;
                if (string.IsNullOrEmpty(previousBeatId))
                    continue;

                if (HasRevealOrSystemActionEffect(stage.entryEffects) &&
                    !HasActionBeatWithEnterWhen(safeBeats, actions, FlowActionKinds.REVEAL, StepConditionRules.BEAT_COMPLETED, previousBeatId))
                {
                    Fail(result, "stages[" + i + "]의 entry reveal/system action effect가 previous stage last beat의 beat_completed로 lowering되지 않았습니다.");
                }

                if (HasEntryArrivalSpawnCustomer(stage.entryEffects))
                {
                    var acceptableActionIds = new List<string>();
                    if (!string.IsNullOrEmpty(previousBeatId) && IsFocusBeatId(previousBeatId))
                    {
                        string actionId = GetPrimaryOwnedActionId(actions, previousBeatId, FlowActionKinds.CAMERA_FOCUS);
                        if (!string.IsNullOrEmpty(actionId))
                            acceptableActionIds.Add(actionId);
                    }
                    if (HasEntryFocusBeforeSpawnCustomerArrival(stage.entryEffects) &&
                        stageFirstBeatIds != null &&
                        i < stageFirstBeatIds.Length &&
                        !string.IsNullOrEmpty(stageFirstBeatIds[i]) &&
                        IsFocusBeatId(stageFirstBeatIds[i]))
                    {
                        string actionId = GetPrimaryOwnedActionId(actions, stageFirstBeatIds[i], FlowActionKinds.CAMERA_FOCUS);
                        if (!string.IsNullOrEmpty(actionId))
                            acceptableActionIds.Add(actionId);
                    }

                    List<string> expectedTargetIds = BuildEntrySpawnCustomerTargetIds(
                        stage.entryEffects,
                        PromptIntentEffectTimingKinds.ARRIVAL);

                    if (acceptableActionIds.Count == 0 ||
                        expectedTargetIds.Count == 0 ||
                        !HasCustomerSpawnActionBeatForTargets(safeBeats, actions, acceptableActionIds, expectedTargetIds, StepConditionRules.ACTION_COMPLETED))
                    {
                        Fail(result, "stages[" + i + "]의 entry spawn_customer arrival timing이 이전 focus camera action 완료 기준으로 lowering되지 않았습니다.");
                    }
                }

                if (HasEntryCompletedSpawnCustomer(stage.entryEffects) &&
                    !HasActionBeatWithEnterWhen(safeBeats, actions, FlowActionKinds.CUSTOMER_SPAWN, StepConditionRules.BEAT_COMPLETED, previousBeatId))
                {
                    Fail(result, "stages[" + i + "]의 entry spawn_customer completed timing이 previous stage last beat의 beat_completed로 lowering되지 않았습니다.");
                }
            }
        }

        private static void ValidateUnlockStageCompletionRules(
            ScenarioModelStageDefinition[] stages,
            UnlockDefinition[] unlocks,
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            IntentAuditValidationResult result)
        {
            var unlockByUnlockerId = new Dictionary<string, UnlockDefinition>(StringComparer.Ordinal);
            UnlockDefinition[] safeUnlocks = unlocks ?? new UnlockDefinition[0];
            for (int i = 0; i < safeUnlocks.Length; i++)
            {
                UnlockDefinition unlock = safeUnlocks[i];
                if (unlock == null || string.IsNullOrWhiteSpace(unlock.unlockerId))
                    continue;

                unlockByUnlockerId[unlock.unlockerId.Trim()] = unlock;
            }

            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null)
                    continue;

                ScenarioModelObjectiveDefinition unlockObjective = FindUnlockObjective(stage.objectives);
                if (unlockObjective == null)
                    continue;

                string expectedUnlockerId = BuildUnlockerId(unlockObjective.targetObjectId);
                if (!unlockByUnlockerId.TryGetValue(expectedUnlockerId, out UnlockDefinition unlock) || unlock == null)
                {
                    Fail(result, "stages[" + i + "]의 unlock_object가 compiled unlocks[]에 lowering되지 않았습니다.");
                    continue;
                }

                HashSet<string> expectedTargetKeys = BuildExpectedUnlockTargetKeys(stage.completionEffects);
                HashSet<string> actualTargetKeys = BuildActivationTargetKeySet(unlock.targets);
                if (!SetEquals(expectedTargetKeys, actualTargetKeys))
                    Fail(result, "stages[" + i + "]의 unlock_object completion effect가 unlock.targets에 올바르게 lowering되지 않았습니다.");

                string lastBeatId = ResolveLastBeatId(stage);
                if (string.IsNullOrEmpty(lastBeatId))
                    continue;

                if (expectedTargetKeys.Count == 0 &&
                    HasRevealOrSystemActionEffect(stage.completionEffects) &&
                    !HasActionBeatWithEnterWhen(safeBeats, actions, FlowActionKinds.REVEAL, StepConditionRules.BEAT_COMPLETED, lastBeatId))
                {
                    Fail(result, "stages[" + i + "]의 unlock stage completion effect가 final beat의 beat_completed로 lowering되지 않았습니다.");
                }

                var completionFocusActionIds = new List<string>();
                if (IsFocusBeatId(lastBeatId))
                {
                    string actionId = GetPrimaryOwnedActionId(actions, lastBeatId, FlowActionKinds.CAMERA_FOCUS);
                    if (!string.IsNullOrEmpty(actionId))
                        completionFocusActionIds.Add(actionId);
                }

                if (HasCompletionArrivalSpawnCustomer(stage.completionEffects) &&
                    !HasCustomerSpawnActionBeatForTargets(safeBeats, actions, completionFocusActionIds, CollectCompletionSpawnCustomerTargetIds(stage.completionEffects), StepConditionRules.ACTION_COMPLETED))
                {
                    Fail(result, "stages[" + i + "]의 unlock stage completion spawn_customer arrival timing이 final focus camera action 완료 기준으로 lowering되지 않았습니다.");
                }

                if (HasCompletionCompletedSpawnCustomer(stage.completionEffects) &&
                    !HasActionBeatWithEnterWhen(safeBeats, actions, FlowActionKinds.CUSTOMER_SPAWN, StepConditionRules.BEAT_COMPLETED, lastBeatId))
                {
                    Fail(result, "stages[" + i + "]의 unlock stage completion spawn_customer completed timing이 final beat의 beat_completed로 lowering되지 않았습니다.");
                }
            }
        }

        private static Dictionary<string, FlowBeatDefinition> BuildBeatLookup(FlowBeatDefinition[] beats)
        {
            var values = new Dictionary<string, FlowBeatDefinition>(StringComparer.Ordinal);
            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            for (int i = 0; i < safeBeats.Length; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null || string.IsNullOrWhiteSpace(beat.id))
                    continue;

                values[beat.id.Trim()] = beat;
            }

            return values;
        }

        private static bool HasActionCompletedEnterWhen(Dictionary<string, FlowBeatDefinition> beatById, string beatId, string prerequisiteActionId)
        {
            return beatById.TryGetValue(beatId, out FlowBeatDefinition beat) &&
                   beat != null &&
                   beat.enterWhen != null &&
                   string.Equals(beat.enterWhen.type, StepConditionRules.ACTION_COMPLETED, StringComparison.Ordinal) &&
                   string.Equals(beat.enterWhen.targetId, prerequisiteActionId, StringComparison.Ordinal);
        }

        private static bool HasBeatCompletedEnterWhen(Dictionary<string, FlowBeatDefinition> beatById, string beatId, string prerequisiteBeatId)
        {
            return beatById.TryGetValue(beatId, out FlowBeatDefinition beat) &&
                   beat != null &&
                   beat.enterWhen != null &&
                   string.Equals(beat.enterWhen.type, StepConditionRules.BEAT_COMPLETED, StringComparison.Ordinal) &&
                   string.Equals(beat.enterWhen.targetId, prerequisiteBeatId, StringComparison.Ordinal);
        }

        private static bool HasActionBeatWithEnterWhen(
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            string actionKind,
            string conditionType,
            string targetId)
        {
            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            for (int i = 0; i < safeBeats.Length; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null || beat.enterWhen == null)
                    continue;

                if (!string.Equals(beat.enterWhen.type, conditionType, StringComparison.Ordinal) ||
                    !string.Equals(beat.enterWhen.targetId, targetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (HasOwnedAction(actions, beat.id, actionKind))
                    return true;
            }

            return false;
        }

        private static bool HasCustomerSpawnActionBeatForTargets(
            FlowBeatDefinition[] beats,
            FlowActionDefinition[] actions,
            IEnumerable<string> acceptableTriggerIds,
            IEnumerable<string> expectedTargetIds,
            string conditionType)
        {
            var acceptableTriggerSet = new HashSet<string>(acceptableTriggerIds ?? new string[0], StringComparer.Ordinal);
            var expectedTargetSet = new HashSet<string>(expectedTargetIds ?? new string[0], StringComparer.Ordinal);
            if (acceptableTriggerSet.Count == 0 || expectedTargetSet.Count == 0)
                return false;

            var actualTargetSet = new HashSet<string>(StringComparer.Ordinal);
            FlowBeatDefinition[] safeBeats = beats ?? new FlowBeatDefinition[0];
            for (int i = 0; i < safeBeats.Length; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null || beat.enterWhen == null)
                    continue;

                if (!string.Equals(beat.enterWhen.type, conditionType, StringComparison.Ordinal) ||
                    !acceptableTriggerSet.Contains(beat.enterWhen.targetId ?? string.Empty))
                {
                    continue;
                }

                FlowActionDefinition[] ownedActions = GetOwnedActions(actions, beat.id);
                for (int actionIndex = 0; actionIndex < ownedActions.Length; actionIndex++)
                {
                    FlowActionDefinition action = ownedActions[actionIndex];
                    if (action == null || !string.Equals(action.kind, FlowActionKinds.CUSTOMER_SPAWN, StringComparison.Ordinal))
                        continue;

                    string targetId = action.payload != null && action.payload.customerSpawn != null && action.payload.customerSpawn.targetId != null
                        ? action.payload.customerSpawn.targetId.Trim()
                        : string.Empty;
                    if (!string.IsNullOrEmpty(targetId))
                        actualTargetSet.Add(targetId);
                }
            }

            return actualTargetSet.SetEquals(expectedTargetSet);
        }

        private static bool HasOwnedAction(FlowActionDefinition[] actions, string ownerBeatId, string actionKind)
        {
            return TryGetOwnedAction(actions, ownerBeatId, actionKind, out _);
        }

        private static string GetPrimaryOwnedActionId(FlowActionDefinition[] actions, string ownerBeatId, string actionKind)
        {
            return TryGetOwnedAction(actions, ownerBeatId, actionKind, out FlowActionDefinition action)
                ? (action.id ?? string.Empty).Trim()
                : string.Empty;
        }

        private static bool TryGetOwnedAction(
            FlowActionDefinition[] actions,
            string ownerBeatId,
            string actionKind,
            out FlowActionDefinition matchedAction)
        {
            matchedAction = null;
            FlowActionDefinition[] ownedActions = GetOwnedActions(actions, ownerBeatId);
            for (int i = 0; i < ownedActions.Length; i++)
            {
                FlowActionDefinition action = ownedActions[i];
                if (action == null || !string.Equals(action.kind, actionKind, StringComparison.Ordinal))
                    continue;

                matchedAction = action;
                return true;
            }

            return false;
        }

        private static FlowActionDefinition[] GetOwnedActions(FlowActionDefinition[] actions, string ownerBeatId)
        {
            string normalizedOwnerBeatId = ownerBeatId != null ? ownerBeatId.Trim() : string.Empty;
            var values = new List<FlowActionDefinition>();
            FlowActionDefinition[] safeActions = actions ?? new FlowActionDefinition[0];
            for (int i = 0; i < safeActions.Length; i++)
            {
                FlowActionDefinition action = safeActions[i];
                if (action == null ||
                    !string.Equals(action.ownerBeatId != null ? action.ownerBeatId.Trim() : string.Empty, normalizedOwnerBeatId, StringComparison.Ordinal))
                {
                    continue;
                }

                values.Add(action);
            }

            return values.ToArray();
        }

        private static IEnumerable<string> CollectCompletionSpawnCustomerTargetIds(ScenarioModelEffectDefinition[] completionEffects)
        {
            var values = new List<string>();
            ScenarioModelEffectDefinition[] safeEffects = completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null || !string.Equals(effect.kind, PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                    continue;

                string targetId = effect.targetObjectId != null ? effect.targetObjectId.Trim() : string.Empty;
                if (!string.IsNullOrEmpty(targetId))
                    values.Add(IntentAuthoringUtility.BuildSpawnKey(targetId));
            }

            return values;
        }

        private static void ValidateFacilityAcceptedItems(
            PlayableScenarioModel model,
            FacilityAcceptedItemDefinition[] actualDefinitions,
            IntentAuditValidationResult result)
        {
            Dictionary<string, string> spawnKeys = BuildSpawnKeyLookup(model != null ? model.objects : null);
            HashSet<string> expectedKeys = BuildFacilityAcceptedItemKeySet(IntentAuthoringUtility.BuildFacilityAcceptedItems(model, spawnKeys, result.Errors));
            HashSet<string> actualKeys = BuildFacilityAcceptedItemKeySet(actualDefinitions);

            foreach (string key in expectedKeys)
            {
                if (!actualKeys.Contains(key))
                    Fail(result, "facilityAcceptedItems가 scenario model objective와 같은 값으로 lowering되지 않았습니다: '" + key + "'.");
            }

            foreach (string key in actualKeys)
            {
                if (!expectedKeys.Contains(key))
                    Fail(result, "facilityAcceptedItems에 scenario model objective에 없는 entry '" + key + "'가 있습니다.");
            }
        }

        private static void ValidateFacilityOutputItems(
            PlayableScenarioModel model,
            FacilityOutputItemDefinition[] actualDefinitions,
            IntentAuditValidationResult result)
        {
            Dictionary<string, string> spawnKeys = BuildSpawnKeyLookup(model != null ? model.objects : null);
            HashSet<string> expectedKeys = BuildFacilityOutputItemKeySet(IntentAuthoringUtility.BuildFacilityOutputItems(model, spawnKeys, result.Errors));
            HashSet<string> actualKeys = BuildFacilityOutputItemKeySet(actualDefinitions);

            foreach (string key in expectedKeys)
            {
                if (!actualKeys.Contains(key))
                    Fail(result, "facilityOutputItems가 scenario model objective와 같은 값으로 lowering되지 않았습니다: '" + key + "'.");
            }

            foreach (string key in actualKeys)
            {
                if (!expectedKeys.Contains(key))
                    Fail(result, "facilityOutputItems에 scenario model objective에 없는 entry '" + key + "'가 있습니다.");
            }
        }

        private static void ValidateItemPrices(
            ScenarioModelSaleValueDefinition[] saleValues,
            ItemPriceDefinition[] itemPrices,
            IntentAuditValidationResult result)
        {
            var priceByItemKey = new Dictionary<string, int>(StringComparer.Ordinal);
            ItemPriceDefinition[] safeItemPrices = itemPrices ?? new ItemPriceDefinition[0];
            for (int i = 0; i < safeItemPrices.Length; i++)
            {
                ItemPriceDefinition value = safeItemPrices[i];
                string itemKey = ItemRefUtility.ToStableKey(value != null ? value.item : null);
                if (value == null || string.IsNullOrWhiteSpace(itemKey))
                    continue;

                priceByItemKey[itemKey] = value.price;
            }

            ScenarioModelSaleValueDefinition[] safeSaleValues = saleValues ?? new ScenarioModelSaleValueDefinition[0];
            for (int i = 0; i < safeSaleValues.Length; i++)
            {
                ScenarioModelSaleValueDefinition value = safeSaleValues[i];
                string itemKey = ItemRefUtility.ToStableKey(value != null ? value.item : null);
                if (value == null || string.IsNullOrWhiteSpace(itemKey))
                    continue;

                if (!priceByItemKey.TryGetValue(itemKey, out int price) || price != value.amount)
                    Fail(result, "saleValues[" + i + "]가 compiled itemPrices[]에 같은 값으로 lowering되지 않았습니다.");
            }
        }

        private static void ValidatePlayerOptions(
            PlayableScenarioPlayerOptions expected,
            PlayableScenarioPlayerOptions actual,
            IntentAuditValidationResult result)
        {
            if (expected.itemStacker.maxCount != actual.itemStacker.maxCount ||
                expected.itemStacker.popIntervalSeconds != actual.itemStacker.popIntervalSeconds)
            {
                Fail(result, "playerOptions가 scenario model과 같은 값으로 lowering되지 않았습니다.");
            }
        }

        private static void ValidateFacilityOptions(
            ScenarioModelObjectDefinition[] objects,
            PlayableScenarioFacilityOptionDefinition[] actualDefinitions,
            IntentAuditValidationResult result)
        {
            Dictionary<string, PlayableScenarioFacilityOptions> expectedByFacilityId = BuildFacilityOptionsLookup(objects);
            Dictionary<string, PlayableScenarioFacilityOptions> actualByFacilityId = BuildFacilityOptionsLookup(actualDefinitions);

            foreach (KeyValuePair<string, PlayableScenarioFacilityOptions> pair in expectedByFacilityId)
            {
                if (!actualByFacilityId.TryGetValue(pair.Key, out PlayableScenarioFacilityOptions actual) ||
                    !FacilityOptionsEqual(pair.Value, actual))
                {
                    Fail(result, "facilityOptions가 scenario model과 같은 값으로 lowering되지 않았습니다: '" + pair.Key + "'.");
                }
            }

            foreach (KeyValuePair<string, PlayableScenarioFacilityOptions> pair in actualByFacilityId)
            {
                if (!expectedByFacilityId.ContainsKey(pair.Key))
                    Fail(result, "facilityOptions에 scenario model object에 없는 facilityId '" + pair.Key + "'가 있습니다.");
            }
        }

        private static void ValidateSpawnStartState(
            ScenarioModelObjectDefinition[] objects,
            CompiledSpawnData[] spawns,
            IntentAuditValidationResult result)
        {
            var spawnBySpawnKey = new Dictionary<string, CompiledSpawnData>(StringComparer.Ordinal);
            CompiledSpawnData[] safeSpawns = spawns ?? new CompiledSpawnData[0];
            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData value = safeSpawns[i];
                if (value == null || string.IsNullOrWhiteSpace(value.spawnKey))
                    continue;

                spawnBySpawnKey[value.spawnKey.Trim()] = value;
            }

            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = IntentAuthoringUtility.Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId))
                    continue;

                string spawnKey = IntentAuthoringUtility.BuildSpawnKey(objectId);
                if (!spawnBySpawnKey.TryGetValue(spawnKey, out CompiledSpawnData spawn) || spawn == null)
                    continue;

                bool expectedStartActive = value.startsPresent && value.startsActive;
                if (spawn.startActive != expectedStartActive)
                    Fail(result, "objects[" + i + "]의 lifecycle이 spawn.startActive에 올바르게 lowering되지 않았습니다.");
            }
        }

        private static void ValidateSpawnPositions(
            ScenarioModelObjectDefinition[] objects,
            CompiledSpawnData[] spawns,
            LayoutSpecDocument layoutSpec,
            IntentAuditValidationResult result)
        {
            var spawnBySpawnKey = new Dictionary<string, CompiledSpawnData>(StringComparer.Ordinal);
            Dictionary<string, SerializableVector3> expectedPositions = LayoutSpecGeometryUtility.BuildPositionLookup(objects, layoutSpec, result.Errors);
            CompiledSpawnData[] safeSpawns = spawns ?? new CompiledSpawnData[0];
            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData value = safeSpawns[i];
                if (value == null || string.IsNullOrWhiteSpace(value.spawnKey))
                    continue;

                spawnBySpawnKey[value.spawnKey.Trim()] = value;
            }

            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = IntentAuthoringUtility.Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId))
                    continue;

                string spawnKey = IntentAuthoringUtility.BuildSpawnKey(objectId);
                if (!spawnBySpawnKey.TryGetValue(spawnKey, out CompiledSpawnData spawn) || spawn == null)
                    continue;

                if (!expectedPositions.TryGetValue(objectId, out SerializableVector3 expectedPosition))
                {
                    Fail(result, "objects[" + i + "]의 placement expected position을 계산하지 못했습니다.");
                    continue;
                }

                if (spawn.localPosition.x != expectedPosition.x || spawn.localPosition.z != expectedPosition.z)
                {
                    Fail(result, "objects[" + i + "]의 placement가 spawn.localPosition에 올바르게 lowering되지 않았습니다.");
                }
            }
        }

        private static void ValidatePhysicsAreaStartState(
            ScenarioModelObjectDefinition[] objects,
            CompiledPhysicsAreaDefinition[] physicsAreas,
            IntentAuditValidationResult result)
        {
            var physicsAreaBySpawnKey = new Dictionary<string, CompiledPhysicsAreaDefinition>(StringComparer.Ordinal);
            CompiledPhysicsAreaDefinition[] safePhysicsAreas = physicsAreas ?? new CompiledPhysicsAreaDefinition[0];
            for (int i = 0; i < safePhysicsAreas.Length; i++)
            {
                CompiledPhysicsAreaDefinition value = safePhysicsAreas[i];
                if (value == null || string.IsNullOrWhiteSpace(value.spawnKey))
                    continue;

                physicsAreaBySpawnKey[value.spawnKey.Trim()] = value;
            }

            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = IntentAuthoringUtility.Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) ||
                    !string.Equals(IntentAuthoringUtility.Normalize(value != null ? value.role : string.Empty), PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
                {
                    continue;
                }

                string spawnKey = IntentAuthoringUtility.BuildSpawnKey(objectId);
                if (!physicsAreaBySpawnKey.TryGetValue(spawnKey, out CompiledPhysicsAreaDefinition definition) || definition == null)
                    continue;

                bool expectedStartActive = value.startsPresent && value.startsActive;
                if (definition.startActive != expectedStartActive)
                    Fail(result, "objects[" + i + "] physics_area lifecycle이 compiled physicsAreas.startActive에 올바르게 lowering되지 않았습니다.");
            }
        }

        private static void ValidatePhysicsAreaPositions(
            ScenarioModelObjectDefinition[] objects,
            CompiledPhysicsAreaDefinition[] physicsAreas,
            LayoutSpecDocument layoutSpec,
            IntentAuditValidationResult result)
        {
            var physicsAreaBySpawnKey = new Dictionary<string, CompiledPhysicsAreaDefinition>(StringComparer.Ordinal);
            CompiledPhysicsAreaDefinition[] safePhysicsAreas = physicsAreas ?? new CompiledPhysicsAreaDefinition[0];
            for (int i = 0; i < safePhysicsAreas.Length; i++)
            {
                CompiledPhysicsAreaDefinition value = safePhysicsAreas[i];
                if (value == null || string.IsNullOrWhiteSpace(value.spawnKey))
                    continue;

                physicsAreaBySpawnKey[value.spawnKey.Trim()] = value;
            }

            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = IntentAuthoringUtility.Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) ||
                    !string.Equals(IntentAuthoringUtility.Normalize(value != null ? value.role : string.Empty), PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
                {
                    continue;
                }

                string spawnKey = IntentAuthoringUtility.BuildSpawnKey(objectId);
                if (!physicsAreaBySpawnKey.TryGetValue(spawnKey, out CompiledPhysicsAreaDefinition definition) || definition == null)
                    continue;

                if (!LayoutSpecGeometryUtility.TryGetPlacement(layoutSpec, objectId, out LayoutSpecPlacementEntry placement) ||
                    placement == null ||
                    !placement.hasWorldPosition)
                {
                    Fail(result, "layoutSpec.placements에 physics_area '" + objectId + "'의 world position이 없습니다.");
                    continue;
                }

                if (definition.localPosition.x != placement.worldX || definition.localPosition.z != placement.worldZ)
                    Fail(result, "objects[" + i + "] physics_area placement가 compiled physicsAreas.localPosition에 올바르게 lowering되지 않았습니다.");
            }
        }

        private static List<string> BuildEntrySpawnCustomerTargetIds(
            ScenarioModelEffectDefinition[] entryEffects,
            string timing)
        {
            var targetIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string normalizedTiming = timing != null ? timing.Trim() : string.Empty;
            ScenarioModelEffectDefinition[] effects = entryEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null ||
                    !string.Equals(effect.kind, PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                {
                    continue;
                }

                string effectTiming = effect.timing != null ? effect.timing.Trim() : string.Empty;
                if (!string.Equals(effectTiming, normalizedTiming, StringComparison.Ordinal))
                    continue;

                string targetId = BuildUnlockerId(effect.targetObjectId);
                if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId))
                    continue;

                targetIds.Add(targetId);
            }

            return targetIds;
        }

        private static bool HasEntryFocusBeforeSpawnCustomerArrival(ScenarioModelEffectDefinition[] entryEffects)
        {
            ScenarioModelEffectDefinition[] effects = entryEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null ||
                    !string.Equals(effect.kind != null ? effect.kind.Trim() : string.Empty, PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                    continue;
                string timing = effect.timing != null ? effect.timing.Trim() : string.Empty;
                if (!string.Equals(timing, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
                    continue;
                if (IntentAuthoringUtility.HasEntryFocusBeforeIndex(effects, i))
                    return true;
            }
            return false;
        }

        private static bool HasRevealOrSystemActionEffect(ScenarioModelEffectDefinition[] effects)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                string kind = safeEffects[i] != null ? safeEffects[i].kind : string.Empty;
                if (string.Equals(kind, PromptIntentEffectKinds.REVEAL_OBJECT, StringComparison.Ordinal) ||
                    string.Equals(kind, PromptIntentEffectKinds.ACTIVATE_OBJECT, StringComparison.Ordinal) ||
                    string.Equals(kind, PromptIntentEffectKinds.REVEAL_ENDCARD, StringComparison.Ordinal) ||
                    string.Equals(kind, PromptIntentEffectKinds.END_GAME, StringComparison.Ordinal) ||
                    string.Equals(kind, PromptIntentEffectKinds.HIDE_GUIDE, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static ScenarioModelObjectiveDefinition FindUnlockObjective(ScenarioModelObjectiveDefinition[] objectives)
        {
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = safeObjectives[i];
                if (objective != null &&
                    string.Equals(objective.kind, PromptIntentObjectiveKinds.UNLOCK_OBJECT, StringComparison.Ordinal))
                {
                    return objective;
                }
            }

            return null;
        }

        private static ScenarioModelStageDefinition FindStageById(ScenarioModelStageDefinition[] stages, string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                return null;

            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage != null &&
                    string.Equals(stage.id != null ? stage.id.Trim() : string.Empty, stageId, StringComparison.Ordinal))
                {
                    return stage;
                }
            }

            return null;
        }

        private static HashSet<string> BuildExpectedUnlockTargetKeys(ScenarioModelEffectDefinition[] effects)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    string.Equals(effect.kind, PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal) ||
                    string.Equals(effect.kind, PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                {
                    continue;
                }

                string targetKey = BuildExpectedActivationTargetKey(effect);
                if (!string.IsNullOrEmpty(targetKey))
                    keys.Add(targetKey);
            }

            return keys;
        }

        private static string BuildExpectedActivationTargetKey(ScenarioModelEffectDefinition effect)
        {
            if (effect == null)
                return string.Empty;

            string kind = effect.kind ?? string.Empty;
            if (string.Equals(kind, PromptIntentEffectKinds.REVEAL_OBJECT, StringComparison.Ordinal) ||
                string.Equals(kind, PromptIntentEffectKinds.ACTIVATE_OBJECT, StringComparison.Ordinal))
            {
                string targetObjectId = effect.targetObjectId != null ? effect.targetObjectId.Trim() : string.Empty;
                return string.IsNullOrEmpty(targetObjectId)
                    ? string.Empty
                    : ActivationTargetKinds.SCENE_REF + ":" + BuildUnlockerId(targetObjectId);
            }

            if (string.Equals(kind, PromptIntentEffectKinds.REVEAL_ENDCARD, StringComparison.Ordinal) ||
                string.Equals(kind, PromptIntentEffectKinds.END_GAME, StringComparison.Ordinal) ||
                string.Equals(kind, PromptIntentEffectKinds.HIDE_GUIDE, StringComparison.Ordinal))
            {
                return ActivationTargetKinds.SYSTEM_ACTION + ":" + IntentAuthoringUtility.BuildRuntimeSystemActionTargetId(kind, string.Empty);
            }

            return string.Empty;
        }

        private static HashSet<string> BuildActivationTargetKeySet(ActivationTargetDefinition[] targets)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            ActivationTargetDefinition[] safeTargets = targets ?? new ActivationTargetDefinition[0];
            for (int i = 0; i < safeTargets.Length; i++)
            {
                ActivationTargetDefinition target = safeTargets[i];
                if (target == null)
                    continue;

                string kind = target.kind != null ? target.kind.Trim() : string.Empty;
                string id = target.id != null ? target.id.Trim() : string.Empty;
                if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
                    continue;

                keys.Add(kind + ":" + id);
            }

            return keys;
        }

        private static bool SetEquals(HashSet<string> left, HashSet<string> right)
        {
            if (left == null || right == null)
                return left == right;

            if (left.Count != right.Count)
                return false;

            foreach (string value in left)
            {
                if (!right.Contains(value))
                    return false;
            }

            return true;
        }

        private static string ResolveLastBeatId(ScenarioModelStageDefinition stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return string.Empty;

            string lastCompletionGuideBeatId = ResolveLastCompletionGuideBeatId(stage);
            if (!string.IsNullOrEmpty(lastCompletionGuideBeatId))
                return lastCompletionGuideBeatId;

            string stageId = stage.id.Trim();
            ScenarioModelEffectDefinition[] completionEffects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = completionEffects.Length - 1; i >= 0; i--)
            {
                ScenarioModelEffectDefinition effect = completionEffects[i];
                if (effect == null ||
                    !string.Equals(effect.kind, PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                {
                    continue;
                }

                return stageId + "__completion_focus_" + i.ToString("00");
            }

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            if (objectives.Length > 0)
                return stageId + "__objective_" + (objectives.Length - 1).ToString("00");

            string lastEntrySetupBeatId = ResolveLastEntrySetupBeatId(stage);
            if (!string.IsNullOrEmpty(lastEntrySetupBeatId))
                return lastEntrySetupBeatId;

            return string.Empty;
        }

        private static string ResolveLastEntrySetupBeatId(ScenarioModelStageDefinition stage)
        {
            string lastEntryGuideBeatId = ResolveLastEntryGuideBeatId(stage);
            if (!string.IsNullOrEmpty(lastEntryGuideBeatId))
                return lastEntryGuideBeatId;

            return ResolveLastEntryFocusBeatId(stage);
        }

        private static string ResolveLastEntryGuideBeatId(ScenarioModelStageDefinition stage)
        {
            List<string> guideBeatIds = BuildEntryGuideBeatIds(stage);
            return guideBeatIds.Count == 0 ? string.Empty : guideBeatIds[guideBeatIds.Count - 1];
        }

        private static string ResolveLastCompletionGuideBeatId(ScenarioModelStageDefinition stage)
        {
            List<string> guideBeatIds = BuildCompletionGuideBeatIds(stage);
            return guideBeatIds.Count == 0 ? string.Empty : guideBeatIds[guideBeatIds.Count - 1];
        }

        private static string BuildUnlockerId(string targetObjectId)
        {
            string normalizedTargetObjectId = IntentAuthoringUtility.Normalize(targetObjectId);
            return IntentAuthoringUtility.BuildSpawnKey(normalizedTargetObjectId);
        }

        private static Dictionary<string, string> BuildSpawnKeyLookup(ScenarioModelObjectDefinition[] objects)
        {
            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = IntentAuthoringUtility.Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) || lookup.ContainsKey(objectId))
                    continue;

                lookup.Add(objectId, IntentAuthoringUtility.BuildSpawnKey(objectId));
            }

            return lookup;
        }

        private static Dictionary<string, PlayableScenarioFacilityOptions> BuildFacilityOptionsLookup(ScenarioModelObjectDefinition[] objects)
        {
            Dictionary<string, string> spawnKeys = BuildSpawnKeyLookup(objects);
            var lookup = new Dictionary<string, PlayableScenarioFacilityOptions>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string role = IntentAuthoringUtility.Normalize(value.role);
                if (!string.Equals(role, PromptIntentObjectRoles.SELLER, StringComparison.Ordinal) &&
                    !string.Equals(role, PromptIntentObjectRoles.PROCESSOR, StringComparison.Ordinal) &&
                    !string.Equals(role, PromptIntentObjectRoles.GENERATOR, StringComparison.Ordinal))
                {
                    continue;
                }

                string objectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.IsNullOrEmpty(objectId) || !spawnKeys.TryGetValue(objectId, out string facilityId))
                    continue;

                lookup[facilityId] = value.facilityOptions;
            }

            return lookup;
        }

        private static Dictionary<string, PlayableScenarioFacilityOptions> BuildFacilityOptionsLookup(PlayableScenarioFacilityOptionDefinition[] definitions)
        {
            var lookup = new Dictionary<string, PlayableScenarioFacilityOptions>(StringComparer.Ordinal);
            PlayableScenarioFacilityOptionDefinition[] safeDefinitions = definitions ?? new PlayableScenarioFacilityOptionDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                PlayableScenarioFacilityOptionDefinition definition = safeDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.facilityId))
                    continue;

                lookup[definition.facilityId.Trim()] = definition.options;
            }

            return lookup;
        }

        private static bool FacilityOptionsEqual(PlayableScenarioFacilityOptions expected, PlayableScenarioFacilityOptions actual)
        {
            return expected.conversionInterval == actual.conversionInterval &&
                   expected.inputCountPerConversion == actual.inputCountPerConversion &&
                   expected.inputItemMoveInterval == actual.inputItemMoveInterval &&
                   expected.playerDropInterval == actual.playerDropInterval &&
                   expected.playerGetItemInterval == actual.playerGetItemInterval &&
                   expected.spawnInterval == actual.spawnInterval &&
                   expected.customerSellingInterval == actual.customerSellingInterval &&
                   expected.costPerMoneyPile == actual.costPerMoneyPile &&
                   expected.customerReqMin == actual.customerReqMin &&
                   expected.customerReqMax == actual.customerReqMax &&
                   expected.itemStacker.maxCount == actual.itemStacker.maxCount &&
                   expected.itemStacker.popIntervalSeconds == actual.itemStacker.popIntervalSeconds &&
                   expected.moneyStacker.maxCount == actual.moneyStacker.maxCount &&
                   expected.moneyStacker.popIntervalSeconds == actual.moneyStacker.popIntervalSeconds;
        }

        private static HashSet<string> BuildFacilityAcceptedItemKeySet(FacilityAcceptedItemDefinition[] definitions)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            FacilityAcceptedItemDefinition[] safeDefinitions = definitions ?? new FacilityAcceptedItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FacilityAcceptedItemDefinition definition = safeDefinitions[i];
                string itemKey = ItemRefUtility.ToStableKey(definition != null ? definition.item : null);
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.facilityId) ||
                    string.IsNullOrWhiteSpace(itemKey))
                {
                    continue;
                }

                keys.Add(definition.facilityId.Trim() + "::" + itemKey + "::" + definition.laneIndex);
            }

            return keys;
        }

        private static HashSet<string> BuildFacilityOutputItemKeySet(FacilityOutputItemDefinition[] definitions)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            FacilityOutputItemDefinition[] safeDefinitions = definitions ?? new FacilityOutputItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FacilityOutputItemDefinition definition = safeDefinitions[i];
                string itemKey = ItemRefUtility.ToStableKey(definition != null ? definition.item : null);
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.facilityId) ||
                    string.IsNullOrWhiteSpace(itemKey))
                {
                    continue;
                }

                keys.Add(definition.facilityId.Trim() + "::" + itemKey);
            }

            return keys;
        }

        private static bool HasEffect(ScenarioModelEffectDefinition[] effects, string kind)
        {
            return CountEffects(effects, kind) > 0;
        }

        private static string ResolveFirstObjectiveBeatId(ScenarioModelStageDefinition stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return string.Empty;

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            string stageId = stage.id.Trim();
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] != null)
                    return stageId + "__objective_" + i.ToString("00");
            }

            return string.Empty;
        }

        private static bool FirstObjectiveUsesArrivalTiming(ScenarioModelStageDefinition stage)
        {
            if (stage == null)
                return false;

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < objectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = objectives[i];
                if (objective == null)
                    continue;

                return objective.absorbsArrow &&
                    string.Equals(ResolveArrowTiming(objective), PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal);
            }

            return false;
        }

        private static string ResolveArrowTiming(ScenarioModelObjectiveDefinition objective)
        {
            if (objective == null)
                return string.Empty;

            return PromptIntentCapabilityRegistry.ResolveArrowTiming(objective.arrowTiming, objective.arrowOnFocusArrival);
        }

        private static List<string> BuildObjectiveBeatIds(ScenarioModelStageDefinition stage)
        {
            var beatIds = new List<string>();
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return beatIds;

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            string stageId = stage.id.Trim();
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] == null)
                    continue;

                beatIds.Add(stageId + "__objective_" + i.ToString("00"));
            }

            return beatIds;
        }

        private static string ResolveLastEntryFocusBeatId(ScenarioModelStageDefinition stage)
        {
            List<string> focusBeatIds = BuildEntryFocusBeatIds(stage);
            return focusBeatIds.Count == 0 ? string.Empty : focusBeatIds[focusBeatIds.Count - 1];
        }

        private static List<string> BuildCompletionFocusBeatIds(ScenarioModelStageDefinition stage)
        {
            var beatIds = new List<string>();
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return beatIds;

            string stageId = stage.id.Trim();
            ScenarioModelEffectDefinition[] completionEffects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < completionEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = completionEffects[i];
                if (effect == null || !string.Equals(effect.kind, PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                    continue;

                beatIds.Add(stageId + "__completion_focus_" + i.ToString("00"));
            }

            return beatIds;
        }

        private static List<string> BuildCompletionGuideBeatIds(ScenarioModelStageDefinition stage)
        {
            var beatIds = new List<string>();
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return beatIds;

            string stageId = stage.id.Trim();
            ScenarioModelEffectDefinition[] completionEffects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < completionEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = completionEffects[i];
                if (effect == null || !string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal))
                    continue;

                beatIds.Add(stageId + "__completion_guide_" + i.ToString("00"));
            }

            return beatIds;
        }

        private static List<string> BuildEntryFocusBeatIds(ScenarioModelStageDefinition stage)
        {
            var beatIds = new List<string>();
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return beatIds;

            string stageId = stage.id.Trim();
            ScenarioModelEffectDefinition[] entryEffects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < entryEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = entryEffects[i];
                if (effect == null || !string.Equals(effect.kind, PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                    continue;

                beatIds.Add(stageId + "__focus_" + i.ToString("00"));
            }

            return beatIds;
        }

        private static List<string> BuildEntryGuideBeatIds(ScenarioModelStageDefinition stage)
        {
            var beatIds = new List<string>();
            if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                return beatIds;

            string stageId = stage.id.Trim();
            ScenarioModelEffectDefinition[] entryEffects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < entryEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = entryEffects[i];
                if (effect == null || !string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal))
                    continue;

                beatIds.Add(stageId + "__entry_guide_" + i.ToString("00"));
            }

            return beatIds;
        }

        private static bool IsFocusBeatId(string beatId)
        {
            string normalizedBeatId = beatId != null ? beatId.Trim() : string.Empty;
            return normalizedBeatId.IndexOf("__focus_", StringComparison.Ordinal) >= 0 ||
                   normalizedBeatId.IndexOf("__completion_focus_", StringComparison.Ordinal) >= 0;
        }

        private static int CountEffects(ScenarioModelEffectDefinition[] effects, string kind)
        {
            int count = 0;
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                if (string.Equals(safeEffects[i] != null ? safeEffects[i].kind : string.Empty, kind, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static string BuildActionCompletedTargetId(string beatId, FlowActionDefinition[] actions)
        {
            if (!IsFocusBeatId(beatId))
                return string.Empty;

            return GetPrimaryOwnedActionId(actions, beatId, FlowActionKinds.CAMERA_FOCUS);
        }

        private static bool HasOneOfEnterWhen(
            Dictionary<string, FlowBeatDefinition> beatById,
            string beatId,
            string actionTargetId,
            string beatTargetId)
        {
            bool matchesAction = !string.IsNullOrEmpty(actionTargetId) && HasActionCompletedEnterWhen(beatById, beatId, actionTargetId);
            bool matchesBeat = !string.IsNullOrEmpty(beatTargetId) && HasBeatCompletedEnterWhen(beatById, beatId, beatTargetId);
            return matchesAction || matchesBeat;
        }

        private static bool HasEntryArrivalSpawnCustomer(ScenarioModelEffectDefinition[] effects)
        {
            return CountSpawnCustomerWithTiming(effects, PromptIntentEffectTimingKinds.ARRIVAL) > 0;
        }

        private static bool HasEntryCompletedSpawnCustomer(ScenarioModelEffectDefinition[] effects)
        {
            return CountSpawnCustomerWithTiming(effects, PromptIntentEffectTimingKinds.COMPLETED) > 0 ||
                CountSpawnCustomerWithTiming(effects, string.Empty) > 0;
        }

        private static bool HasCompletionArrivalSpawnCustomer(ScenarioModelEffectDefinition[] effects)
        {
            return CountSpawnCustomerWithTiming(effects, PromptIntentEffectTimingKinds.ARRIVAL) > 0;
        }

        private static bool HasCompletionCompletedSpawnCustomer(ScenarioModelEffectDefinition[] effects)
        {
            return CountSpawnCustomerWithTiming(effects, PromptIntentEffectTimingKinds.COMPLETED) > 0 ||
                CountSpawnCustomerWithTiming(effects, string.Empty) > 0;
        }

        private static int CountSpawnCustomerWithTiming(ScenarioModelEffectDefinition[] effects, string timing)
        {
            int count = 0;
            string normalizedTiming = timing != null ? timing.Trim() : string.Empty;
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    !string.Equals(effect.kind, PromptIntentEffectKinds.SPAWN_CUSTOMER, StringComparison.Ordinal))
                {
                    continue;
                }

                string effectTiming = effect.timing != null ? effect.timing.Trim() : string.Empty;
                if (string.Equals(effectTiming, normalizedTiming, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static IntentAuditValidationResult FinalizeResult(IntentAuditValidationResult result)
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
                    result.FailureCode = PlayableFailureCode.IntentAuditFailed;
                result.Message = result.Errors[0];
            }

            return result;
        }

        private static IntentAuditValidationResult Fail(IntentAuditValidationResult result, string message)
        {
            return Fail(result, new ValidationIssueRecord(
                ValidationRuleId.INTENT_AUDIT_GENERIC,
                ValidationSeverity.Blocker,
                message,
                "IntentAudit"));
        }

        private static IntentAuditValidationResult Fail(IntentAuditValidationResult result, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return result;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.IntentAuditFailed;
            result.Errors.Add(issue.message ?? string.Empty);
            result.Issues ??= new List<ValidationIssueRecord>();
            result.Issues.Add(issue);
            return result;
        }
    }
}
