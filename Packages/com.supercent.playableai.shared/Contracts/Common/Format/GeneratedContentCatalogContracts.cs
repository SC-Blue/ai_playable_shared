using System;
using Supercent.PlayableAI.Common.Contracts;

namespace Supercent.PlayableAI.Common.Format
{
    public enum ContentCatalogCategory
    {
        unknown = 0,
        core = 1,
        ui = 2,
        feature = 3,
        character = 4,
        environment = 5,
        item = 6,
        guide = 7,
        unlocker = 8,
    }

    public enum ContentCatalogSubscriptionType
    {
        none = 0,
        @object = 1,
        design = 2,
        dependency = 3,
    }

    public enum ContentCatalogPlacementMode
    {
        none = 0,
        free = 1,
        fill = 2,
        perimeter = 3,
        path = 4,
        guide = 5,
    }

    public static class GeneratedContentCatalogContracts
    {
        public const int SCHEMA_VERSION = 1;
        public const string FILE_NAME = "CONTENT_CATALOG.generated.json";
        public const string DEFAULT_DESIGN_ID = "basic";
        public const string DESIGN_MODE_SINGLE_PREFAB = "single_prefab";
        public const string DESIGN_MODE_ASSEMBLED_PATH = "assembled_path";
        public const string DESIGN_MODE_ENVIRONMENT = "environment";
        public const string VARIATION_MODE_SINGLE = "single";
    }

    public static class CatalogCategoryIds
    {
        public const string FEATURE = "feature";
        public const string UNLOCKER = "unlocker";
        public const string ITEM = "item";
        public const string CHARACTER = "character";
        public const string ENVIRONMENT = "environment";
        public const string UI = "ui";
    }

    public static class GameplayRoleIds
    {
        public const string PLAYER = "player";
        public const string GENERATOR = "generator";
        public const string PROCESSOR = "processor";
        public const string SELLER = "seller";
        public const string UNLOCK_PAD = "unlock_pad";
        public const string PHYSICS_AREA = "physics_area";
        public const string RAIL = "rail";
        public const string CUSTOMER = "customer";
        public const string ITEM = "item";
    }

    public static class EnvironmentRoleIds
    {
        public const string FLOOR = "floor";
        public const string WALL = "wall";
        public const string FENCE = "fence";
        public const string ROAD = "road";
    }

    public static class PlacementModeIds
    {
        public const string FILL = "fill";
        public const string PERIMETER = "perimeter";
        public const string PATH = "path";
    }

    public static class VariationModeIds
    {
        public const string SINGLE = "single";
        public const string CONNECTED3 = "connected3";
    }

    public static class DesignModeIds
    {
        public const string SINGLE_PREFAB = GeneratedContentCatalogContracts.DESIGN_MODE_SINGLE_PREFAB;
        public const string ASSEMBLED_PATH = GeneratedContentCatalogContracts.DESIGN_MODE_ASSEMBLED_PATH;
        public const string ENVIRONMENT = GeneratedContentCatalogContracts.DESIGN_MODE_ENVIRONMENT;
    }

    public static class CatalogIdentityRules
    {
        public const char STABLE_ENTRY_DELIMITER = '/';
        public const string MONEY_OBJECT_ID = "money";
        public const string MONEY_DESIGN_ID = "money_pile";
        public const string CUSTOMER_OBJECT_ID = "customer";
        public const string CUSTOMER_DESIGN_ID = "car";
    }

    public static class ContentCatalogTokenUtility
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static string NormalizeCatalogId(string value)
        {
            return Normalize(value);
        }

        public static string BuildStableEntryId(string objectId, string designId)
        {
            string normalizedObjectId = Normalize(objectId);
            string normalizedDesignId = Normalize(designId);
            if (string.IsNullOrEmpty(normalizedObjectId) || string.IsNullOrEmpty(normalizedDesignId))
                return string.Empty;

            return normalizedObjectId + CatalogIdentityRules.STABLE_ENTRY_DELIMITER + normalizedDesignId;
        }

        public static bool IsStableEntryId(string value)
        {
            string normalized = Normalize(value);
            int delimiterIndex = normalized.IndexOf(CatalogIdentityRules.STABLE_ENTRY_DELIMITER);
            return delimiterIndex > 0 && delimiterIndex < normalized.Length - 1;
        }

        public static bool TrySplitStableEntryId(string stableEntryId, out string objectId, out string designId)
        {
            objectId = string.Empty;
            designId = string.Empty;

            string normalized = Normalize(stableEntryId);
            if (string.IsNullOrEmpty(normalized))
                return false;

            int delimiterIndex = normalized.IndexOf(CatalogIdentityRules.STABLE_ENTRY_DELIMITER);
            if (delimiterIndex <= 0 || delimiterIndex >= normalized.Length - 1)
                return false;

            objectId = Normalize(normalized.Substring(0, delimiterIndex));
            designId = Normalize(normalized.Substring(delimiterIndex + 1));
            return !string.IsNullOrEmpty(objectId) && !string.IsNullOrEmpty(designId);
        }

        public static string BuildObjectDesignSelectionKey(string objectId, string designId)
        {
            return BuildStableEntryId(objectId, designId);
        }

