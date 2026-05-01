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
                if (string.Equals(Normalize(capabilities[i]), normalizedCapability, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

