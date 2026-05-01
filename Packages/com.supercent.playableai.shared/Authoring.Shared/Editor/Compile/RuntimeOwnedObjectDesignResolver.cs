using System.Collections.Generic;
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
    }

    internal static class RuntimeOwnedObjectDesignResolver
    {
        public static RuntimeOwnedObjectDesignResolution Resolve(
            CompiledSpawnData[] spawns,
            FeatureAcceptedItemDefinition[] featureAcceptedItems,
            FeatureOutputItemDefinition[] featureOutputItems,
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
                    result.Errors);

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

                CollectRequiredObjectDesigns(metadata.generatedItemKeys, requiredObjectDesignKeys, result.Errors);

                if (metadata.containsCustomerSingleLine)
                    AddRequiredObjectDesign(
                        CatalogIdentityRules.CUSTOMER_OBJECT_ID,
                        CatalogIdentityRules.CUSTOMER_DESIGN_ID,
                        requiredObjectDesignKeys);

            }

            IReadOnlyList<EditorBasedCatalog.Entry> editorEntries = catalog.EditorBased.GetEntries();
            for (int i = 0; i < editorEntries.Count; i++)
            {
                EditorBasedCatalog.Entry entry = editorEntries[i];
                if (entry == null || entry.prefab == null)
                    continue;

                if (!PortablePrefabMetadataUtility.TryGetMetadata(entry.prefab, out CatalogPrefabMetadata metadata))
                    continue;

                if (metadata.containsMoneyHandler)
                {
                    AddRequiredObjectDesign(
                        CatalogIdentityRules.MONEY_OBJECT_ID,
                        CatalogIdentityRules.MONEY_DESIGN_ID,
                        requiredObjectDesignKeys);
                }
            }

            CollectAcceptedItems(featureAcceptedItems, requiredObjectDesignKeys);
            CollectOutputItems(featureOutputItems, requiredObjectDesignKeys);
            CollectPricedItems(itemPrices, requiredObjectDesignKeys);
            CollectCurrencyVisuals(currencies, requiredObjectDesignKeys);

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

        private static void CollectDescriptorRuntimeOwnedObjectDesigns(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
            string spawnKey,
            HashSet<string> featureOutputItemTargets,
            HashSet<string> requiredObjectDesignKeys,
            List<string> errors)
        {
            if (!TryResolveFeatureDescriptorForGameplayObjectId(catalog, gameplayObjectId, out FeatureDescriptor descriptor) ||
                descriptor.inputOutputSemantics == null)
            {
                return;
            }

            if (descriptor.inputOutputSemantics.containsCustomerSingleLine)
            {
                AddRequiredObjectDesign(
                    CatalogIdentityRules.CUSTOMER_OBJECT_ID,
                    CatalogIdentityRules.CUSTOMER_DESIGN_ID,
                    requiredObjectDesignKeys);
            }

            if (descriptor.inputOutputSemantics.containsMoneyHandler)
            {
                AddRequiredObjectDesign(
                    CatalogIdentityRules.MONEY_OBJECT_ID,
                    CatalogIdentityRules.MONEY_DESIGN_ID,
                    requiredObjectDesignKeys);
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
            return HasAnyValue(semantics != null ? semantics.generatedItems : null) ||
                   HasAnyValue(semantics != null ? semantics.outputItems : null);
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

                AddRequiredObjectDesign(
                    CatalogIdentityRules.MONEY_OBJECT_ID,
                    CatalogIdentityRules.MONEY_DESIGN_ID,
                    requiredObjectDesignKeys);
            }
        }
    }
}
