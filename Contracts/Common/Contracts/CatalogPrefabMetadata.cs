using System;

namespace Supercent.PlayableAI.Common.Contracts
{
    [Serializable]
    public sealed class CatalogPrefabMetadata
    {
        public string[] generatedItemKeys = new string[0];
        public string[] outputItemKeys = new string[0];
        public string[] capabilities = new string[0];
        public bool supportsCustomerFeature;
        public bool containsCustomerSingleLine;
        public bool containsMoneyHandler;
        public bool supportsRuntimeItemDesignSource;
        public bool supportsDummyImages;
        public bool supportsItemDummyImageSprite;
        public bool supportsContentPathOption;
        public int placementFootprintWidthCells;
        public int placementFootprintDepthCells;
        public float placementFootprintCenterOffsetX;
        public float placementFootprintCenterOffsetZ;
    }

    public static class CatalogPrefabCapabilityIds
    {
        public const string CUSTOMER_FEATURE = "customer_feature";
        public const string CUSTOMER_SINGLE_LINE = "customer_single_line";
        public const string MONEY_HANDLER = "money_handler";
        public const string RUNTIME_ITEM_DESIGN_SOURCE = "runtime_item_design_source";
        public const string DUMMY_IMAGES = "dummy_images";
        public const string ITEM_DUMMY_IMAGE_SPRITE = "item_dummy_image_sprite";
        public const string CONTENT_PATH_OPTION = "content_path_option";
    }

    public static class CatalogPrefabMetadataCapabilityUtility
    {
        public static bool HasCapability(CatalogPrefabMetadata metadata, string capability)
        {
            string normalizedCapability = Normalize(capability);
            if (metadata == null || string.IsNullOrEmpty(normalizedCapability))
                return false;

            string[] capabilities = metadata.capabilities ?? new string[0];
            for (int i = 0; i < capabilities.Length; i++)
            {
                string normalizedEntry = Normalize(capabilities[i]);
                if (string.Equals(normalizedEntry, normalizedCapability, StringComparison.Ordinal) ||
                    AreDummyImageCapabilityAliases(normalizedEntry, normalizedCapability))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasVerifiedCapability(CatalogPrefabMetadata metadata, string capability)
        {
            string normalizedCapability = Normalize(capability);
            if (metadata == null || string.IsNullOrEmpty(normalizedCapability))
                return false;

            switch (normalizedCapability)
            {
                case CatalogPrefabCapabilityIds.CUSTOMER_FEATURE:
                    return metadata.supportsCustomerFeature;
                case CatalogPrefabCapabilityIds.CUSTOMER_SINGLE_LINE:
                    return metadata.containsCustomerSingleLine;
                case CatalogPrefabCapabilityIds.MONEY_HANDLER:
                    return metadata.containsMoneyHandler;
                case CatalogPrefabCapabilityIds.RUNTIME_ITEM_DESIGN_SOURCE:
                    return metadata.supportsRuntimeItemDesignSource;
                case CatalogPrefabCapabilityIds.DUMMY_IMAGES:
                    return metadata.supportsDummyImages || metadata.supportsItemDummyImageSprite;
                case CatalogPrefabCapabilityIds.ITEM_DUMMY_IMAGE_SPRITE:
                    return metadata.supportsItemDummyImageSprite || metadata.supportsDummyImages;
                case CatalogPrefabCapabilityIds.CONTENT_PATH_OPTION:
                    return metadata.supportsContentPathOption;
                default:
                    return HasCapability(metadata, normalizedCapability);
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool AreDummyImageCapabilityAliases(string left, string right)
        {
            return (string.Equals(left, CatalogPrefabCapabilityIds.DUMMY_IMAGES, StringComparison.Ordinal) &&
                    string.Equals(right, CatalogPrefabCapabilityIds.ITEM_DUMMY_IMAGE_SPRITE, StringComparison.Ordinal)) ||
                   (string.Equals(left, CatalogPrefabCapabilityIds.ITEM_DUMMY_IMAGE_SPRITE, StringComparison.Ordinal) &&
                    string.Equals(right, CatalogPrefabCapabilityIds.DUMMY_IMAGES, StringComparison.Ordinal));
        }
    }
}
