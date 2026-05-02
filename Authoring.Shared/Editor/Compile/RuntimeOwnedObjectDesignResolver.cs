using System.Collections.Generic;
using System.Text;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Generation.Editor.Compile
{
    internal sealed class RuntimeOwnedObjectDesignSelection
    {
        public string objectId = string.Empty;
        public string designId = string.Empty;
    }

    internal sealed class RuntimeOwnedObjectDesignResolution
    {
        public List<RuntimeOwnedObjectDesignSelection> RequiredObjectDesigns = new List<RuntimeOwnedObjectDesignSelection>();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    internal static class RuntimeOwnedObjectDesignResolver
    {
        public static RuntimeOwnedObjectDesignResolution Resolve(
            CompiledSpawnData[] spawns,
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            FeatureOutputItemDefinition[] featureOutputItems,
            PlayableScenarioFeatureOptionDefinition[] featureOptions,
            ItemPriceDefinition[] itemPrices,
            CurrencyDefinition[] currencies,
            PlayableObjectCatalog catalog)
        {
            var result = new RuntimeOwnedObjectDesignResolution();
            var requiredObjectDesignKeys = new HashSet<string>(System.StringComparer.Ordinal);
            CompiledSpawnData[] safeSpawns = spawns ?? new CompiledSpawnData[0];

            if (catalog == null)
            {
                result.Errors.Add("PlayableObjectCatalog가 필요합니다.");
                return result;
            }

            HashSet<string> featureOutputItemTargets = BuildFeatureOutputItemTargets(featureOutputItems);
            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData spawn = safeSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.objectId))
                    continue;

                CollectDescriptorRuntimeOwnedObjectDesigns(
                    catalog,
                    spawn.objectId,
                    spawn.spawnKey,
                    featureOutputItemTargets,
                    requiredObjectDesignKeys,
                    result.Errors,
                    result.Warnings);

                if (spawn.designIndex < 0)
                    continue;

                if (!catalog.TryResolveGameplayPrefab(spawn.objectId.Trim(), spawn.designIndex, out GameObject prefab, out _))
                {
                    result.Errors.Add("gameplaySpawns[" + i + "]에서 objectId '" + spawn.objectId + "', designIndex '" + spawn.designIndex + "'에 대한 prefab을 해석하지 못했습니다.");
                    continue;
                }

                if (!PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata))
                {
                    result.Errors.Add("gameplaySpawns[" + i + "]의 prefab metadata를 읽지 못했습니다.");
                    continue;
                }

                ValidateSelectedFeatureDesignCapabilities(catalog, spawn.objectId, spawn.spawnKey, metadata, result.Errors);
                CollectRequiredObjectDesigns(metadata.generatedItemKeys, requiredObjectDesignKeys, result.Errors);

                if (CatalogPrefabMetadataCapabilityUtility.HasCapability(metadata, CatalogPrefabCapabilityIds.CUSTOMER_SINGLE_LINE))
                    AddSingleAvailableObjectDesign(
                        catalog,
                        CatalogIdentityRules.CUSTOMER_OBJECT_ID,
                        requiredObjectDesignKeys,
                        result.Errors,
                        result.Warnings,
                        "customer line feature");

            }

            IReadOnlyList<EditorBasedCatalog.Entry> editorEntries = catalog.EditorBased.GetEntries();
            for (int i = 0; i < editorEntries.Count; i++)
            {
                EditorBasedCatalog.Entry entry = editorEntries[i];
                if (entry == null || entry.prefab == null)
                    continue;

                if (!PortablePrefabMetadataUtility.TryGetMetadata(entry.prefab, out CatalogPrefabMetadata metadata))
                    continue;

                if (CatalogPrefabMetadataCapabilityUtility.HasCapability(metadata, CatalogPrefabCapabilityIds.MONEY_HANDLER))
                    AddSingleAvailableObjectDesign(
                        catalog,
                        CatalogIdentityRules.MONEY_OBJECT_ID,
                        requiredObjectDesignKeys,
                        result.Errors,
                        result.Warnings,
                        "money handler");
            }

            CollectAcceptedItems(featureAcceptedItems, requiredObjectDesignKeys);
            CollectOutputItems(featureOutputItems, requiredObjectDesignKeys);
            CollectFeatureOptionItemRefs(featureOptions, catalog, requiredObjectDesignKeys, result.Errors);
            CollectPricedItems(itemPrices, requiredObjectDesignKeys);
            CollectCurrencyVisuals(currencies, catalog, result.Errors, result.Warnings, requiredObjectDesignKeys);

            var sortedKeys = new List<string>(requiredObjectDesignKeys);
            sortedKeys.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                if (!ContentCatalogTokenUtility.TrySplitEntryId(sortedKeys[i], out string objectId, out string designId))
                    continue;

                result.RequiredObjectDesigns.Add(new RuntimeOwnedObjectDesignSelection
                {
                    objectId = objectId,
                    designId = designId,
                });
            }

            return result;
        }

        private static void ValidateSelectedFeatureDesignCapabilities(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
            string spawnKey,
            CatalogPrefabMetadata metadata,
            List<string> errors)
        {
            if (!TryResolveFeatureDescriptorForGameplayObjectId(catalog, gameplayObjectId, out FeatureDescriptor descriptor) ||
                descriptor.layoutRequirements == null)
            {
                return;
            }

            string[] requiredCapabilities = NormalizeStringArray(descriptor.layoutRequirements.requiredDesignCapabilities);
            for (int i = 0; i < requiredCapabilities.Length; i++)
            {
                string capability = requiredCapabilities[i];
                if (!CatalogPrefabMetadataCapabilityUtility.HasVerifiedCapability(metadata, capability))
                    errors.Add("feature '" + spawnKey + "' design에는 catalog capability '" + capability + "'가 필요합니다.");
            }
        }

        private static void CollectDescriptorRuntimeOwnedObjectDesigns(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
            string spawnKey,
            HashSet<string> featureOutputItemTargets,
            HashSet<string> requiredObjectDesignKeys,
            List<string> errors,
            List<string> warnings)
        {
            if (!TryResolveFeatureDescriptorForGameplayObjectId(catalog, gameplayObjectId, out FeatureDescriptor descriptor) ||
                descriptor.inputOutputSemantics == null)
            {
                return;
            }

            if (descriptor.inputOutputSemantics.containsCustomerSingleLine)
            {
                AddSingleAvailableObjectDesign(
                    catalog,
                    CatalogIdentityRules.CUSTOMER_OBJECT_ID,
                    requiredObjectDesignKeys,
                    errors,
                    warnings,
                    "feature descriptor customer line");
            }

            if (descriptor.inputOutputSemantics.containsMoneyHandler)
            {
                AddSingleAvailableObjectDesign(
                    catalog,
                    CatalogIdentityRules.MONEY_OBJECT_ID,
                    requiredObjectDesignKeys,
                    errors,
                    warnings,
                    "feature descriptor money handler");
            }

            if (RequiresFeatureOutputItem(descriptor) &&
                !featureOutputItemTargets.Contains(FeatureDescriptorUtility.Normalize(spawnKey)))
            {
                errors.Add(
                    "feature '" + spawnKey + "'에는 featureOutputItems entry가 필요합니다. " +
                    "해당 feature의 output item objective를 input_intent.json에 선언한 뒤 재생성하세요.");
            }
        }

        private static bool TryResolveFeatureDescriptorForGameplayObjectId(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
            out FeatureDescriptor descriptor)
        {
            descriptor = null;
            if (catalog == null || string.IsNullOrWhiteSpace(gameplayObjectId))
                return false;

            string normalizedGameplayObjectId = PlayableFeatureTypeIds.Normalize(gameplayObjectId);
            FeatureDescriptor[] descriptors = catalog.FeatureDescriptors ?? new FeatureDescriptor[0];
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                FeatureDescriptor value = descriptors[descriptorIndex];
                if (value == null)
                    continue;

                if (string.Equals(PlayableFeatureTypeIds.Normalize(value.featureType), normalizedGameplayObjectId, System.StringComparison.Ordinal))
                {
                    descriptor = value;
                    return true;
                }

                FeatureCompiledGameplayRoleDescriptor[] mappings =
                    value.compiledGameplayRoleMappings ?? new FeatureCompiledGameplayRoleDescriptor[0];
                for (int mappingIndex = 0; mappingIndex < mappings.Length; mappingIndex++)
                {
                    FeatureCompiledGameplayRoleDescriptor mapping = mappings[mappingIndex];
                    if (mapping != null &&
                        string.Equals(PlayableFeatureTypeIds.Normalize(mapping.gameplayObjectId), normalizedGameplayObjectId, System.StringComparison.Ordinal))
                    {
                        descriptor = value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void CollectRequiredObjectDesigns(string[] entryIds, HashSet<string> requiredObjectDesignKeys, List<string> errors)
        {
            string[] safeEntryIds = entryIds ?? new string[0];
            for (int i = 0; i < safeEntryIds.Length; i++)
            {
                string entryId = safeEntryIds[i] != null ? safeEntryIds[i].Trim() : string.Empty;
                if (string.IsNullOrEmpty(entryId))
                    continue;

                if (!ContentCatalogTokenUtility.TrySplitEntryId(entryId, out string objectId, out string designId))
                {
                    errors.Add("generatedItemKeys[" + i + "] '" + entryId + "'는 canonical entryId 형식이어야 합니다.");
                    continue;
                }

                AddRequiredObjectDesign(objectId, designId, requiredObjectDesignKeys);
            }
        }

        private static void AddRequiredObjectDesign(string objectId, string designId, HashSet<string> requiredObjectDesignKeys)
        {
            string entryId = ContentCatalogTokenUtility.BuildEntryId(objectId, designId);
            if (!string.IsNullOrEmpty(entryId))
                requiredObjectDesignKeys.Add(entryId);
        }

        private static void AddSingleAvailableObjectDesign(
            PlayableObjectCatalog catalog,
            string objectId,
            HashSet<string> requiredObjectDesignKeys,
            List<string> errors,
            List<string> warnings,
            string context)
        {
            if (catalog == null)
            {
                if (errors != null)
                    errors.Add(context + " runtime-owned design을 해석하려면 PlayableObjectCatalog가 필요합니다.");
                return;
            }

            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                if (errors != null)
                    errors.Add(context + " runtime-owned objectId가 비어 있습니다.");
                return;
            }

            if (!catalog.TryGetGameplayEntry(normalizedObjectId, out GameplayCatalogEntry entry) || entry == null)
            {
                if (errors != null)
                    errors.Add(context + " runtime-owned object '" + normalizedObjectId + "'를 catalog에서 찾지 못했습니다.");
                return;
            }

            DesignVariantEntry[] designs = entry.designs ?? new DesignVariantEntry[0];
            var candidates = new List<string>();
            for (int i = 0; i < designs.Length; i++)
            {
                DesignVariantEntry design = designs[i];
                string designId = design != null ? design.designId != null ? design.designId.Trim() : string.Empty : string.Empty;
                if (string.IsNullOrEmpty(designId))
                    continue;

                if (!catalog.TryResolveGameplayDesignIndex(normalizedObjectId, designId, out _))
                    continue;

                candidates.Add(designId);
            }

            if (candidates.Count > 0)
            {
                string resolvedDesignId = SelectRuntimeOwnedDesignId(normalizedObjectId, candidates);
                AddRequiredObjectDesign(normalizedObjectId, resolvedDesignId, requiredObjectDesignKeys);
                if (candidates.Count > 1 && warnings != null)
                {
                    AddUniqueWarning(
                        warnings,
                        context + " runtime-owned object '" + normalizedObjectId +
                        "'에 catalog design 후보가 여러 개 있어 '" + resolvedDesignId +
                        "'를 추론 선택했습니다. 이후 compiled plan validation에서 objectDesign binding을 재검증합니다.");
                }
                return;
            }

            if (errors != null)
                errors.Add(context + " runtime-owned object '" + normalizedObjectId + "'에 사용 가능한 catalog design이 없습니다.");
        }

        private static string SelectRuntimeOwnedDesignId(string objectId, List<string> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return string.Empty;

            string normalizedObjectId = objectId != null ? objectId.Trim().ToLowerInvariant() : string.Empty;
            string best = candidates[0] ?? string.Empty;
            int bestScore = ScoreRuntimeOwnedDesignCandidate(normalizedObjectId, best);
            for (int i = 1; i < candidates.Count; i++)
            {
                string candidate = candidates[i] ?? string.Empty;
                int score = ScoreRuntimeOwnedDesignCandidate(normalizedObjectId, candidate);
                if (score > bestScore ||
                    score == bestScore && string.CompareOrdinal(candidate, best) < 0)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int ScoreRuntimeOwnedDesignCandidate(string objectId, string designId)
        {
            string normalizedDesignId = designId != null ? designId.Trim().ToLowerInvariant() : string.Empty;
            int score = 0;

            if (string.Equals(normalizedDesignId, "default", System.StringComparison.Ordinal))
                score += 40;
            if (string.Equals(normalizedDesignId, "basic", System.StringComparison.Ordinal))
                score += 35;
            if (normalizedDesignId.EndsWith("_01", System.StringComparison.Ordinal) ||
                normalizedDesignId.EndsWith("-01", System.StringComparison.Ordinal))
                score += 10;

            if (string.Equals(objectId, CatalogIdentityRules.CUSTOMER_OBJECT_ID, System.StringComparison.Ordinal))
            {
                if (normalizedDesignId.Contains("stickman"))
                    score += 70;
                if (normalizedDesignId.Contains("human") || normalizedDesignId.Contains("person"))
                    score += 60;
                if (normalizedDesignId.Contains("customer"))
                    score += 25;
                if (normalizedDesignId.Contains("car") || normalizedDesignId.Contains("vehicle"))
                    score -= 50;
            }

            return score;
        }

        private static void AddUniqueWarning(List<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning) || warnings.Contains(warning))
                return;

            warnings.Add(warning);
        }

        private static void CollectAcceptedItems(
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            HashSet<string> requiredObjectDesignKeys)
        {
            FeatureAcceptedItemDefinition[] safeDefinitions = featureAcceptedItems ?? new FeatureAcceptedItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FeatureAcceptedItemDefinition definition = safeDefinitions[i];
                if (definition == null || !ItemRefUtility.IsValid(definition.item))
                    continue;

                AddRequiredObjectDesign(definition.item.familyId, definition.item.variantId, requiredObjectDesignKeys);
            }
        }

        private static void CollectOutputItems(
            FeatureOutputItemDefinition[] featureOutputItems,
            HashSet<string> requiredObjectDesignKeys)
        {
            FeatureOutputItemDefinition[] safeDefinitions = featureOutputItems ?? new FeatureOutputItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FeatureOutputItemDefinition definition = safeDefinitions[i];
                if (definition == null || !ItemRefUtility.IsValid(definition.item))
                    continue;

                AddRequiredObjectDesign(definition.item.familyId, definition.item.variantId, requiredObjectDesignKeys);
            }
        }

        private static void CollectFeatureOptionItemRefs(
            PlayableScenarioFeatureOptionDefinition[] featureOptions,
            PlayableObjectCatalog catalog,
            HashSet<string> requiredObjectDesignKeys,
            List<string> errors)
        {
            PlayableScenarioFeatureOptionDefinition[] safeDefinitions = featureOptions ?? new PlayableScenarioFeatureOptionDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                PlayableScenarioFeatureOptionDefinition definition = safeDefinitions[i];
                if (definition == null)
                    continue;

                string featureType = FeatureDescriptorUtility.Normalize(definition.featureType);
                if (string.IsNullOrEmpty(featureType) ||
                    catalog == null ||
                    !catalog.TryGetFeatureDescriptor(featureType, out FeatureDescriptor descriptor))
                {
                    continue;
                }

                FeatureOptionFieldDescriptor[] fields = descriptor.optionSchema != null
                    ? descriptor.optionSchema.fields ?? new FeatureOptionFieldDescriptor[0]
                    : new FeatureOptionFieldDescriptor[0];
                string optionsJson = definition.options.optionsJson != null
                    ? definition.options.optionsJson.Trim()
                    : string.Empty;

                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FeatureOptionFieldDescriptor field = fields[fieldIndex];
                    if (field == null ||
                        !string.Equals(FeatureDescriptorUtility.Normalize(field.valueType), FeatureDescriptorContracts.VALUE_TYPE_ITEM_REF, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string fieldId = FeatureDescriptorUtility.Normalize(field.fieldId);
                    if (string.IsNullOrEmpty(fieldId))
                        continue;

                    string context = "featureOptions '" + definition.featureId + "' item_ref 옵션 '" + fieldId + "'";
                    if (string.IsNullOrWhiteSpace(optionsJson))
                    {
                        if (field.required)
                            errors.Add(context + "가 필요하지만 optionsJson이 비어 있습니다.");
                        continue;
                    }

                    if (!TryFindTopLevelJsonPropertyValue(optionsJson, fieldId, out string valueJson, out string findError))
                    {
                        if (field.required)
                            errors.Add(context + "가 없습니다: " + findError);
                        continue;
                    }

                    if (!TryParseItemRefObject(valueJson, out ItemRef item, out string itemError))
                    {
                        errors.Add(context + " 파싱 실패: " + itemError);
                        continue;
                    }

                    if (!ItemRefUtility.IsValid(item))
                    {
                        errors.Add(context + "는 familyId와 variantId가 모두 필요합니다.");
                        continue;
                    }

                    AddRequiredObjectDesign(item.familyId, item.variantId, requiredObjectDesignKeys);
                    ValidateRequiredItemDesignCapabilities(catalog, item, field.requiredItemDesignCapabilities, context, errors);
                }
            }
        }

        private static void ValidateRequiredItemDesignCapabilities(
            PlayableObjectCatalog catalog,
            ItemRef item,
            string[] requiredCapabilities,
            string context,
            List<string> errors)
        {
            string[] normalizedCapabilities = NormalizeStringArray(requiredCapabilities);
            if (normalizedCapabilities.Length == 0)
                return;

            if (catalog == null)
            {
                errors.Add(context + " capability 검증에는 PlayableObjectCatalog가 필요합니다.");
                return;
            }

            string itemKey = ItemRefUtility.ToItemKey(item);
            if (!catalog.TryResolveGameplayDesignIndex(item.familyId, item.variantId, out int designIndex) ||
                !catalog.TryResolveGameplayPrefab(item.familyId, designIndex, out GameObject prefab, out _) ||
                prefab == null)
            {
                errors.Add(context + "가 참조하는 item '" + itemKey + "' design source prefab을 catalog에서 해석하지 못했습니다.");
                return;
            }

            if (!PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata) || metadata == null)
            {
                errors.Add(context + "가 참조하는 item '" + itemKey + "' prefab metadata를 읽지 못했습니다.");
                return;
            }

            for (int i = 0; i < normalizedCapabilities.Length; i++)
            {
                string capability = normalizedCapabilities[i];
                if (!CatalogPrefabMetadataCapabilityUtility.HasVerifiedCapability(metadata, capability))
                {
                    errors.Add(context + "가 참조하는 item '" + itemKey + "'에는 catalog capability '" + capability + "'가 필요합니다.");
                }
            }
        }

        private static string[] NormalizeStringArray(string[] values)
        {
            string[] safeValues = values ?? new string[0];
            var normalized = new List<string>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = FeatureDescriptorUtility.Normalize(safeValues[i]);
                if (!string.IsNullOrEmpty(value) && !normalized.Contains(value))
                    normalized.Add(value);
            }

            normalized.Sort(System.StringComparer.Ordinal);
            return normalized.ToArray();
        }

        private static HashSet<string> BuildFeatureOutputItemTargets(FeatureOutputItemDefinition[] featureOutputItems)
        {
            var targets = new HashSet<string>(System.StringComparer.Ordinal);
            FeatureOutputItemDefinition[] safeDefinitions = featureOutputItems ?? new FeatureOutputItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FeatureOutputItemDefinition definition = safeDefinitions[i];
                string targetId = FeatureDescriptorUtility.Normalize(definition != null ? definition.targetId : string.Empty);
                if (!string.IsNullOrEmpty(targetId))
                    targets.Add(targetId);
            }

            return targets;
        }

        private static bool RequiresFeatureOutputItem(FeatureDescriptor descriptor)
        {
            FeatureInputOutputSemantics semantics = descriptor != null ? descriptor.inputOutputSemantics : null;
            if (HasAnyValue(semantics != null ? semantics.generatedItems : null) ||
                HasAnyValue(semantics != null ? semantics.outputItems : null))
            {
                return true;
            }

            FeatureObjectiveDescriptor[] objectives = descriptor != null ? descriptor.objectiveKinds ?? new FeatureObjectiveDescriptor[0] : new FeatureObjectiveDescriptor[0];
            for (int i = 0; i < objectives.Length; i++)
            {
                if (ObjectiveImpliesFeatureOutputItem(descriptor, objectives[i]))
                    return true;
            }

            return false;
        }

        private static bool ObjectiveImpliesFeatureOutputItem(FeatureDescriptor descriptor, FeatureObjectiveDescriptor objective)
        {
            if (descriptor == null ||
                objective == null ||
                objective.requiresItem ||
                !objective.requiresInputItem)
            {
                return false;
            }

            FeatureGameplaySignalDescriptor signal = FindGameplaySignal(descriptor, objective.completionGameplaySignalId);
            return signal != null &&
                   (signal.requiresItem || signal.supportsItem);
        }

        private static FeatureGameplaySignalDescriptor FindGameplaySignal(FeatureDescriptor descriptor, string signalId)
        {
            string normalizedSignalId = FeatureDescriptorUtility.Normalize(signalId);
            if (string.IsNullOrEmpty(normalizedSignalId))
                return null;

            FeatureGameplaySignalDescriptor[] signals = descriptor != null ? descriptor.gameplaySignals ?? new FeatureGameplaySignalDescriptor[0] : new FeatureGameplaySignalDescriptor[0];
            for (int i = 0; i < signals.Length; i++)
            {
                FeatureGameplaySignalDescriptor signal = signals[i];
                if (signal != null &&
                    string.Equals(FeatureDescriptorUtility.Normalize(signal.signalId), normalizedSignalId, System.StringComparison.Ordinal))
                {
                    return signal;
                }
            }

            return null;
        }

        private static bool HasAnyValue(string[] values)
        {
            string[] safeValues = values ?? new string[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(safeValues[i]))
                    return true;
            }

            return false;
        }

        private static bool TryParseItemRefObject(string json, out ItemRef item, out string error)
        {
            item = new ItemRef();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON object가 비어 있습니다.";
                return false;
            }

            if (!TryFindTopLevelJsonPropertyValue(json, "familyId", out string familyJson, out string familyError))
            {
                error = "familyId가 없습니다: " + familyError;
                return false;
            }

            if (!TryFindTopLevelJsonPropertyValue(json, "variantId", out string variantJson, out string variantError))
            {
                error = "variantId가 없습니다: " + variantError;
                return false;
            }

            if (!TryReadJsonStringValue(familyJson, out string familyId, out string familyReadError))
            {
                error = "familyId가 문자열이 아닙니다: " + familyReadError;
                return false;
            }

            if (!TryReadJsonStringValue(variantJson, out string variantId, out string variantReadError))
            {
                error = "variantId가 문자열이 아닙니다: " + variantReadError;
                return false;
            }

            item.familyId = familyId;
            item.variantId = variantId;
            return true;
        }

        private static bool TryReadJsonStringValue(string json, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            int index = 0;
            SkipWhitespace(json, ref index);
            if (!TryReadJsonString(json, ref index, out value, out error))
                return false;

            SkipWhitespace(json, ref index);
            if (index != json.Length)
            {
                error = "문자열 뒤에 추가 토큰이 있습니다.";
                return false;
            }

            return true;
        }

        private static bool TryFindTopLevelJsonPropertyValue(string json, string propertyName, out string valueJson, out string error)
        {
            valueJson = string.Empty;
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
                    error = "속성을 찾지 못했습니다.";
                    return false;
                }

                if (!TryReadJsonString(json, ref index, out string key, out error))
                    return false;

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    error = "속성 '" + key + "' 뒤에 ':'가 없습니다.";
                    return false;
                }

                index++;
                SkipWhitespace(json, ref index);
                int valueStart = index;
                if (!TrySkipJsonValue(json, ref index, out error))
                    return false;
                int valueEnd = index;

                if (string.Equals(FeatureDescriptorUtility.Normalize(key), FeatureDescriptorUtility.Normalize(propertyName), System.StringComparison.Ordinal))
                {
                    valueJson = json.Substring(valueStart, valueEnd - valueStart).Trim();
                    return true;
                }

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (index < json.Length && json[index] == '}')
                {
                    error = "속성을 찾지 못했습니다.";
                    return false;
                }

                error = index >= json.Length
                    ? "JSON object가 닫히지 않았습니다."
                    : "속성 뒤에 ',' 또는 '}'가 없습니다.";
                return false;
            }
        }

        private static bool TrySkipJsonValue(string json, ref int index, out string error)
        {
            error = string.Empty;
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                error = "JSON value가 없습니다.";
                return false;
            }

            char first = json[index];
            if (first == '"')
                return TryReadJsonString(json, ref index, out _, out error);
            if (first == '{')
                return TrySkipJsonContainer(json, ref index, '{', '}', out error);
            if (first == '[')
                return TrySkipJsonContainer(json, ref index, '[', ']', out error);

            while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
                index++;
            return true;
        }

        private static bool TrySkipJsonContainer(string json, ref int index, char open, char close, out string error)
        {
            error = string.Empty;
            if (index >= json.Length || json[index] != open)
            {
                error = "JSON container 시작 토큰이 올바르지 않습니다.";
                return false;
            }

            var expectedClosers = new List<char>();
            while (index < json.Length)
            {
                char c = json[index];
                if (c == '"')
                {
                    if (!TryReadJsonString(json, ref index, out _, out error))
                        return false;
                    continue;
                }

                if (c == '{')
                {
                    expectedClosers.Add('}');
                    index++;
                    continue;
                }

                if (c == '[')
                {
                    expectedClosers.Add(']');
                    index++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    if (expectedClosers.Count == 0 ||
                        expectedClosers[expectedClosers.Count - 1] != c)
                    {
                        error = "JSON container 닫힘 토큰이 맞지 않습니다.";
                        return false;
                    }

                    expectedClosers.RemoveAt(expectedClosers.Count - 1);
                    index++;
                    if (expectedClosers.Count == 0)
                        return true;
                    continue;
                }

                index++;
            }

            error = "JSON container가 닫히지 않았습니다.";
            return false;
        }

        private static bool TryReadJsonString(string json, ref int index, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            if (index >= json.Length || json[index] != '"')
            {
                error = "JSON 문자열 시작 토큰이 없습니다.";
                return false;
            }

            index++;
            var builder = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= json.Length)
                {
                    error = "JSON 문자열 escape가 닫히지 않았습니다.";
                    return false;
                }

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
                        {
                            error = "JSON unicode escape가 짧습니다.";
                            return false;
                        }

                        string hex = json.Substring(index, 4);
                        if (!ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ushort code))
                        {
                            error = "JSON unicode escape가 올바르지 않습니다.";
                            return false;
                        }

                        builder.Append((char)code);
                        index += 4;
                        break;
                    default:
                        error = "지원하지 않는 JSON escape입니다.";
                        return false;
                }
            }

            error = "JSON 문자열이 닫히지 않았습니다.";
            return false;
        }

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
        }

        private static void CollectPricedItems(
            ItemPriceDefinition[] itemPrices,
            HashSet<string> requiredObjectDesignKeys)
        {
            ItemPriceDefinition[] safeDefinitions = itemPrices ?? new ItemPriceDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                ItemPriceDefinition definition = safeDefinitions[i];
                if (definition == null || !ItemRefUtility.IsValid(definition.item))
                    continue;

                AddRequiredObjectDesign(definition.item.familyId, definition.item.variantId, requiredObjectDesignKeys);
            }
        }

        private static void CollectCurrencyVisuals(
            CurrencyDefinition[] currencies,
            PlayableObjectCatalog catalog,
            List<string> errors,
            List<string> warnings,
            HashSet<string> requiredObjectDesignKeys)
        {
            CurrencyDefinition[] safeDefinitions = currencies ?? new CurrencyDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                CurrencyDefinition definition = safeDefinitions[i];
                if (definition == null)
                    continue;

                string currencyId = definition.currencyId != null ? definition.currencyId.Trim() : string.Empty;
                if (!string.Equals(currencyId, CatalogIdentityRules.MONEY_OBJECT_ID, System.StringComparison.Ordinal))
                    continue;

                AddSingleAvailableObjectDesign(
                    catalog,
                    CatalogIdentityRules.MONEY_OBJECT_ID,
                    requiredObjectDesignKeys,
                    errors,
                    warnings,
                    "currency visual '" + currencyId + "'");
            }
        }
    }
}
