using System;
using System.Collections.Generic;
using System.Text;
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
        public List<string> Warnings = new List<string>();
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
        public UnlockDefinition UnlockDefinition;
    }

    internal sealed class ObjectiveGuideCapability
    {
        public string TargetEventKey;
        public string CompletionStepConditionType;
        public string CompletionGameplaySignalId;
        public bool SupportsProjectedCurrencyGuide;
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
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities = BuildObjectiveGuideCapabilities(catalog);
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

                LoweredStageState loweredStage = LowerStage(stage, FindNextStage(stages, i), FindNthNextStage(stages, i, 2), previousStageId, stageStatesById, spawnKeys, saleValuesByObjectId, outputItemsByTargetId, objectiveGuideCapabilities, result);
                if (loweredStage == null)
                    continue;

                loweredStages.Add(loweredStage);
                if (!string.IsNullOrEmpty(loweredStage.StageId))
                    stageStatesById[loweredStage.StageId] = loweredStage;
                previousStageId = loweredStage.StageId;
            }

            FeatureAcceptedItemDefinition[] featureAcceptedItems = BuildFeatureAcceptedItems(model, spawnKeys, result);
            ItemPriceDefinition[] itemPrices = BuildItemPrices(model.saleValues);
            CurrencyDefinition[] currencies = BuildCurrencies(model.currencies);
            CompiledSpawnData[] spawns = BuildSpawns(objects, positions, layoutSpec, catalog, featureAcceptedItems, result);
            PlayableScenarioFeatureOptionDefinition[] featureOptions = BuildFeatureOptions(model.objects, spawnKeys, catalog, result);
            if (result.Errors.Count > 0)
                return FinalizeFailure(result);
            FeatureJsonPayload[] featureLayouts = BuildFeatureLayouts(objects, spawnKeys, layoutSpec, result);
            ContentSelectionDefinition[] contentSelections = BuildContentSelections(model.contentSelections, catalog, result);
            ObjectDesignSelectionDefinition[] objectDesigns = BuildObjectDesigns(spawns, featureAcceptedItems, featureOutputItems, featureOptions, itemPrices, currencies, model.contentSelections, catalog, result);
            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            var unlocks = new List<UnlockDefinition>();
            for (int i = 0; i < loweredStages.Count; i++)
            {
                LoweredStageState stage = loweredStages[i];
                if (stage.UnlockDefinition != null)
                    unlocks.Add(stage.UnlockDefinition);
            }

            FlowBeatDefinition[] loweredFlowBeats = BuildCompiledFlowBeats(loweredStages, result, out FlowActionDefinition[] loweredFlowActions);

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
                currencies = currencies,
                itemPrices = itemPrices,
                featureAcceptedItems = featureAcceptedItems,
                featureOutputItems = featureOutputItems,
                playerOptions = model.playerOptions,
                featureOptions = featureOptions,
                featureLayouts = featureLayouts,
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
            var compiledSelections = new List<ContentSelectionDefinition>();
            var selectedObjectIds = new HashSet<string>(StringComparer.Ordinal);
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
                    if (IsRuntimeOwnedContentSelectionDesign(catalog, objectId, designId))
                        continue;

                    result.Errors.Add(
                        "contentSelections[" + i + "]에서 objectId '" + objectId + "'의 designId '" + designId +
                        "'를 content selection design으로 해석하지 못했습니다.");
                    continue;
                }

                compiledSelections.Add(new ContentSelectionDefinition
                {
                    objectId = objectId,
                    designId = designId,
                    designIndex = resolvedDesignIndex,
                });
                selectedObjectIds.Add(objectId);
            }

            AppendMissingRequiredContentSelections(compiledSelections, selectedObjectIds, catalog, result);
            return compiledSelections.ToArray();
        }

        private static void AppendMissingRequiredContentSelections(
            List<ContentSelectionDefinition> compiledSelections,
            HashSet<string> selectedObjectIds,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            if (compiledSelections == null || selectedObjectIds == null || catalog == null)
                return;

            for (int i = 0; i < ContentSelectionRules.REQUIRED_OBJECT_IDS.Length; i++)
            {
                string objectId = IntentAuthoringUtility.Normalize(ContentSelectionRules.REQUIRED_OBJECT_IDS[i]);
                if (string.IsNullOrEmpty(objectId) || selectedObjectIds.Contains(objectId))
                    continue;

                if (!catalog.TryResolveContentSelectionDesign(objectId, 0, out DesignVariantEntry design, out int resolvedDesignIndex) ||
                    design == null ||
                    string.IsNullOrWhiteSpace(design.designId))
                {
                    result.Errors.Add("필수 UI content '" + objectId + "'의 기본 catalog design을 해석하지 못했습니다.");
                    continue;
                }

                compiledSelections.Add(new ContentSelectionDefinition
                {
                    objectId = objectId,
                    designId = IntentAuthoringUtility.Normalize(design.designId),
                    designIndex = resolvedDesignIndex,
                });
                selectedObjectIds.Add(objectId);
            }
        }

        private static bool IsRuntimeOwnedContentSelectionDesign(
            PlayableObjectCatalog catalog,
            string objectId,
            string designId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(designId))
                return false;

            string normalizedObjectId = IntentAuthoringUtility.Normalize(objectId);
            if (!string.Equals(normalizedObjectId, "customer", StringComparison.Ordinal))
                return false;

            return catalog.IsValidGameplayDesignId(normalizedObjectId, IntentAuthoringUtility.Normalize(designId));
        }

        private static FlowBeatDefinition[] BuildCompiledFlowBeats(
            List<LoweredStageState> loweredStages,
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

                beats.AddRange(BuildStageBoundaryActionBeats(stage.StageId, "entry", stage.EntryRevealRules, actions, result));

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

                beats.AddRange(BuildStageBoundaryActionBeats(stage.StageId, "completion", stage.CompletionRevealRules, actions, result));
            }

            compiledActions = actions.ToArray();
            return beats.ToArray();
        }

        private static List<FlowBeatDefinition> BuildStageBoundaryActionBeats(
            string stageId,
            string phase,
            List<RevealRuleDefinition> revealEffectRules,
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
                   ItemRefUtility.ToItemKey(left != null ? left.item : null) == ItemRefUtility.ToItemKey(right != null ? right.item : null) &&
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

        private static ScenarioModelStageDefinition FindNextStage(ScenarioModelStageDefinition[] stages, int currentIndex)
        {
            return FindNthNextStage(stages, currentIndex, 1);
        }

        private static ScenarioModelStageDefinition FindNthNextStage(ScenarioModelStageDefinition[] stages, int currentIndex, int ordinal)
        {
            if (ordinal <= 0)
                return null;

            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            int found = 0;
            for (int i = currentIndex + 1; i < safeStages.Length; i++)
            {
                if (safeStages[i] == null)
                    continue;

                found++;
                if (found == ordinal)
                    return safeStages[i];
            }

            return null;
        }

        private static Dictionary<string, ObjectiveGuideCapability> BuildObjectiveGuideCapabilities(PlayableObjectCatalog catalog)
        {
            var capabilities = new Dictionary<string, ObjectiveGuideCapability>(StringComparer.Ordinal);
            PromptIntentObjectiveCapabilityDescriptor[] registryCapabilities = PromptIntentCapabilityRegistry.GetObjectiveCapabilities();
            for (int i = 0; i < registryCapabilities.Length; i++)
            {
                PromptIntentObjectiveCapabilityDescriptor capability = registryCapabilities[i];
                string kind = IntentAuthoringUtility.Normalize(capability != null ? capability.kind : string.Empty);
                if (string.IsNullOrEmpty(kind))
                    continue;

                capabilities[kind] = new ObjectiveGuideCapability
                {
                    TargetEventKey = IntentAuthoringUtility.Normalize(capability.targetEventKey),
                    CompletionStepConditionType = IntentAuthoringUtility.Normalize(capability.completionStepConditionType),
                    CompletionGameplaySignalId = IntentAuthoringUtility.Normalize(capability.completionGameplaySignalId),
                    SupportsProjectedCurrencyGuide = capability.supportsProjectedCurrencyGuide,
                };
            }

            FeatureDescriptor[] featureDescriptors = catalog != null ? catalog.FeatureDescriptors ?? new FeatureDescriptor[0] : new FeatureDescriptor[0];
            for (int i = 0; i < featureDescriptors.Length; i++)
            {
                FeatureObjectiveDescriptor[] objectives = featureDescriptors[i] != null ? featureDescriptors[i].objectiveKinds ?? new FeatureObjectiveDescriptor[0] : new FeatureObjectiveDescriptor[0];
                for (int j = 0; j < objectives.Length; j++)
                {
                    FeatureObjectiveDescriptor objective = objectives[j];
                    string kind = IntentAuthoringUtility.Normalize(objective != null ? objective.kind : string.Empty);
                    if (string.IsNullOrEmpty(kind))
                        continue;

                    if (!capabilities.TryGetValue(kind, out ObjectiveGuideCapability capability))
                    {
                        capability = new ObjectiveGuideCapability();
                        capabilities[kind] = capability;
                    }

                    capability.TargetEventKey = PreferNormalized(objective != null ? objective.targetEventKey : string.Empty, capability.TargetEventKey);
                    capability.CompletionStepConditionType = PreferNormalized(objective != null ? objective.completionStepConditionType : string.Empty, capability.CompletionStepConditionType);
                    capability.CompletionGameplaySignalId = PreferNormalized(objective != null ? objective.completionGameplaySignalId : string.Empty, capability.CompletionGameplaySignalId);
                    capability.SupportsProjectedCurrencyGuide = capability.SupportsProjectedCurrencyGuide || (objective != null && objective.supportsProjectedCurrencyGuide);
                }
            }

            return capabilities;
        }

        private static bool TryGetObjectiveGuideCapability(
            Dictionary<string, ObjectiveGuideCapability> capabilities,
            string kind,
            out ObjectiveGuideCapability capability)
        {
            capability = null;
            string normalizedKind = IntentAuthoringUtility.Normalize(kind);
            return !string.IsNullOrEmpty(normalizedKind) &&
                capabilities != null &&
                capabilities.TryGetValue(normalizedKind, out capability) &&
                capability != null;
        }

        private static string PreferNormalized(string candidate, string fallback)
        {
            string normalizedCandidate = IntentAuthoringUtility.Normalize(candidate);
            return !string.IsNullOrEmpty(normalizedCandidate)
                ? normalizedCandidate
                : IntentAuthoringUtility.Normalize(fallback);
        }

        private static LoweredStageState LowerStage(
            ScenarioModelStageDefinition stage,
            ScenarioModelStageDefinition nextStage,
            ScenarioModelStageDefinition followingStage,
            string previousStageId,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByObjectId,
            Dictionary<string, ItemRef> outputItemsByTargetId,
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
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
            FlowBeatDefinition[] entryGuideBeats = BuildEntryGuideBeats(stage, nextStage, followingStage, stageStatesById, spawnKeys, objectiveGuideCapabilities, stageId, lastIntroBeatId, loweredStage.Actions, result);
            string lastEntrySetupBeatId = entryGuideBeats.Length > 0
                ? entryGuideBeats[entryGuideBeats.Length - 1].id
                : lastIntroBeatId;
            FlowBeatDefinition[] entryFeatureActionBeats = BuildEntryFeatureActionBeats(stage, stageStatesById, spawnKeys, stageId, lastEntrySetupBeatId, loweredStage.Actions, result);
            lastEntrySetupBeatId = entryFeatureActionBeats.Length > 0
                ? entryFeatureActionBeats[entryFeatureActionBeats.Length - 1].id
                : lastEntrySetupBeatId;
            FlowBeatDefinition[] objectiveBeats = BuildObjectiveBeats(stage, stageStatesById, spawnKeys, saleValuesByObjectId, outputItemsByTargetId, objectiveGuideCapabilities, stageId, lastEntrySetupBeatId, loweredStage.Actions, result);
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
            for (int i = 0; i < entryFeatureActionBeats.Length; i++)
                loweredStage.Beats.Add(entryFeatureActionBeats[i]);
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
                            completeOnArrival =
                                ShouldCompleteFocusOnArrivalForGuide(entryEffects, i) ||
                                ShouldCompleteFocusOnArrivalForFirstObjective(stage, entryEffects, i),
                        },
                    },
                });
            }

            return beats.ToArray();
        }

        private static bool ShouldCompleteFocusOnArrivalForGuide(ScenarioModelEffectDefinition[] effects, int focusEffectIndex)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = focusEffectIndex + 1; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null)
                    continue;

                if (PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    return false;

                if (!string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal))
                    continue;

                return string.Equals(
                    IntentAuthoringUtility.Normalize(effect.timing),
                    PromptIntentEffectTimingKinds.ARRIVAL,
                    StringComparison.Ordinal);
            }

            return false;
        }

        private static bool ShouldCompleteFocusOnArrivalForFirstObjective(
            ScenarioModelStageDefinition stage,
            ScenarioModelEffectDefinition[] effects,
            int focusEffectIndex)
        {
            if (!IsLastCameraFocusEffect(effects, focusEffectIndex) || HasEntrySetupBeatAfterIntro(effects))
                return false;

            ScenarioModelObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new ScenarioModelObjectiveDefinition[0] : new ScenarioModelObjectiveDefinition[0];
            if (objectives.Length == 0 || objectives[0] == null || !objectives[0].absorbsArrow)
                return false;

            return string.Equals(
                ResolveArrowTiming(objectives[0]),
                PromptIntentEffectTimingKinds.ARRIVAL,
                StringComparison.Ordinal);
        }

        private static bool IsLastCameraFocusEffect(ScenarioModelEffectDefinition[] effects, int focusEffectIndex)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = focusEffectIndex + 1; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect != null && PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    return false;
            }

            return true;
        }

        private static bool HasEntrySetupBeatAfterIntro(ScenarioModelEffectDefinition[] effects)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null)
                    continue;

                if (string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_GUIDE_ARROW, StringComparison.Ordinal) ||
                    TryResolveFeatureActionKind(effect.kind, out string _))
                    return true;
            }

            return false;
        }

        private static FlowBeatDefinition[] BuildObjectiveBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ScenarioModelSaleValueDefinition> saleValuesByObjectId,
            Dictionary<string, ItemRef> outputItemsByTargetId,
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
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
                    objectiveGuideCapabilities,
                    actions,
                    result));
            }

            return beats.ToArray();
        }

        private static FlowBeatDefinition[] BuildEntryFeatureActionBeats(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string stageId,
            string firstPrerequisiteBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            var beats = new List<FlowBeatDefinition>();
            ScenarioModelEffectDefinition[] entryEffects = stage != null ? stage.entryEffects ?? new ScenarioModelEffectDefinition[0] : new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < entryEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = entryEffects[i];
                if (effect == null || !TryResolveFeatureActionKind(effect.kind, out string actionKind))
                    continue;

                string beatId = stageId + "__entry_feature_action_" + i.ToString("00");
                string actionId = BuildBeatScopedFlowActionId(beatId, string.Empty, actionKind);
                beats.Add(new FlowBeatDefinition
                {
                    id = beatId,
                    enterWhen = beats.Count == 0
                        ? BuildEntryActionBeatEnterWhen(stage, stageStatesById, spawnKeys, firstPrerequisiteBeatId, result)
                        : BuildBeatCompletedShowWhen(beats[beats.Count - 1].id),
                    completeWhen = BuildActionCompletedShowWhen(actionId),
                });

                actions.Add(new FlowActionDefinition
                {
                    id = actionId,
                    ownerBeatId = beatId,
                    kind = actionKind,
                    triggerMode = FlowActionTriggerModes.ON_BEAT_ENTER,
                    when = new ReactiveConditionGroupDefinition(),
                    payload = new FlowActionPayloadDefinition
                    {
                        featureAction = new FeatureActionPayload
                        {
                            targetId = ResolveSpawnKey(spawnKeys, effect.targetObjectId, result, beatId + ".actions." + actionKind + ".payload.featureAction.targetId"),
                            eventKey = IntentAuthoringUtility.Normalize(effect.eventKey),
                            designIndex = -1,
                        },
                    },
                });
            }

            return beats.ToArray();
        }

        private static bool TryResolveFeatureActionKind(string effectKind, out string actionKind)
        {
            actionKind = string.Empty;
            string normalizedEffectKind = IntentAuthoringUtility.Normalize(effectKind);
            if (string.IsNullOrEmpty(normalizedEffectKind))
                return false;
            if (PromptIntentCapabilityRegistry.TryGetEffectSystemActionAuthoringId(normalizedEffectKind, out string _))
                return false;
            if (PromptIntentCapabilityRegistry.EffectBuildsActivationTarget(normalizedEffectKind))
                return false;

            string[] semanticTags = PromptIntentCapabilityRegistry.GetEffectSemanticTags(normalizedEffectKind);
            for (int i = 0; i < semanticTags.Length; i++)
            {
                string tag = IntentAuthoringUtility.Normalize(semanticTags[i]);
                if (string.IsNullOrEmpty(tag))
                    continue;

                actionKind = tag;
                return true;
            }

            return false;
        }

        private static StepConditionDefinition BuildEntryActionBeatEnterWhen(
            ScenarioModelStageDefinition stage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            string firstPrerequisiteBeatId,
            ScenarioModelLoweringResult result)
        {
            if (!string.IsNullOrEmpty(firstPrerequisiteBeatId))
                return BuildBeatCompletedShowWhen(firstPrerequisiteBeatId);

            return stage != null && stage.enterCondition != null
                ? BuildStageEntryShowWhen(stage.enterCondition, stageStatesById, spawnKeys, result)
                : new StepConditionDefinition { type = StepConditionRules.ALWAYS };
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
                            completeOnArrival = ShouldCompleteFocusOnArrivalForGuide(completionEffects, i),
                        },
                    },
                });
            }

            return beats.ToArray();
        }

        private static FlowBeatDefinition[] BuildEntryGuideBeats(
            ScenarioModelStageDefinition stage,
            ScenarioModelStageDefinition nextStage,
            ScenarioModelStageDefinition followingStage,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
            string stageId,
            string lastIntroBeatId,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result)
        {
            return BuildGuideBeats(
                stage != null ? stage.entryEffects : null,
                stage != null ? stage.objectives : null,
                nextStage != null ? nextStage.enterCondition : null,
                nextStage != null ? nextStage.objectives : null,
                followingStage != null ? followingStage.enterCondition : null,
                stage != null ? stage.enterCondition : null,
                stageStatesById,
                spawnKeys,
                objectiveGuideCapabilities,
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
                null,
                null,
                null,
                null,
                spawnKeys,
                null,
                stageId,
                "__completion_guide_",
                "__completion_focus_",
                lastCompletionSetupBeatId,
                actions,
                result);
        }

        private static FlowBeatDefinition[] BuildGuideBeats(
            ScenarioModelEffectDefinition[] effects,
            ScenarioModelObjectiveDefinition[] objectives,
            ScenarioModelConditionDefinition nextStageEnterCondition,
            ScenarioModelObjectiveDefinition[] nextStageObjectives,
            ScenarioModelConditionDefinition followingStageEnterCondition,
            ScenarioModelConditionDefinition enterCondition,
            Dictionary<string, LoweredStageState> stageStatesById,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
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
                ReactiveConditionGroupDefinition projectedCurrencyWhen =
                    BuildProjectedCurrencyEntryGuideCondition(objectives, nextStageEnterCondition, effect, spawnKeys, objectiveGuideCapabilities, result, beatId) ??
                    BuildProjectedCurrencyEntryGuideCondition(nextStageObjectives, followingStageEnterCondition, effect, spawnKeys, objectiveGuideCapabilities, result, beatId);
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
                    triggerMode = projectedCurrencyWhen != null ? FlowActionTriggerModes.REACTIVE : FlowActionTriggerModes.ON_BEAT_ENTER,
                    when = projectedCurrencyWhen ?? new ReactiveConditionGroupDefinition(),
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

        private static ReactiveConditionGroupDefinition BuildProjectedCurrencyEntryGuideCondition(
            ScenarioModelObjectiveDefinition[] objectives,
            ScenarioModelConditionDefinition nextStageEnterCondition,
            ScenarioModelEffectDefinition effect,
            Dictionary<string, string> spawnKeys,
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
            ScenarioModelLoweringResult result,
            string beatId)
        {
            if (effect == null)
                return null;

            string effectEventKey = IntentAuthoringUtility.Normalize(effect.eventKey);
            string effectTargetObjectId = IntentAuthoringUtility.Normalize(effect.targetObjectId);
            if (string.IsNullOrEmpty(effectEventKey) || string.IsNullOrEmpty(effectTargetObjectId))
                return null;

            string effectTargetId = ResolveKnownSpawnKey(spawnKeys, effectTargetObjectId);
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = safeObjectives[i];
                if (objective == null)
                    continue;

                string objectiveKind = IntentAuthoringUtility.Normalize(objective.kind);
                if (!TryGetObjectiveGuideCapability(objectiveGuideCapabilities, objectiveKind, out ObjectiveGuideCapability objectiveCapability))
                    continue;

                string objectiveTargetObjectId = IntentAuthoringUtility.Normalize(objective.targetObjectId);
                string objectiveTargetId = ResolveKnownSpawnKey(spawnKeys, objectiveTargetObjectId);
                if (string.IsNullOrEmpty(objectiveKind) ||
                    !objectiveCapability.SupportsProjectedCurrencyGuide ||
                    !string.Equals(IntentAuthoringUtility.Normalize(objectiveCapability.TargetEventKey), effectEventKey, StringComparison.Ordinal) ||
                    !TargetsMatch(effectTargetObjectId, effectTargetId, objectiveTargetObjectId, objectiveTargetId) ||
                    string.IsNullOrEmpty(IntentAuthoringUtility.Normalize(objective.currencyId)))
                {
                    continue;
                }

                if (!string.Equals(objectiveCapability.CompletionStepConditionType, StepConditionRules.GAMEPLAY_SIGNAL, StringComparison.Ordinal) ||
                    !PromptIntentCapabilityRegistry.GameplaySignalSupportsCurrencyId(objectiveCapability.CompletionGameplaySignalId))
                {
                    continue;
                }

                int amount = ResolveObjectiveAmountValue(objective);
                if (amount <= 0)
                    amount = ResolveCurrencyThresholdAmount(nextStageEnterCondition, objective.currencyId);
                if (amount <= 0)
                    continue;

                return BuildProjectedCurrencyArrowCondition(objective, spawnKeys, result, beatId, amount);
            }

            return null;
        }

        private static bool TargetsMatch(string leftObjectId, string leftTargetId, string rightObjectId, string rightTargetId)
        {
            if (!string.IsNullOrEmpty(leftTargetId) && !string.IsNullOrEmpty(rightTargetId))
                return string.Equals(leftTargetId, rightTargetId, StringComparison.Ordinal);

            if (!string.IsNullOrEmpty(leftTargetId) && string.Equals(leftTargetId, rightObjectId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrEmpty(rightTargetId) && string.Equals(rightTargetId, leftObjectId, StringComparison.Ordinal))
                return true;

            return string.Equals(leftObjectId, rightObjectId, StringComparison.Ordinal);
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
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
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
                AddObjectiveArrowGuideAction(
                    beatId,
                    string.IsNullOrEmpty(IntentAuthoringUtility.Normalize(objective.arrowTargetObjectId))
                        ? objective.targetObjectId
                        : objective.arrowTargetObjectId,
                    IntentAuthoringUtility.Normalize(objective.arrowEventKey),
                    spawnKeys,
                    actions,
                    result,
                    true);
            }
            else if (ShouldCreateProjectedCurrencyArrow(objective, objectiveGuideCapabilities, out ObjectiveGuideCapability objectiveCapability) && !HasOwnedArrowGuideAction(actions, beatId))
            {
                AddObjectiveArrowGuideAction(
                    beatId,
                    objective.targetObjectId,
                    objectiveCapability.TargetEventKey,
                    spawnKeys,
                    actions,
                    result,
                    false,
                    FlowActionTriggerModes.REACTIVE,
                    BuildProjectedCurrencyArrowCondition(objective, spawnKeys, result, beatId));
            }
            else if (IsUnlockObjectObjective(objective) && !HasOwnedArrowGuideAction(actions, beatId))
            {
                AddObjectiveArrowGuideAction(
                    beatId,
                    objective.targetObjectId,
                    FlowTargetEventKeys.ROOT,
                    spawnKeys,
                    actions,
                    result,
                    false);
            }

            return beat;
        }

        private static bool IsUnlockObjectObjective(ScenarioModelObjectiveDefinition objective)
        {
            return objective != null &&
                string.Equals(
                    IntentAuthoringUtility.Normalize(objective.kind),
                    PromptIntentObjectiveKinds.UNLOCK_OBJECT,
                    StringComparison.Ordinal);
        }

        private static bool ShouldCreateProjectedCurrencyArrow(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, ObjectiveGuideCapability> objectiveGuideCapabilities,
            out ObjectiveGuideCapability objectiveCapability)
        {
            objectiveCapability = null;
            if (objective == null || ResolveObjectiveAmountValue(objective) <= 0)
                return false;

            string kind = IntentAuthoringUtility.Normalize(objective.kind);
            if (!TryGetObjectiveGuideCapability(objectiveGuideCapabilities, kind, out objectiveCapability))
                return false;

            if (string.IsNullOrEmpty(kind) ||
                !objectiveCapability.SupportsProjectedCurrencyGuide ||
                string.IsNullOrEmpty(IntentAuthoringUtility.Normalize(objective.targetObjectId)) ||
                string.IsNullOrEmpty(IntentAuthoringUtility.Normalize(objective.currencyId)) ||
                string.IsNullOrEmpty(IntentAuthoringUtility.Normalize(objectiveCapability.TargetEventKey)))
            {
                return false;
            }

            return string.Equals(objectiveCapability.CompletionStepConditionType, StepConditionRules.GAMEPLAY_SIGNAL, StringComparison.Ordinal) &&
                PromptIntentCapabilityRegistry.GameplaySignalSupportsCurrencyId(objectiveCapability.CompletionGameplaySignalId);
        }

        private static ReactiveConditionGroupDefinition BuildProjectedCurrencyArrowCondition(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result,
            string beatId)
        {
            return BuildProjectedCurrencyArrowCondition(objective, spawnKeys, result, beatId, ResolveObjectiveAmountValue(objective));
        }

        private static ReactiveConditionGroupDefinition BuildProjectedCurrencyArrowCondition(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result,
            string beatId,
            int amount)
        {
            return new ReactiveConditionGroupDefinition
            {
                mode = ReactiveConditionRules.MODE_ALL,
                conditions = new[]
                {
                    new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.PROJECTED_CURRENCY_AT_LEAST,
                        targetId = ResolveSpawnKey(
                            spawnKeys,
                            objective.targetObjectId,
                            result,
                            beatId + ".actions.arrow_guide.when.conditions[0].targetId"),
                        currencyId = IntentAuthoringUtility.Normalize(objective.currencyId),
                        amount = amount,
                    },
                },
            };
        }

        private static int ResolveCurrencyThresholdAmount(ScenarioModelConditionDefinition condition, string currencyId)
        {
            if (condition == null)
                return 0;

            string conditionKind = IntentAuthoringUtility.Normalize(condition.kind);
            if (!string.Equals(PromptIntentCapabilityRegistry.GetConditionStepConditionType(conditionKind), StepConditionRules.CURRENCY_AT_LEAST, StringComparison.Ordinal))
                return 0;

            if (!string.Equals(IntentAuthoringUtility.Normalize(condition.currencyId), IntentAuthoringUtility.Normalize(currencyId), StringComparison.Ordinal))
                return 0;

            return condition.amount > 0 ? condition.amount : condition.amountValue;
        }

        private static int ResolveObjectiveAmountValue(ScenarioModelObjectiveDefinition objective)
        {
            if (objective == null)
                return 0;

            return objective.amount > 0 ? objective.amount : objective.amountValue;
        }

        private static bool HasOwnedArrowGuideAction(List<FlowActionDefinition> actions, string beatId)
        {
            if (actions == null)
                return false;

            string normalizedBeatId = IntentAuthoringUtility.Normalize(beatId);
            for (int i = 0; i < actions.Count; i++)
            {
                FlowActionDefinition action = actions[i];
                if (action == null)
                    continue;

                if (string.Equals(IntentAuthoringUtility.Normalize(action.ownerBeatId), normalizedBeatId, StringComparison.Ordinal) &&
                    string.Equals(IntentAuthoringUtility.Normalize(action.kind), FlowActionKinds.ARROW_GUIDE, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AddObjectiveArrowGuideAction(
            string beatId,
            string targetObjectId,
            string eventKey,
            Dictionary<string, string> spawnKeys,
            List<FlowActionDefinition> actions,
            ScenarioModelLoweringResult result,
            bool requireExplicitEventKey,
            string triggerMode = FlowActionTriggerModes.ON_BEAT_ENTER,
            ReactiveConditionGroupDefinition when = null)
        {
            string arrowEventKey = IntentAuthoringUtility.Normalize(eventKey);
            if (string.IsNullOrEmpty(arrowEventKey))
            {
                if (requireExplicitEventKey)
                {
                    result.Errors.Add(beatId + ".actions.arrow_guide.payload.arrowGuide.eventKey가 비어 있습니다. absorbed show_arrow는 explicit eventKey가 필요합니다.");
                    if (result.FailureCode == PlayableFailureCode.None)
                        result.FailureCode = PlayableFailureCode.LoweringFailed;
                }
                else
                {
                    arrowEventKey = FlowTargetEventKeys.ROOT;
                }
            }

            actions.Add(new FlowActionDefinition
            {
                id = BuildBeatScopedFlowActionId(beatId, string.Empty, FlowActionKinds.ARROW_GUIDE),
                ownerBeatId = beatId,
                kind = FlowActionKinds.ARROW_GUIDE,
                triggerMode = string.IsNullOrWhiteSpace(triggerMode) ? FlowActionTriggerModes.ON_BEAT_ENTER : triggerMode.Trim(),
                when = when ?? new ReactiveConditionGroupDefinition(),
                payload = new FlowActionPayloadDefinition
                {
                    arrowGuide = new ArrowGuideActionPayload
                    {
                        targetId = ResolveSpawnKey(
                            spawnKeys,
                            targetObjectId,
                            result,
                            beatId + ".actions.arrow_guide.payload.arrowGuide.targetId"),
                        eventKey = arrowEventKey,
                        autoHideOnBeatExit = true,
                    },
                },
            });
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
                            ? IntentAuthoringUtility.Normalize(objective.currencyId)
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
            _ = spawnKeys;
            _ = outputItemsByTargetId;
            return null;
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
                    PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                {
                    continue;
                }

                if (!TryBuildActivationTarget(effect, spawnKeys, result, out ActivationTargetDefinition target) || target == null)
                    continue;

                string uniqueKey = BuildActivationTargetUniqueKey(target);
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
                    string.Equals(effect.kind, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryBuildActivationTarget(effect, spawnKeys, result, out ActivationTargetDefinition target))
                    continue;

                string uniqueKey = BuildActivationTargetUniqueKey(target);
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
                    PromptIntentCapabilityRegistry.IsCameraFocusEffectKind(effect.kind))
                    continue;

                if (stageHasUnlockObjective)
                    continue;

                if (!TryBuildActivationTarget(effect, spawnKeys, result, out ActivationTargetDefinition target))
                    continue;

                string uniqueKey = BuildActivationTargetUniqueKey(target);
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
                case StepConditionRules.PROJECTED_CURRENCY_AT_LEAST:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.PROJECTED_CURRENCY_AT_LEAST,
                        targetId = ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId"),
                        currencyId = IntentAuthoringUtility.Normalize(enterCondition.currencyId),
                        amount = enterCondition.amount,
                    };
                case StepConditionRules.UNLOCKER_UNLOCKED:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.UNLOCKER_UNLOCKED,
                        unlockerId = ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId"),
                    };
                case StepConditionRules.CAPABILITY_LEVEL_AT_LEAST:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.CAPABILITY_LEVEL_AT_LEAST,
                        targetId = IntentAuthoringUtility.Normalize(enterCondition.targetObjectId),
                        amount = enterCondition.amount,
                    };
                case StepConditionRules.GAMEPLAY_SIGNAL:
                    return new StepConditionDefinition
                    {
                        type = StepConditionRules.GAMEPLAY_SIGNAL,
                        signalId = gameplaySignalId,
                        targetId = ResolveGameplaySignalTargetId(
                            gameplaySignalId,
                            enterCondition.targetObjectId,
                            spawnKeys,
                            result,
                            "enterWhen.targetObjectId"),
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
                case StepConditionRules.PROJECTED_CURRENCY_AT_LEAST:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.PROJECTED_CURRENCY_AT_LEAST,
                        targetId = ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId"),
                        currencyId = IntentAuthoringUtility.Normalize(enterCondition.currencyId),
                        amount = enterCondition.amount,
                    };
                case StepConditionRules.UNLOCKER_UNLOCKED:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.UNLOCKER_UNLOCKED,
                        unlockerId = ResolveSpawnKey(spawnKeys, enterCondition.targetObjectId, result, "enterWhen.targetObjectId"),
                    };
                case StepConditionRules.CAPABILITY_LEVEL_AT_LEAST:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.CAPABILITY_LEVEL_AT_LEAST,
                        targetId = IntentAuthoringUtility.Normalize(enterCondition.targetObjectId),
                        amount = enterCondition.amount,
                    };
                case StepConditionRules.GAMEPLAY_SIGNAL:
                    return new ReactiveConditionDefinition
                    {
                        type = StepConditionRules.GAMEPLAY_SIGNAL,
                        signalId = gameplaySignalId,
                        targetId = ResolveGameplaySignalTargetId(
                            gameplaySignalId,
                            enterCondition.targetObjectId,
                            spawnKeys,
                            result,
                            "enterWhen.targetObjectId"),
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

        private static string ResolveGameplaySignalTargetId(
            string gameplaySignalId,
            string targetObjectId,
            Dictionary<string, string> spawnKeys,
            ScenarioModelLoweringResult result,
            string label)
        {
            if (!PromptIntentCapabilityRegistry.GameplaySignalSupportsTargetId(gameplaySignalId))
                return string.Empty;

            string normalizedTargetObjectId = IntentAuthoringUtility.Normalize(targetObjectId);
            if (string.IsNullOrEmpty(normalizedTargetObjectId) &&
                !PromptIntentCapabilityRegistry.GameplaySignalRequiresTargetId(gameplaySignalId))
            {
                return string.Empty;
            }

            return ResolveSpawnKey(spawnKeys, normalizedTargetObjectId, result, label);
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
                if (string.Equals(effectKind, PromptIntentEffectKinds.SET_CAPABILITY_LEVEL, StringComparison.Ordinal))
                {
                    target = new ActivationTargetDefinition
                    {
                        kind = ActivationTargetKinds.SYSTEM_ACTION,
                        id = SystemActionIds.SET_CAPABILITY_LEVEL,
                        capabilityId = IntentAuthoringUtility.Normalize(effect.targetObjectId),
                        level = Math.Max(0, effect.amount),
                    };
                    return true;
                }

                target = new ActivationTargetDefinition
                {
                    kind = ActivationTargetKinds.SYSTEM_ACTION,
                    id = IntentAuthoringUtility.BuildRuntimeSystemActionTargetId(effectKind, string.Empty),
                };
                return true;
            }

            return false;
        }

        private static string BuildActivationTargetUniqueKey(ActivationTargetDefinition target)
        {
            if (target == null)
                return string.Empty;

            return IntentAuthoringUtility.Normalize(target.kind) +
                ":" + IntentAuthoringUtility.Normalize(target.id) +
                ":" + IntentAuthoringUtility.Normalize(target.capabilityId) +
                ":" + Math.Max(0, target.level);
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
                string itemKey = ItemRefUtility.ToItemKey(value != null ? value.item : null);
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
            var spawns = new List<CompiledSpawnData>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string scenarioObjectId = IntentAuthoringUtility.Normalize(value.id);
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

                bool runtimeOwnedDescriptorObject = IsDescriptorRuntimeOwnedObject(catalog, value.role);
                string resolvedDesignId = value.designId;
                int designIndex = -1;
                if (!runtimeOwnedDescriptorObject)
                {
                    designIndex = IntentAuthoringUtility.ResolveGameplayDesignIndex(catalog, gameplayObjectId, resolvedDesignId, result.Errors, "objects[" + i + "]");
                    if (designIndex < 0 && result.FailureCode == PlayableFailureCode.None)
                        result.FailureCode = PlayableFailureCode.LoweringFailed;
                }

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

        private static bool IsDescriptorRuntimeOwnedObject(PlayableObjectCatalog catalog, string role)
        {
            return global::PlayableAI.AuthoringCore.CatalogRoleUtility.TryResolveDescriptorObjectRole(
                       catalog,
                       role,
                       out _,
                       out FeatureObjectRoleDescriptor descriptorRole) &&
                   descriptorRole != null &&
                   !descriptorRole.catalogBacked &&
                   !descriptorRole.supportsDesignId;
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

        private static ObjectDesignSelectionDefinition[] BuildObjectDesigns(
            CompiledSpawnData[] spawns,
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            FeatureOutputItemDefinition[] featureOutputItems,
            PlayableScenarioFeatureOptionDefinition[] featureOptions,
            ItemPriceDefinition[] itemPrices,
            CurrencyDefinition[] currencies,
            ContentSelectionDefinition[] contentSelections,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            RuntimeOwnedObjectDesignResolution resolution = RuntimeOwnedObjectDesignResolver.Resolve(spawns, featureAcceptedItems, featureOutputItems, featureOptions, itemPrices, currencies, catalog, contentSelections);
            for (int i = 0; i < resolution.Errors.Count; i++)
                result.Errors.Add(resolution.Errors[i]);
            for (int i = 0; i < resolution.Warnings.Count; i++)
                AddUniqueWarning(result.Warnings, resolution.Warnings[i]);

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

        private static void AddUniqueWarning(List<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning) || warnings.Contains(warning))
                return;

            warnings.Add(warning);
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

        private static PlayableScenarioFeatureOptionDefinition[] BuildFeatureOptions(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, string> spawnKeys,
            PlayableObjectCatalog catalog,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            var values = new List<PlayableScenarioFeatureOptionDefinition>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                PlayableScenarioFeatureOptions sourceOptions = value.featureOptions;
                string featureType = IntentAuthoringUtility.Normalize(sourceOptions.featureType);
                if (string.IsNullOrEmpty(featureType))
                    continue;

                string objectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.IsNullOrEmpty(objectId) || !spawnKeys.TryGetValue(objectId, out string targetId))
                    continue;

                if (catalog == null || !catalog.TryGetFeatureDescriptor(featureType, out FeatureDescriptor descriptor))
                {
                    result.Errors.Add("objects[" + i + "] feature '" + featureType + "' descriptor를 찾지 못했습니다.");
                    continue;
                }

                string loweredOptionsJson = LowerFeatureOptionsJson(
                    descriptor,
                    sourceOptions.optionsJson,
                    spawnKeys,
                    result.Errors,
                    "objects[" + i + "] feature '" + featureType + "'");

                var compiledOptions = new PlayableScenarioFeatureOptions
                {
                    featureType = featureType,
                    targetId = targetId,
                    optionsJson = loweredOptionsJson,
                };

                values.Add(new PlayableScenarioFeatureOptionDefinition
                {
                    featureId = featureType + ":" + targetId,
                    featureType = featureType,
                    targetId = targetId,
                    options = compiledOptions,
                });
            }

            return values.ToArray();
        }

        internal static string LowerFeatureOptionsJson(
            FeatureDescriptor descriptor,
            string optionsJson,
            Dictionary<string, string> spawnKeys,
            List<string> errors,
            string context)
        {
            string trimmedJson = optionsJson != null ? optionsJson.Trim() : string.Empty;
            FeatureOptionFieldDescriptor[] fields = descriptor != null && descriptor.optionSchema != null
                ? descriptor.optionSchema.fields ?? new FeatureOptionFieldDescriptor[0]
                : new FeatureOptionFieldDescriptor[0];

            var targetObjectFields = new List<FeatureOptionFieldDescriptor>();
            for (int i = 0; i < fields.Length; i++)
            {
                FeatureOptionFieldDescriptor field = fields[i];
                if (field == null)
                    continue;

                string valueType = FeatureDescriptorUtility.Normalize(field.valueType);
                if (!string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_TARGET_OBJECT_ID, StringComparison.Ordinal))
                    continue;

                string fieldId = FeatureDescriptorUtility.Normalize(field.fieldId);
                if (string.IsNullOrEmpty(fieldId))
                    continue;

                targetObjectFields.Add(field);
            }

            if (targetObjectFields.Count == 0 || string.IsNullOrWhiteSpace(trimmedJson))
                return trimmedJson;

            if (!TryParseTopLevelJsonStringProperties(trimmedJson, out List<JsonStringPropertyToken> tokens, out string parseError))
            {
                errors.Add(context + " optionsJson 파싱 실패: " + parseError);
                return trimmedJson;
            }

            var replacements = new List<JsonStringReplacement>();
            for (int i = 0; i < targetObjectFields.Count; i++)
            {
                FeatureOptionFieldDescriptor field = targetObjectFields[i];
                string fieldId = FeatureDescriptorUtility.Normalize(field.fieldId);
                JsonStringPropertyToken token = FindStringToken(tokens, fieldId);
                if (token == null)
                {
                    if (field.required)
                        errors.Add(context + " optionsJson에 필수 target_object_id 옵션 '" + fieldId + "'가 없습니다.");
                    continue;
                }

                string sourceObjectId = IntentAuthoringUtility.Normalize(token.Value);
                if (string.IsNullOrEmpty(sourceObjectId))
                {
                    if (field.required)
                        errors.Add(context + " optionsJson target_object_id 옵션 '" + fieldId + "'가 비어 있습니다.");
                    continue;
                }

                if (!spawnKeys.TryGetValue(sourceObjectId, out string compiledTargetId) ||
                    string.IsNullOrEmpty(compiledTargetId))
                {
                    errors.Add(context + " optionsJson target_object_id 옵션 '" + fieldId + "'가 알 수 없는 object id를 참조합니다: " + sourceObjectId);
                    continue;
                }

                replacements.Add(new JsonStringReplacement
                {
                    Start = token.ValueStart,
                    End = token.ValueEnd,
                    Value = "\"" + EscapeJsonString(compiledTargetId) + "\"",
                });
            }

            if (replacements.Count == 0)
                return trimmedJson;

            replacements.Sort((left, right) => right.Start.CompareTo(left.Start));
            var builder = new StringBuilder(trimmedJson);
            for (int i = 0; i < replacements.Count; i++)
            {
                JsonStringReplacement replacement = replacements[i];
                builder.Remove(replacement.Start, replacement.End - replacement.Start);
                builder.Insert(replacement.Start, replacement.Value);
            }

            return builder.ToString();
        }

        private static JsonStringPropertyToken FindStringToken(List<JsonStringPropertyToken> tokens, string fieldId)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                JsonStringPropertyToken token = tokens[i];
                if (token != null && string.Equals(FeatureDescriptorUtility.Normalize(token.Key), fieldId, StringComparison.Ordinal))
                    return token;
            }

            return null;
        }

        private static bool TryParseTopLevelJsonStringProperties(
            string json,
            out List<JsonStringPropertyToken> tokens,
            out string error)
        {
            tokens = new List<JsonStringPropertyToken>();
            error = string.Empty;
            int index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{')
            {
                error = "JSON object가 아닙니다.";
                return false;
            }

            index++;
            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    error = "JSON object가 닫히지 않았습니다.";
                    return false;
                }

                if (json[index] == '}')
                {
                    index++;
                    SkipWhitespace(json, ref index);
                    if (index != json.Length)
                    {
                        error = "JSON object 뒤에 추가 문자가 있습니다.";
                        return false;
                    }

                    return true;
                }

                if (json[index] != '"')
                {
                    error = "JSON object key는 문자열이어야 합니다.";
                    return false;
                }

                if (!TryParseJsonString(json, ref index, out string key, out _, out _))
                {
                    error = "JSON object key 문자열 파싱에 실패했습니다.";
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    error = "JSON object key '" + key + "' 뒤에 ':'가 없습니다.";
                    return false;
                }

                index++;
                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    error = "JSON object key '" + key + "' 값이 없습니다.";
                    return false;
                }

                if (json[index] == '"')
                {
                    if (!TryParseJsonString(json, ref index, out string value, out int valueStart, out int valueEnd))
                    {
                        error = "JSON object key '" + key + "' 문자열 값 파싱에 실패했습니다.";
                        return false;
                    }

                    tokens.Add(new JsonStringPropertyToken
                    {
                        Key = key,
                        Value = value,
                        ValueStart = valueStart,
                        ValueEnd = valueEnd,
                    });
                }
                else if (!TrySkipJsonValue(json, ref index))
                {
                    error = "JSON object key '" + key + "' 값을 건너뛰지 못했습니다.";
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    error = "JSON object가 닫히지 않았습니다.";
                    return false;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == '}')
                    continue;

                error = "JSON object 항목 구분자가 올바르지 않습니다.";
                return false;
            }
        }

        private static bool TryParseJsonString(
            string json,
            ref int index,
            out string value,
            out int valueStart,
            out int valueEnd)
        {
            value = string.Empty;
            valueStart = index;
            valueEnd = index;
            if (index >= json.Length || json[index] != '"')
                return false;

            var builder = new StringBuilder();
            index++;
            while (index < json.Length)
            {
                char ch = json[index++];
                if (ch == '"')
                {
                    valueEnd = index;
                    value = builder.ToString();
                    return true;
                }

                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (index >= json.Length)
                    return false;

                char escaped = json[index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (index + 4 > json.Length)
                            return false;
                        string hex = json.Substring(index, 4);
                        if (!ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                            return false;
                        builder.Append((char)code);
                        index += 4;
                        break;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool TrySkipJsonValue(string json, ref int index)
        {
            if (index >= json.Length)
                return false;

            char ch = json[index];
            if (ch == '"')
                return TryParseJsonString(json, ref index, out _, out _, out _);

            if (ch == '{' || ch == '[')
            {
                char open = ch;
                char close = ch == '{' ? '}' : ']';
                int depth = 0;
                while (index < json.Length)
                {
                    ch = json[index];
                    if (ch == '"')
                    {
                        if (!TryParseJsonString(json, ref index, out _, out _, out _))
                            return false;
                        continue;
                    }

                    if (ch == open)
                    {
                        depth++;
                    }
                    else if (ch == close)
                    {
                        depth--;
                        index++;
                        if (depth == 0)
                            return true;
                        continue;
                    }

                    index++;
                }

                return false;
            }

            while (index < json.Length)
            {
                ch = json[index];
                if (ch == ',' || ch == '}')
                    return true;
                index++;
            }

            return true;
        }

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            return builder.ToString();
        }

        private sealed class JsonStringPropertyToken
        {
            public string Key;
            public string Value;
            public int ValueStart;
            public int ValueEnd;
        }

        private sealed class JsonStringReplacement
        {
            public int Start;
            public int End;
            public string Value;
        }

        private static FeatureJsonPayload[] BuildFeatureLayouts(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, string> spawnKeys,
            LayoutSpecDocument layoutSpec,
            ScenarioModelLoweringResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            var values = new List<FeatureJsonPayload>();
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string featureType = IntentAuthoringUtility.Normalize(value.featureOptions.featureType);
                if (string.IsNullOrEmpty(featureType))
                    continue;

                string objectId = IntentAuthoringUtility.Normalize(value.id);
                if (string.IsNullOrEmpty(objectId) || !spawnKeys.TryGetValue(objectId, out string targetId))
                    continue;

                if (!LayoutSpecGeometryUtility.TryGetPlacement(layoutSpec, objectId, out LayoutSpecPlacementEntry placement) ||
                    placement == null)
                {
                    result.Errors.Add("objects[" + i + "] feature '" + featureType + "'의 layoutSpec placement를 찾지 못했습니다.");
                    continue;
                }

                FeatureJsonPayload layout = placement.featureLayout ?? new FeatureJsonPayload();
                string layoutFeatureType = IntentAuthoringUtility.Normalize(layout.featureType);
                if (!string.IsNullOrEmpty(layoutFeatureType) &&
                    !string.Equals(layoutFeatureType, featureType, StringComparison.Ordinal))
                {
                    result.Errors.Add("objects[" + i + "] featureLayout.featureType '" + layoutFeatureType + "'가 featureOptions.featureType '" + featureType + "'와 다릅니다.");
                    continue;
                }

                values.Add(new FeatureJsonPayload
                {
                    featureType = featureType,
                    targetId = targetId,
                    json = layout.json != null ? layout.json.Trim() : string.Empty,
                });
            }

            return values.ToArray();
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

        private static string ResolveKnownSpawnKey(Dictionary<string, string> spawnKeys, string objectId)
        {
            string normalizedObjectId = IntentAuthoringUtility.Normalize(objectId);
            if (spawnKeys != null && spawnKeys.TryGetValue(normalizedObjectId, out string spawnKey))
                return spawnKey;

            if (spawnKeys != null)
            {
                foreach (string knownSpawnKey in spawnKeys.Values)
                {
                    if (string.Equals(knownSpawnKey, normalizedObjectId, StringComparison.Ordinal))
                        return normalizedObjectId;
                }
            }

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

