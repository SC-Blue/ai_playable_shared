using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Generation.Editor.Compile
{
    public sealed class ScenarioModelBuildResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public PlayableScenarioModel Model;
        public List<string> Errors = new List<string>();
    }

    public static class ScenarioModelBuilder
    {
        public static ScenarioModelBuildResult Build(PlayablePromptIntent intent)
        {
            var result = new ScenarioModelBuildResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (intent == null)
                return Fail(result, "PlayablePromptIntent가 null입니다.");

            Dictionary<string, PromptIntentCurrencyDefinition> currencies = BuildCurrencyLookup(intent.currencies, result);
            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            var objects = BuildObjects(intent.objects, currencies, result);
            var stages = BuildStages(intent.stages, currencies, result);
            ApplyLifecycle(objects, stages);
            var saleValues = BuildSaleValues(intent.saleValues, currencies, result);

            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            result.Model = new PlayableScenarioModel
            {
                themeId = Normalize(intent.themeId),
                currencies = BuildModelCurrencies(intent.currencies, currencies, result),
                saleValues = saleValues,
                objects = objects,
                contentSelections = CopyContentSelections(intent.contentSelections),
                stages = stages,
                playerOptions = BuildPlayerOptions(intent.playerOptions),
            };
            if (result.Errors.Count > 0)
                return FinalizeFailure(result);

            result.IsValid = true;
            result.Message = "Scenario model이 생성되었습니다.";
            return result;
        }

        private static Dictionary<string, PromptIntentCurrencyDefinition> BuildCurrencyLookup(
            PromptIntentCurrencyDefinition[] currencies,
            ScenarioModelBuildResult result)
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
                    result.Errors.Add("중복된 currencies currencyId '" + currencyId + "'입니다.");
                    continue;
                }

                currencyById.Add(currencyId, value);
            }

            return currencyById;
        }

        private static ScenarioModelCurrencyDefinition[] BuildModelCurrencies(
            PromptIntentCurrencyDefinition[] currencies,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result)
        {
            PromptIntentCurrencyDefinition[] safeCurrencies = currencies ?? new PromptIntentCurrencyDefinition[0];
            var values = new ScenarioModelCurrencyDefinition[safeCurrencies.Length];
            for (int i = 0; i < safeCurrencies.Length; i++)
            {
                PromptIntentCurrencyDefinition currency = safeCurrencies[i];
                if (currency == null)
                {
                    values[i] = null;
                    continue;
                }

                if (!TryValidateCurrencyAmountValue(currency.currencyId, currency.startingAmountValue, currencyById, out int validatedAmountValue, out string error))
                {
                    result.Errors.Add("currencies[" + i + "].startingAmountValue 검증에 실패했습니다: " + error);
                    values[i] = null;
                    continue;
                }

                values[i] = new ScenarioModelCurrencyDefinition
                {
                    currencyId = Normalize(currency.currencyId),
                    unitValue = currency.unitValue,
                    startingAmount = validatedAmountValue,
                    startVisualMode = Normalize(currency.startVisualMode),
                };
            }

            return values;
        }

        private static ScenarioModelSaleValueDefinition[] BuildSaleValues(
            PromptIntentSaleValueDefinition[] saleValues,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result)
        {
            PromptIntentSaleValueDefinition[] safeValues = saleValues ?? new PromptIntentSaleValueDefinition[0];
            var values = new ScenarioModelSaleValueDefinition[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PromptIntentSaleValueDefinition saleValue = safeValues[i];
                if (saleValue == null)
                {
                    values[i] = null;
                    continue;
                }

                if (!TryValidateCurrencyAmountValue(saleValue.currencyId, saleValue.amountValue, currencyById, out int validatedAmountValue, out string error))
                {
                    result.Errors.Add("saleValues[" + i + "].amountValue 검증에 실패했습니다: " + error);
                    values[i] = null;
                    continue;
                }

                values[i] = new ScenarioModelSaleValueDefinition
                {
                    item = ItemRefUtility.Clone(saleValue.item),
                    currencyId = Normalize(saleValue.currencyId),
                    amount = validatedAmountValue,
                    amountValue = saleValue.amountValue,
                };
            }

            return values;
        }

        private static ScenarioModelObjectDefinition[] BuildObjects(
            PromptIntentObjectDefinition[] objects,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result)
        {
            PromptIntentObjectDefinition[] safeObjects = objects ?? new PromptIntentObjectDefinition[0];
            var values = new ScenarioModelObjectDefinition[safeObjects.Length];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                PromptIntentObjectDefinition value = safeObjects[i];
                values[i] = value == null
                    ? null
                    : new ScenarioModelObjectDefinition
                    {
                        id = Normalize(value.id),
                        role = Normalize(value.role),
                        designId = Normalize(value.designId),
                        featureOptions = CopyObjectFeatureOptions(value.featureOptions),
                        startsPresent = true,
                        startsActive = true,
                        firstPresentingStageId = string.Empty,
                        firstActivatingStageId = string.Empty,
                    };
            }

            return values;
        }

        private static ScenarioModelStageDefinition[] BuildStages(
            PromptIntentStageDefinition[] stages,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result)
        {
            PromptIntentStageDefinition[] safeStages = stages ?? new PromptIntentStageDefinition[0];
            var values = new ScenarioModelStageDefinition[safeStages.Length];
            for (int i = 0; i < safeStages.Length; i++)
            {
                PromptIntentStageDefinition stage = safeStages[i];
                if (stage == null)
                {
                    values[i] = null;
                    continue;
                }

                ScenarioModelObjectiveDefinition[] objectives = BuildObjectives(stage.objectives, currencyById, result, i);
                ScenarioModelEffectDefinition[] entryEffects = BuildEffects(stage.onEnter, currencyById, result, "stages[" + i + "].onEnter");
                ScenarioModelEffectDefinition[] completionEffects = BuildEffects(stage.onComplete, currencyById, result, "stages[" + i + "].onComplete");
                ApplyArrowAbsorption(entryEffects, objectives);

                values[i] = new ScenarioModelStageDefinition
                {
                    id = Normalize(stage.id),
                    enterCondition = BuildCondition(stage.enterWhen, currencyById, result, "stages[" + i + "].enterWhen"),
                    entryEffects = entryEffects,
                    objectives = objectives,
                    completionEffects = completionEffects,
                };
            }

            return values;
        }

        private static ScenarioModelConditionDefinition BuildCondition(
            PromptIntentConditionDefinition condition,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result,
            string label)
        {
            if (condition == null)
                return new ScenarioModelConditionDefinition();

            int validatedAmountValue = 0;
            string kind = Normalize(condition.kind);
            if (UsesCurrencyAmount(kind) &&
                !TryValidateCurrencyAmountValue(condition.currencyId, condition.amountValue, currencyById, out validatedAmountValue, out string error))
            {
                result.Errors.Add(label + ".amountValue 검증에 실패했습니다: " + error);
            }
            else if (UsesCapabilityLevelAmount(kind))
            {
                validatedAmountValue = Math.Max(0, condition.amountValue);
            }

            return new ScenarioModelConditionDefinition
            {
                kind = kind,
                stageId = Normalize(condition.stageId),
                targetObjectId = Normalize(condition.targetObjectId),
                item = ItemRefUtility.Clone(condition.item),
                currencyId = Normalize(condition.currencyId),
                amount = validatedAmountValue,
                amountValue = condition.amountValue,
            };
        }

        private static ScenarioModelObjectiveDefinition[] BuildObjectives(
            PromptIntentObjectiveDefinition[] objectives,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result,
            int stageIndex)
        {
            PromptIntentObjectiveDefinition[] safeObjectives = objectives ?? new PromptIntentObjectiveDefinition[0];
            var values = new ScenarioModelObjectiveDefinition[safeObjectives.Length];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                PromptIntentObjectiveDefinition value = safeObjectives[i];
                if (value == null)
                {
                    values[i] = null;
                    continue;
                }

                string kind = Normalize(value.kind);
                int validatedAmountValue = 0;
                if (UsesCurrencyAmount(kind) &&
                    !TryValidateCurrencyAmountValue(value.currencyId, value.amountValue, currencyById, out validatedAmountValue, out string error))
                {
                    result.Errors.Add("stages[" + stageIndex + "].objectives[" + i + "].amountValue 검증에 실패했습니다: " + error);
                }

                values[i] = new ScenarioModelObjectiveDefinition
                {
                    id = BuildObjectiveId(stageIndex, i),
                    kind = kind,
                    targetObjectId = Normalize(value.targetObjectId),
                    arrowTargetObjectId = string.Empty,
                    arrowEventKey = string.Empty,
                    arrowTiming = string.Empty,
                    arrowOnFocusArrival = false,
                    item = ItemRefUtility.Clone(value.item),
                    inputItem = ItemRefUtility.Clone(value.inputItem),
                    currencyId = Normalize(value.currencyId),
                    amount = validatedAmountValue,
                    amountValue = value.amountValue,
                    seconds = value.seconds,
                    absorbsArrow = false,
                };
            }

            return values;
        }

        private static ScenarioModelEffectDefinition[] BuildEffects(
            PromptIntentEffectDefinition[] effects,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            ScenarioModelBuildResult result,
            string label)
        {
            PromptIntentEffectDefinition[] safeEffects = effects ?? new PromptIntentEffectDefinition[0];
            var values = new List<ScenarioModelEffectDefinition>();
            for (int i = 0; i < safeEffects.Length; i++)
            {
                PromptIntentEffectDefinition value = safeEffects[i];
                if (value == null)
                    continue;

                string kind = Normalize(value.kind);
                int validatedAmountValue = 0;
                if (UsesCurrencyAmount(kind) &&
                    !TryValidateCurrencyAmountValue(value.currencyId, value.amountValue, currencyById, out validatedAmountValue, out string error))
                {
                    result.Errors.Add(label + "[" + i + "].amountValue 검증에 실패했습니다: " + error);
                }
                else if (UsesCapabilityLevelAmount(kind))
                {
                    validatedAmountValue = Math.Max(0, value.amountValue);
                }

                values.Add(new ScenarioModelEffectDefinition
                {
                    kind = kind,
                    timing = Normalize(value.timing),
                    targetObjectId = Normalize(value.targetObjectId),
                    eventKey = Normalize(value.eventKey),
                    item = ItemRefUtility.Clone(value.item),
                    currencyId = Normalize(value.currencyId),
                    amount = validatedAmountValue,
                    amountValue = value.amountValue,
                    seconds = value.seconds,
                });
            }

            return values.ToArray();
        }

        private static PlayableScenarioPlayerOptions BuildPlayerOptions(PromptIntentPlayerOptions playerOptions)
        {
            PlayableScenarioPlayerOptions options = PortableScenarioTuningDefaults.CreatePlayerOptions();
            if (playerOptions == null)
                return options;

            if (playerOptions.itemStackMaxCount > 0)
                options.itemStacker.maxCount = playerOptions.itemStackMaxCount;

            return options;
        }

        private static ContentSelectionDefinition[] CopyContentSelections(ContentSelectionDefinition[] selections)
        {
            if (selections == null || selections.Length == 0)
                return new ContentSelectionDefinition[0];

            var copied = new ContentSelectionDefinition[selections.Length];
            for (int i = 0; i < selections.Length; i++)
            {
                ContentSelectionDefinition selection = selections[i];
                if (selection == null)
                    continue;

                copied[i] = new ContentSelectionDefinition
                {
                    objectId = Normalize(selection.objectId),
                    designId = Normalize(selection.designId),
                    designIndex = selection.designIndex,
                };
            }

            return copied;
        }

        private static PlayableScenarioFeatureOptions CopyObjectFeatureOptions(
            PlayableScenarioFeatureOptions featureOptions)
        {
            PlayableScenarioFeatureOptions options = featureOptions;
            options.featureType = Normalize(options.featureType);
            options.targetId = Normalize(options.targetId);
            options.optionsJson = options.optionsJson != null ? options.optionsJson.Trim() : string.Empty;
            return options;
        }

        private static PromptIntentObjectPlacementDefinition CopyPlacement(PromptIntentObjectPlacementDefinition value)
        {
            if (value == null)
                return null;

            return new PromptIntentObjectPlacementDefinition
            {
                hasWorldPosition = value.hasWorldPosition,
                worldX = value.worldX,
                worldZ = value.worldZ,
                hasResolvedYaw = value.hasResolvedYaw,
                resolvedYawDegrees = value.resolvedYawDegrees,
                solverPlacementSource = Normalize(value.solverPlacementSource),
                orientationReason = Normalize(value.orientationReason),
                anchorDeltaCellsX = value.anchorDeltaCellsX,
                anchorDeltaCellsZ = value.anchorDeltaCellsZ,
                hasImageBounds = value.hasImageBounds,
                centerPxX = value.centerPxX,
                centerPxY = value.centerPxY,
                bboxWidthPx = value.bboxWidthPx,
                bboxHeightPx = value.bboxHeightPx,
                bboxConfidence = value.bboxConfidence,
                featureLayout = CopyFeatureJsonPayload(value.featureLayout),
            };
        }

        private static FeatureJsonPayload CopyFeatureJsonPayload(FeatureJsonPayload value)
        {
            if (value == null)
                return null;

            return new FeatureJsonPayload
            {
                featureType = Normalize(value.featureType),
                targetId = Normalize(value.targetId),
                json = value.json != null ? value.json.Trim() : string.Empty,
            };
        }

        private static void ApplyArrowAbsorption(
            ScenarioModelEffectDefinition[] entryEffects,
            ScenarioModelObjectiveDefinition[] objectives)
        {
            ScenarioModelEffectDefinition[] safeEffects = entryEffects ?? new ScenarioModelEffectDefinition[0];
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];
            var arrowEffectIndices = new List<int>();
            for (int i = 0; i < safeEffects.Length; i++)
            {
                if (Normalize(safeEffects[i] != null ? safeEffects[i].kind : string.Empty) != PromptIntentEffectKinds.SHOW_ARROW)
                    continue;

                arrowEffectIndices.Add(i);
            }

            if (arrowEffectIndices.Count == 0)
                return;

            int arrowCursor = 0;
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = safeObjectives[i];
                if (objective == null || !PromptIntentObjectiveKinds.CanAbsorbArrow(objective.kind))
                    continue;

                if (arrowCursor >= arrowEffectIndices.Count)
                    break;

                int arrowEffectIndex = arrowEffectIndices[arrowCursor];
                ScenarioModelEffectDefinition arrowEffect = safeEffects[arrowEffectIndex];
                string arrowTargetObjectId = Normalize(arrowEffect != null ? arrowEffect.targetObjectId : string.Empty);
                string arrowEventKey = Normalize(arrowEffect != null ? arrowEffect.eventKey : string.Empty);
                string arrowTiming = Normalize(arrowEffect != null ? arrowEffect.timing : string.Empty);
                bool arrowOnFocusArrival = string.Equals(arrowTiming, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal);

                objective.absorbsArrow = true;
                objective.arrowTargetObjectId = string.IsNullOrEmpty(arrowTargetObjectId)
                    ? Normalize(objective.targetObjectId)
                    : arrowTargetObjectId;
                objective.arrowEventKey = arrowEventKey;
                objective.arrowTiming = arrowTiming;
                objective.arrowOnFocusArrival = arrowOnFocusArrival;
                safeEffects[arrowEffectIndex] = null;
                arrowCursor++;
            }
        }

        private static void ApplyLifecycle(
            ScenarioModelObjectDefinition[] objects,
            ScenarioModelStageDefinition[] stages)
        {
            var objectsById = new Dictionary<string, ScenarioModelObjectDefinition>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) || objectsById.ContainsKey(objectId))
                    continue;

                objectsById.Add(objectId, value);
            }

            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null)
                    continue;

                ApplyLifecycleEffects(objectsById, stage.entryEffects, stage.id);
                ApplyLifecycleEffects(objectsById, stage.completionEffects, stage.id);
            }

            FinalizeLifecycle(objectsById);
        }

        private static void ApplyLifecycleEffects(
            Dictionary<string, ScenarioModelObjectDefinition> objectsById,
            ScenarioModelEffectDefinition[] effects,
            string stageId)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null)
                    continue;

                string kind = Normalize(effect.kind);
                if (kind != PromptIntentEffectKinds.REVEAL_OBJECT && kind != PromptIntentEffectKinds.ACTIVATE_OBJECT)
                    continue;

                string objectId = Normalize(effect.targetObjectId);
                if (string.IsNullOrEmpty(objectId))
                    continue;
                if (!objectsById.TryGetValue(objectId, out ScenarioModelObjectDefinition value) || value == null)
                    continue;

                string normalizedStageId = Normalize(stageId);
                if (kind == PromptIntentEffectKinds.REVEAL_OBJECT)
                {
                    if (string.IsNullOrEmpty(value.firstPresentingStageId))
                        value.firstPresentingStageId = normalizedStageId;
                    continue;
                }

                if (string.IsNullOrEmpty(value.firstActivatingStageId))
                    value.firstActivatingStageId = normalizedStageId;
                if (string.IsNullOrEmpty(value.firstPresentingStageId))
                    value.firstPresentingStageId = normalizedStageId;
            }
        }

        private static void FinalizeLifecycle(Dictionary<string, ScenarioModelObjectDefinition> objectsById)
        {
            foreach (KeyValuePair<string, ScenarioModelObjectDefinition> entry in objectsById)
            {
                ScenarioModelObjectDefinition value = entry.Value;
                if (value == null)
                    continue;

                if (!string.IsNullOrEmpty(value.firstPresentingStageId) && string.IsNullOrEmpty(value.firstActivatingStageId))
                    value.firstActivatingStageId = value.firstPresentingStageId;
                else if (string.IsNullOrEmpty(value.firstPresentingStageId) && !string.IsNullOrEmpty(value.firstActivatingStageId))
                    value.firstPresentingStageId = value.firstActivatingStageId;

                value.startsPresent = string.IsNullOrEmpty(value.firstPresentingStageId);
                value.startsActive = string.IsNullOrEmpty(value.firstActivatingStageId);
            }
        }

        private static bool UsesCurrencyAmount(string kind)
        {
            string normalizedKind = Normalize(kind);
            return normalizedKind == PromptIntentConditionKinds.BALANCE_AT_LEAST ||
                   normalizedKind == PromptIntentObjectiveKinds.UNLOCK_OBJECT ||
                   PromptIntentContractRegistry.ConditionSupportsAmountValue(normalizedKind) ||
                   PromptIntentContractRegistry.ObjectiveSupportsAmountValue(normalizedKind);
        }

        private static bool UsesCapabilityLevelAmount(string kind)
        {
            string normalizedKind = Normalize(kind);
            return normalizedKind == PromptIntentConditionKinds.CAPABILITY_LEVEL_AT_LEAST ||
                   normalizedKind == PromptIntentEffectKinds.SET_CAPABILITY_LEVEL;
        }

        private static bool TryValidateCurrencyAmountValue(
            string currencyId,
            int amountValue,
            Dictionary<string, PromptIntentCurrencyDefinition> currencyById,
            out int validatedAmountValue,
            out string error)
        {
            validatedAmountValue = 0;
            error = string.Empty;

            string normalizedCurrencyId = Normalize(currencyId);
            if (string.IsNullOrEmpty(normalizedCurrencyId))
            {
                error = "currencyId가 비어 있습니다.";
                return false;
            }

            if (!currencyById.TryGetValue(normalizedCurrencyId, out PromptIntentCurrencyDefinition currency) || currency == null)
            {
                error = "currencyId '" + normalizedCurrencyId + "'를 currencies[]에서 찾지 못했습니다.";
                return false;
            }

            if (currency.unitValue <= 0)
            {
                error = "currency '" + normalizedCurrencyId + "'의 unitValue가 0보다 커야 합니다.";
                return false;
            }

            if (amountValue < 0)
            {
                error = "amountValue는 0 이상이어야 합니다.";
                return false;
            }

            if (amountValue % currency.unitValue != 0)
            {
                error = "amountValue '" + amountValue + "'가 currency '" + normalizedCurrencyId + "'의 unitValue '" + currency.unitValue + "'로 나누어떨어지지 않습니다.";
                return false;
            }

            validatedAmountValue = amountValue;
            return true;
        }

        private static string BuildObjectiveId(int stageIndex, int objectiveIndex)
        {
            return "stage_" + stageIndex + "_objective_" + objectiveIndex;
        }

        private static class PortableScenarioTuningDefaults
        {
            public const int PlayerItemStackMaxCount = 10;
            public const float PlayerItemStackPopIntervalSeconds = 0f;

            public static PlayableScenarioPlayerOptions CreatePlayerOptions()
            {
                return new PlayableScenarioPlayerOptions
                {
                    itemStacker = new PlayableScenarioFeatureOptions.StackerTuning
                    {
                        maxCount = PlayerItemStackMaxCount,
                        popIntervalSeconds = PlayerItemStackPopIntervalSeconds,
                    },
                };
            }
        }

        private static string Normalize(string value)
        {
            return IntentAuthoringUtility.Normalize(value);
        }

        private static ScenarioModelBuildResult Fail(ScenarioModelBuildResult result, string message)
        {
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.ModelBuildFailed;
            result.Errors.Add(message);
            result.Message = message;
            return result;
        }

        private static ScenarioModelBuildResult FinalizeFailure(ScenarioModelBuildResult result)
        {
            result.IsValid = false;
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.ModelBuildFailed;
            result.Message = result.Errors.Count > 0 ? result.Errors[0] : "Scenario model 생성에 실패했습니다.";
            return result;
        }
    }
}