        public static bool ValidateObjectId(string objectId, out string errorMessage)
        {
            string normalized = Normalize(objectId);
            if (string.IsNullOrEmpty(normalized))
            {
                errorMessage = "objectId가 비어 있습니다.";
                return false;
            }

            if (normalized.IndexOf(CatalogIdentityRules.STABLE_ENTRY_DELIMITER) >= 0)
            {
                errorMessage = "objectId '" + normalized + "'에는 '/'가 포함되면 안 됩니다.";
                return false;
            }

            if (!IsLowerSnakeCaseToken(normalized))
            {
                errorMessage = "objectId '" + normalized + "'는 lower_snake_case여야 합니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool ValidateDesignId(string designId, out string errorMessage)
        {
            string normalized = Normalize(designId);
            if (string.IsNullOrEmpty(normalized))
            {
                errorMessage = "designId가 비어 있습니다.";
                return false;
            }

            if (normalized.IndexOf(CatalogIdentityRules.STABLE_ENTRY_DELIMITER) >= 0)
            {
                errorMessage = "designId '" + normalized + "'에는 '/'가 포함되면 안 됩니다.";
                return false;
            }

            if (!IsLowerSnakeCaseToken(normalized))
            {
                errorMessage = "designId '" + normalized + "'는 lower_snake_case여야 합니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool ValidateCatalogEntryIdentity(
            string objectId,
            string designId,
            string stableEntryId,
            out string errorMessage)
        {
            if (!ValidateObjectId(objectId, out errorMessage))
                return false;

            if (!ValidateDesignId(designId, out errorMessage))
                return false;

            string expectedStableEntryId = BuildStableEntryId(objectId, designId);
            string normalizedStableEntryId = Normalize(stableEntryId);
            if (!string.IsNullOrEmpty(normalizedStableEntryId) &&
                !string.Equals(expectedStableEntryId, normalizedStableEntryId, StringComparison.Ordinal))
            {
                errorMessage = "stableEntryId '" + normalizedStableEntryId + "'가 canonical identity '" + expectedStableEntryId + "'와 일치하지 않습니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool IsLowerSnakeCaseToken(string value)
        {
            string normalized = Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return false;

            char first = normalized[0];
            if (first < 'a' || first > 'z')
                return false;

            for (int i = 1; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if ((c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_')
                    continue;

                return false;
            }

            return true;
        }

        public static string ToToken(ContentCatalogCategory value)
        {
            return value switch
            {
                ContentCatalogCategory.core => "core",
                ContentCatalogCategory.ui => "ui",
                ContentCatalogCategory.feature => "feature",
                ContentCatalogCategory.character => "character",
                ContentCatalogCategory.environment => "environment",
                ContentCatalogCategory.item => "item",
                ContentCatalogCategory.guide => "guide",
                ContentCatalogCategory.unlocker => "unlocker",
                _ => "unknown",
            };
        }

        public static string ToToken(ContentCatalogSubscriptionType value)
        {
            return value switch
            {
                ContentCatalogSubscriptionType.@object => "object",
                ContentCatalogSubscriptionType.design => "design",
                ContentCatalogSubscriptionType.dependency => "dependency",
                _ => "none",
            };
        }

        public static string ToToken(ContentCatalogPlacementMode value)
        {
            return value switch
            {
                ContentCatalogPlacementMode.free => "free",
                ContentCatalogPlacementMode.fill => "fill",
                ContentCatalogPlacementMode.perimeter => "perimeter",
                ContentCatalogPlacementMode.path => "path",
                ContentCatalogPlacementMode.guide => "guide",
                _ => "none",
            };
        }
    }

    [Serializable]
    public sealed class GeneratedContentCatalogAssetReference
    {
        public string assetPath = string.Empty;
        public string assetGuid = string.Empty;
    }

    [Serializable]
    public sealed class GeneratedContentCatalogPathAssets
    {
        public GeneratedContentCatalogAssetReference straightPrefab = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference cornerPrefab = new GeneratedContentCatalogAssetReference();
    }

    [Serializable]
    public sealed class GeneratedContentCatalogEnvironmentAssets
    {
        public GeneratedContentCatalogAssetReference straightPrefab = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference cornerPrefab = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference tJunctionPrefab = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference crossPrefab = new GeneratedContentCatalogAssetReference();
    }

    [Serializable]
    public sealed class GeneratedContentCatalogDocument
    {
        public int schemaVersion = GeneratedContentCatalogContracts.SCHEMA_VERSION;
        public string generatedAtUtc = string.Empty;
        public string contentHash = string.Empty;
        public GeneratedContentCatalogEntry[] entries = new GeneratedContentCatalogEntry[0];
        public string[] availableBuiltinFeatureTypes = Array.Empty<string>();
        public FeatureDescriptor[] featureDescriptors = Array.Empty<FeatureDescriptor>();
    }

    [Serializable]
    public sealed class GeneratedContentCatalogEntry
    {
        public string stableEntryId = string.Empty;
        public string contentId = string.Empty;
        public string objectId = string.Empty;
        public string designId = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string category = string.Empty;
        public string subscriptionType = string.Empty;
        public string placementMode = string.Empty;
        public string designMode = GeneratedContentCatalogContracts.DESIGN_MODE_SINGLE_PREFAB;
        public string variationMode = GeneratedContentCatalogContracts.VARIATION_MODE_SINGLE;
        public string prefabName = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabAssetGuid = string.Empty;
        public GeneratedContentCatalogPathAssets pathAssets = new GeneratedContentCatalogPathAssets();
        public GeneratedContentCatalogEnvironmentAssets environmentAssets = new GeneratedContentCatalogEnvironmentAssets();
        public int footprintWidthCells = 1;
        public int footprintDepthCells = 1;
        public float footprintCenterOffsetX;
        public float footprintCenterOffsetZ;
    }
}
