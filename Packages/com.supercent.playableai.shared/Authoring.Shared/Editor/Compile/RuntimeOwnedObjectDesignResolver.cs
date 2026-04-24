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

            for (int i = 0; i < safeSpawns.Length; i++)
            {
                CompiledSpawnData spawn = safeSpawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.objectId))
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

                CollectRequiredObjectDesigns(metadata.generatedItemStableKeys, requiredObjectDesignKeys, result.Errors);

                if (metadata.containsCustomerSingleLine)
                    AddRequiredObjectDesign(
                        CatalogIdentityRules.CUSTOMER_OBJECT_ID,
                        CatalogIdentityRules.CUSTOMER_DESIGN_ID,
                        requiredObjectDesignKeys);

                if (metadata.containsSellerFeature)
                    AddRequiredObjectDesign(
                        CatalogIdentityRules.MONEY_OBJECT_ID,
                        CatalogIdentityRules.MONEY_DESIGN_ID,
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

            var sortedKeys = new List<string>(requiredObjectDesignKeys);
            sortedKeys.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                if (!ContentCatalogTokenUtility.TrySplitStableEntryId(sortedKeys[i], out string objectId, out string designId))
                    continue;

                result.RequiredObjectDesigns.Add(new RuntimeOwnedObjectDesignSelection
                {
                    objectId = objectId,
                    designId = designId,
                });
            }

            return result;
        }

        private static void CollectRequiredObjectDesigns(string[] stableEntryIds, HashSet<string> requiredObjectDesignKeys, List<string> errors)
        {
            string[] safeStableEntryIds = stableEntryIds ?? new string[0];
            for (int i = 0; i < safeStableEntryIds.Length; i++)
            {
                string stableEntryId = safeStableEntryIds[i] != null ? safeStableEntryIds[i].Trim() : string.Empty;
                if (string.IsNullOrEmpty(stableEntryId))
                    continue;

                if (!ContentCatalogTokenUtility.TrySplitStableEntryId(stableEntryId, out string objectId, out string designId))
                {
                    errors.Add("generatedItemStableKeys[" + i + "] '" + stableEntryId + "'는 canonical stableEntryId 형식이어야 합니다.");
                    continue;
                }

                AddRequiredObjectDesign(objectId, designId, requiredObjectDesignKeys);
            }
        }

        private static void AddRequiredObjectDesign(string objectId, string designId, HashSet<string> requiredObjectDesignKeys)
        {
            string stableEntryId = ContentCatalogTokenUtility.BuildStableEntryId(objectId, designId);
            if (!string.IsNullOrEmpty(stableEntryId))
                requiredObjectDesignKeys.Add(stableEntryId);
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
    }
}
