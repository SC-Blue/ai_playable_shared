#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Runtime.Gameplay;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    internal static class PortablePrefabMetadataUtility
    {
        public static bool TryGetMetadata(GameObject prefab, out CatalogPrefabMetadata metadata)
        {
            metadata = new CatalogPrefabMetadata();
            if (prefab == null)
                return false;

            var generatedItemStableKeys = new HashSet<string>(StringComparer.Ordinal);
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
                CollectSerializedItemStableKey(components[i], "_generatedItem", generatedItemStableKeys);

            metadata.generatedItemStableKeys = ToSortedArray(generatedItemStableKeys);
            metadata.outputItemStableKeys = new string[0];
            metadata.supportsCustomerFacility = prefab.GetComponentInChildren<CustomerFacilityBase>(true) != null;
            metadata.containsCustomerSingleLine = prefab.GetComponentInChildren<CustomerSingleLine>(true) != null;
            metadata.containsItemSellFacility = prefab.GetComponentInChildren<ItemSellFacility>(true) != null;
            metadata.containsMoneyHandler = prefab.GetComponentInChildren<MoneyHandler>(true) != null;

            PlayablePlacementFootprint footprint = prefab.GetComponentInChildren<PlayablePlacementFootprint>(true);
            if (footprint != null)
            {
                metadata.placementFootprintWidthCells = footprint.WidthCells;
                metadata.placementFootprintDepthCells = footprint.DepthCells;
                metadata.placementFootprintCenterOffsetX = footprint.LocalCenterOffset.x;
                metadata.placementFootprintCenterOffsetZ = footprint.LocalCenterOffset.z;
                metadata.placementOverlapAllowances = new CatalogFootprintOverlapAllowanceMetadata[0];
            }

            return true;
        }

        private static void CollectSerializedItemStableKey(
            Component component,
            string fieldName,
            HashSet<string> values)
        {
            if (component == null || string.IsNullOrWhiteSpace(fieldName))
                return;

            FieldInfo field = FindField(component.GetType(), fieldName);
            if (field == null)
                return;

            object rawValue = field.GetValue(component);
            if (rawValue is not ItemRef itemRef || !ItemRefUtility.IsValid(itemRef))
                return;

            values.Add(ItemRefUtility.ToStableKey(itemRef));
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field;

                current = current.BaseType;
            }

            return null;
        }

        private static string[] ToSortedArray(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                return new string[0];

            string[] sorted = new string[values.Count];
            values.CopyTo(sorted);
            Array.Sort(sorted, StringComparer.Ordinal);
            return sorted;
        }
    }
}
#endif
