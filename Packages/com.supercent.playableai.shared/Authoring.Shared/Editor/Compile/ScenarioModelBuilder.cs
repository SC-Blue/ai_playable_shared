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
                stages = stages,
                playerOptions = BuildPlayerOptions(intent.scenarioOptions),
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
                        facilityOptions = BuildObjectFacilityOptions(value.scenarioOptions, value.role),
                        sellerRequestableItems = BuildSellerRequestableItems(value.scenarioOptions, currencyById, result),
                        physicsAreaOptions = CopyPhysicsAreaOptions(value.physicsAreaOptions),
                        railOptions = BuildRailOptions(value.railOptions),
                        startsPresent = true,
                        startsActive = true,
                        firstPresentingStageId = string.Empty,
                        firstActivatingStageId = string.Empty,
                    };
            }

            return values;
        }

        private static ScenarioModelSellerRequestableItemDefinition[] BuildSellerRequestableItems(
            PromptIntentObjectScenarioOptions scenarioOptions,
            Dictionary<string, PromptIntentCurrencyDefinition> currencies,
            ScenarioModelBuildResult result)
        {
            PromptIntentSellerRequestableItemDefinition[] safeValues =
                scenarioOptions != null ? scenarioOptions.requestableItems ?? new PromptIntentSellerRequestableItemDefinition[0] : new PromptIntentSellerRequestableItemDefinition[0];
            var values = new ScenarioModelSellerRequestableItemDefinition[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PromptIntentSellerRequestableItemDefinition value = safeValues[i];
                values[i] = value == null
                    ? null
                    : new ScenarioModelSellerRequestableItemDefinition
                    {
                        item = ItemRefUtility.Clone(value.item),
                        startCondition = BuildCondition(
                            value.startWhen,
                            currencies,
                            result,
                            "objects[].scenarioOptions.requestableItems[" + i + "].startWhen"),
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

        private static PlayableScenarioPlayerOptions BuildPlayerOptions(PromptIntentScenarioOptions scenarioOptions)
        {
            PlayableScenarioPlayerOptions options = PortableScenarioTuningDefaults.CreatePlayerOptions();
            if (scenarioOptions == null)
                return options;

            if (scenarioOptions.itemStackMaxCount > 0)
                options.itemStacker.maxCount = scenarioOptions.itemStackMaxCount;

            return options;
        }

        private static PlayableScenarioFacilityOptions BuildObjectFacilityOptions(
            PromptIntentObjectScenarioOptions scenarioOptions,
            string role)
        {
            PlayableScenarioFacilityOptions options = FacilityScenarioOptionRules.CreateRoleDefaultFacilityOptions(role);
            ClearUnsupportedFacilityScenarioOptions(role, ref options);
            if (scenarioOptions == null)
                return options;

            if (scenarioOptions.customerRequestCount != null)
            {
                options.customerReqMin = scenarioOptions.customerRequestCount.min;
                options.customerReqMax = scenarioOptions.customerRequestCount.max;
            }

            if (scenarioOptions.inputCountPerConversion > 0)
                options.inputCountPerConversion = scenarioOptions.inputCountPerConversion;
            if (scenarioOptions.conversionIntervalSeconds > 0f)
                options.conversionInterval = scenarioOptions.conversionIntervalSeconds;
            if (scenarioOptions.inputItemMoveIntervalSeconds > 0f)
                options.inputItemMoveInterval = scenarioOptions.inputItemMoveIntervalSeconds;
            if (scenarioOptions.spawnIntervalSeconds > 0f)
                options.spawnInterval = scenarioOptions.spawnIntervalSeconds;

            return options;
        }

        private static void ClearUnsupportedFacilityScenarioOptions(
            string role,
            ref PlayableScenarioFacilityOptions options)
        {
            if (!FacilityScenarioOptionRules.SupportsCustomerRequestCount(role))
            {
                options.customerReqMin = 0;
                options.customerReqMax = 0;
            }

            if (!FacilityScenarioOptionRules.SupportsInputCountPerConversion(role))
                options.inputCountPerConversion = 0;

            if (!FacilityScenarioOptionRules.SupportsConversionIntervalSeconds(role))
                options.conversionInterval = 0f;

            if (!FacilityScenarioOptionRules.SupportsInputItemMoveIntervalSeconds(role))
                options.inputItemMoveInterval = 0f;

            if (!FacilityScenarioOptionRules.SupportsSpawnIntervalSeconds(role))
                options.spawnInterval = 0f;
        }

        private static PhysicsAreaOptionsDefinition CopyPhysicsAreaOptions(PhysicsAreaOptionsDefinition value)
        {
            if (value == null)
                return null;

            return new PhysicsAreaOptionsDefinition
            {
                item = ItemRefUtility.Clone(value.item),
            };
        }

        private static RailOptionsDefinition BuildRailOptions(RailOptionsDefinition value)
        {
            if (value == null)
                return null;

            return new RailOptionsDefinition
            {
                item = ItemRefUtility.Clone(value.item),
                spawnIntervalSeconds = value.spawnIntervalSeconds > 0f ? value.spawnIntervalSeconds : PortableScenarioTuningDefaults.RailSpawnIntervalSeconds,
                travelDurationSeconds = value.travelDurationSeconds > 0f ? value.travelDurationSeconds : PortableScenarioTuningDefaults.RailTravelDurationSeconds,
                sinkEndpointTargetObjectId = Normalize(value.sinkEndpointTargetObjectId),
            };
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
                physicsAreaLayout = CopyPhysicsAreaLayout(value.physicsAreaLayout),
                railLayout = CopyRailLayout(value.railLayout),
            };
        }

        private static PhysicsAreaLayoutDefinition CopyPhysicsAreaLayout(PhysicsAreaLayoutDefinition value)
        {
            if (value == null)
                return null;

            return new PhysicsAreaLayoutDefinition
            {
                realPhysicsZoneBounds = CopyWorldBounds(value.realPhysicsZoneBounds),
                fakeSpriteZoneBounds = CopyWorldBounds(value.fakeSpriteZoneBounds),
                overlapAllowances = CopyOverlapAllowances(value.overlapAllowances),
            };
        }

        private static RailLayoutDefinition CopyRailLayout(RailLayoutDefinition value)
        {
            if (value == null)
                return null;

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
                return null;

            return new WorldBoundsDefinition
            {
                hasWorldBounds = value.hasWorldBounds,
                worldX = value.worldX,
                worldZ = value.worldZ,
                worldWidth = value.worldWidth,
                worldDepth = value.worldDepth,
            };
        }

        private static PlacementOverlapAllowanceDefinition[] CopyOverlapAllowances(PlacementOverlapAllowanceDefinition[] values)
        {
            PlacementOverlapAllowanceDefinition[] safeValues = values ?? new PlacementOverlapAllowanceDefinition[0];
            if (safeValues.Length == 0)
                return new PlacementOverlapAllowanceDefinition[0];

            var copies = new PlacementOverlapAllowanceDefinition[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PlacementOverlapAllowanceDefinition value = safeValues[i];
                copies[i] = value == null
                    ? new PlacementOverlapAllowanceDefinition()
                    : new PlacementOverlapAllowanceDefinition
                    {
                        counterpartRole = value.counterpartRole,
                        widthCells = value.widthCells,
                        depthCells = value.depthCells,
                        centerOffsetX = value.centerOffsetX,
                        centerOffsetZ = value.centerOffsetZ,
                    };
            }

            return copies;
        }

        private static PlayableScenarioFacilityOptions CreateRoleDefaultFacilityOptions(string role)
        {
            return FacilityScenarioOptionRules.CreateRoleDefaultFacilityOptions(role);
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
                   normalizedKind == PromptIntentObjectiveKinds.UNLOCK_OBJECT;
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
            public const float RailSpawnIntervalSeconds = 1f;
            public const float RailTravelDurationSeconds = 1f;

            public static PlayableScenarioPlayerOptions CreatePlayerOptions()
            {
                return new PlayableScenarioPlayerOptions
                {
                    itemStacker = new PlayableScenarioFacilityOptions.StackerTuning
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
