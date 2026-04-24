using System;
using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Generation.Editor.Compile
{
    public sealed class ScenarioModelLoweringResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public CompiledPlayablePlan Plan;
        public List<string> Errors = new List<string>();
    }

    internal sealed class LoweredStageState
    {
        public string StageId;
        public string PreviousStageId;
        public string FirstBeatId;
        public string LastBeatId;
        public ScenarioModelConditionDefinition EnterCondition;
        public List<FlowBeatDefinition> Beats = new List<FlowBeatDefinition>();
        public List<FlowActionDefinition> Actions = new List<FlowActionDefinition>();
        public List<RevealRuleDefinition> EntryRevealRules = new List<RevealRuleDefinition>();
        public List<RevealRuleDefinition> CompletionRevealRules = new List<RevealRuleDefinition>();
        public List<CustomerSpawnRuleDefinition> EntryCustomerSpawnRules = new List<CustomerSpawnRuleDefinition>();
        public List<CustomerSpawnRuleDefinition> CompletionCustomerSpawnRules = new List<CustomerSpawnRuleDefinition>();
        public UnlockDefinition UnlockDefinition;
    }

    public static class ScenarioModelLoweringCompiler
    {
        public static ScenarioModelLoweringResult Compile(PlayableScenarioModel model, PlayableObjectCatalog catalog)
        {
            return Compile(model, catalog, null);
        }

        public static ScenarioModelLoweringResult Compile(
            PlayableScenarioModel model,
            PlayableObjectCatalog catalog,
            LayoutSpecDocument layoutSpec)
        {
            var result = new ScenarioModelLoweringResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (model == null)
                return Fail(result, "PlayableScenarioModel이 null입니다.");
            if (catalog == null)
                return Fail(result, "PlayableObjectCatalog가 필요합니다.");
            if (layoutSpec == null)
                return Fail(result, "layoutSpec이 필요합니다. Step3 geometry 없이 lowering할 수 없습니다.");
            if (!ValidateCatalogContract(catalog, result))
                return FinalizeFailure(result);

            ScenarioModelObjectDefinition[] objects = model.objects ?? new ScenarioModelObjectDefinition[0];
            Dictionary<string, SerializableVector3> positions = LayoutSpecGeometryUtility.BuildPositionLookup(objects, layoutSpec, result.Errors);
            Dictionary<string, string> spawnKeys = BuildSpawnKeyLookup(objects);
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByObjectId = BuildSaleValueLookup(model.saleValues);
            FeatureOutputItemDefinition[] featureOutputItems = BuildFeatureOutputItems(model, spawnKeys, result);
            Dictionary<string, ItemRef> outputItemsByTargetId = BuildFeatureOutputItemLookup(featureOutputItems);
            Dictionary<string, LoweredStageState> stageStatesById = new Dictionary<string, LoweredStageState>(StringComparer.Ordinal);
            var loweredStages = new List<LoweredStageState>();

            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];
            string previousStageId = string.Empty;
            for (int i = 0; i < stages.Length; i++)
            {
                ScenarioModelStageDefinition stage = stages[i];
                if (stage == null)
                    continue;

                LoweredStageState loweredStage = LowerStage(stage, previousStageId, stageStatesById, spawnKeys, saleValuesByObjectId, outputItemsByTargetId, catalog, result);
                if (loweredStage == null)
                    continue;

                loweredStages.Add(loweredStage);
                if (!string.IsNullOrEmpty(loweredStage.StageId))
                    stageStatesById[loweredStage.StageId] = loweredStage;
                previousStageId = loweredStage.StageId;
            }

            FeatureAcceptedItemDefinition[] featureAcceptedItems = BuildFeatureAcceptedItems(model, spawnKeys, result);
            SellerRequestableItemRuleDefinition[] sellerRequestRules =
                BuildSellerRequestableItemRules(model.objects, spawnKeys, featureAcceptedItems, result);
            ItemPriceDefinition[] itemPrices = BuildItemPrices(model.saleValues);
            CompiledSpawnData[] spawns = BuildSpawns(objects, positions, layoutSpec, catalog, featureAcceptedItems, result);
            CompiledPhysicsAreaDefinition[] physicsAreas = BuildPhysicsAreas(objects, positions, layoutSpec, result);
            CompiledRailDefinition[] rails = BuildRails(objects, spawnKeys, layoutSpec, result);
            ObjectDesignSelectionDefinition[] objectDesigns = BuildObjectDesigns(spawns, featureAcceptedItems, featureOutputItems, itemPrices, catalog, result);
            ContentSelectionDefinition[] contentSelections = BuildContentSelections(model.contentSelections, catalog, result);
            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            var unlocks = new List<UnlockDefinition>();
            for (int i = 0; i < loweredStages.Count; i++)
            {
                LoweredStageState stage = loweredStages[i];
                if (stage.UnlockDefinition != null)
                    unlocks.Add(stage.UnlockDefinition);
            }

            FlowBeatDefinition[] loweredFlowBeats = BuildCompiledFlowBeats(loweredStages, sellerRequestRules, result, out FlowActionDefinition[] loweredFlowActions);

            // IntentAuditValidator uses stage index i into model.stages; stageFirstBeatIds must align with model.stages.
            var stageFirstBeatIds = new string[stages.Length];
            for (int i = 0; i < stages.Length; i++)
            {
                ScenarioModelStageDefinition stage = stages[i];
                if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                {
                    stageFirstBeatIds[i] = string.Empty;
                    continue;
                }
                if (stageStatesById.TryGetValue(stage.id.Trim(), out LoweredStageState lowered) && lowered != null)
                    stageFirstBeatIds[i] = lowered.FirstBeatId ?? string.Empty;
                else
                    stageFirstBeatIds[i] = string.Empty;
            }

            result.Plan = new CompiledPlayablePlan
            {
                flowSchemaVersion = CompiledPlayablePlan.FLOW_SCHEMA_VERSION,
                themeId = IntentAuthoringUtility.Normalize(model.themeId),
                objectDesigns = objectDesigns,
                contentSelections = contentSelections,
                spawns = spawns,
                physicsAreas = physicsAreas,
                rails = rails,
                currencies = BuildCurrencies(model.currencies),
                itemPrices = itemPrices,
                featureAcceptedItems = featureAcceptedItems,
                featureOutputItems = featureOutputItems,
                playerOptions = model.playerOptions,
                featureOptions = BuildFeatureOptions(model.objects, spawnKeys),
                unlocks = unlocks.ToArray(),
                beats = loweredFlowBeats,
                actions = loweredFlowActions,
                stageFirstBeatIds = stageFirstBeatIds,
            };
            result.IsValid = true;
            result.Message = "CompiledPlayablePlan lowering이 완료되었습니다.";
            return result;
        }

        private static ContentSelectionDefinition[] BuildContentSelections(
            ContentSelectionDefinition[] selections,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            ContentSelectionDefinition[] safeSelections = selections ?? new ContentSelectionDefinition[0];
            if (safeSelections.Length == 0)
                return new ContentSelectionDefinition[0];

            var compiledSelections = new ContentSelectionDefinition[safeSelections.Length];
            for (int i = 0; i < safeSelections.Length; i++)
            {
                ContentSelectionDefinition selection = safeSelections[i];
                if (selection == null)
                    continue;

                string objectId = IntentAuthoringUtility.Normalize(selection.objectId);
                string designId = IntentAuthoringUtility.Normalize(selection.designId);
                if (ContentSelectionRules.IsUnsetDesignId(designId))
                {
                    result.Errors.Add(
                        "contentSelections[" + i + "]의 objectId '" + objectId +
                        "'는 실제 카탈로그 design을 선택해야 합니다. '" + ContentSelectionRules.DESIGN_ID_NOT_SET + "'는 허용되지 않습니다.");
                    continue;
                }

                if (!catalog.TryResolveContentSelectionDesignIndex(objectId, designId, out int resolvedDesignIndex))
                {
                    result.Errors.Add(
                        "contentSelections[" + i + "]에서 objectId '" + objectId + "'의 designId '" + designId +
                        "'를 content selection design으로 해석하지 못했습니다.");
                    continue;
                }

                compiledSelections[i] = new ContentSelectionDefinition
                {
                    objectId = objectId,
                    designId = designId,
                    designIndex = resolvedDesignIndex,
                };
            }

            return compiledSelections;
        }

        private static FlowBeatDefinition[] BuildCompiledFlowBeats(
            List<LoweredStageState> loweredStages,
            SellerRequestableItemRuleDefinition[] sellerRequestRules,
            ScenarioModelLoweringResult result,
            out FlowActionDefinition[] compiledActions)
        {
            var beats = new List<FlowBeatDefinition>();
            var actions = new List<FlowActionDefinition>();
            List<LoweredStageState> safeStages = loweredStages ?? new List<LoweredStageState>();
            for (int i = 0; i < safeStages.Count; i++)
            {
                LoweredStageState stage = safeStages[i];
                if (stage == null)
                    continue;

                beats.AddRange(BuildStageBoundaryActionBeats(stage.StageId, "entry", stage.EntryRevealRules, stage.EntryCustomerSpawnRules, actions, result));

                List<FlowBeatDefinition> stageFlowBeats = stage.Beats ?? new List<FlowBeatDefinition>();
                for (int beatIndex = 0; beatIndex < stageFlowBeats.Count; beatIndex++)
                    beats.Add(PostBakePlayableSettingsUtility.CopyFlowBeats(new[] { stageFlowBeats[beatIndex] })[0]);

                List<FlowActionDefinition> stageActions = stage.Actions ?? new List<FlowActionDefinition>();
                for (int actionIndex = 0; actionIndex < stageActions.Count; actionIndex++)
                {
                    FlowActionDefinition copiedAction = PostBakePlayableSettingsUtility.CopyFlowActions(new[] { stageActions[actionIndex] })[0];
                    if (copiedAction != null)
                        actions.Add(copiedAction);
                }

                beats.AddRange(BuildStageBoundaryActionBeats(stage.StageId, "completion", stage.CompletionRevealRules, stage.CompletionCustomerSpawnRules, actions, result));
            }

            AttachSellerRequestActions(beats, actions, sellerRequestRules, result);
            compiledActions = actions.ToArray();
            return beats.ToArray();
        }

        private static List<FlowBeatDefinition> BuildStageBoundaryActionBeats(
            string stageId,
            string phase,
            List<RevealRuleDefinition> revealEffectRules,
            List<CustomerSpawnRuleDefinition> customerSpawnEffectRules,
            List<FlowActionDefinition> compiledActions,
            ScenarioModelLoweringResult result)
        {
            var beats = new List<FlowBeatDefinition>();
            RevealRuleDefinition[] safeRevealRules = revealEffectRules != null ? revealEffectRules.ToArray() : new RevealRuleDefinition[0];
            for (int i = 0; i < safeRevealRules.Length; i++)
            {
                RevealRuleDefinition rule = safeRevealRules[i];
                if (rule == null)
                    continue;

                beats.Add(BuildSyntheticActionBeatFromReactiveRule(
                    BuildSyntheticFlowBeatId(stageId, phase, "reveal", i),
                    rule.when,
                    new FlowActionDefinition
                    {
                        id = "reveal",
                        kind = FlowActionKinds.REVEAL,
                        payload = new FlowActionPayloadDefinition
                        {
                            reveal = new RevealActionPayload
                            {
                                targets = PostBakePlayableSettingsUtility.CopyTargets(rule.targets),
                            },
                        },
                    },
                    compiledActions,
                    result));
            }

            CustomerSpawnRuleDefinition[] safeCustomerRules = customerSpawnEffectRules != null ? customerSpawnEffectRules.ToArray() : new CustomerSpawnRuleDefinition[0];
            for (int i = 0; i < safeCustomerRules.Length; i++)
            {
                CustomerSpawnRuleDefinition rule = safeCustomerRules[i];
                if (rule == null)
                    continue;

                beats.Add(BuildSyntheticActionBeatFromReactiveRule(
                    BuildSyntheticFlowBeatId(stageId, phase, "customer_spawn", i),
                    rule.startWhen,
                    new FlowActionDefinition
                    {
                        id = "customer_spawn",
                        kind = FlowActionKinds.CUSTOMER_SPAWN,
                        payload = new FlowActionPayloadDefinition
                        {
                            customerSpawn = new CustomerSpawnActionPayload
                            {
                                targetId = IntentAuthoringUtility.Normalize(rule.targetId),
                                customerDesignIndex = rule.customerDesignIndex,
                            },
                        },
                    },
                    compiledActions,
                    result));
            }

            return beats;
        }

        private static FlowBeatDefinition BuildSyntheticActionBeatFromReactiveRule(
            string beatId,
            ReactiveConditionGroupDefinition trigger,
            FlowActionDefinition action,
            List<FlowActionDefinition> compiledActions,
            ScenarioModelLoweringResult result)
        {
            FlowActionDefinition normalizedAction = PostBakePlayableSettingsUtility.CopyFlowActions(new[] { action })[0] ?? new FlowActionDefinition();
            if (!TryMapReactiveTriggerToSyntheticBeat(
                    trigger,
                    out StepConditionDefinition enterWhen,
                    out string actionTriggerMode,
                    out ReactiveConditionGroupDefinition actionWhen,
                    out string mappingError))
            {
                result.Errors.Add(mappingError);
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
                enterWhen = new StepConditionDefinition { type = StepConditionRules.ALWAYS };
                actionTriggerMode = FlowActionTriggerModes.ON_BEAT_ENTER;
                actionWhen = new ReactiveConditionGroupDefinition();
            }

            normalizedAction.ownerBeatId = beatId;
            normalizedAction.triggerMode = actionTriggerMode;
            normalizedAction.when = actionWhen ?? new ReactiveConditionGroupDefinition();
            normalizedAction.id = MakeUniqueFlowActionId(compiledActions, BuildBeatScopedFlowActionId(beatId, normalizedAction.id, normalizedAction.kind));
            compiledActions.Add(normalizedAction);

            return new FlowBeatDefinition
            {
                id = beatId,
                enterWhen = enterWhen ?? new StepConditionDefinition(),
                completeWhen = BuildActionCompletedShowWhen(normalizedAction.id),
            };
        }

        private static void AttachSellerRequestActions(
            List<FlowBeatDefinition> beats,
            List<FlowActionDefinition> compiledActions,
            SellerRequestableItemRuleDefinition[] sellerRequestRules,
            ScenarioModelLoweringResult result)
        {
            List<FlowBeatDefinition> safeBeats = beats ?? new List<FlowBeatDefinition>();
            SellerRequestableItemRuleDefinition[] safeRules = sellerRequestRules ?? new SellerRequestableItemRuleDefinition[0];
            for (int i = 0; i < safeRules.Length; i++)
            {
                SellerRequestableItemRuleDefinition rule = safeRules[i];
                if (rule == null)
                    continue;

                FlowActionDefinition action = new FlowActionDefinition
                {
                    id = "seller_request",
                    kind = FlowActionKinds.SELLER_REQUEST,
                    payload = new FlowActionPayloadDefinition
                    {
                        sellerRequest = new SellerRequestActionPayload
                        {
                            targetId = IntentAuthoringUtility.Normalize(rule.targetId),
                            item = ItemRefUtility.Clone(rule.item),
                        },
                    },
                };

                if (TryAttachSellerRequestActionToMatchingBeat(safeBeats, compiledActions, rule.startWhen, action))
                    continue;

                safeBeats.Add(BuildSyntheticActionBeatFromReactiveRule(
                    BuildSyntheticFlowBeatId("synthetic", "seller", "request", i),
                    rule.startWhen,
                    action,
                    compiledActions,
                    result));
            }
        }

        private static bool TryAttachSellerRequestActionToMatchingBeat(
            List<FlowBeatDefinition> beats,
            List<FlowActionDefinition> compiledActions,
            ReactiveConditionGroupDefinition trigger,
            FlowActionDefinition action)
        {
            ReactiveConditionDefinition[] conditions = trigger != null ? trigger.conditions ?? new ReactiveConditionDefinition[0] : new ReactiveConditionDefinition[0];
            if (conditions.Length != 1)
                return false;

            StepConditionDefinition condition = ConvertReactiveConditionToStepCondition(conditions[0]);
            if (condition == null)
                return false;

            List<FlowBeatDefinition> safeBeats = beats ?? new List<FlowBeatDefinition>();
            if (string.Equals(IntentAuthoringUtility.Normalize(condition.type), StepConditionRules.ALWAYS, StringComparison.Ordinal))
            {
                if (safeBeats.Count == 0)
                    return false;

                if (HasReactiveDelay(trigger))
                {
                    AppendActionToBeat(
                        safeBeats[0],
                        compiledActions,
                        action,
                        FlowActionTriggerModes.REACTIVE,
                        PostBakePlayableSettingsUtility.CopyReactiveConditionGroup(trigger));
                }
                else
                {
                    AppendActionToBeat(safeBeats[0], compiledActions, action, FlowActionTriggerModes.ON_BEAT_ENTER, null);
                }

                return true;
            }

            for (int i = 0; i < safeBeats.Count; i++)
            {
                FlowBeatDefinition beat = safeBeats[i];
                if (beat == null)
                    continue;

                if (StepConditionsEqual(beat.enterWhen, condition))
                {
                    if (HasReactiveDelay(trigger))
                    {
                        AppendActionToBeat(
                            beat,
                            compiledActions,
                            action,
                            FlowActionTriggerModes.REACTIVE,
                            PostBakePlayableSettingsUtility.CopyReactiveConditionGroup(trigger));
                    }
                    else
                    {
                        AppendActionToBeat(beat, compiledActions, action, FlowActionTriggerModes.ON_BEAT_ENTER, null);
                    }

                    return true;
                }

                if (StepConditionsEqual(beat.completeWhen, condition))
                {
                    if (HasReactiveDelay(trigger))
                    {
                        AppendActionToBeat(
                            beat,
                            compiledActions,
                            action,
                            FlowActionTriggerModes.REACTIVE,
                            PostBakePlayableSettingsUtility.CopyReactiveConditionGroup(trigger));
                    }
                    else
                    {
                        AppendActionToBeat(beat, compiledActions, action, FlowActionTriggerModes.ON_BEAT_COMPLETE, null);
                    }

                    return true;
                }
            }

            return false;
        }

        private static void AppendActionToBeat(
            FlowBeatDefinition beat,
            List<FlowActionDefinition> compiledActions,
            FlowActionDefinition action,
            string triggerMode,
            ReactiveConditionGroupDefinition when)
        {
            if (beat == null || action == null)
                return;

            FlowActionDefinition copied = PostBakePlayableSettingsUtility.CopyFlowActions(new[] { action })[0] ?? new FlowActionDefinition();
            copied.ownerBeatId = IntentAuthoringUtility.Normalize(beat.id);
            copied.triggerMode = IntentAuthoringUtility.Normalize(triggerMode);
            copied.when = when != null
                ? PostBakePlayableSettingsUtility.CopyReactiveConditionGroup(when)
                : new ReactiveConditionGroupDefinition();
            copied.id = MakeUniqueFlowActionId(compiledActions, BuildBeatScopedFlowActionId(beat.id, copied.id, copied.kind));
            compiledActions.Add(copied);
        }

        private static string BuildSyntheticFlowBeatId(string stageId, string phase, string kind, int index)
        {
            return "flow_rule__" +
                   IntentAuthoringUtility.Normalize(stageId) + "__" +
                   IntentAuthoringUtility.Normalize(phase) + "__" +
                   IntentAuthoringUtility.Normalize(kind) + "_" +
                   index.ToString("00");
        }

        private static bool TryMapReactiveTriggerToSyntheticBeat(
            ReactiveConditionGroupDefinition trigger,
            out StepConditionDefinition enterWhen,
            out string actionTriggerMode,
            out ReactiveConditionGroupDefinition actionWhen,
            out string errorMessage)
        {
            enterWhen = new StepConditionDefinition { type = StepConditionRules.ALWAYS };
            actionTriggerMode = FlowActionTriggerModes.ON_BEAT_ENTER;
            actionWhen = new ReactiveConditionGroupDefinition();
            errorMessage = string.Empty;

            ReactiveConditionGroupDefinition safeTrigger = PostBakePlayableSettingsUtility.CopyReactiveConditionGroup(trigger);
            ReactiveConditionDefinition[] sourceConditions = safeTrigger.conditions ?? new ReactiveConditionDefinition[0];
            var conditions = new List<ReactiveConditionDefinition>();
            for (int i = 0; i < sourceConditions.Length; i++)
            {
                if (sourceConditions[i] != null)
                    conditions.Add(PostBakePlayableSettingsUtility.CopyReactiveCondition(sourceConditions[i]));
            }

            if (conditions.Count == 0)
                return true;

            if (conditions.Count == 1)
            {
                StepConditionDefinition mappedCondition =
                    ConvertReactiveConditionToStepCondition(conditions[0]) ?? new StepConditionDefinition { type = StepConditionRules.ALWAYS };
                enterWhen = mappedCondition;
                if (!HasReactiveDelay(safeTrigger))
                    return true;

                actionTriggerMode = FlowActionTriggerModes.REACTIVE;
                actionWhen = new ReactiveConditionGroupDefinition
                {
                    mode = string.IsNullOrWhiteSpace(safeTrigger.mode) ? ReactiveConditionRules.MODE_ALL : safeTrigger.mode.Trim(),
                    delaySeconds = safeTrigger.delaySeconds,
                    conditions = new[] { PostBakePlayableSettingsUtility.CopyReactiveCondition(conditions[0]) },
                };
                return true;
            }

            int primaryIndex = FindPrimaryTriggerConditionIndex(conditions);
            ReactiveConditionDefinition primary = conditions[primaryIndex];
            conditions.RemoveAt(primaryIndex);
            enterWhen = ConvertReactiveConditionToStepCondition(primary) ?? new StepConditionDefinition { type = StepConditionRules.ALWAYS };

            if (conditions.Count != 1)
            {
                errorMessage = "beat-centric compile은 다중 reactive trigger를 지원하지 않습니다.";
                return false;
            }

            actionTriggerMode = FlowActionTriggerModes.REACTIVE;
            actionWhen = new ReactiveConditionGroupDefinition
            {
                mode = string.IsNullOrWhiteSpace(safeTrigger.mode) ? ReactiveConditionRules.MODE_ALL : safeTrigger.mode.Trim(),
                delaySeconds = safeTrigger.delaySeconds,
                conditions = new[] { PostBakePlayableSettingsUtility.CopyReactiveCondition(conditions[0]) },
            };
            return true;
        }

        private static int FindPrimaryTriggerConditionIndex(List<ReactiveConditionDefinition> conditions)
        {
            List<ReactiveConditionDefinition> safeConditions = conditions ?? new List<ReactiveConditionDefinition>();
            for (int i = 0; i < safeConditions.Count; i++)
            {
                string type = IntentAuthoringUtility.Normalize(safeConditions[i] != null ? safeConditions[i].type : string.Empty);
                if (string.Equals(type, ReactiveConditionRules.BEAT_COMPLETED, StringComparison.Ordinal))
                    return i;
            }

            for (int i = 0; i < safeConditions.Count; i++)
            {
                string type = IntentAuthoringUtility.Normalize(safeConditions[i] != null ? safeConditions[i].type : string.Empty);
                if (string.Equals(type, StepConditionRules.ACTION_COMPLETED, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        private static bool HasReactiveDelay(ReactiveConditionGroupDefinition trigger)
        {
            return trigger != null && trigger.delaySeconds > 0f;
        }

        private static StepConditionDefinition ConvertReactiveConditionToStepCondition(ReactiveConditionDefinition condition)
        {
            if (condition == null)
                return new StepConditionDefinition { type = StepConditionRules.ALWAYS };

            return new StepConditionDefinition
            {
                type = IntentAuthoringUtility.Normalize(condition.type),
                targetId = IntentAuthoringUtility.Normalize(condition.targetId),
                currencyId = IntentAuthoringUtility.Normalize(condition.currencyId),
                amount = condition.amount,
                seconds = condition.seconds,
                signalId = IntentAuthoringUtility.Normalize(condition.signalId),
                unlockerId = IntentAuthoringUtility.Normalize(condition.unlockerId),
                item = ItemRefUtility.Clone(condition.item),
            };
        }

        private static bool StepConditionsEqual(StepConditionDefinition left, StepConditionDefinition right)
        {
            string leftType = IntentAuthoringUtility.Normalize(left != null ? left.type : string.Empty);
            string rightType = IntentAuthoringUtility.Normalize(right != null ? right.type : string.Empty);
            return string.Equals(leftType, rightType, StringComparison.Ordinal) &&
                   string.Equals(IntentAuthoringUtility.Normalize(left != null ? left.targetId : string.Empty), IntentAuthoringUtility.Normalize(right != null ? right.targetId : string.Empty), StringComparison.Ordinal) &&
                   string.Equals(IntentAuthoringUtility.Normalize(left != null ? left.currencyId : string.Empty), IntentAuthoringUtility.Normalize(right != null ? right.currencyId : string.Empty), StringComparison.Ordinal) &&
                   string.Equals(IntentAuthoringUtility.Normalize(left != null ? left.signalId : string.Empty), IntentAuthoringUtility.Normalize(right != null ? right.signalId : string.Empty), StringComparison.Ordinal) &&
                   string.Equals(IntentAuthoringUtility.Normalize(left != null ? left.unlockerId : string.Empty), IntentAuthoringUtility.Normalize(right != null ? right.unlockerId : string.Empty), StringComparison.Ordinal) &&
                   ItemRefUtility.ToStableKey(left != null ? left.item : null) == ItemRefUtility.ToStableKey(right != null ? right.item : null) &&
                   (left != null ? left.amount : 0) == (right != null ? right.amount : 0) &&
                   Math.Abs((left != null ? left.seconds : 0f) - (right != null ? right.seconds : 0f)) < 0.0001f;
        }

        private static string MakeUniqueFlowActionId(List<FlowActionDefinition> actions, string baseId)
        {
            string candidate = string.IsNullOrWhiteSpace(baseId) ? "action" : baseId.Trim();
            string unique = candidate;
            int suffix = 2;
            List<FlowActionDefinition> safeActions = actions ?? new List<FlowActionDefinition>();
            while (ContainsFlowActionId(safeActions, unique))
                unique = candidate + "_" + suffix++;
            return unique;
        }

        private static bool ContainsFlowActionId(List<FlowActionDefinition> actions, string actionId)
        {
            string normalized = IntentAuthoringUtility.Normalize(actionId);
            List<FlowActionDefinition> safeActions = actions ?? new List<FlowActionDefinition>();
            for (int i = 0; i < safeActions.Count; i++)
            {
                if (string.Equals(IntentAuthoringUtility.Normalize(safeActions[i] != null ? safeActions[i].id : string.Empty), normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static LoweredStageState LowerStage(
            ScenarioModelStageDefinition stage,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByObjectId,
            Dictionary<string, ItemRef> outputItemsByTargetId,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            string stageId = IntentAuthoringUtility.Normalize(stage.id);
            var loweredStage = new LoweredStageState
            {
                StageId = stageId,
                PreviousStageId = previousStageId,
                EnterCondition = stage.enterCondition,
            };

            FlowBeatDefinition[] introBeats = BuildIntroBeats(stage, stageStatesById, spawnKeys, stageId, loweredStage.Actions, result);
            string lastIntroBeatId = introBeats.Length > 0 ? introBeats[introBeats.Length - 1].id : string.Empty;
            FlowBeatDefinition[] entryGuideBeats = BuildEntryGuideBeats(stage, stageStatesById, spawnKeys, stageId, lastIntroBeatId, loweredStage.Actions, result);
            string lastEntrySetupBeatId = entryGuideBeats.Length > 0
                ? entryGuideBeats[entryGuideBeats.Length - 1].id
                : lastIntroBeatId;
            FlowBeatDefinition[] objectiveBeats = BuildObjectiveBeats(stage, stageStatesById, spawnKeys, saleValuesByObjectId, outputItemsByTargetId, stageId, lastEntrySetupBeatId, loweredStage.Actions, result);
            string lastObjectiveOrEntrySetupBeatId = objectiveBeats.Length > 0
                ? objectiveBeats[objectiveBeats.Length - 1].id
                : lastEntrySetupBeatId;
            FlowBeatDefinition[] completionFocusBeats = BuildCompletionFocusBeats(stage, stageStatesById, spawnKeys, stageId, lastObjectiveOrEntrySetupBeatId, loweredStage.Actions, result);
            string lastCompletionSetupBeatId = completionFocusBeats.Length > 0
                ? completionFocusBeats[completionFocusBeats.Length - 1].id
                : lastObjectiveOrEntrySetupBeatId;
            FlowBeatDefinition[] completionGuideBeats = BuildCompletionGuideBeats(stage, spawnKeys, stageId, lastCompletionSetupBeatId, loweredStage.Actions, result);
            for (int i = 0; i < introBeats.Length; i++)
                loweredStage.Beats.Add(introBeats[i]);
            for (int i = 0; i < entryGuideBeats.Length; i++)
                loweredStage.Beats.Add(entryGuideBeats[i]);
            for (int i = 0; i < objectiveBeats.Length; i++)
                loweredStage.Beats.Add(objectiveBeats[i]);
            for (int i = 0; i < completionFocusBeats.Length; i++)
                loweredStage.Beats.Add(completionFocusBeats[i]);
            for (int i = 0; i < completionGuideBeats.Length; i++)
                loweredStage.Beats.Add(completionGuideBeats[i]);

            loweredStage.FirstBeatId = loweredStage.Beats.Count > 0 ? loweredStage.Beats[0].id : string.Empty;
            loweredStage.LastBeatId = loweredStage.Beats.Count > 0 ? loweredStage.Beats[loweredStage.Beats.Count - 1].id : string.Empty;
            loweredStage.UnlockDefinition = BuildUnlockDefinition(stage, spawnKeys, result);
            loweredStage.EntryRevealRules.AddRange(BuildEntryRevealRules(stage, loweredStage, previousStageId, stageStatesById, spawnKeys, result));
            loweredStage.CompletionRevealRules.AddRange(BuildCompletionRevealRules(stage, loweredStage.LastBeatId, spawnKeys, result));
            loweredStage.EntryCustomerSpawnRules.AddRange(BuildEntryCustomerSpawnRules(stage, loweredStage, previousStageId, stageStatesById, spawnKeys, catalog, result));
            loweredStage.CompletionCustomerSpawnRules.AddRange(BuildCompletionCustomerSpawnRules(stage, loweredStage.LastBeatId, spawnKeys, catalog, result));
            return loweredStage;
        }

        private static FlowBeatDefinition[] BuildIntroBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string stageId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            var beats = new List<FlowBeatDefinition>();
            ScenarioModelEffectDefinition[] entryEffects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < entryEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = entryEffects[i];
                if (effect == null || !PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    continue;

                string beatId = stageId + "__focus_" + i.ToString("00");
                string actionId = BuildBeatScopedFlowActionId(beatId, string.Empty, FlowActionKinds.CAMERA_FOCUS);
                beats.Add(new FlowBeatDefinition
                {
                    id = beatId,
                    enterWhen = beats.Count == 0
                        ? BuildStageEntryShowWhen(stage.enterCondition, stageStatesById, spawnKeys, result)
                        : BuildActionCompletedShowWhen(BuildBeatScopedFlowActionId(beats[beats.Count - 1].id, string.Empty, FlowActionKinds.CAMERA_FOCUS)),
                    completeWhen = BuildActionCompletedShowWhen(actionId),
                });

                actions.Add(new FlowActionDefinition
                {
                    id = actionId,
                    ownerBeatId = beatId,
                    kind = FlowActionKinds.CAMERA_FOCUS,
                    triggerMode = FlowActionTriggerModes.ON_BEAT_ENTER,
                    when = new ReactiveConditionGroupDefinition(),
                    payload = new FlowActionPayloadDefinition
                    {
                        cameraFocus = new CameraFocusActionPayload
                        {
                            targetId = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, "focus_camera.targetObjectId"),
                            eventKey = FlowTargetEventKeys.ROOT,
                            movingTime = DefaultProperties.CameraMovingTime,
                            startDelay = 0f,
                            returnDelay = DefaultProperties.CameraReturnDelay,
                        },
                    },
                });
            }

            return beats.ToArray();
        }

        private static FlowBeatDefinition[] BuildObjectiveBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByObjectId,
            Dictionary<string, ItemRef> outputItemsByTargetId,
            string stageId,
            string lastEntrySetupBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            var beats = new List<FlowBeatDefinition>();
            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < objectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = objectives[i];
                if (objective == null)
                    continue;

                bool isFirstObjectiveBeat = beats.Count == 0;
                StepConditionDefinition enterWhen;
                if (isFirstObjectiveBeat)
                {
                    bool usesArrowArrivalTiming = objective.absorbsArrow &&
                        string.Equals(ResolveArrowTiming(objective), PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal);
                    enterWhen = usesArrowArrivalTiming && !string.IsNullOrEmpty(lastEntrySetupBeatId) && IsFocusBeatId(lastEntrySetupBeatId)
                        ? BuildActionCompletedShowWhen(BuildBeatScopedFlowActionId(lastEntrySetupBeatId, string.Empty, FlowActionKinds.CAMERA_FOCUS))
                        : BuildFirstObjectiveShowWhen(stage.enterCondition, stageStatesById, spawnKeys, lastEntrySetupBeatId, result);
                }
                else
                {
                    enterWhen = BuildBeatCompletedShowWhen(beats[beats.Count - 1].id);
                }

                beats.Add(BuildObjectiveBeat(
                    stageId + "__objective_" + i.ToString("00"),
                    objective,
                    enterWhen,
                    spawnKeys,
                    saleValuesByObjectId,
                    outputItemsByTargetId,
                    actions,
                    result));
            }

            return beats.ToArray();
        }

        private static FlowBeatDefinition[] BuildCompletionFocusBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string stageId,
            string lastObjectiveOrIntroBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            var beats = new List<FlowBeatDefinition>();
            ScenarioModelEffectDefinition[] completionEffects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < completionEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = completionEffects[i];
                if (effect == null || !PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    continue;

                string beatId = stageId + "__completion_focus_" + i.ToString("00");
                string actionId = BuildBeatScopedFlowActionId(beatId, string.Empty, FlowActionKinds.CAMERA_FOCUS);
                beats.Add(new FlowBeatDefinition
                {
                    id = beatId,
                    enterWhen = beats.Count == 0
                        ? BuildFirstCompletionFocusShowWhen(stage.enterCondition, stageStatesById, spawnKeys, lastObjectiveOrIntroBeatId, result)
                        : BuildBeatCompletedShowWhen(beats[beats.Count - 1].id),
                    completeWhen = BuildActionCompletedShowWhen(actionId),
                });

                actions.Add(new FlowActionDefinition
                {
                    id = actionId,
                    ownerBeatId = beatId,
                    kind = FlowActionKinds.CAMERA_FOCUS,
                    triggerMode = FlowActionTriggerModes.ON_BEAT_ENTER,
                    when = new ReactiveConditionGroupDefinition(),
                    payload = new FlowActionPayloadDefinition
                    {
                        cameraFocus = new CameraFocusActionPayload
                        {
                            targetId = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, "completion.focus_camera.targetObjectId"),
                            eventKey = FlowTargetEventKeys.ROOT,
                            movingTime = DefaultProperties.CameraMovingTime,
                            startDelay = 0f,
                            returnDelay = DefaultProperties.CameraReturnDelay,
                        },
                    },
                });
            }

            return beats.ToArray();
        }

        private static FlowBeatDefinition[] BuildEntryGuideBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string stageId,
            string lastIntroBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            return BuildGuideBeats(
                stage != null ? stage.entryEffects : null,
                stage != null ? stage.enterCondition : null,
                stageStatesById,
                spawnKeys,
                stageId,
                "__entry_guide_",
                "__focus_",
                lastIntroBeatId,
                actions,
                result);
        }

        private static FlowBeatDefinition[] BuildCompletionGuideBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, string> spawnKeys,
            string stageId,
            string lastCompletionSetupBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            return BuildGuideBeats(
                stage != null ? stage.completionEffects : null,
                null,
                null,
                spawnKeys,
                stageId,
                "__completion_guide_",
                "__completion_focus_",
                lastCompletionSetupBeatId,
                actions,
                result);
        }

        private static FlowBeatDefinition[] BuildGuideBeats(
            ScenarioModelEffectDefinition[] effects,
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string stageId,
            string beatIdPrefix,
            string focusBeatPrefix,
            string firstPrerequisiteBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            var beats = new List<FlowBeatDefinition>();
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null || !string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal))
                    continue;

                string beatId = stageId + beatIdPrefix + i.ToString("00");
                string actionId = BuildBeatScopedFlowActionId(beatId, string.Empty, FlowActionKinds.ARROW_GUIDE);
                beats.Add(new FlowBeatDefinition
                {
                    id = beatId,
                    enterWhen = beats.Count == 0
                        ? BuildGuideBeatEnterWhen(safeEffects, i, enterCondition, stageStatesById, spawnKeys, stageId, focusBeatPrefix, firstPrerequisiteBeatId, result)
                        : BuildBeatCompletedShowWhen(beats[beats.Count - 1].id),
                    completeWhen = BuildActionCompletedShowWhen(actionId),
                });

                actions.Add(new FlowActionDefinition
                {
                    id = actionId,
                    ownerBeatId = beatId,
                    kind = FlowActionKinds.ARROW_GUIDE,
                    triggerMode = FlowActionTriggerModes.ON_BEAT_ENTER,
                    when = new ReactiveConditionGroupDefinition(),
                    payload = new FlowActionPayloadDefinition
                    {
                        arrowGuide = new ArrowGuideActionPayload
                        {
                            targetId = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, beatId + ".actions.arrow_guide.payload.arrowGuide.targetId"),
                            eventKey = IntentAuthoringUtility.Normalize(effect.eventKey),
                            autoHideOnBeatExit = true,
                        },
                    },
                });
            }

            return beats.ToArray();
        }

        private static StepConditionDefinition BuildGuideBeatEnterWhen(
            ScenarioModelEffectDefinition[] effects,
            int effectIndex,
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string stageId,
            string focusBeatPrefix,
            string firstPrerequisiteBeatId,
            ScenarioModelLoweringResult result)
        {
            string timing = IntentAuthoringUtility.Normalize(effects[effectIndex] != null ? effects[effectIndex].timing : string.Empty);
            string precedingFocusBeatId = ResolvePrecedingFocusBeatId(effects, effectIndex, stageId, focusBeatPrefix);
            if (!string.IsNullOrEmpty(precedingFocusBeatId))
            {
                if (string.Equals(timing, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
                    return BuildActionCompletedShowWhen(BuildBeatScopedFlowActionId(precedingFocusBeatId, string.Empty, FlowActionKinds.CAMERA_FOCUS));

                return BuildBeatCompletedShowWhen(precedingFocusBeatId);
            }

            if (!string.IsNullOrEmpty(firstPrerequisiteBeatId))
                return BuildBeatCompletedShowWhen(firstPrerequisiteBeatId);

            return enterCondition != null
                ? BuildStageEntryShowWhen(enterCondition, stageStatesById, spawnKeys, result)
                : new StepConditionDefinition { type = StepConditionRules.ALWAYS };
        }

        private static string ResolvePrecedingFocusBeatId(
            ScenarioModelEffectDefinition[] effects,
            int effectIndex,
            string stageId,
            string focusBeatPrefix)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = effectIndex - 1; i >= 0; i--)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null || !PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    continue;

                return stageId + focusBeatPrefix + i.ToString("00");
            }

            return string.Empty;
        }

        private static FlowBeatDefinition BuildObjectiveBeat(
            string beatId,
            ScenarioModelObjectiveDefinition objective,
            StepConditionDefinition enterWhen,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByObjectId,
            Dictionary<string, ItemRef> outputItemsByTargetId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            var beat = new FlowBeatDefinition
            {
                id = beatId,
                enterWhen = enterWhen ?? new StepConditionDefinition { type = StepConditionRules.ALWAYS },
                completeWhen = BuildObjectiveCompletionCondition(objective, spawnKeys, saleValuesByObjectId, outputItemsByTargetId, result),
            };

            if (objective.absorbsArrow)
            {
                string arrowEventKey = IntentAuthoringUtility.Normalize(objective.arrowEventKey);
                if (string.IsNullOrEmpty(arrowEventKey))
                {
                    result.Errors.Add(beatId + ".actions.arrow_guide.payload.arrowGuide.eventKey가 비어 있습니다. absorbed show_arrow는 explicit eventKey가 필요합니다.");
                    if (result.FailureCode == PlayableFailureCode.None)
                        result.FailureCode = PlayableFailureCode.LoweringFailed;
                }

                actions.Add(new FlowActionDefinition
                {
                    id = BuildBeatScopedFlowActionId(beatId, string.Empty, FlowActionKinds.ARROW_GUIDE),
                    ownerBeatId = beatId,
                    kind = FlowActionKinds.ARROW_GUIDE,
                    triggerMode = FlowActionTriggerModes.ON_BEAT_ENTER,
                    when = new ReactiveConditionGroupDefinition(),
                    payload = new FlowActionPayloadDefinition
                    {
                        arrowGuide = new ArrowGuideActionPayload
                        {
                            targetId = ResolveSpawnKey(
                                spawnKeys,
                                string.IsNullOrEmpty(IntentAuthoringUtility.Normalize(objective.arrowTargetObjectId))
                                    ? objective.targetObjectId
                                    : objective.arrowTargetObjectId,
                                result,
                                beatId + ".actions.arrow_guide.payload.arrowGuide.targetId"),
                            eventKey = arrowEventKey,
                            autoHideOnBeatExit = true,
                        },
                    },
                });
            }

            return beat;
        }

        private static StepConditionDefinition BuildFirstObjectiveShowWhen(
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string lastIntroBeatId,
            ScenarioModelLoweringResult result)
        {
            if (!string.IsNullOrEmpty(lastIntroBeatId))
                return BuildActionCompletedShowWhen(BuildBeatScopedFlowActionId(lastIntroBeatId, string.Empty, FlowActionKinds.CAMERA_FOCUS));

            return BuildStageEntryShowWhen(enterCondition, stageStatesById, spawnKeys, result);
        }

        private static StepConditionDefinition BuildFirstCompletionFocusShowWhen(
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string lastObjectiveOrIntroBeatId,
            ScenarioModelLoweringResult result)
        {
            if (!string.IsNullOrEmpty(lastObjectiveOrIntroBeatId))
                return BuildBeatCompletedShowWhen(lastObjectiveOrIntroBeatId);

            return BuildStageEntryShowWhen(enterCondition, stageStatesById, spawnKeys, result);
        }

        private static StepConditionDefinition BuildBeatCompletedShowWhen(string beatId)
        {
            return new StepConditionDefinition
            {
                type = StepConditionRules.BEAT_COMPLETED,
                targetId = beatId ?? string.Empty,
            };
        }

        private static StepConditionDefinition BuildActionCompletedShowWhen(string actionId)
        {
            return new StepConditionDefinition
            {
                type = StepConditionRules.ACTION_COMPLETED,
                targetId = actionId ?? string.Empty,
            };
        }

        private static string ResolveArrowTiming(ScenarioModelObjectiveDefinition objective)
        {
            if (objective == null)
                return string.Empty;

            return PromptIntentCapabilityRegistry.ResolveArrowTiming(objective.arrowTiming, objective.arrowOnFocusArrival);
        }

        private static StepConditionDefinition BuildObjectiveCompletionCondition(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByItemKey,
            Dictionary<string, ItemRef> outputItemsByTargetId,
            ScenarioModelLoweringResult result)
        {
            string kind = objective != null ? objective.kind : string.Empty;
            string completionType = PromptIntentCapabilityRegistry.GetObjectiveCompletionStepConditionType(kind);
            string completionSignalId = PromptIntentCapabilityRegistry.GetObjectiveCompletionGameplaySignalId(kind);
            ItemRef completionItem = ResolveObjectiveCompletionItem(objective, spawnKeys, outputItemsByTargetId);
            switch (completionType)
            {
                case StepConditionRules.UNLOCKER_UNLOCKED:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.UNLOCKER_UNLOCKED,
                        unlockerId = ResolveSpawnKey(spawnKeys, objective.targetObjectId, result, "unlock_object.targetObjectId"),
                    };
                case StepConditionRules.GAMEPLAY_SIGNAL:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.GAMEPLAY_SIGNAL,
                        signalId = completionSignalId,
                        targetId = PromptIntentCapabilityRegistry.GameplaySignalSupportsTargetId(completionSignalId)
                            ? ResolveSpawnKey(spawnKeys, objective.targetObjectId, result, kind + ".targetObjectId")
                            : string.Empty,
                        item = PromptIntentCapabilityRegistry.GameplaySignalSupportsItem(completionSignalId)
                            ? ItemRefUtility.Clone(completionItem)
                            : null,
                        currencyId = PromptIntentCapabilityRegistry.GameplaySignalSupportsCurrencyId(completionSignalId)
                            ? (string.Equals(completionSignalId, GameplaySignalIds.SALE_COMPLETED, StringComparison.Ordinal)
                                ? TryResolveSaleCurrencyId(objective.item, saleValuesByItemKey)
                                : IntentAuthoringUtility.Normalize(objective.currencyId))
                            : string.Empty,
                    };
                case StepConditionRules.TIMEOUT:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.TIMEOUT,
                        seconds = objective.seconds,
                    };
                default:
                    return new StepConditionDefinition();
            }
        }

        private static ItemRef ResolveObjectiveCompletionItem(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ItemRef> outputItemsByTargetId)
        {
            if (objective == null)
                return null;

            if (ItemRefUtility.IsValid(objective.item))
                return objective.item;

            string normalizedKind = IntentAuthoringUtility.Normalize(objective.kind);
            if (!string.Equals(normalizedKind, PromptIntentObjectiveKinds.CONVERT_ITEM, StringComparison.Ordinal))
                return null;

            string normalizedTargetObjectId = IntentAuthoringUtility.Normalize(objective.targetObjectId);
            string targetId = string.Empty;
            if (spawnKeys != null)
                spawnKeys.TryGetValue(normalizedTargetObjectId, out targetId);
            if (string.IsNullOrEmpty(targetId) ||
                outputItemsByTargetId == null ||
                !outputItemsByTargetId.TryGetValue(targetId, out ItemRef outputItem) ||
                !ItemRefUtility.IsValid(outputItem))
            {
                return null;
            }

            return outputItem;
        }

        private static Dictionary<string, ItemRef> BuildFeatureOutputItemLookup(FeatureOutputItemDefinition[] featureOutputItems)
        {
            var lookup = new Dictionary<string, ItemRef>(StringComparer.Ordinal);
            FeatureOutputItemDefinition[] safeItems = featureOutputItems ?? new FeatureOutputItemDefinition[0];
            for (int i = 0; i < safeItems.Length; i++)
            {
                FeatureOutputItemDefinition definition = safeItems[i];
                if (definition == null)
                    continue;

                string targetId = IntentAuthoringUtility.Normalize(definition.targetId);
                if (string.IsNullOrEmpty(targetId) || !ItemRefUtility.IsValid(definition.item))
                    continue;

                lookup[targetId] = ItemRefUtility.Clone(definition.item);
            }

            return lookup;
        }

        private static UnlockDefinition BuildUnlockDefinition(
            ScenarioModelStageDefinition stage,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            ScenarioModelObjectiveDefinition unlockObjective = null;
            for (int i = 0; i < objectives.Length; i++)
            {
                if (!PromptIntentContractRegistry.IsUnlockObjectiveKind(objectives[i] != null ? objectives[i].kind : string.Empty))
                    continue;

                unlockObjective = objectives[i];
                break;
            }

            if (unlockObjective == null)
                return null;

            ActivationTargetDefinition[] targets = BuildUnlockTargets(stage, spawnKeys, result);
            return new UnlockDefinition
            {
                unlockerId = ResolveSpawnKey(spawnKeys, unlockObjective.targetObjectId, result, "unlock_object.targetObjectId"),
                currencyId = IntentAuthoringUtility.Normalize(unlockObjective.currencyId),
                cost = unlockObjective.amount,
                targets = targets,
            };
        }

        private static ActivationTargetDefinition[] BuildUnlockTargets(
            ScenarioModelStageDefinition stage,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            var targets = new List<ActivationTargetDefinition>();
            var seenTargetIds = new HashSet<string>(StringComparer.Ordinal);
            ScenarioModelEffectDefinition[] effects = stage != null ? stage.completionEffects ?? new ScenarioModelEffectDefinition[0] : new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null ||
                    PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind) ||
                    PromptIntentCapabilityRegistry.IsCustomerSpawnEffectKind(effect.kind))
                {
                    continue;
                }

                if (!TryBuildActivationTarget(effect, spawnKeys, result, out ActivationTargetDefinition target) || target == null)
                    continue;

                string uniqueKey = IntentAuthoringUtility.Normalize(target.kind) + ":" + IntentAuthoringUtility.Normalize(target.id);
                if (!seenTargetIds.Add(uniqueKey))
                    continue;

                targets.Add(target);
            }

            return targets.ToArray();
        }

        private static List<RevealRuleDefinition> BuildEntryRevealRules(
            ScenarioModelStageDefinition stage,
            LoweredStageState loweredStage,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            var rules = new List<RevealRuleDefinition>();
            var seenTargetIds = new HashSet<string>(StringComparer.Ordinal);
            ScenarioModelEffectDefinition[] effects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];
            ReactiveConditionGroupDefinition trigger = BuildStageEntryReactiveTrigger(loweredStage.EnterCondition, previousStageId, stageStatesById, spawnKeys, result);
            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null ||
                    PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind) ||
                    string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal) ||
                    PromptIntentCapabilityRegistry.IsCustomerSpawnEffectKind(effect.kind))
                {
                    continue;
                }

                if (!TryBuildActivationTarget(effect, spawnKeys, result, out ActivationTargetDefinition target))
                    continue;

                string uniqueKey = IntentAuthoringUtility.Normalize(target.kind) + ":" + IntentAuthoringUtility.Normalize(target.id);
                if (!seenTargetIds.Add(uniqueKey))
                    continue;

                rules.Add(new RevealRuleDefinition
                {
                    targets = new[] { target },
                    when = trigger,
                });
            }

            return rules;
        }

        private static List<RevealRuleDefinition> BuildCompletionRevealRules(
            ScenarioModelStageDefinition stage,
            string lastBeatId,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            var rules = new List<RevealRuleDefinition>();
            var seenTargetIds = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(lastBeatId))
                return rules;

            bool stageHasUnlockObjective = HasUnlockObjective(stage);

            ScenarioModelEffectDefinition[] effects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null ||
                    PromptIntentCapabilityRegistry.IsCustomerSpawnEffectKind(effect.kind) ||
                    PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    continue;

                if (stageHasUnlockObjective)
                    continue;

                if (!TryBuildActivationTarget(effect, spawnKeys, result, out ActivationTargetDefinition target))
                    continue;

                string uniqueKey = IntentAuthoringUtility.Normalize(target.kind) + ":" + IntentAuthoringUtility.Normalize(target.id);
                if (!seenTargetIds.Add(uniqueKey))
                    continue;

                rules.Add(new RevealRuleDefinition
                {
                    targets = new[] { target },
                    when = BuildBeatCompletedTrigger(lastBeatId),
                });
            }

            return rules;
        }

        private static bool HasObjective(ScenarioModelStageDefinition stage, string objectiveKind)
        {
            ScenarioModelObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new ScenarioModelObjectiveDefinition[0] : new ScenarioModelObjectiveDefinition[0];
            string normalizedKind = IntentAuthoringUtility.Normalize(objectiveKind);
            for (int i = 0; i < objectives.Length; i++)
            {
                if (string.Equals(IntentAuthoringUtility.Normalize(objectives[i] != null ? objectives[i].kind : string.Empty), normalizedKind, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasUnlockObjective(ScenarioModelStageDefinition stage)
        {
            ScenarioModelObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new ScenarioModelObjectiveDefinition[0] : new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < objectives.Length; i++)
            {
                if (PromptIntentContractRegistry.IsUnlockObjectiveKind(objectives[i] != null ? objectives[i].kind : string.Empty))
                    return true;
            }

            return false;
        }

        private static List<CustomerSpawnRuleDefinition> BuildEntryCustomerSpawnRules(
            ScenarioModelStageDefinition stage,
            LoweredStageState loweredStage,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            var rules = new List<CustomerSpawnRuleDefinition>();
            ScenarioModelEffectDefinition[] effects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];

            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null || !PromptIntentCapabilityRegistry.IsCustomerSpawnEffectKind(effect.kind))
                    continue;

                int customerDesignIndex = ResolveDefaultCustomerDesignIndex(catalog, result);
                rules.Add(new CustomerSpawnRuleDefinition
                {
                    targetId = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, "spawn_customer.targetObjectId"),
                    customerDesignIndex = customerDesignIndex,
                    startWhen = BuildEntryCustomerSpawnTrigger(effect, i, effects, loweredStage, previousStageId, stageStatesById, spawnKeys, result),
                });
            }

            return rules;
        }

        private static List<CustomerSpawnRuleDefinition> BuildCompletionCustomerSpawnRules(
            ScenarioModelStageDefinition stage,
            string lastBeatId,
            Dictionary<string, string> spawnKeys,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            var rules = new List<CustomerSpawnRuleDefinition>();
            if (string.IsNullOrEmpty(lastBeatId))
                return rules;

            ScenarioModelEffectDefinition[] effects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect == null || !PromptIntentCapabilityRegistry.IsCustomerSpawnEffectKind(effect.kind))
                    continue;

                int customerDesignIndex = ResolveDefaultCustomerDesignIndex(catalog, result);
                rules.Add(new CustomerSpawnRuleDefinition
                {
                    targetId = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, "spawn_customer.targetObjectId"),
                    customerDesignIndex = customerDesignIndex,
                    startWhen = BuildCompletionCustomerSpawnTrigger(effect, lastBeatId, result),
                });
            }

            return rules;
        }

        private static ReactiveConditionGroupDefinition BuildEntryCustomerSpawnTrigger(
            ScenarioModelEffectDefinition effect,
            int spawnIndexInEntry,
            ScenarioModelEffectDefinition[] entryEffects,
            LoweredStageState loweredStage,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            string timing = IntentAuthoringUtility.Normalize(effect != null ? effect.timing : string.Empty);
            if (string.Equals(timing, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
            {
                string triggerBeatId = ResolvePreviousStageLastBeatId(loweredStage, previousStageId, stageStatesById);
                if (!string.IsNullOrEmpty(triggerBeatId) && IsFocusBeatId(triggerBeatId))
                    return BuildActionCompletedTrigger(BuildBeatScopedFlowActionId(triggerBeatId, string.Empty, FlowActionKinds.CAMERA_FOCUS));

                if (IntentAuthoringUtility.HasEntryFocusBeforeIndex(entryEffects, spawnIndexInEntry) &&
                    loweredStage != null &&
                    !string.IsNullOrEmpty(loweredStage.FirstBeatId) &&
                    IsFocusBeatId(loweredStage.FirstBeatId))
                {
                    return BuildActionCompletedTrigger(BuildBeatScopedFlowActionId(loweredStage.FirstBeatId, string.Empty, FlowActionKinds.CAMERA_FOCUS));
                }

                result.Errors.Add("entry spawn_customer timing 'arrival'은 이전 stage의 마지막 beat가 focus camera여야 합니다.");
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
            }

            return BuildStageEntryReactiveTrigger(
                loweredStage != null ? loweredStage.EnterCondition : null,
                previousStageId,
                stageStatesById,
                spawnKeys,
                result);
        }

        private static string ResolvePreviousStageLastBeatId(
            LoweredStageState loweredStage,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById)
        {
            string referencedStageId = string.Empty;
            ScenarioModelConditionDefinition enterCondition = loweredStage != null ? loweredStage.EnterCondition : null;
            string enterKind = IntentAuthoringUtility.Normalize(enterCondition != null ? enterCondition.kind : string.Empty);
            if (string.Equals(enterKind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
                referencedStageId = IntentAuthoringUtility.Normalize(enterCondition != null ? enterCondition.stageId : string.Empty);
            else
                referencedStageId = IntentAuthoringUtility.Normalize(previousStageId);

            if (string.IsNullOrEmpty(referencedStageId) || stageStatesById == null)
                return string.Empty;

            if (!stageStatesById.TryGetValue(referencedStageId, out LoweredStageState previousStage) ||
                previousStage == null)
            {
                return string.Empty;
            }

            return IntentAuthoringUtility.Normalize(previousStage.LastBeatId);
        }

        private static ReactiveConditionGroupDefinition BuildCompletionCustomerSpawnTrigger(
            ScenarioModelEffectDefinition effect,
            string lastBeatId,
            ScenarioModelLoweringResult result)
        {
            string timing = IntentAuthoringUtility.Normalize(effect != null ? effect.timing : string.Empty);
            if (string.Equals(timing, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
            {
                if (IsFocusBeatId(lastBeatId))
                    return BuildActionCompletedTrigger(BuildBeatScopedFlowActionId(lastBeatId, string.Empty, FlowActionKinds.CAMERA_FOCUS));

                result.Errors.Add("completion spawn_customer timing 'arrival'은 같은 stage의 마지막 beat가 focus camera여야 합니다.");
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
            }

            return BuildBeatCompletedTrigger(lastBeatId);
        }

        private static ReactiveConditionGroupDefinition BuildStageEntryReactiveTrigger(
            ScenarioModelConditionDefinition enterCondition,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            var conditions = new List<ReactiveConditionDefinition>();
            string normalizedPreviousStageId = IntentAuthoringUtility.Normalize(previousStageId);
            string normalizedEnterKind = IntentAuthoringUtility.Normalize(enterCondition != null ? enterCondition.kind : string.Empty);

            // Non-stage_completed linear stages still wait for the immediately previous stage's last beat,
            // so later threshold/reveal stages cannot surface before the authored stage order advances.
            if (!string.IsNullOrEmpty(normalizedPreviousStageId) &&
                normalizedEnterKind != PromptIntentConditionKinds.START &&
                normalizedEnterKind != PromptIntentConditionKinds.STAGE_COMPLETED)
            {
                if (stageStatesById != null &&
                    stageStatesById.TryGetValue(normalizedPreviousStageId, out LoweredStageState previousStageState) &&
                    previousStageState != null &&
                    !string.IsNullOrEmpty(previousStageState.LastBeatId))
                {
                    conditions.Add(new ReactiveConditionDefinition
                    {
                        type = ReactiveConditionRules.BEAT_COMPLETED,
                        targetId = previousStageState.LastBeatId,
                    });
                }
                else
                {
                    result.Errors.Add("linear stage entry trigger가 이전 stage '" + normalizedPreviousStageId + "'의 last beat를 찾지 못했습니다.");
                    if (result.FailureCode == PlayableFailureCode.None)
                        result.FailureCode = PlayableFailureCode.LoweringFailed;
                }
            }

            if (enterCondition != null && !string.IsNullOrWhiteSpace(enterCondition.kind))
            {
                if (string.Equals(enterCondition.kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
                {
                    string previousStageReference = IntentAuthoringUtility.Normalize(enterCondition.stageId);
                    if (stageStatesById != null &&
                        stageStatesById.TryGetValue(previousStageReference, out LoweredStageState previousStage) &&
                        previousStage != null &&
                        !string.IsNullOrEmpty(previousStage.LastBeatId))
                    {
                        conditions.Add(new ReactiveConditionDefinition
                        {
                            type = ReactiveConditionRules.BEAT_COMPLETED,
                            targetId = previousStage.LastBeatId,
                        });
                    }
                    else
                    {
                        result.Errors.Add("stage_completed entry trigger가 참조하는 이전 stage '" + previousStageReference + "'의 last beat를 찾지 못했습니다.");
                        if (result.FailureCode == PlayableFailureCode.None)
                            result.FailureCode = PlayableFailureCode.LoweringFailed;
                    }
                }
                else if (!string.Equals(enterCondition.kind, PromptIntentConditionKinds.START, StringComparison.Ordinal))
                {
                    conditions.Add(BuildReactiveConditionFromStageEnter(enterCondition, spawnKeys, result));
                }
            }

            if (conditions.Count == 0)
                conditions.Add(new ReactiveConditionDefinition { type = StepConditionRules.ALWAYS });

            return new ReactiveConditionGroupDefinition
            {
                mode = ReactiveConditionRules.MODE_ALL,
                delaySeconds = 0f,
                conditions = conditions.ToArray(),
            };
        }

        private static StepConditionDefinition BuildStageEntryShowWhen(
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            if (enterCondition == null || string.IsNullOrWhiteSpace(enterCondition.kind))
                return new StepConditionDefinition { type = StepConditionRules.ALWAYS };

            string conditionKind = IntentAuthoringUtility.Normalize(enterCondition.kind);
            string stepConditionType = PromptIntentCapabilityRegistry.GetConditionStepConditionType(conditionKind);
            string gameplaySignalId = PromptIntentCapabilityRegistry.GetConditionGameplaySignalId(conditionKind);
            switch (stepConditionType)
            {
                case StepConditionRules.ALWAYS:
                    return new StepConditionDefinition { type = StepConditionRules.ALWAYS };
                case StepConditionRules.BEAT_COMPLETED:
                    string previousStageReference = IntentAuthoringUtility.Normalize(enterCondition.stageId);
                    if (stageStatesById != null &&
                        stageStatesById.TryGetValue(previousStageReference, out LoweredStageState previousStage) &&
                        !string.IsNullOrEmpty(previousStage.LastBeatId))
                    {
                        return new StepConditionDefinition
                        {
                            type = StepConditionRules.BEAT_COMPLETED,
                            targetId = previousStage.LastBeatId,
                        };
                    }

                    result.Errors.Add("stage_completed entry showWhen이 참조하는 이전 stage '" + previousStageReference + "'의 last beat를 찾지 못했습니다.");
                    if (result.FailureCode == PlayableFailureCode.None)
                        result.FailureCode = PlayableFailureCode.LoweringFailed;
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.BEAT_COMPLETED,
                        targetId = string.Empty,
                    };
                case StepConditionRules.CURRENCY_AT_LEAST:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.CURRENCY_AT_LEAST,
                        currencyId = IntentAuthoringUtility.Normalize(enterCondition.currencyId),
                        amount = enterCondition.amount,
                    };
                case StepConditionRules.UNLOCKER_UNLOCKED:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.UNLOCKER_UNLOCKED,
                        unlockerId = ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId"),
                    };
                case StepConditionRules.GAMEPLAY_SIGNAL:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.GAMEPLAY_SIGNAL,
                        signalId = gameplaySignalId,
                        targetId = PromptIntentCapabilityRegistry.GameplaySignalSupportsTargetId(gameplaySignalId)
                            ? ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId")
                            : string.Empty,
                        item = PromptIntentCapabilityRegistry.GameplaySignalSupportsItem(gameplaySignalId)
                            ? ItemRefUtility.Clone(enterCondition.item)
                            : null,
                        currencyId = PromptIntentCapabilityRegistry.GameplaySignalSupportsCurrencyId(gameplaySignalId)
                            ? IntentAuthoringUtility.Normalize(enterCondition.currencyId)
                            : string.Empty,
                    };
                default:
                    return new StepConditionDefinition { type = StepConditionRules.ALWAYS };
            }
        }

        private static ReactiveConditionDefinition BuildReactiveConditionFromStageEnter(
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            if (enterCondition == null || string.IsNullOrWhiteSpace(enterCondition.kind))
                return new ReactiveConditionDefinition { type = StepConditionRules.ALWAYS };

            string conditionKind = IntentAuthoringUtility.Normalize(enterCondition.kind);
            string reactiveConditionType = PromptIntentCapabilityRegistry.GetConditionReactiveConditionType(conditionKind);
            string gameplaySignalId = PromptIntentCapabilityRegistry.GetConditionGameplaySignalId(conditionKind);
            switch (reactiveConditionType)
            {
                case StepConditionRules.ALWAYS:
                    return new ReactiveConditionDefinition { type = StepConditionRules.ALWAYS };
                case StepConditionRules.CURRENCY_AT_LEAST:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.CURRENCY_AT_LEAST,
                        currencyId = IntentAuthoringUtility.Normalize(enterCondition.currencyId),
                        amount = enterCondition.amount,
                    };
                case StepConditionRules.UNLOCKER_UNLOCKED:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.UNLOCKER_UNLOCKED,
                        unlockerId = ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId"),
                    };
                case StepConditionRules.GAMEPLAY_SIGNAL:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.GAMEPLAY_SIGNAL,
                        signalId = gameplaySignalId,
                        targetId = PromptIntentCapabilityRegistry.GameplaySignalSupportsTargetId(gameplaySignalId)
                            ? ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId")
                            : string.Empty,
                        item = PromptIntentCapabilityRegistry.GameplaySignalSupportsItem(gameplaySignalId)
                            ? ItemRefUtility.Clone(enterCondition.item)
                            : null,
                        currencyId = PromptIntentCapabilityRegistry.GameplaySignalSupportsCurrencyId(gameplaySignalId)
                            ? IntentAuthoringUtility.Normalize(enterCondition.currencyId)
                            : string.Empty,
                    };
                default:
                    return new ReactiveConditionDefinition { type = StepConditionRules.ALWAYS };
            }
        }

        private static ReactiveConditionGroupDefinition BuildBeatCompletedTrigger(string beatId)
        {
            return new ReactiveConditionGroupDefinition
            {
                mode = ReactiveConditionRules.MODE_ALL,
                delaySeconds = 0f,
                conditions = new[]
                {
                    new ReactiveConditionDefinition
                    {
                        type = ReactiveConditionRules.BEAT_COMPLETED,
                        targetId = beatId ?? string.Empty,
                    }
                },
            };
        }

        private static ReactiveConditionGroupDefinition BuildActionCompletedTrigger(string actionId)
        {
            return new ReactiveConditionGroupDefinition
            {
                mode = ReactiveConditionRules.MODE_ALL,
                delaySeconds = 0f,
                conditions = new[]
                {
                    new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.ACTION_COMPLETED,
                        targetId = actionId ?? string.Empty,
                    },
                },
            };
        }

        private static string BuildBeatScopedFlowActionId(string beatId, string explicitId, string kind)
        {
            string normalizedBeatId = IntentAuthoringUtility.Normalize(beatId);
            string normalizedExplicitId = IntentAuthoringUtility.Normalize(explicitId);
            if (!string.IsNullOrEmpty(normalizedExplicitId))
                return string.IsNullOrEmpty(normalizedBeatId) ? normalizedExplicitId : normalizedBeatId + "__" + normalizedExplicitId;

            string normalizedKind = IntentAuthoringUtility.Normalize(kind);
            string suffix = string.IsNullOrEmpty(normalizedKind) ? "action" : normalizedKind;
            return string.IsNullOrEmpty(normalizedBeatId) ? suffix : normalizedBeatId + "__" + suffix;
        }

        private static bool IsFocusBeatId(string beatId)
        {
            string normalizedBeatId = IntentAuthoringUtility.Normalize(beatId);
            return normalizedBeatId.IndexOf("__focus_", StringComparison.Ordinal) >= 0 ||
                normalizedBeatId.IndexOf("__completion_focus_", StringComparison.Ordinal) >= 0;
        }

        private static bool TryBuildActivationTarget(
            ScenarioModelEffectDefinition effect,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result,
            out ActivationTargetDefinition target)
        {
            target = null;
            if (effect == null)
                return false;

            string effectKind = IntentAuthoringUtility.Normalize(effect.kind);
            if (PromptIntentCapabilityRegistry.EffectBuildsSceneActivationTarget(effectKind))
            {
                target = new ActivationTargetDefinition
                {
                    kind = ActivationTargetKinds.SCENE_REF,
                    id = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, "effect.targetObjectId"),
                };
                return true;
            }

            if (PromptIntentCapabilityRegistry.EffectBuildsSystemActionTarget(effectKind))
            {
                target = new ActivationTargetDefinition
                {
                    kind = ActivationTargetKinds.SYSTEM_ACTION,
                    id = IntentAuthoringUtility.BuildRuntimeSystemActionTargetId(effectKind, string.Empty),
                };
                return true;
            }

            return false;
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

        private static Dictionary<string, ScenarioModelSaleValueDefinition> BuildSaleValueLookup(ScenarioModelSaleValueDefinition[] saleValues)
        {
            var lookup = new Dictionary<string, ScenarioModelSaleValueDefinition>(StringComparer.Ordinal);
            ScenarioModelSaleValueDefinition[] safeValues = saleValues ?? new ScenarioModelSaleValueDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                ScenarioModelSaleValueDefinition value = safeValues[i];
                string itemKey = ItemRefUtility.ToStableKey(value != null ? value.item : null);
                if (string.IsNullOrEmpty(itemKey))
                    continue;

                lookup[itemKey] = value;
            }

            return lookup;
        }

        private static CompiledSpawnData[] BuildSpawns(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, SerializableVector3> positions,
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            Dictionary<string, int> acceptedItemCountByTargetId = BuildAcceptedItemCountLookup(featureAcceptedItems);
            var spawns = new List<CompiledSpawnData>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string scenarioObjectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.Equals(IntentAuthoringUtility.Normalize(value.role), PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
                    continue;

                if (!IntentAuthoringUtility.TryResolveCatalogObjectId(catalog, value.role, out string gameplayObjectId, out string error))
                {
                    result.Errors.Add("objects[" + i + "]의 catalog objectId를 해석하지 못했습니다: " + error);
                    continue;
                }

                if (!positions.TryGetValue(scenarioObjectId, out SerializableVector3 position))
                {
                    result.Errors.Add("objects[" + i + "]에 대한 deterministic position을 해석하지 못했습니다.");
                    continue;
                }

                string resolvedDesignId = ResolveSpawnDesignId(value, gameplayObjectId, scenarioObjectId, acceptedItemCountByTargetId);
                int designIndex = IntentAuthoringUtility.ResolveGameplayDesignIndex(catalog, gameplayObjectId, resolvedDesignId, result.Errors, "objects[" + i + "]");
                if (designIndex < 0 && result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;

                spawns.Add(new CompiledSpawnData
                {
                    spawnKey = IntentAuthoringUtility.BuildSpawnKey(scenarioObjectId),
                    objectId = gameplayObjectId,
                    designIndex = designIndex,
                    parentRef = string.Empty,
                    startActive = value.startsPresent && value.startsActive,
                    localPosition = position,
                    hasResolvedYaw = ResolveHasResolvedYaw(layoutSpec, value, result, "objects[" + i + "]"),
                    resolvedYawDegrees = ResolveResolvedYawDegrees(layoutSpec, value),
                    solverPlacementSource = ResolveSolverPlacementSource(layoutSpec, value),
                    orientationReason = ResolveOrientationReason(layoutSpec, value),
                    anchorDeltaCellsX = ResolveAnchorDeltaCellsX(layoutSpec, value),
                    anchorDeltaCellsZ = ResolveAnchorDeltaCellsZ(layoutSpec, value),
                });
            }

            return spawns.ToArray();
        }

        private static Dictionary<string, int> BuildAcceptedItemCountLookup(FeatureAcceptedItemDefinition[] definitions)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            FeatureAcceptedItemDefinition[] safeDefinitions = definitions ?? new FeatureAcceptedItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FeatureAcceptedItemDefinition definition = safeDefinitions[i];
                string targetId = IntentAuthoringUtility.Normalize(definition != null ? definition.targetId : string.Empty);
                if (string.IsNullOrEmpty(targetId))
                    continue;

                if (counts.TryGetValue(targetId, out int currentCount))
                    counts[targetId] = currentCount + 1;
                else
                    counts.Add(targetId, 1);
            }

            return counts;
        }

        private static bool ResolveHasResolvedYaw(
            LayoutSpecDocument layoutSpec,
            ScenarioModelObjectDefinition value,
            ScenarioModelLoweringResult result,
            string label)
        {
            if (IsPlayer(value))
                return layoutSpec != null && layoutSpec.playerStart != null && layoutSpec.playerStart.hasResolvedYaw;

            if (TryGetLayoutPlacement(layoutSpec, value, out LayoutSpecPlacementEntry placement))
                return placement.hasResolvedYaw;

            result.Errors.Add(label + "의 layoutSpec placement를 찾지 못했습니다.");
            return false;
        }

        private static float ResolveResolvedYawDegrees(LayoutSpecDocument layoutSpec, ScenarioModelObjectDefinition value)
        {
            if (IsPlayer(value))
                return layoutSpec != null && layoutSpec.playerStart != null ? layoutSpec.playerStart.resolvedYawDegrees : 0f;

            return TryGetLayoutPlacement(layoutSpec, value, out LayoutSpecPlacementEntry placement)
                ? placement.resolvedYawDegrees
                : 0f;
        }

        private static string ResolveSolverPlacementSource(LayoutSpecDocument layoutSpec, ScenarioModelObjectDefinition value)
        {
            if (IsPlayer(value))
                return layoutSpec != null && layoutSpec.playerStart != null && layoutSpec.playerStart.hasWorldPosition
                    ? "draft_layout"
                    : string.Empty;

            return TryGetLayoutPlacement(layoutSpec, value, out LayoutSpecPlacementEntry placement)
                ? placement.solverPlacementSource ?? string.Empty
                : string.Empty;
        }

        private static string ResolveOrientationReason(LayoutSpecDocument layoutSpec, ScenarioModelObjectDefinition value)
        {
            if (IsPlayer(value))
            {
                return layoutSpec != null &&
                       layoutSpec.playerStart != null &&
                       layoutSpec.playerStart.hasResolvedYaw
                    ? "draft_layout"
                    : string.Empty;
            }

            return TryGetLayoutPlacement(layoutSpec, value, out LayoutSpecPlacementEntry placement)
                ? placement.orientationReason ?? string.Empty
                : string.Empty;
        }

        private static float ResolveAnchorDeltaCellsX(LayoutSpecDocument layoutSpec, ScenarioModelObjectDefinition value)
        {
            return TryGetLayoutPlacement(layoutSpec, value, out LayoutSpecPlacementEntry placement)
                ? placement.anchorDeltaCellsX
                : 0f;
        }

        private static float ResolveAnchorDeltaCellsZ(LayoutSpecDocument layoutSpec, ScenarioModelObjectDefinition value)
        {
            return TryGetLayoutPlacement(layoutSpec, value, out LayoutSpecPlacementEntry placement)
                ? placement.anchorDeltaCellsZ
                : 0f;
        }

        private static PhysicsAreaLayoutDefinition ResolvePhysicsAreaLayout(
            LayoutSpecDocument layoutSpec,
            string objectId,
            ScenarioModelLoweringResult result,
            string label)
        {
            if (!LayoutSpecGeometryUtility.TryGetPlacement(layoutSpec, objectId, out LayoutSpecPlacementEntry placement) ||
                placement == null)
            {
                result.Errors.Add(label + " physics_area의 layoutSpec placement를 찾지 못했습니다.");
                return new PhysicsAreaLayoutDefinition();
            }

            LayoutSpecPhysicsAreaLayoutEntry layout = placement.physicsAreaLayout;
            bool hasRealBounds = layout != null &&
                                 layout.realPhysicsZoneBounds != null &&
                                 layout.realPhysicsZoneBounds.hasWorldBounds;
            bool hasFakeBounds = layout != null &&
                                 layout.fakeSpriteZoneBounds != null &&
                                 layout.fakeSpriteZoneBounds.hasWorldBounds;
            if (!hasRealBounds || !hasFakeBounds)
            {
                result.Errors.Add(label + " physics_area에는 layoutSpec.physicsAreaLayout.real/fake bounds가 모두 필요합니다.");
                return new PhysicsAreaLayoutDefinition();
            }

            return LayoutSpecGeometryUtility.ToPhysicsAreaLayout(layout);
        }

        private static RailLayoutDefinition ResolveRailLayout(
            LayoutSpecDocument layoutSpec,
            string objectId,
            ScenarioModelLoweringResult result,
            string label)
        {
            if (!LayoutSpecGeometryUtility.TryGetPlacement(layoutSpec, objectId, out LayoutSpecPlacementEntry placement) ||
                placement == null)
            {
                result.Errors.Add(label + " rail의 layoutSpec placement를 찾지 못했습니다.");
                return new RailLayoutDefinition();
            }

            RailPathAnchorDefinition[] pathCells = placement.railLayout != null
                ? placement.railLayout.pathCells ?? Array.Empty<RailPathAnchorDefinition>()
                : Array.Empty<RailPathAnchorDefinition>();
            if (pathCells.Length == 0)
            {
                result.Errors.Add(label + " rail에는 layoutSpec.railLayout.pathCells가 필요합니다.");
                return new RailLayoutDefinition();
            }

            return LayoutSpecGeometryUtility.ToRailLayout(placement.railLayout);
        }

        private static bool TryGetLayoutPlacement(
            LayoutSpecDocument layoutSpec,
            ScenarioModelObjectDefinition value,
            out LayoutSpecPlacementEntry placement)
        {
            placement = null;
            if (IsPlayer(value))
                return false;

            string objectId = IntentAuthoringUtility.Normalize(value != null ? value.id : string.Empty);
            return LayoutSpecGeometryUtility.TryGetPlacement(layoutSpec, objectId, out placement);
        }

        private static bool IsPlayer(ScenarioModelObjectDefinition value)
        {
            return string.Equals(
                IntentAuthoringUtility.Normalize(value != null ? value.role : string.Empty),
                PromptIntentObjectRoles.PLAYER,
                StringComparison.Ordinal);
        }

        private static string ResolveSpawnDesignId(
            ScenarioModelObjectDefinition value,
            string gameplayObjectId,
            string scenarioObjectId,
            Dictionary<string, int> acceptedItemCountByTargetId)
        {
            return value.designId;
        }

        private static CompiledPhysicsAreaDefinition[] BuildPhysicsAreas(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, SerializableVector3> positions,
            LayoutSpecDocument layoutSpec,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            var definitions = new List<CompiledPhysicsAreaDefinition>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null || !string.Equals(IntentAuthoringUtility.Normalize(value.role), PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
                    continue;

                string scenarioObjectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.IsNullOrEmpty(scenarioObjectId))
                    continue;

                if (!positions.TryGetValue(scenarioObjectId, out SerializableVector3 position))
                {
                    result.Errors.Add("objects[" + i + "] physics_area의 position을 해석하지 못했습니다.");
                    continue;
                }

                if (value.physicsAreaOptions == null)
                {
                    result.Errors.Add("objects[" + i + "] physics_area에는 physicsAreaOptions가 필요합니다.");
                    continue;
                }

                definitions.Add(new CompiledPhysicsAreaDefinition
                {
                    objectId = scenarioObjectId,
                    spawnKey = IntentAuthoringUtility.BuildSpawnKey(scenarioObjectId),
                    startActive = value.startsPresent && value.startsActive,
                    localPosition = position,
                    options = CopyPhysicsAreaOptions(value.physicsAreaOptions),
                    layout = ResolvePhysicsAreaLayout(layoutSpec, scenarioObjectId, result, "objects[" + i + "]"),
                });
            }

            return definitions.ToArray();
        }

        private static CompiledRailDefinition[] BuildRails(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, string> spawnKeys,
            LayoutSpecDocument layoutSpec,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            var definitions = new List<CompiledRailDefinition>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null || !string.Equals(IntentAuthoringUtility.Normalize(value.role), PromptIntentObjectRoles.RAIL, StringComparison.Ordinal))
                    continue;

                string scenarioObjectId = IntentAuthoringUtility.Normalize(value.id);
                string spawnKey = ResolveSpawnKey(spawnKeys, scenarioObjectId, result, "objects[" + i + "].id");
                if (string.IsNullOrEmpty(spawnKey))
                    continue;

                if (value.railOptions == null)
                {
                    result.Errors.Add("objects[" + i + "] rail에는 railOptions가 필요합니다.");
                    continue;
                }

                definitions.Add(new CompiledRailDefinition
                {
                    objectId = scenarioObjectId,
                    spawnKey = spawnKey,
                    options = CopyRailOptions(value.railOptions),
                    layout = ResolveRailLayout(layoutSpec, scenarioObjectId, result, "objects[" + i + "]"),
                });
            }

            return definitions.ToArray();
        }

        private static ObjectDesignSelectionDefinition[] BuildObjectDesigns(
            CompiledSpawnData[] spawns,
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            FeatureOutputItemDefinition[] featureOutputItems,
            ItemPriceDefinition[] itemPrices,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            RuntimeOwnedObjectDesignResolution resolution = RuntimeOwnedObjectDesignResolver.Resolve(spawns, featureAcceptedItems, featureOutputItems, itemPrices, catalog);
            for (int i = 0; i < resolution.Errors.Count; i++)
                result.Errors.Add(resolution.Errors[i]);

            var selections = new List<ObjectDesignSelectionDefinition>();
            for (int i = 0; i < resolution.RequiredObjectDesigns.Count; i++)
            {
                RuntimeOwnedObjectDesignSelection selection = resolution.RequiredObjectDesigns[i];
                if (selection == null)
                    continue;

                if (!TryResolveRuntimeOwnedDesignSelection(catalog, selection.objectId, selection.designId, result, out int designIndex))
                    continue;

                if (designIndex < 0)
                    continue;

                selections.Add(new ObjectDesignSelectionDefinition
                {
                    objectId = selection.objectId,
                    designId = selection.designId,
                    designIndex = designIndex,
                });
            }

            return selections.ToArray();
        }

        private static PhysicsAreaOptionsDefinition CopyPhysicsAreaOptions(PhysicsAreaOptionsDefinition value)
        {
            if (value == null)
                return new PhysicsAreaOptionsDefinition();

            return new PhysicsAreaOptionsDefinition
            {
                item = ItemRefUtility.Clone(value.item),
            };
        }

        private static RailOptionsDefinition CopyRailOptions(RailOptionsDefinition value)
        {
            if (value == null)
                return new RailOptionsDefinition();

            return new RailOptionsDefinition
            {
                item = ItemRefUtility.Clone(value.item),
                spawnIntervalSeconds = value.spawnIntervalSeconds,
                travelDurationSeconds = value.travelDurationSeconds,
                sinkEndpointTargetObjectId = IntentAuthoringUtility.Normalize(value.sinkEndpointTargetObjectId),
            };
        }

        private static PhysicsAreaLayoutDefinition CopyPhysicsAreaLayout(PhysicsAreaLayoutDefinition value)
        {
            if (value == null)
                return new PhysicsAreaLayoutDefinition();

            return new PhysicsAreaLayoutDefinition
            {
                realPhysicsZoneBounds = CopyWorldBounds(value.realPhysicsZoneBounds),
                fakeSpriteZoneBounds = CopyWorldBounds(value.fakeSpriteZoneBounds),
            };
        }

        private static RailLayoutDefinition CopyRailLayout(RailLayoutDefinition value)
        {
            if (value == null)
                return new RailLayoutDefinition();

            return new RailLayoutDefinition
            {
                pathCells = CopyRailPathAnchors(value.pathCells),
            };
        }

        private static RailPathAnchorDefinition[] CopyRailPathAnchors(RailPathAnchorDefinition[] values)
        {
            RailPathAnchorDefinition[] safeValues = values ?? new RailPathAnchorDefinition[0];
            var copies = new RailPathAnchorDefinition[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                RailPathAnchorDefinition value = safeValues[i];
                copies[i] = value == null
                    ? new RailPathAnchorDefinition()
                    : new RailPathAnchorDefinition
                    {
                        worldX = value.worldX,
                        worldZ = value.worldZ,
                    };
            }

            return copies;
        }

        private static WorldBoundsDefinition CopyWorldBounds(WorldBoundsDefinition value)
        {
            if (value == null)
                return new WorldBoundsDefinition();

            return new WorldBoundsDefinition
            {
                hasWorldBounds = value.hasWorldBounds,
                worldX = value.worldX,
                worldZ = value.worldZ,
                worldWidth = value.worldWidth,
                worldDepth = value.worldDepth,
            };
        }

        private static CurrencyDefinition[] BuildCurrencies(ScenarioModelCurrencyDefinition[] currencies)
        {
            ScenarioModelCurrencyDefinition[] safeCurrencies = currencies ?? new ScenarioModelCurrencyDefinition[0];
            var values = new CurrencyDefinition[safeCurrencies.Length];
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                ScenarioModelCurrencyDefinition value = safeCurrencies[i];
                values[i] = value == null
                    ? null
                    : new CurrencyDefinition
                    {
                        currencyId = IntentAuthoringUtility.Normalize(value.currencyId),
                        startBalance = value.startingAmount,
                        unitValue = value.unitValue,
                        startVisualMode = IntentAuthoringUtility.Normalize(value.startVisualMode),
                    };
            }

            return values;
        }

        private static ItemPriceDefinition[] BuildItemPrices(ScenarioModelSaleValueDefinition[] saleValues)
        {
            ScenarioModelSaleValueDefinition[] safeValues = saleValues ?? new ScenarioModelSaleValueDefinition[0];
            var values = new ItemPriceDefinition[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                ScenarioModelSaleValueDefinition value = safeValues[i];
                values[i] = value == null
                    ? null
                    : new ItemPriceDefinition
                    {
                        item = ItemRefUtility.Clone(value.item),
                        price = value.amount,
                    };
            }

            return values;
        }

        private static FeatureAcceptedItemDefinition[] BuildFeatureAcceptedItems(
            PlayableScenarioModel model,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            FeatureAcceptedItemDefinition[] values = IntentAuthoringUtility.BuildFeatureAcceptedItems(model, spawnKeys, result.Errors);
            if (result.Errors.Count > 0 && result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return values;
        }

        private static FeatureOutputItemDefinition[] BuildFeatureOutputItems(
            PlayableScenarioModel model,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            FeatureOutputItemDefinition[] values = IntentAuthoringUtility.BuildFeatureOutputItems(model, spawnKeys, result.Errors);
            if (result.Errors.Count > 0 && result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return values;
        }

        private static SellerRequestableItemRuleDefinition[] BuildSellerRequestableItemRules(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, string> spawnKeys,
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            var acceptedItemKeysByTargetId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            FeatureAcceptedItemDefinition[] safeAcceptedItems = featureAcceptedItems ?? new FeatureAcceptedItemDefinition[0];
            for (int i = 0; i < safeAcceptedItems.Length; i++)
            {
                FeatureAcceptedItemDefinition acceptedItem = safeAcceptedItems[i];
                if (acceptedItem == null)
                    continue;

                string targetId = IntentAuthoringUtility.Normalize(acceptedItem.targetId);
                string itemKey = ItemRefUtility.ToStableKey(acceptedItem.item);
                if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(itemKey))
                    continue;

                if (!acceptedItemKeysByTargetId.TryGetValue(targetId, out HashSet<string> itemKeys))
                {
                    itemKeys = new HashSet<string>(StringComparer.Ordinal);
                    acceptedItemKeysByTargetId.Add(targetId, itemKeys);
                }

                itemKeys.Add(itemKey);
            }

            var values = new List<SellerRequestableItemRuleDefinition>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string role = IntentAuthoringUtility.Normalize(value.role);
                if (!string.Equals(role, PromptIntentObjectRoles.SELLER, StringComparison.Ordinal))
                    continue;

                string objectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.IsNullOrEmpty(objectId) || !spawnKeys.TryGetValue(objectId, out string targetId))
                    continue;

                ScenarioModelSellerRequestableItemDefinition[] requestableItems = value.sellerRequestableItems ?? new ScenarioModelSellerRequestableItemDefinition[0];
                for (int requestIndex = 0; requestIndex < requestableItems.Length; requestIndex++)
                {
                    ScenarioModelSellerRequestableItemDefinition requestableItem = requestableItems[requestIndex];
                    if (requestableItem == null)
                        continue;

                    string itemKey = ItemRefUtility.ToStableKey(requestableItem.item);
                    if (string.IsNullOrEmpty(itemKey))
                        continue;

                    if (!acceptedItemKeysByTargetId.TryGetValue(targetId, out HashSet<string> acceptedItems) || !acceptedItems.Contains(itemKey))
                    {
                        result.Errors.Add("seller '" + objectId + "'의 requestable item '" + itemKey + "'은(는) accepted item으로 선언되어야 합니다.");
                        continue;
                    }

                    values.Add(new SellerRequestableItemRuleDefinition
                    {
                        targetId = targetId,
                        item = ItemRefUtility.Clone(requestableItem.item),
                        startWhen = BuildRequestableItemStartWhen(requestableItem.startCondition, spawnKeys, result),
                    });
                }
            }

            if (result.Errors.Count > 0 && result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return values.ToArray();
        }

        private static ReactiveConditionGroupDefinition BuildRequestableItemStartWhen(
            ScenarioModelConditionDefinition startCondition,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result)
        {
            string kind = IntentAuthoringUtility.Normalize(startCondition != null ? startCondition.kind : string.Empty);
            if (string.Equals(kind, PromptIntentConditionKinds.STAGE_COMPLETED, StringComparison.Ordinal))
            {
                result.Errors.Add("seller requestable item startWhen은 stage_completed를 지원하지 않습니다.");
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
            }

            return new ReactiveConditionGroupDefinition
            {
                mode = ReactiveConditionRules.MODE_ALL,
                delaySeconds = 0f,
                conditions = new[]
                {
                    BuildReactiveConditionFromStageEnter(startCondition, spawnKeys, result),
                },
            };
        }

        private static PlayableScenarioFeatureOptionDefinition[] BuildFeatureOptions(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, string> spawnKeys)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            var values = new List<PlayableScenarioFeatureOptionDefinition>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string role = IntentAuthoringUtility.Normalize(value.role);
                string featureType = ResolveFeatureType(role);
                if (!PromptIntentContractRegistry.ObjectRoleSupportsScenarioOptions(role) ||
                    string.IsNullOrEmpty(featureType))
                    continue;

                string objectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.IsNullOrEmpty(objectId) || !spawnKeys.TryGetValue(objectId, out string targetId))
                    continue;

                values.Add(new PlayableScenarioFeatureOptionDefinition
                {
                    featureId = role + ":" + targetId,
                    featureType = featureType,
                    targetId = targetId,
                    options = value.featureOptions.NormalizeForFeatureType(featureType),
                });
            }

            return values.ToArray();
        }

        private static string ResolveFeatureType(string role)
        {
            return PromptIntentContractRegistry.ResolveFeatureTypeForRole(IntentAuthoringUtility.Normalize(role));
        }

        private static int ResolveDefaultCustomerDesignIndex(PlayableObjectCatalog catalog, ScenarioModelLoweringResult result)
        {
            int designIndex = -1;
            if (catalog != null && catalog.TryResolveGameplayDesignIndex("customer", "car", out int preferredDesignIndex))
                designIndex = preferredDesignIndex;
            else
            {
                designIndex = IntentAuthoringUtility.ResolveGameplayDesignIndex(
                    catalog,
                    "customer",
                    string.Empty,
                    result.Errors,
                    "customer");
            }

            if (designIndex < 0 && result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return designIndex;
        }

        private static bool TryResolveRuntimeOwnedDesignSelection(
            PlayableObjectCatalog catalog,
            string runtimeOwnedObjectId,
            string runtimeOwnedDesignId,
            ScenarioModelLoweringResult result,
            out int designIndex)
        {
            designIndex = -1;
            string normalizedObjectId = IntentAuthoringUtility.Normalize(runtimeOwnedObjectId);
            string normalizedDesignId = IntentAuthoringUtility.Normalize(runtimeOwnedDesignId);
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                result.Errors.Add("runtime-owned objectId가 비어 있습니다.");
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
                return false;
            }

            if (!ContentCatalogTokenUtility.ValidateObjectId(normalizedObjectId, out string objectIdError))
            {
                result.Errors.Add("runtime-owned object '" + normalizedObjectId + "' identity 오류: " + objectIdError);
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
                return false;
            }

            if (!ContentCatalogTokenUtility.ValidateDesignId(normalizedDesignId, out string designIdError))
            {
                result.Errors.Add("runtime-owned object '" + normalizedObjectId + "' design identity 오류: " + designIdError);
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.LoweringFailed;
                return false;
            }

            designIndex = IntentAuthoringUtility.ResolveGameplayDesignIndex(
                catalog,
                normalizedObjectId,
                normalizedDesignId,
                result.Errors,
                "runtime-owned object '" + normalizedObjectId + "/" + normalizedDesignId + "'");
            if (designIndex >= 0)
                return true;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return false;
        }

        private static string TryResolveSaleCurrencyId(
            ItemRef item,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByItemKey)
        {
            string itemKey = ItemRefUtility.ToStableKey(item);
            if (string.IsNullOrEmpty(itemKey))
                return string.Empty;

            return saleValuesByItemKey.TryGetValue(itemKey, out ScenarioModelSaleValueDefinition value) && value != null
                ? IntentAuthoringUtility.Normalize(value.currencyId)
                : string.Empty;
        }

        private static string ResolveSpawnKey(
            Dictionary<string, string> spawnKeys,
            string objectId,
            ScenarioModelLoweringResult result,
            string label)
        {
            string normalizedObjectId = IntentAuthoringUtility.Normalize(objectId);
            if (spawnKeys.TryGetValue(normalizedObjectId, out string spawnKey))
                return spawnKey;

            result.Errors.Add(label + "에서 알 수 없는 object id '" + normalizedObjectId + "'를 참조하고 있습니다.");
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return string.Empty;
        }

        private static bool ValidateCatalogContract(PlayableObjectCatalog catalog, ScenarioModelLoweringResult result)
        {
            PlayableObjectCatalogValidationResult validation = PlayableObjectCatalogContractValidator.Validate(catalog);
            if (validation.IsValid)
                return true;

            for (int i = 0; i < validation.Errors.Count; i++)
                result.Errors.Add("catalog contract 오류: " + validation.Errors[i].message);

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            return false;
        }

        private static ScenarioModelLoweringResult Fail(ScenarioModelLoweringResult result, string message)
        {
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            result.Errors.Add(message);
            result.Message = message;
            return result;
        }

        private static ScenarioModelLoweringResult FinalizeFailure(ScenarioModelLoweringResult result)
        {
            result.IsValid = false;
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.LoweringFailed;
            result.Message = result.Errors.Count > 0 ? result.Errors[0] : "Scenario model lowering에 실패했습니다.";
            return result;
        }
    }
}
