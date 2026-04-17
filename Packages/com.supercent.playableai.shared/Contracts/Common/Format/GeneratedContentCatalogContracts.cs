using System;

namespace Supercent.PlayableAI.Common.Format
{
    public enum ContentCatalogCategory
    {
        Unknown = 0,
        Core = 1,
        UI = 2,
        Facility = 3,
        Character = 4,
        Environment = 5,
        Item = 6,
        Guide = 7,
    }

    public enum ContentCatalogSubscriptionType
    {
        None = 0,
        Object = 1,
        Design = 2,
        Dependency = 3,
    }

    public enum ContentCatalogPlacementMode
    {
        None = 0,
        Free = 1,
        Fill = 2,
        Perimeter = 3,
        Path = 4,
        Guide = 5,
    }

    public static class GeneratedContentCatalogContracts
    {
        public const int SCHEMA_VERSION = 1;
        public const string FILE_NAME = "CONTENT_CATALOG.generated.json";
        public const string DEFAULT_DESIGN_ID = "default";
        public const string DESIGN_MODE_SINGLE_PREFAB = "single_prefab";
        public const string DESIGN_MODE_ASSEMBLED_PATH = "assembled_path";
        public const string DESIGN_MODE_ENVIRONMENT = "environment";
        public const string VARIATION_MODE_SINGLE = "single";
    }

    public static class ContentCatalogTokenUtility
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static string BuildStableEntryId(string objectId, string designId)
        {
            string normalizedObjectId = Normalize(objectId);
            string normalizedDesignId = Normalize(designId);
            if (string.IsNullOrEmpty(normalizedObjectId))
                return string.Empty;

            if (string.IsNullOrEmpty(normalizedDesignId))
                normalizedDesignId = GeneratedContentCatalogContracts.DEFAULT_DESIGN_ID;

            return normalizedObjectId + "/" + normalizedDesignId;
        }

        public static string ToToken(ContentCatalogCategory value)
        {
            return value switch
            {
                ContentCatalogCategory.Core => "core",
                ContentCatalogCategory.UI => "ui",
                ContentCatalogCategory.Facility => "facility",
                ContentCatalogCategory.Character => "character",
                ContentCatalogCategory.Environment => "environment",
                ContentCatalogCategory.Item => "item",
                ContentCatalogCategory.Guide => "guide",
                _ => "unknown",
            };
        }

        public static string ToToken(ContentCatalogSubscriptionType value)
        {
            return value switch
            {
                ContentCatalogSubscriptionType.Object => "object",
                ContentCatalogSubscriptionType.Design => "design",
                ContentCatalogSubscriptionType.Dependency => "dependency",
                _ => "none",
            };
        }

        public static string ToToken(ContentCatalogPlacementMode value)
        {
            return value switch
            {
                ContentCatalogPlacementMode.Free => "free",
                ContentCatalogPlacementMode.Fill => "fill",
                ContentCatalogPlacementMode.Perimeter => "perimeter",
                ContentCatalogPlacementMode.Path => "path",
                ContentCatalogPlacementMode.Guide => "guide",
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
        public GeneratedContentCatalogAssetReference straightTopImage = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference cornerTopImage = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference straightPrefab = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference cornerPrefab = new GeneratedContentCatalogAssetReference();
    }

    [Serializable]
    public sealed class GeneratedContentCatalogEnvironmentAssets
    {
        public GeneratedContentCatalogAssetReference straightTopImage = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference cornerTopImage = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference tJunctionTopImage = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogAssetReference crossTopImage = new GeneratedContentCatalogAssetReference();
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
    }

    [Serializable]
    public sealed class GeneratedContentCatalogEntry
    {
        public string stableEntryId = string.Empty;
        public string contentId = string.Empty;
        public string objectId = string.Empty;
        public string designId = GeneratedContentCatalogContracts.DEFAULT_DESIGN_ID;
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
        public GeneratedContentCatalogAssetReference topImage = new GeneratedContentCatalogAssetReference();
        public GeneratedContentCatalogPathAssets pathAssets = new GeneratedContentCatalogPathAssets();
        public GeneratedContentCatalogEnvironmentAssets environmentAssets = new GeneratedContentCatalogEnvironmentAssets();
        public int footprintWidthCells = 1;
        public int footprintDepthCells = 1;
        public float footprintCenterOffsetX;
        public float footprintCenterOffsetZ;
    }
}
