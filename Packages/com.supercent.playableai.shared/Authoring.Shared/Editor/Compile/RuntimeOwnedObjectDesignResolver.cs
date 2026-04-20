using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Generation.Editor.Compile
{
    internal sealed class RuntimeOwnedObjectDesignResolution
    {
        public List<string> RequiredObjectIds = new List<string>();
        public List<string> Errors = new List<string>();
    }

    internal static class RuntimeOwnedObjectDesignResolver
    {
        public static RuntimeOwnedObjectDesignResolution Resolve(
            CompiledSpawnData[] spawns,
            FacilityAcceptedItemDefinition[] facilityAcceptedItems,
            FacilityOutputItemDefinition[] facilityOutputItems,
            ItemPriceDefinition[] itemPrices,
            PlayableObjectCatalog catalog)
        {
            var result = new RuntimeOwnedObjectDesignResolution();
            var requiredObjectIds = new HashSet<string>(System.StringComparer.Ordinal);
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

                CollectRequiredObjectIds(metadata.generatedItemStableKeys, requiredObjectIds);

                if (metadata.containsCustomerSingleLine)
                    requiredObjectIds.Add("customer");

                if (metadata.containsItemSellFacility)
                    requiredObjectIds.Add("money");
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
                    requiredObjectIds.Add("money");
            }

            CollectAcceptedItems(facilityAcceptedItems, requiredObjectIds);
            CollectOutputItems(facilityOutputItems, requiredObjectIds);
            CollectPricedItems(itemPrices, requiredObjectIds);

            foreach (string objectId in requiredObjectIds)
                result.RequiredObjectIds.Add(objectId);

            result.RequiredObjectIds.Sort(System.StringComparer.Ordinal);
            return result;
        }

        private static void CollectRequiredObjectIds(string[] objectIds, HashSet<string> requiredObjectIds)
        {
            string[] safeObjectIds = objectIds ?? new string[0];
            for (int i = 0; i < safeObjectIds.Length; i++)
            {
                string objectId = safeObjectIds[i] != null ? safeObjectIds[i].Trim() : string.Empty;
                if (!string.IsNullOrEmpty(objectId))
                    requiredObjectIds.Add(objectId);
            }
        }

        private static void CollectAcceptedItems(
            FacilityAcceptedItemDefinition[] facilityAcceptedItems,
            HashSet<string> requiredObjectIds)
        {
            FacilityAcceptedItemDefinition[] safeDefinitions = facilityAcceptedItems ?? new FacilityAcceptedItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FacilityAcceptedItemDefinition definition = safeDefinitions[i];
                if (definition == null || !ItemRefUtility.IsValid(definition.item))
                    continue;

                requiredObjectIds.Add(ItemRefUtility.ToStableKey(definition.item));
            }
        }

        private static void CollectOutputItems(
            FacilityOutputItemDefinition[] facilityOutputItems,
            HashSet<string> requiredObjectIds)
        {
            FacilityOutputItemDefinition[] safeDefinitions = facilityOutputItems ?? new FacilityOutputItemDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                FacilityOutputItemDefinition definition = safeDefinitions[i];
                if (definition == null || !ItemRefUtility.IsValid(definition.item))
                    continue;

                requiredObjectIds.Add(ItemRefUtility.ToStableKey(definition.item));
            }
        }

        private static void CollectPricedItems(
            ItemPriceDefinition[] itemPrices,
            HashSet<string> requiredObjectIds)
        {
            ItemPriceDefinition[] safeDefinitions = itemPrices ?? new ItemPriceDefinition[0];
            for (int i = 0; i < safeDefinitions.Length; i++)
            {
                ItemPriceDefinition definition = safeDefinitions[i];
                if (definition == null || !ItemRefUtility.IsValid(definition.item))
                    continue;

                requiredObjectIds.Add(ItemRefUtility.ToStableKey(definition.item));
            }
        }
    }
}
