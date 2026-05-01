using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Pipeline;
using UnityEngine;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public sealed class PromptIntentJsonValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public PlayablePromptIntent Contract;
        public List<string> Errors = new List<string>();
        public List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();
    }

    public static class PromptIntentJsonValidator
    {
        public static PromptIntentJsonValidationResult Validate(string json, PlayableObjectCatalog catalog)
        {
            var result = new PromptIntentJsonValidationResult();

            AuthoringInputContractDetectionResult detection = AuthoringInputContractDetector.Detect(json);
            if (!detection.IsPromptIntent)
                return Fail(result, PlayableFailureCode.UnsupportedInputContract, detection.Message);

            PromptIntentJsonShapeValidationResult shapeValidation = PromptIntentJsonShapeValidator.Validate(json);
            if (!shapeValidation.IsValid)
                return CopyFailure(result, shapeValidation.FailureCode, shapeValidation.Errors);

            ValidateUnsupportedLifecyclePatterns(json, result);
            ValidateExplicitPlayerOptionZeros(json, result);
            try
            {
                result.Contract = JsonUtility.FromJson<PlayablePromptIntent>(json);
            }
            catch (ArgumentException exception)
            {
                return Fail(result, PlayableFailureCode.InvalidJson, "JSON 역직렬화에 실패했습니다: " + exception.Message);
            }

            if (result.Contract == null)
                return Fail(result, PlayableFailureCode.InvalidJson, "PlayablePromptIntent가 null입니다.");

            if (string.IsNullOrWhiteSpace(result.Contract.themeId))
                Fail(result, PlayableFailureCode.MissingRequiredField, "themeId는 필수입니다.");

            ValidateCurrencies(result.Contract.currencies, result);
            ValidateSaleValues(result.Contract.saleValues, result);
            ValidateObjects(result.Contract.objects, catalog, result);
            ValidateContentSelections(result.Contract.contentSelections, result);
            ValidateStages(result.Contract.stages, result);
            ValidatePlayerOptions(result.Contract.playerOptions, result);

            result.IsValid = result.Errors.Count == 0;
            result.Message = result.IsValid ? "유효합니다." : result.Errors[0];
            if (!result.IsValid && result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.IntentValidationFailed;
            return result;
        }

        private static void ValidateCurrencies(PromptIntentCurrencyDefinition[] values, PromptIntentJsonValidationResult result)
        {
            var seenCurrencyIds = new HashSet<string>(StringComparer.Ordinal);
            PromptIntentCurrencyDefinition[] safeValues = values ?? new PromptIntentCurrencyDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PromptIntentCurrencyDefinition value = safeValues[i];
                if (value == null)
                {
                    Fail(result, PlayableFailureCode.InvalidValue, "currencies[" + i + "]가 null입니다.");
                    continue;
                }

                string currencyId = Normalize(value.currencyId);
                if (string.IsNullOrEmpty(currencyId))
                    Fail(result, PlayableFailureCode.MissingRequiredField, "currencies[" + i + "].currencyId는 필수입니다.");
                else if (!seenCurrencyIds.Add(currencyId))
                    Fail(result, PlayableFailureCode.DuplicateIdentifier, "중복된 currencies currencyId '" + currencyId + "'입니다.");

                if (value.unitValue <= 0)
                    Fail(result, PlayableFailureCode.InvalidValue, "currencies[" + i + "].unitValue는 0보다 커야 합니다.");
                if (value.startingAmountValue < 0)
                    Fail(result, PlayableFailureCode.InvalidValue, "currencies[" + i + "].startingAmountValue는 0 이상이어야 합니다.");
            }
        }

        private static void ValidateSaleValues(PromptIntentSaleValueDefinition[] values, PromptIntentJsonValidationResult result)
        {
            var seenItems = new HashSet<string>(StringComparer.Ordinal);
            PromptIntentSaleValueDefinition[] safeValues = values ?? new PromptIntentSaleValueDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PromptIntentSaleValueDefinition value = safeValues[i];
                if (value == null)
                {
                    Fail(result, PlayableFailureCode.InvalidValue, "saleValues[" + i + "]가 null입니다.");
                    continue;
                }

                string itemKey = ValidateItemRef(value.item, "saleValues[" + i + "].item", result, required: true);
                if (!string.IsNullOrEmpty(itemKey) && !seenItems.Add(itemKey))
                    Fail(result, PlayableFailureCode.DuplicateIdentifier, "중복된 saleValues item '" + itemKey + "'입니다.");

                if (string.IsNullOrEmpty(Normalize(value.currencyId)))
                    Fail(result, PlayableFailureCode.MissingRequiredField, "saleValues[" + i + "].currencyId는 필수입니다.");
                if (value.amountValue <= 0)
                    Fail(result, PlayableFailureCode.InvalidValue, "saleValues[" + i + "].amountValue는 0보다 커야 합니다.");
            }
        }

        private static void ValidateObjects(
            PromptIntentObjectDefinition[] values,
            PlayableObjectCatalog catalog,
            PromptIntentJsonValidationResult result)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            PromptIntentObjectDefinition[] safeValues = values ?? new PromptIntentObjectDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PromptIntentObjectDefinition value = safeValues[i];
                if (value == null)
                {
                    Fail(result, PlayableFailureCode.InvalidValue, "objects[" + i + "]가 null입니다.");
                    continue;
                }

                string objectId = Normalize(value.id);
                string role = Normalize(value.role);
                if (string.IsNullOrEmpty(objectId))
                    Fail(result, PlayableFailureCode.MissingRequiredField, "objects[" + i + "].id는 필수입니다.");
                else if (!seenIds.Add(objectId))
                    Fail(result, PlayableFailureCode.DuplicateIdentifier, "중복된 objects id '" + objectId + "'입니다.");

                if (string.IsNullOrEmpty(role))
                    Fail(result, PlayableFailureCode.MissingRequiredField, "objects[" + i + "].role은 필수입니다.");
                else if (!IsSupportedObjectRole(role, catalog))
                    Fail(result, PlayableFailureCode.InvalidValue, "objects[" + i + "].role '" + role + "'은(는) 지원되지 않습니다.");

                string designId = Normalize(value.designId);
                if (PromptIntentContractRegistry.ObjectRoleSupportsDesignId(role) && string.IsNullOrEmpty(designId))
                    Fail(result, PlayableFailureCode.MissingRequiredField, "objects[" + i + "].designId는 필수입니다. current catalog의 실제 designId를 사용해야 합니다.");
                ValidateObjectFeatureOptions(value.featureOptions, role, objectId, "objects[" + i + "].featureOptions", result);
            }
        }

        private static void ValidateObjectFeatureOptions(
            PlayableScenarioFeatureOptions value,
            string role,
            string objectId,
            string label,
            PromptIntentJsonValidationResult result)
        {
            string featureType = Normalize(value.featureType);
            string targetId = Normalize(value.targetId);
            string optionsJson = Normalize(value.optionsJson);
            bool isCoreObject = IsCoreObjectRole(role);

            if (isCoreObject)
            {
                if (!string.IsNullOrEmpty(featureType) ||
                    !string.IsNullOrEmpty(targetId) ||
                    !string.IsNullOrEmpty(optionsJson))
                {
                    Fail(result, PlayableFailureCode.InvalidValue, label + "는 core object role '" + role + "'에서 사용할 수 없습니다.");
                }

                return;
            }

            if (string.IsNullOrEmpty(featureType))
                Fail(result, PlayableFailureCode.MissingRequiredField, label + ".featureType이 필요합니다.");
            else
            {
                string expectedFeatureType = ResolveFeatureTypeForRole(role);
                if (string.IsNullOrEmpty(expectedFeatureType))
                    Fail(result, PlayableFailureCode.InvalidValue, label + "의 object role '" + role + "'에 연결된 active feature descriptor가 없습니다.");
                else if (!string.Equals(featureType, expectedFeatureType, StringComparison.Ordinal))
                    Fail(result, PlayableFailureCode.InvalidValue, label + ".featureType '" + featureType + "'는 object role '" + role + "'의 descriptor featureType '" + expectedFeatureType + "'와 같아야 합니다.");
            }

            if (string.IsNullOrEmpty(targetId))
                Fail(result, PlayableFailureCode.MissingRequiredField, label + ".targetId가 필요합니다.");
            else if (!string.Equals(targetId, objectId, StringComparison.Ordinal))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".targetId는 objects[].id와 일치해야 합니다.");

            if (string.IsNullOrEmpty(optionsJson))
                Fail(result, PlayableFailureCode.MissingRequiredField, label + ".optionsJson이 필요합니다.");
            else if (!LooksLikeJsonObject(optionsJson))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".optionsJson은 JSON object 문자열이어야 합니다.");
        }

        private static bool LooksLikeJsonObject(string value)
        {
            string normalized = Normalize(value);
            return normalized.Length >= 2 && normalized[0] == '{' && normalized[normalized.Length - 1] == '}';
        }

        private static string ResolveFeatureTypeForRole(string role)
        {
            return Normalize(PromptIntentContractRegistry.ResolveFeatureTypeForRole(role));
        }

        private static bool IsCoreObjectRole(string role)
        {
            string normalized = Normalize(role);
            return string.Equals(normalized, PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal) ||
                   string.Equals(normalized, PromptIntentObjectRoles.UNLOCK_PAD, StringComparison.Ordinal);
        }

        private static bool IsSupportedObjectRole(string role, PlayableObjectCatalog catalog)
        {
            string normalizedRole = Normalize(role);
            if (string.IsNullOrEmpty(normalizedRole))
                return false;
            if (PromptIntentObjectRoles.IsSupported(normalizedRole))
                return true;
            if (TryResolveCatalogFeatureObjectRole(catalog, normalizedRole, out _))
                return true;
            return TryResolveRuntimeDescriptorCatalogRole(catalog, normalizedRole, out _);
        }

        private static bool TryResolveRuntimeDescriptorCatalogRole(PlayableObjectCatalog catalog, string role, out string objectId)
        {
            objectId = string.Empty;
            if (catalog == null)
                return false;

            string normalizedRole = Normalize(role);
            FeatureDescriptor[] descriptors = catalog.FeatureDescriptors ?? new FeatureDescriptor[0];
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                FeatureDescriptor descriptor = descriptors[descriptorIndex];
                if (descriptor == null)
                    continue;

                FeatureCompiledGameplayRoleDescriptor[] mappings =
                    descriptor.compiledGameplayRoleMappings ?? new FeatureCompiledGameplayRoleDescriptor[0];
                for (int mappingIndex = 0; mappingIndex < mappings.Length; mappingIndex++)
                {
                    FeatureCompiledGameplayRoleDescriptor mapping = mappings[mappingIndex];
                    if (mapping == null ||
                        !string.Equals(Normalize(mapping.role), normalizedRole, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string mappedObjectId = Normalize(mapping.gameplayObjectId);
                    if (string.IsNullOrEmpty(mappedObjectId))
                        continue;
                    if (!catalog.TryGetGameplayEntry(mappedObjectId, out GameplayCatalogEntry entry) || entry == null)
                        continue;

                    objectId = mappedObjectId;
                    return true;
                }

                FeatureObjectRoleDescriptor[] roles =
                    descriptor.objectRoles ?? new FeatureObjectRoleDescriptor[0];
                for (int roleIndex = 0; roleIndex < roles.Length; roleIndex++)
                {
                    FeatureObjectRoleDescriptor objectRole = roles[roleIndex];
                    if (objectRole == null ||
                        !objectRole.catalogBacked ||
                        !string.Equals(Normalize(objectRole.role), normalizedRole, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string descriptorFeatureType = Normalize(descriptor.featureType);
                    if (string.IsNullOrEmpty(descriptorFeatureType))
                        continue;
                    if (!catalog.TryGetGameplayEntry(descriptorFeatureType, out GameplayCatalogEntry entry) || entry == null)
                        continue;

                    objectId = descriptorFeatureType;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveCatalogFeatureObjectRole(PlayableObjectCatalog catalog, string role, out string objectId)
        {
            objectId = string.Empty;
            if (catalog == null)
                return false;

            string normalizedRole = Normalize(role);
            if (string.IsNullOrEmpty(normalizedRole))
                return false;

            if (!catalog.TryGetGameplayEntry(normalizedRole, out GameplayCatalogEntry entry) || entry == null)
                return false;
            if (!string.Equals(Normalize(entry.category), GameplayCatalog.FEATURE_CATEGORY, StringComparison.Ordinal))
                return false;

            objectId = normalizedRole;
            return true;
        }

        private static void ValidateContentSelections(
            ContentSelectionDefinition[] values,
            PromptIntentJsonValidationResult result)
        {
            var seenObjectIds = new HashSet<string>(StringComparer.Ordinal);
            ContentSelectionDefinition[] safeValues = values ?? new ContentSelectionDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                ContentSelectionDefinition value = safeValues[i];
                string label = "contentSelections[" + i + "]";
                if (value == null)
                {
                    Fail(result, PlayableFailureCode.InvalidValue, label + "가 null입니다.");
                    continue;
                }

                string objectId = Normalize(value.objectId);
                if (string.IsNullOrEmpty(objectId))
                    Fail(result, PlayableFailureCode.MissingRequiredField, label + ".objectId는 필수입니다.");
                else if (!seenObjectIds.Add(objectId))
                    Fail(result, PlayableFailureCode.DuplicateIdentifier, "중복된 contentSelections objectId '" + objectId + "'입니다.");

                string designId = Normalize(value.designId);
                if (string.IsNullOrEmpty(designId))
                    Fail(result, PlayableFailureCode.MissingRequiredField, label + ".designId는 필수입니다.");
                else if (ContentSelectionRules.IsUnsetDesignId(designId))
                    Fail(result, PlayableFailureCode.InvalidValue, label + ".designId는 '" + ContentSelectionRules.DESIGN_ID_NOT_SET + "'일 수 없습니다. 실제 카탈로그 design을 선택해야 합니다.");
            }

            for (int i = 0; i < ContentSelectionRules.REQUIRED_OBJECT_IDS.Length; i++)
            {
                string requiredObjectId = ContentSelectionRules.REQUIRED_OBJECT_IDS[i];
                if (!seenObjectIds.Contains(requiredObjectId))
                {
                    Fail(
                        result,
                        PlayableFailureCode.MissingRequiredField,
                        "필수 UI content '" + requiredObjectId + "'에 대한 contentSelections entry가 필요합니다.");
                }
            }
        }

        private static void ValidateStages(PromptIntentStageDefinition[] values, PromptIntentJsonValidationResult result)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            PromptIntentStageDefinition[] safeValues = values ?? new PromptIntentStageDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                PromptIntentStageDefinition stage = safeValues[i];
                if (stage == null)
                {
                    Fail(result, PlayableFailureCode.InvalidValue, "stages[" + i + "]가 null입니다.");
                    continue;
                }

                string stageId = Normalize(stage.id);
                if (string.IsNullOrEmpty(stageId))
                    Fail(result, PlayableFailureCode.MissingRequiredField, "stages[" + i + "].id는 필수입니다.");
                else if (!seenIds.Add(stageId))
                    Fail(result, PlayableFailureCode.DuplicateIdentifier, "중복된 stages id '" + stageId + "'입니다.");

                ValidateCondition(stage.enterWhen, "stages[" + i + "].enterWhen", result);
                ValidateEffects(stage.onEnter, "stages[" + i + "].onEnter", result);
                ValidateObjectives(stage.objectives, "stages[" + i + "].objectives", result);
                ValidateEffects(stage.onComplete, "stages[" + i + "].onComplete", result);
            }
        }

        private static void ValidatePlayerOptions(PromptIntentPlayerOptions value, PromptIntentJsonValidationResult result)
        {
            if (value == null)
                return;

            if (value.itemStackMaxCount != 0 && value.itemStackMaxCount < 1)
                Fail(result, PlayableFailureCode.InvalidValue, "playerOptions.itemStackMaxCount는 1 이상이어야 합니다.");
        }

        private static void ValidateExplicitPlayerOptionZeros(string json, PromptIntentJsonValidationResult result)
        {
            if (HasExplicitZeroValue(json, "itemStackMaxCount"))
                Fail(result, PlayableFailureCode.InvalidValue, "playerOptions.itemStackMaxCount에 0을 명시할 수 없습니다.");
        }

        private static bool HasExplicitZeroValue(string json, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
                return false;

            string pattern = "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*0+(?:\\.0+)?(?=\\s*[,}])";
            return Regex.IsMatch(json, pattern, RegexOptions.CultureInvariant);
        }

        private static void ValidateEffects(PromptIntentEffectDefinition[] values, string label, PromptIntentJsonValidationResult result)
        {
            PromptIntentEffectDefinition[] safeValues = values ?? new PromptIntentEffectDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
                ValidateEffect(safeValues[i], label + "[" + i + "]", result);
        }

        private static void ValidateObjectives(PromptIntentObjectiveDefinition[] values, string label, PromptIntentJsonValidationResult result)
        {
            PromptIntentObjectiveDefinition[] safeValues = values ?? new PromptIntentObjectiveDefinition[0];
            for (int i = 0; i < safeValues.Length; i++)
                ValidateObjective(safeValues[i], label + "[" + i + "]", result);
        }

        private static void ValidateCondition(PromptIntentConditionDefinition value, string label, PromptIntentJsonValidationResult result)
        {
            if (value == null)
            {
                Fail(result, PlayableFailureCode.MissingRequiredField, label + "가 필요합니다.");
                return;
            }

            string kind = Normalize(value.kind);
            if (!PromptIntentConditionKinds.IsSupported(kind))
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + ".kind '" + kind + "'은(는) 지원되지 않습니다.");
                return;
            }

            ValidateConditionStringField(
                value.stageId,
                label + ".stageId",
                kind,
                PromptIntentContractRegistry.ConditionSupportsStageId(kind),
                PromptIntentContractRegistry.ConditionRequiresStageId(kind),
                result);
            ValidateConditionStringField(
                value.targetObjectId,
                label + ".targetObjectId",
                kind,
                PromptIntentContractRegistry.ConditionSupportsTargetObjectId(kind),
                PromptIntentContractRegistry.ConditionRequiresTargetObjectId(kind),
                result);
            ValidateConditionItemField(
                value.item,
                label + ".item",
                kind,
                PromptIntentContractRegistry.ConditionSupportsItem(kind),
                PromptIntentContractRegistry.ConditionRequiresItem(kind),
                result);
            ValidateConditionStringField(
                value.currencyId,
                label + ".currencyId",
                kind,
                PromptIntentContractRegistry.ConditionSupportsCurrencyId(kind),
                PromptIntentContractRegistry.ConditionRequiresCurrencyId(kind),
                result);

            if (PromptIntentContractRegistry.ConditionRequiresPositiveAmountValue(kind))
            {
                if (value.amountValue <= 0)
                    Fail(result, PlayableFailureCode.InvalidValue, label + ".amountValue는 kind '" + kind + "'에서 0보다 커야 합니다.");
            }
            else if (!PromptIntentContractRegistry.ConditionSupportsAmountValue(kind) && value.amountValue != 0)
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + ".amountValue는 kind '" + kind + "'에서 허용되지 않습니다.");
            }
        }

        private static void ValidateObjective(PromptIntentObjectiveDefinition value, string label, PromptIntentJsonValidationResult result)
        {
            if (value == null)
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + "가 null입니다.");
                return;
            }

            string kind = Normalize(value.kind);
            if (!PromptIntentObjectiveKinds.IsSupported(kind))
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + ".kind '" + kind + "'은(는) 지원되지 않습니다.");
                return;
            }

            ValidateObjectiveStringField(
                value.targetObjectId,
                label + ".targetObjectId",
                kind,
                PromptIntentContractRegistry.ObjectiveSupportsTargetObjectId(kind),
                PromptIntentContractRegistry.ObjectiveRequiresTargetObjectId(kind),
                result);
            ValidateObjectiveItemField(
                value.item,
                label + ".item",
                kind,
                PromptIntentContractRegistry.ObjectiveSupportsItem(kind),
                PromptIntentContractRegistry.ObjectiveRequiresItem(kind),
                result);
            ValidateObjectiveItemField(
                value.inputItem,
                label + ".inputItem",
                kind,
                PromptIntentContractRegistry.ObjectiveSupportsInputItem(kind),
                PromptIntentContractRegistry.ObjectiveRequiresInputItem(kind),
                result);
            ValidateObjectiveStringField(
                value.currencyId,
                label + ".currencyId",
                kind,
                PromptIntentContractRegistry.ObjectiveSupportsCurrencyId(kind),
                PromptIntentContractRegistry.ObjectiveRequiresCurrencyId(kind),
                result);

            if (PromptIntentContractRegistry.ObjectiveRequiresPositiveAmountValue(kind))
            {
                if (value.amountValue <= 0)
                    Fail(result, PlayableFailureCode.InvalidValue, label + ".amountValue는 kind '" + kind + "'에서 0보다 커야 합니다.");
            }
            else if (!PromptIntentContractRegistry.ObjectiveSupportsAmountValue(kind) && value.amountValue != 0)
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + ".amountValue는 kind '" + kind + "'에서 허용되지 않습니다.");
            }

            if (PromptIntentContractRegistry.ObjectiveRequiresPositiveSeconds(kind))
            {
                if (value.seconds <= 0f)
                    Fail(result, PlayableFailureCode.InvalidValue, label + ".seconds는 kind '" + kind + "'에서 0보다 커야 합니다.");
            }
            else if (!PromptIntentContractRegistry.ObjectiveSupportsSeconds(kind) && value.seconds != 0f)
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + ".seconds는 kind '" + kind + "'에서 허용되지 않습니다.");
            }
        }

        private static void ValidateConditionStringField(
            string value,
            string label,
            string kind,
            bool supportsField,
            bool requiresField,
            PromptIntentJsonValidationResult result)
        {
            if (requiresField)
            {
                RequireValue(value, label, result, kind);
                return;
            }

            if (!supportsField)
                RequireEmpty(value, label, result, kind);
        }

        private static void ValidateConditionItemField(
            ItemRef value,
            string label,
            string kind,
            bool supportsField,
            bool requiresField,
            PromptIntentJsonValidationResult result)
        {
            if (requiresField)
            {
                RequireItemValue(value, label, result, kind);
                return;
            }

            if (!supportsField)
                RequireEmptyItem(value, label, result, kind);
        }

        private static void ValidateObjectiveStringField(
            string value,
            string label,
            string kind,
            bool supportsField,
            bool requiresField,
            PromptIntentJsonValidationResult result)
        {
            if (requiresField)
            {
                RequireValue(value, label, result, kind);
                return;
            }

            if (!supportsField)
                RequireEmpty(value, label, result, kind);
        }

        private static void ValidateObjectiveItemField(
            ItemRef value,
            string label,
            string kind,
            bool supportsField,
            bool requiresField,
            PromptIntentJsonValidationResult result)
        {
            if (requiresField)
            {
                RequireItemValue(value, label, result, kind);
                return;
            }

            if (!supportsField)
                RequireEmptyItem(value, label, result, kind);
        }

        private static void ValidateEffect(PromptIntentEffectDefinition value, string label, PromptIntentJsonValidationResult result)
        {
            if (value == null)
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + "가 null입니다.");
                return;
            }

            string kind = Normalize(value.kind);
            if (!PromptIntentEffectKinds.IsSupported(kind))
            {
                Fail(result, PlayableFailureCode.InvalidValue, label + ".kind '" + kind + "'은(는) 지원되지 않습니다.");
                return;
            }

            string timing = Normalize(value.timing);
            string eventKey = Normalize(value.eventKey);
            if (value.timing != null && string.IsNullOrEmpty(timing))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".timing이 비어 있습니다. 생략하거나 유효한 timing을 사용해야 합니다.");
            if (!string.IsNullOrEmpty(timing) && !PromptIntentEffectTimingKinds.IsSupported(timing))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".timing '" + timing + "'은(는) 지원되지 않습니다.");
            if (!string.IsNullOrEmpty(timing) && !PromptIntentEffectKinds.SupportsExplicitTiming(kind))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".timing은 explicit timing을 지원하는 effect kind에서만 사용할 수 있습니다.");
            if (PromptIntentEffectKinds.RequiresEventKey(kind) && string.IsNullOrEmpty(eventKey))
                Fail(result, PlayableFailureCode.MissingRequiredField, label + ".eventKey는 kind '" + kind + "'에서 필수입니다.");
            if (!string.IsNullOrEmpty(eventKey) && !PromptIntentEffectKinds.SupportsEventKey(kind))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".eventKey는 kind '" + kind + "'에서 허용되지 않습니다.");
            if (!string.IsNullOrEmpty(eventKey) && !FlowTargetEventKeys.IsSupported(eventKey))
                Fail(result, PlayableFailureCode.InvalidValue, label + ".eventKey '" + value.eventKey + "'는 지원되지 않습니다.");

            if (PromptIntentEffectKinds.RequiresTargetObjectId(kind))
                RequireValue(value.targetObjectId, label + ".targetObjectId", result, kind);
            else
                RequireEmpty(value.targetObjectId, label + ".targetObjectId", result, kind);

            RequireEmptyItem(value.item, label + ".item", result, kind);
            RequireEmpty(value.currencyId, label + ".currencyId", result, kind);
            if (value.amountValue != 0)
                Fail(result, PlayableFailureCode.InvalidValue, label + ".amountValue는 kind '" + kind + "'에서 허용되지 않습니다.");
            if (value.seconds != 0f)
                Fail(result, PlayableFailureCode.InvalidValue, label + ".seconds는 kind '" + kind + "'에서 허용되지 않습니다.");
        }

        private static string ValidateItemRef(ItemRef item, string label, PromptIntentJsonValidationResult result, bool required)
        {
            if (item == null)
            {
                if (required)
                    Fail(result, PlayableFailureCode.MissingRequiredField, label + "가 필요합니다.");
                return string.Empty;
            }

            string familyId = Normalize(item.familyId);
            string variantId = Normalize(item.variantId);
            bool hasFamily = !string.IsNullOrEmpty(familyId);
            bool hasVariant = !string.IsNullOrEmpty(variantId);
            if (!hasFamily && !hasVariant)
            {
                if (required)
                    Fail(result, PlayableFailureCode.MissingRequiredField, label + ".familyId와 " + label + ".variantId는 필수입니다.");
                return string.Empty;
            }

            if (!hasFamily)
                Fail(result, PlayableFailureCode.MissingRequiredField, label + ".familyId는 필수입니다.");
            if (!hasVariant)
                Fail(result, PlayableFailureCode.MissingRequiredField, label + ".variantId는 필수입니다.");

            return hasFamily && hasVariant ? familyId + "/" + variantId : string.Empty;
        }

        private static void RequireValue(string value, string label, PromptIntentJsonValidationResult result, string kind)
        {
            if (string.IsNullOrWhiteSpace(value))
                Fail(result, PlayableFailureCode.MissingRequiredField, label + "는 kind '" + kind + "'에서 필수입니다.");
        }

        private static void RequireEmpty(string value, string label, PromptIntentJsonValidationResult result, string kind)
        {
            if (!string.IsNullOrWhiteSpace(value))
                Fail(result, PlayableFailureCode.InvalidValue, label + "는 kind '" + kind + "'에서 허용되지 않습니다.");
        }

        private static void RequireItemValue(ItemRef item, string label, PromptIntentJsonValidationResult result, string kind)
        {
            ValidateItemRef(item, label, result, required: true);
        }

        private static void RequireEmptyItem(ItemRef item, string label, PromptIntentJsonValidationResult result, string kind)
        {
            if (!ItemRefUtility.IsEmpty(item))
                Fail(result, PlayableFailureCode.InvalidValue, label + "는 kind '" + kind + "'에서 허용되지 않습니다.");
        }

        private static void ValidateUnsupportedLifecyclePatterns(string json, PromptIntentJsonValidationResult result)
        {
            string safeJson = json ?? string.Empty;
            bool hasVisibleAtStart =
                Regex.IsMatch(safeJson, "\"initialVisibility\"\\s*:\\s*\"visible\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                Regex.IsMatch(safeJson, "\"visible_non_interactive\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            bool hasInactiveAtStart =
                Regex.IsMatch(safeJson, "\"initialInteractionMode\"\\s*:\\s*\"inactive\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                Regex.IsMatch(safeJson, "\"visible_non_interactive\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (hasVisibleAtStart && hasInactiveAtStart)
            {
                Fail(result, PlayableFailureCode.IntentValidationFailed, "visible but non-interactive lifecycle는 이번 runtime contract에서 지원되지 않습니다.");
            }
        }

        private static PromptIntentJsonValidationResult Fail(PromptIntentJsonValidationResult result, PlayableFailureCode code, string message)
        {
            return Fail(result, code, new ValidationIssueRecord(
                MapFailureCodeToRuleId(code),
                ValidationSeverity.Blocker,
                message,
                "PromptIntentJson"));
        }

        private static PromptIntentJsonValidationResult Fail(PromptIntentJsonValidationResult result, PlayableFailureCode code, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return result;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = code;
            result.Errors.Add(issue.message ?? string.Empty);
            result.Issues ??= new List<ValidationIssueRecord>();
            result.Issues.Add(issue);
            return result;
        }

        private static string MapFailureCodeToRuleId(PlayableFailureCode code)
        {
            switch (code)
            {
                case PlayableFailureCode.InvalidJson:
                    return ValidationRuleId.INTENT_JSON_PARSE_FAILED;
                case PlayableFailureCode.UnknownKey:
                    return ValidationRuleId.INTENT_JSON_UNKNOWN_KEY;
                case PlayableFailureCode.MissingRequiredField:
                    return ValidationRuleId.INTENT_JSON_MISSING_REQUIRED_FIELD;
                case PlayableFailureCode.InvalidValue:
                    return ValidationRuleId.INTENT_JSON_INVALID_VALUE;
                case PlayableFailureCode.DuplicateIdentifier:
                    return ValidationRuleId.INTENT_JSON_DUPLICATE_IDENTIFIER;
                default:
                    return ValidationRuleId.INTENT_JSON_GENERIC;
            }
        }

        private static PromptIntentJsonValidationResult CopyFailure(
            PromptIntentJsonValidationResult result,
            PlayableFailureCode code,
            List<string> errors)
        {
            List<string> safeErrors = errors ?? new List<string>();
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = code;

            for (int i = 0; i < safeErrors.Count; i++)
                result.Errors.Add(safeErrors[i]);

            if (result.Errors.Count == 0)
                result.Errors.Add("PlayablePromptIntent JSON shape 검증에 실패했습니다.");

            result.Message = result.Errors[0];
            result.IsValid = false;
            return result;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public sealed class PromptIntentJsonShapeValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public List<string> Errors = new List<string>();
    }

    public static class PromptIntentJsonShapeValidator
    {
        private enum JsonObjectSchema
        {
            Root = 0,
            Currency,
            SaleValue,
            ScenarioObject,
            ContentSelection,
            FeatureOptions,
            FeatureLayout,
            WorldBounds,
            Stage,
            Condition,
            Objective,
            Effect,
            ItemRef,
            RootPlayerOptions,
        }

        private enum JsonValueContract
        {
            Any = 0,
            CurrencyArray,
            SaleValueArray,
            ObjectArray,
            ContentSelectionArray,
            FeatureOptionsObject,
            FeatureLayoutObject,
            WorldBoundsObject,
            StageArray,
            ConditionObject,
            ObjectiveArray,
            EffectArray,
            ItemRefObject,
            RootPlayerOptionsObject,
        }

        public static PromptIntentJsonShapeValidationResult Validate(string json)
        {
            var result = new PromptIntentJsonShapeValidationResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (string.IsNullOrWhiteSpace(json))
                return Fail(result, PlayableFailureCode.InvalidJson, "JSON 입력이 비어 있습니다.");

            var parser = new JsonShapeParser(json, result);
            parser.Validate();
            if (result.Errors.Count == 0)
            {
                result.IsValid = true;
                result.FailureCode = PlayableFailureCode.None;
                result.Message = "유효합니다.";
            }
            else
            {
                result.IsValid = false;
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.UnknownKey;
                result.Message = result.Errors[0];
            }

            return result;
        }

        private static PromptIntentJsonShapeValidationResult Fail(
            PromptIntentJsonShapeValidationResult result,
            PlayableFailureCode code,
            string message)
        {
            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = code;
            result.Errors.Add(message);
            return result;
        }

        private sealed class JsonShapeParser
        {
            private readonly string _json;
            private readonly PromptIntentJsonShapeValidationResult _result;
            private int _index;

            public JsonShapeParser(
                string json,
                PromptIntentJsonShapeValidationResult result)
            {
                _json = json ?? string.Empty;
                _result = result;
                _index = 0;
            }

            public void Validate()
            {
                if (!ValidateRoot())
                    return;

                SkipWhitespace();
                if (_index < _json.Length)
                    Fail(PlayableFailureCode.InvalidJson, "JSON 객체 뒤에 불필요한 내용이 남아 있습니다.");
            }

            private bool ValidateRoot()
            {
                SkipWhitespace();
                if (!ValidateObject(JsonObjectSchema.Root, "root"))
                    return false;

                return true;
            }

            private bool ValidateObject(JsonObjectSchema schema, string label)
            {
                if (!TryConsume('{'))
                    return Fail(PlayableFailureCode.InvalidJson, label + "는 JSON object여야 합니다.");

                SkipWhitespace();
                if (TryConsume('}'))
                    return true;

                while (_index < _json.Length)
                {
                    if (!TryReadString(out string key))
                        return Fail(PlayableFailureCode.InvalidJson, label + "의 property key를 읽지 못했습니다.");

                    SkipWhitespace();
                    if (!TryConsume(':'))
                        return Fail(PlayableFailureCode.InvalidJson, label + "." + key + " 뒤에 ':'가 필요합니다.");

                    if (!TryResolveContract(schema, key, out JsonValueContract contract))
                    {
                        return Fail(PlayableFailureCode.UnknownKey, BuildUnknownKeyMessage(label, key));
                    }

                    string propertyLabel = BuildPropertyLabel(label, key);
                    if (!ValidateValue(contract, propertyLabel))
                        return false;

                    SkipWhitespace();
                    if (TryConsume('}'))
                        return true;
                    if (!TryConsume(','))
                        return Fail(PlayableFailureCode.InvalidJson, label + "의 property 구분자 ','가 필요합니다.");

                    SkipWhitespace();
                }

                return Fail(PlayableFailureCode.InvalidJson, label + " object가 올바르게 닫히지 않았습니다.");
            }

            private bool ValidateArray(JsonObjectSchema itemSchema, string label)
            {
                if (!TryConsume('['))
                    return Fail(PlayableFailureCode.InvalidJson, label + "는 JSON array여야 합니다.");

                SkipWhitespace();
                if (TryConsume(']'))
                    return true;

                int elementIndex = 0;
                while (_index < _json.Length)
                {
                    string elementLabel = label + "[" + elementIndex + "]";
                    if (Peek() == '{')
                    {
                        if (!ValidateObject(itemSchema, elementLabel))
                            return false;
                    }
                    else
                    {
                        if (!SkipValue())
                            return Fail(PlayableFailureCode.InvalidJson, elementLabel + "를 읽지 못했습니다.");
                    }

                    elementIndex++;
                    SkipWhitespace();
                    if (TryConsume(']'))
                        return true;
                    if (!TryConsume(','))
                        return Fail(PlayableFailureCode.InvalidJson, label + "의 array 구분자 ','가 필요합니다.");

                    SkipWhitespace();
                }

                return Fail(PlayableFailureCode.InvalidJson, label + " array가 올바르게 닫히지 않았습니다.");
            }

            private bool ValidateValue(JsonValueContract contract, string label)
            {
                SkipWhitespace();
                switch (contract)
                {
                    case JsonValueContract.CurrencyArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.Currency, label);
                        return SkipValue();
                    case JsonValueContract.SaleValueArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.SaleValue, label);
                        return SkipValue();
                    case JsonValueContract.ObjectArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.ScenarioObject, label);
                        return SkipValue();
                    case JsonValueContract.ContentSelectionArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.ContentSelection, label);
                        return SkipValue();
                    case JsonValueContract.FeatureOptionsObject:
                        if (Peek() == '{')
                            return ValidateObject(JsonObjectSchema.FeatureOptions, label);
                        return SkipValue();
                    case JsonValueContract.FeatureLayoutObject:
                        if (Peek() == '{')
                            return ValidateObject(JsonObjectSchema.FeatureLayout, label);
                        return SkipValue();
                    case JsonValueContract.WorldBoundsObject:
                        if (Peek() == '{')
                            return ValidateObject(JsonObjectSchema.WorldBounds, label);
                        return SkipValue();
                    case JsonValueContract.StageArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.Stage, label);
                        return SkipValue();
                    case JsonValueContract.ConditionObject:
                        if (Peek() == '{')
                            return ValidateObject(JsonObjectSchema.Condition, label);
                        return SkipValue();
                    case JsonValueContract.ObjectiveArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.Objective, label);
                        return SkipValue();
                    case JsonValueContract.EffectArray:
                        if (Peek() == '[')
                            return ValidateArray(JsonObjectSchema.Effect, label);
                        return SkipValue();
                    case JsonValueContract.ItemRefObject:
                        if (Peek() == '{')
                            return ValidateObject(JsonObjectSchema.ItemRef, label);
                        return SkipValue();
                    case JsonValueContract.RootPlayerOptionsObject:
                        if (Peek() == '{')
                            return ValidateObject(JsonObjectSchema.RootPlayerOptions, label);
                        return SkipValue();
                    default:
                        return SkipValue();
                }
            }

            private bool SkipValue()
            {
                SkipWhitespace();
                char current = Peek();
                switch (current)
                {
                    case '{':
                        return SkipObject();
                    case '[':
                        return SkipArray();
                    case '"':
                        return TryReadString(out _);
                    case 't':
                        return TryReadLiteral("true");
                    case 'f':
                        return TryReadLiteral("false");
                    case 'n':
                        return TryReadLiteral("null");
                    default:
                        return TryReadNumber();
                }
            }

            private bool SkipObject()
            {
                if (!TryConsume('{'))
                    return false;

                SkipWhitespace();
                if (TryConsume('}'))
                    return true;

                while (_index < _json.Length)
                {
                    if (!TryReadString(out _))
                        return false;

                    SkipWhitespace();
                    if (!TryConsume(':'))
                        return false;

                    if (!SkipValue())
                        return false;

                    SkipWhitespace();
                    if (TryConsume('}'))
                        return true;
                    if (!TryConsume(','))
                        return false;

                    SkipWhitespace();
                }

                return false;
            }

            private bool SkipArray()
            {
                if (!TryConsume('['))
                    return false;

                SkipWhitespace();
                if (TryConsume(']'))
                    return true;

                while (_index < _json.Length)
                {
                    if (!SkipValue())
                        return false;

                    SkipWhitespace();
                    if (TryConsume(']'))
                        return true;
                    if (!TryConsume(','))
                        return false;

                    SkipWhitespace();
                }

                return false;
            }

            private bool TryReadString(out string value)
            {
                value = string.Empty;
                if (!TryConsume('"'))
                    return false;

                var chars = new List<char>();
                while (_index < _json.Length)
                {
                    char current = _json[_index++];
                    if (current == '"')
                    {
                        value = new string(chars.ToArray());
                        return true;
                    }

                    if (current != '\\')
                    {
                        chars.Add(current);
                        continue;
                    }

                    if (_index >= _json.Length)
                        return false;

                    char escaped = _json[_index++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            chars.Add(escaped);
                            break;
                        case 'b':
                            chars.Add('\b');
                            break;
                        case 'f':
                            chars.Add('\f');
                            break;
                        case 'n':
                            chars.Add('\n');
                            break;
                        case 'r':
                            chars.Add('\r');
                            break;
                        case 't':
                            chars.Add('\t');
                            break;
                        case 'u':
                            if (_index + 4 > _json.Length)
                                return false;

                            string hex = _json.Substring(_index, 4);
                            if (!ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                                return false;

                            chars.Add((char)code);
                            _index += 4;
                            break;
                        default:
                            return false;
                    }
                }

                return false;
            }

            private bool TryReadLiteral(string literal)
            {
                SkipWhitespace();
                if (_index + literal.Length > _json.Length)
                    return false;

                if (!string.Equals(_json.Substring(_index, literal.Length), literal, StringComparison.Ordinal))
                    return false;

                _index += literal.Length;
                return true;
            }

            private bool TryReadNumber()
            {
                SkipWhitespace();
                int start = _index;
                if (Peek() == '-')
                    _index++;

                if (!TryConsumeDigits())
                    return false;

                if (Peek() == '.')
                {
                    _index++;
                    if (!TryConsumeDigits())
                        return false;
                }

                char current = Peek();
                if (current == 'e' || current == 'E')
                {
                    _index++;
                    current = Peek();
                    if (current == '+' || current == '-')
                        _index++;
                    if (!TryConsumeDigits())
                        return false;
                }

                return _index > start;
            }

            private bool TryConsumeDigits()
            {
                int start = _index;
                while (_index < _json.Length && char.IsDigit(_json[_index]))
                    _index++;
                return _index > start;
            }

            private bool TryResolveContract(JsonObjectSchema schema, string key, out JsonValueContract contract)
            {
                contract = JsonValueContract.Any;
                switch (schema)
                {
                    case JsonObjectSchema.Root:
                        switch (key)
                        {
                            case "themeId":
                                return true;
                            case "currencies":
                                contract = JsonValueContract.CurrencyArray;
                                return true;
                            case "saleValues":
                                contract = JsonValueContract.SaleValueArray;
                                return true;
                            case "objects":
                                contract = JsonValueContract.ObjectArray;
                                return true;
                            case "contentSelections":
                                contract = JsonValueContract.ContentSelectionArray;
                                return true;
                            case "stages":
                                contract = JsonValueContract.StageArray;
                                return true;
                            case "playerOptions":
                                contract = JsonValueContract.RootPlayerOptionsObject;
                                return true;
                            default:
                                return false;
                        }
                    case JsonObjectSchema.Currency:
                        return key == "currencyId" ||
                               key == "unitValue" ||
                               key == "startingAmountValue" ||
                               key == "startVisualMode";
                    case JsonObjectSchema.SaleValue:
                        if (key == "currencyId" || key == "amountValue")
                            return true;
                        if (key == "item")
                        {
                            contract = JsonValueContract.ItemRefObject;
                            return true;
                        }
                        return false;
                    case JsonObjectSchema.ScenarioObject:
                        switch (key)
                        {
                            case "id":
                            case "role":
                            case "designId":
                                return true;
                            case "featureOptions":
                                contract = JsonValueContract.FeatureOptionsObject;
                                return true;
                            default:
                                return false;
                        }
                    case JsonObjectSchema.FeatureOptions:
                        return key == "featureType" ||
                               key == "targetId" ||
                               key == "optionsJson";
                    case JsonObjectSchema.FeatureLayout:
                        return key == "featureType" ||
                               key == "targetId" ||
                               key == "json";
                    case JsonObjectSchema.ContentSelection:
                        return key == "objectId" || key == "designId";
                    case JsonObjectSchema.WorldBounds:
                        return key == "hasWorldBounds" ||
                               key == "worldX" ||
                               key == "worldZ" ||
                               key == "worldWidth" ||
                               key == "worldDepth";
                    case JsonObjectSchema.Stage:
                        switch (key)
                        {
                            case "id":
                                return true;
                            case "enterWhen":
                                contract = JsonValueContract.ConditionObject;
                                return true;
                            case "onEnter":
                            case "onComplete":
                                contract = JsonValueContract.EffectArray;
                                return true;
                            case "objectives":
                                contract = JsonValueContract.ObjectiveArray;
                                return true;
                            default:
                                return false;
                        }
                    case JsonObjectSchema.Condition:
                        return key == "kind" ||
                               key == "stageId" ||
                               key == "targetObjectId" ||
                               key == "currencyId" ||
                               key == "amountValue" ||
                               ResolveItemRefContract(key, out contract);
                    case JsonObjectSchema.Objective:
                        return key == "kind" ||
                               key == "targetObjectId" ||
                               key == "currencyId" ||
                               key == "amountValue" ||
                               key == "seconds" ||
                               ResolveItemRefContract(key, out contract) ||
                               ResolveInputItemRefContract(key, out contract);
                    case JsonObjectSchema.Effect:
                        return key == "kind" ||
                               key == "timing" ||
                               key == "targetObjectId" ||
                               key == "eventKey" ||
                               key == "currencyId" ||
                               key == "amountValue" ||
                               key == "seconds" ||
                               ResolveItemRefContract(key, out contract);
                    case JsonObjectSchema.ItemRef:
                        return key == "familyId" || key == "variantId";
                    case JsonObjectSchema.RootPlayerOptions:
                        return key == "itemStackMaxCount";
                    default:
                        return false;
                }
            }

            private static bool ResolveItemRefContract(string key, out JsonValueContract contract)
            {
                contract = JsonValueContract.Any;
                if (key == "item")
                {
                    contract = JsonValueContract.ItemRefObject;
                    return true;
                }

                return false;
            }

            private static bool ResolveInputItemRefContract(string key, out JsonValueContract contract)
            {
                contract = JsonValueContract.Any;
                if (key == "inputItem")
                {
                    contract = JsonValueContract.ItemRefObject;
                    return true;
                }

                return false;
            }

            private static bool ResolveStartWhenContract(string key, out JsonValueContract contract)
            {
                contract = JsonValueContract.Any;
                if (key == "startWhen")
                {
                    contract = JsonValueContract.ConditionObject;
                    return true;
                }

                return false;
            }

            private bool TryConsume(char expected)
            {
                SkipWhitespace();
                if (_index >= _json.Length || _json[_index] != expected)
                    return false;

                _index++;
                return true;
            }

            private char Peek()
            {
                SkipWhitespace();
                return _index < _json.Length ? _json[_index] : '\0';
            }

            private void SkipWhitespace()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                    _index++;
            }

            private bool Fail(PlayableFailureCode code, string message)
            {
                if (_result.FailureCode == PlayableFailureCode.None)
                    _result.FailureCode = code;
                _result.Errors.Add(message);
                return false;
            }

            private static string BuildPropertyLabel(string label, string key)
            {
                return string.Equals(label, "root", StringComparison.Ordinal)
                    ? key
                    : label + "." + key;
            }

            private static string BuildUnknownKeyMessage(string label, string key)
            {
                if (label.StartsWith("contentSelections[", StringComparison.Ordinal) &&
                    string.Equals(key, "designIndex", StringComparison.Ordinal))
                {
                    return label + ".designIndex는 intent contentSelections[]에 허용되지 않습니다. designIndex를 제거하고 objectId와 실제 catalog designId만 남겨야 합니다.";
                }

                if (string.Equals(key, "objectId", StringComparison.Ordinal) ||
                    string.Equals(key, "itemId", StringComparison.Ordinal))
                {
                    return label + "." + key + "는 더 이상 허용되지 않습니다. ItemRef 스키마를 사용해야 합니다.";
                }

                return string.Equals(label, "root", StringComparison.Ordinal)
                    ? "root에 허용되지 않는 key '" + key + "'가 있습니다."
                    : label + "에 허용되지 않는 key '" + key + "'가 있습니다.";
            }
        }
    }
}

