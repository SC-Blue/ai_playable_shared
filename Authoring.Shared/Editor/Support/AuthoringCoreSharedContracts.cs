using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.AuthoringCore
{
    public static class AuthoringCoreSharedContracts
    {
        public const int CATALOG_EXPORT_SCHEMA_VERSION = 8;
        public const int CATALOG_MANIFEST_SCHEMA_VERSION = 1;
        public const int CATALOG_INDEX_SCHEMA_VERSION = 1;
        public const int PLAYABLE_RECIPE_SCHEMA_VERSION = 5;
        public const int PLAYABLE_RECIPE_METADATA_SCHEMA_VERSION = 4;
        public const string DEFAULT_CATALOG_MANIFEST_FILE_NAME = "CATALOG_MANIFEST.generated.json";
        public const string DEFAULT_CATALOG_MANIFEST_MARKDOWN_FILE_NAME = "CATALOG_MANIFEST.generated.md";
        public const string CATALOG_INDEX_FILE_NAME = "CATALOG_INDEX.generated.json";
        public const string CATALOG_INDEX_MARKDOWN_FILE_NAME = "CATALOG_INDEX.generated.md";
        public const string CATALOG_THEMES_DIRECTORY_NAME = "catalogs";
        public const string CATALOG_CORE_FILE_NAME = "CATALOG_CORE.generated.json";
        public const string CATALOG_STEP2_FILE_NAME = "CATALOG_STEP2.generated.json";
        public const string CATALOG_STEP3_FILE_NAME = "CATALOG_STEP3.generated.json";
        public const string CATALOG_SHARD_KIND_CORE = "core";
        public const string CATALOG_SHARD_KIND_STEP2 = "step2";
        public const string CATALOG_SHARD_KIND_STEP3 = "step3";
        public const string CATALOG_USAGE_VALIDATE = "validate";
        public const string CATALOG_USAGE_GENERATE = "generate-playable";
        public const string MANIFEST_FILE_NAME = "manifest.json";
        public const string INPUT_INTENT_FILE_NAME = "input_intent.json";
        public const string COMPILED_PLAN_FILE_NAME = "compiled_plan.json";
        public const string CORE_RESULT_FILE_NAME = "core_result.json";
        public const string GENERATION_RESULT_FILE_NAME = "generation_result.json";
        public const string PLAYABLE_RECIPE_METADATA_FILE_NAME = "playable_recipe_metadata.json";
        public const string DRAFT_LAYOUT_FILE_NAME = "draft_layout.json";
        public const string LAYOUT_SPEC_FILE_NAME = "layout_spec.internal.json";
        public const string LAYOUT_SPEC_COMPAT_FILE_NAME = "layout_spec.internal.compat.json";
        public const string ID_MAPPING_FILE_NAME = "id_mapping.json";
        public const string BAKED_PLAYABLE_DATA_FILE_NAME = "baked_playable_data.json";
    }

    public static class CatalogGameplayDesignModeTokens
    {
        public const string SINGLE_PREFAB = "single_prefab";
        public const string ASSEMBLED_PATH = "assembled_path";
    }

    public static class PlayableRecipeBundleStateTokens
    {
        public const string FRESH = "fresh";
        public const string STALE = "stale";
        public const string BAKED = "baked";
        public const string FAILURE = "failure";
    }

    public static class PlayableRecipeLogSeverityTokens
    {
        public const string INFO = "info";
        public const string WARNING = "warning";
        public const string ERROR = "error";
    }

    public static class PlayableRecipeOperationTokens
    {
        public const string GENERATE = "generate";
        public const string BAKE = "bake";
        public const string SYSTEM = "system";
    }

    public static class AuthoringLayoutRules
    {
        public const float OUTER_ROAD_MIN_FLOOR_CLEARANCE_CELLS = 1f;
        private const float BOUNDARY_TIE_EPSILON = 0.0001f;

        public static float ResolveFloorBoundaryInwardFacingYaw(
            float positionX,
            float positionZ,
            float minWorldX,
            float maxWorldX,
            float minWorldZ,
            float maxWorldZ)
        {
            float centerX = (minWorldX + maxWorldX) * 0.5f;
            float centerZ = (minWorldZ + maxWorldZ) * 0.5f;
            float minDistance = float.MaxValue;
            float bestYaw = 90f;
            float bestAlignment = float.MinValue;

            EvaluateCandidate(
                Math.Abs(positionX - minWorldX),
                90f,
                centerX - positionX,
                ref minDistance,
                ref bestYaw,
                ref bestAlignment);
            EvaluateCandidate(
                Math.Abs(maxWorldX - positionX),
                270f,
                positionX - centerX,
                ref minDistance,
                ref bestYaw,
                ref bestAlignment);
            EvaluateCandidate(
                Math.Abs(positionZ - minWorldZ),
                0f,
                centerZ - positionZ,
                ref minDistance,
                ref bestYaw,
                ref bestAlignment);
            EvaluateCandidate(
                Math.Abs(maxWorldZ - positionZ),
                180f,
                positionZ - centerZ,
                ref minDistance,
                ref bestYaw,
                ref bestAlignment);

            return bestYaw;
        }

        private static void EvaluateCandidate(
            float distance,
            float yawDegrees,
            float inwardAlignment,
            ref float minDistance,
            ref float bestYaw,
            ref float bestAlignment)
        {
            if (distance < minDistance - BOUNDARY_TIE_EPSILON)
            {
                minDistance = distance;
                bestYaw = yawDegrees;
                bestAlignment = inwardAlignment;
                return;
            }

            if (Math.Abs(distance - minDistance) > BOUNDARY_TIE_EPSILON)
                return;

            if (inwardAlignment > bestAlignment + BOUNDARY_TIE_EPSILON)
            {
                bestYaw = yawDegrees;
                bestAlignment = inwardAlignment;
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class CatalogEditorEntryExportData
    {
        public string objectId = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata prefabMetadata = new CatalogPrefabMetadata();
    }

    [Serializable]
    public sealed class CatalogGameplayDesignExportData
    {
        public string designId = string.Empty;
        public string description = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata prefabMetadata = new CatalogPrefabMetadata();
    }

    [Serializable]
    public sealed class CatalogAssembledPathAssetsExportData
    {
        public string straightPrefabAssetPath = string.Empty;
        public string straightPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata straightPrefabMetadata = new CatalogPrefabMetadata();
        public string cornerPrefabAssetPath = string.Empty;
        public string cornerPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata cornerPrefabMetadata = new CatalogPrefabMetadata();
    }

    [Serializable]
    public sealed class CatalogAssembledPathDesignExportData
    {
        public string designId = string.Empty;
        public string description = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata prefabMetadata = new CatalogPrefabMetadata();
        public CatalogAssembledPathAssetsExportData assembledPathAssets = new CatalogAssembledPathAssetsExportData();
    }

    [Serializable]
    public sealed class CatalogEnvironmentDesignExportData
    {
        public string designId = string.Empty;
        public string description = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata prefabMetadata = new CatalogPrefabMetadata();
        public string straightPrefabAssetPath = string.Empty;
        public string straightPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata straightPrefabMetadata = new CatalogPrefabMetadata();
        public string cornerPrefabAssetPath = string.Empty;
        public string cornerPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata cornerPrefabMetadata = new CatalogPrefabMetadata();
        public string tJunctionPrefabAssetPath = string.Empty;
        public string tJunctionPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata tJunctionPrefabMetadata = new CatalogPrefabMetadata();
        public string crossPrefabAssetPath = string.Empty;
        public string crossPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata crossPrefabMetadata = new CatalogPrefabMetadata();
    }

    [Serializable]
    public sealed class CatalogSinglePrefabGameplayEntryExportData
    {
        public string objectId = string.Empty;
        public string category = string.Empty;
        public string designMode = CatalogGameplayDesignModeTokens.SINGLE_PREFAB;
        public CatalogGameplayDesignExportData[] designs = new CatalogGameplayDesignExportData[0];
    }

    [Serializable]
    public sealed class CatalogSinglePrefabGameplaySectionExportData
    {
        public string arrayPath = string.Empty;
        public string label = string.Empty;
        public string expectedCategory = string.Empty;
        public string designMode = CatalogGameplayDesignModeTokens.SINGLE_PREFAB;
        public CatalogSinglePrefabGameplayEntryExportData[] entries = new CatalogSinglePrefabGameplayEntryExportData[0];
    }

    [Serializable]
    public sealed class CatalogAssembledPathGameplayEntryExportData
    {
        public string objectId = string.Empty;
        public string category = string.Empty;
        public string designMode = CatalogGameplayDesignModeTokens.ASSEMBLED_PATH;
        public CatalogAssembledPathDesignExportData[] designs = new CatalogAssembledPathDesignExportData[0];
    }

    [Serializable]
    public sealed class CatalogAssembledPathGameplaySectionExportData
    {
        public string arrayPath = string.Empty;
        public string label = string.Empty;
        public string expectedCategory = string.Empty;
        public string designMode = CatalogGameplayDesignModeTokens.ASSEMBLED_PATH;
        public CatalogAssembledPathGameplayEntryExportData[] entries = new CatalogAssembledPathGameplayEntryExportData[0];
    }

    [Serializable]
    public sealed class CatalogEnvironmentEntryExportData
    {
        public string objectId = string.Empty;
        public string category = string.Empty;
        public string placementMode = string.Empty;
        public string variationMode = string.Empty;
        public CatalogEnvironmentDesignExportData[] designs = new CatalogEnvironmentDesignExportData[0];
    }

    [Serializable]
    public sealed class CatalogEnvironmentSectionExportData
    {
        public string arrayPath = string.Empty;
        public string label = string.Empty;
        public string expectedCategory = string.Empty;
        public string placementMode = string.Empty;
        public CatalogEnvironmentEntryExportData[] entries = new CatalogEnvironmentEntryExportData[0];
    }

    [Serializable]
    public sealed class CatalogContentSelectionDesignExportData
    {
        public string designId = string.Empty;
        public string description = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata prefabMetadata = new CatalogPrefabMetadata();
    }

    [Serializable]
    public sealed class CatalogContentSelectionEntryExportData
    {
        public string objectId = string.Empty;
        public string category = string.Empty;
        public CatalogContentSelectionDesignExportData[] designs = new CatalogContentSelectionDesignExportData[0];
    }

    [Serializable]
    public sealed class PlayableCatalogExportData
    {
        public int schemaVersion = AuthoringCoreSharedContracts.CATALOG_EXPORT_SCHEMA_VERSION;
        public string sourceCatalogAssetPath = string.Empty;
        public string themeId = string.Empty;
        public string prefabsRootPath = string.Empty;
        public FeatureDescriptor[] activeFeatureDescriptors = new FeatureDescriptor[0];
        public string environmentFloorPrefabAssetPath = string.Empty;
        public string environmentFloorPrefabAssetGuid = string.Empty;
        public CatalogPrefabMetadata environmentFloorPrefabMetadata = new CatalogPrefabMetadata();
        public CatalogEditorEntryExportData[] editorBasedEntries = new CatalogEditorEntryExportData[0];
        public CatalogContentSelectionEntryExportData[] contentSelectionEntries = new CatalogContentSelectionEntryExportData[0];
        public CatalogSinglePrefabGameplaySectionExportData[] singlePrefabGameplaySections = new CatalogSinglePrefabGameplaySectionExportData[0];
        public CatalogAssembledPathGameplaySectionExportData[] assembledPathGameplaySections = new CatalogAssembledPathGameplaySectionExportData[0];
        public CatalogEnvironmentSectionExportData[] environmentSections = new CatalogEnvironmentSectionExportData[0];
    }

    [Serializable]
    public sealed class CatalogShardDescriptor
    {
        public string shardKind = string.Empty;
        public string fileName = string.Empty;
        public string contentHash = string.Empty;
    }

    [Serializable]
    public sealed class CatalogExportManifestData
    {
        public int schemaVersion = AuthoringCoreSharedContracts.CATALOG_MANIFEST_SCHEMA_VERSION;
        public string sourceCatalogAssetPath = string.Empty;
        public string themeId = string.Empty;
        public string prefabsRootPath = string.Empty;
        public CatalogShardDescriptor[] shards = new CatalogShardDescriptor[0];
    }

    [Serializable]
    public sealed class CatalogThemeManifestEntry
    {
        public string themeId = string.Empty;
        public string sourceCatalogAssetPath = string.Empty;
        public string prefabsRootPath = string.Empty;
        public string manifestRelativePath = string.Empty;
        public string markdownRelativePath = string.Empty;
    }

    [Serializable]
    public sealed class CatalogManifestIndexData
    {
        public int schemaVersion = AuthoringCoreSharedContracts.CATALOG_INDEX_SCHEMA_VERSION;
        public string defaultThemeId = string.Empty;
        public CatalogThemeManifestEntry[] themes = new CatalogThemeManifestEntry[0];
    }

    [Serializable]
    public sealed class CatalogShardFingerprint
    {
        public string shardKind = string.Empty;
        public string fileName = string.Empty;
        public string contentHash = string.Empty;
    }

    [Serializable]
    public sealed class PlayableRecipeManifest
    {
        public int schemaVersion = AuthoringCoreSharedContracts.PLAYABLE_RECIPE_SCHEMA_VERSION;
        public string playableRecipeId = string.Empty;
        public string createdAtUtc = string.Empty;
        public string mode = string.Empty;
        public string sourceInputPath = string.Empty;
        public string originRecipeId = string.Empty;
        public string originRecipeDirectory = string.Empty;
        public string inputIntentFileName = AuthoringCoreSharedContracts.INPUT_INTENT_FILE_NAME;
        public string compiledPlanFileName = AuthoringCoreSharedContracts.COMPILED_PLAN_FILE_NAME;
        public string coreResultFileName = AuthoringCoreSharedContracts.CORE_RESULT_FILE_NAME;
        public string generationResultFileName = AuthoringCoreSharedContracts.GENERATION_RESULT_FILE_NAME;
        public string draftLayoutFileName = string.Empty;
        public string layoutSpecFileName = string.Empty;
        public string idMappingFileName = string.Empty;
        public PlayableRecipeSourceHashSnapshot generatedSourceHashes = new PlayableRecipeSourceHashSnapshot();
        public string catalogManifestPath = string.Empty;
        public string catalogManifestContentHash = string.Empty;
        public CatalogShardFingerprint[] catalogShardFingerprints = new CatalogShardFingerprint[0];
        public string sourceCatalogAssetPath = string.Empty;
        public string catalogAssetContentHash = string.Empty;
    }

    [Serializable]
    public sealed class PlayableRecipeResultData
    {
        public bool success;
        public string stage = string.Empty;
        public string failureCode = string.Empty;
        public string message = string.Empty;
        public string[] errors = new string[0];
        public string[] warnings = new string[0];
        public PlayableRecipeOperationLogEntry[] operationLogs = new PlayableRecipeOperationLogEntry[0];
        public string playableRecipeDirectory = string.Empty;
        public string inputJsonPath = string.Empty;
        public string compiledPlanPath = string.Empty;
        public string catalogPath = string.Empty;
        public string catalogManifestPath = string.Empty;
        public string draftLayoutPath = string.Empty;
        public string layoutSpecPath = string.Empty;
        public string idMappingPath = string.Empty;
        public string reportDirectory = string.Empty;
        public string bakedDataPath = string.Empty;
        public string scenePath = string.Empty;
        public string timestamp = string.Empty;
    }

    [Serializable]
    public sealed class PlayableRecipeAttemptMetadata
    {
        public string attemptId = string.Empty;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public bool success;
        public string stage = string.Empty;
        public string failureCode = string.Empty;
        public string message = string.Empty;
        public string[] warnings = new string[0];
        public PlayableRecipeOperationLogEntry[] operationLogs = new PlayableRecipeOperationLogEntry[0];
        public string generationResultPath = string.Empty;
        public string reportDirectory = string.Empty;
        public string bakedDataPath = string.Empty;
        public string scenePath = string.Empty;
    }

    [Serializable]
    public sealed class PlayableRecipeOperationLogEntry
    {
        public string timestampUtc = string.Empty;
        public string operationKind = string.Empty;
        public string stage = string.Empty;
        public string severity = string.Empty;
        public string message = string.Empty;
    }

    [Serializable]
    public sealed class PlayableRecipeSourceHashSnapshot
    {
        public string inputIntentContentHash = string.Empty;
        public string enrichedIntentContentHash = string.Empty;
        public string draftLayoutContentHash = string.Empty;
        public string layoutSpecContentHash = string.Empty;
        public string idMappingContentHash = string.Empty;
    }

    [Serializable]
    public sealed class PlayableRecipeMetadata
    {
        public int schemaVersion = AuthoringCoreSharedContracts.PLAYABLE_RECIPE_METADATA_SCHEMA_VERSION;
        public string playableRecipeId = string.Empty;
        public string createdAtUtc = string.Empty;
        public string sourceInputPath = string.Empty;
        public string originRecipeId = string.Empty;
        public string originRecipeDirectory = string.Empty;
        public string bundleState = PlayableRecipeBundleStateTokens.FRESH;
        public string lastGeneratedAtUtc = string.Empty;
        public PlayableRecipeSourceHashSnapshot lastGeneratedSourceHashes = new PlayableRecipeSourceHashSnapshot();
        public string lastUpdatedAtUtc = string.Empty;
        public bool hasBakeHistory;
        public int bakeAttemptCount;
        public string firstBakedAtUtc = string.Empty;
        public string lastBakedAtUtc = string.Empty;
        public string lastResult = "not_started";
        public string lastStage = string.Empty;
        public string lastFailureCode = string.Empty;
        public string lastMessage = string.Empty;
        public string lastGenerationResultPath = string.Empty;
        public string lastReportDirectory = string.Empty;
        public string lastBakedDataPath = string.Empty;
        public string lastScenePath = string.Empty;
        public PlayableRecipeOperationLogEntry[] operationLogs = new PlayableRecipeOperationLogEntry[0];
        public PlayableRecipeAttemptMetadata[] bakeAttempts = new PlayableRecipeAttemptMetadata[0];
    }

    [Serializable]
    public sealed class PlacementConfidenceSummaryData
    {
        public float score;
        public string grade = string.Empty;
        public int entryCount;
        public int bboxEntryCount;
        public int worldAnchorEntryCount;
        public int worldAnchorDominatedEntryCount;
        public int autoRelocatedEntryCount;
        public int severeRiskCount;
        public float pxPerCellMedian;
        public float pxPerCellMedianDeviation;
        public float averageMovementDistance;
        public float maxMovementDistance;
        public bool environmentPreserved;
        public bool topologyChanged;
        public int environmentOmissionRiskCount;
        public int environmentAdjacencyGapRiskCount;
        public int crossFamilyConflictCount;
        public int orientationAmbiguityRiskCount;
        public bool expansionApplied;
        public bool roadParticipatedInBarrierCanonicalization;
        public string[] expandedEntries = new string[0];
        public string[] expandedFamilies = new string[0];
        public int totalExpandedCellCount;
        public string[] expandedBoundarySides = new string[0];
        public int expandedLeftCells;
        public int expandedRightCells;
        public int expandedTopCells;
        public int expandedBottomCells;
        public float solverElapsedMs;
        public int solverTimeoutMs;
        public bool solverTimedOut;
        public string solverSearchMode = string.Empty;
        public PlacementSolverBudgetData solverBudget = new PlacementSolverBudgetData();
        public PlacementPhaseTimingData phaseTimings = new PlacementPhaseTimingData();
    }

    [Serializable]
    public sealed class PlacementSolverBudgetData
    {
        public int timeoutMs;
        public int beamWidth;
        public int maxCandidatesPerObject;
        public int maxExpansionCells;
        public int expansionStateCount;
    }

    [Serializable]
    public sealed class PlacementPhaseTimingData
    {
        public float catalogLoadMs;
        public float intentReadMs;
        public float layoutSpecReadMs;
        public float idMappingReadMs;
        public float placementSolveMs;
        public float confidenceReportMs;
        public float coreValidationMs;
        public float totalMs;
    }

    [Serializable]
    public sealed class PlacementConfidenceEntryData
    {
        public string objectId = string.Empty;
        public string inputSource = string.Empty;
        public string sourceImageId = string.Empty;
        public string laneId = string.Empty;
        public bool hasLaneOrder;
        public int laneOrder;
        public bool hasMinGapToNextCells;
        public float minGapToNextCells;
        public bool hasActualGapToNextCells;
        public float actualGapToNextCells;
        public float score;
        public float bboxConfidence;
        public float worldX;
        public float worldZ;
        public float preferredWorldX;
        public float preferredWorldZ;
        public int widthCells;
        public int depthCells;
        public float movementDistance;
        public float movedCells;
        public float bboxAspectError;
        public bool autoRelocated;
        public bool orientationAmbiguous;
        public float resolvedYawDegrees;
        public string rotationEvidence = string.Empty;
        public string orientationReason = string.Empty;
        public string footprintEvidence = string.Empty;
        public string solverPlacementSource = string.Empty;
        public float anchorDeltaCellsX;
        public float anchorDeltaCellsZ;
        public string placementMode = string.Empty;
        public string variationMode = string.Empty;
        public string[] warnings = new string[0];
    }

    [Serializable]
    public sealed class PlacementConfidenceReportData
    {
        public string generatedAtUtc = string.Empty;
        public string layoutSpecPath = string.Empty;
        public PlacementConfidenceSummaryData summary = new PlacementConfidenceSummaryData();
        public string[] globalWarnings = new string[0];
        public PlacementConfidenceEntryData[] entries = new PlacementConfidenceEntryData[0];
    }

    [Serializable]
    public sealed class LayoutSpecDocument
    {
        public LayoutSpecSourceImageEntry[] sourceImages = new LayoutSpecSourceImageEntry[0];
        public LayoutSpecPlayerStartEntry playerStart = new LayoutSpecPlayerStartEntry();
        public LayoutSpecPlacementEntry[] placements = new LayoutSpecPlacementEntry[0];
        public LayoutSpecEnvironmentEntry[] environment = new LayoutSpecEnvironmentEntry[0];
        public LayoutSpecCustomerPathEntry[] customerPaths = new LayoutSpecCustomerPathEntry[0];
        public LayoutSpecFloorBounds floorBounds = new LayoutSpecFloorBounds();
    }

    [Serializable]
    public sealed class LayoutSpecSourceImageEntry
    {
        public string sourceImageId = string.Empty;
        public string description = string.Empty;
    }

    [Serializable]
    public sealed class LayoutSpecPlayerStartEntry
    {
        public string objectId = string.Empty;
        public string sourceImageId = string.Empty;
        public bool hasWorldPosition;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
        public bool hasImageBounds;
        public float centerPxX;
        public float centerPxY;
        public float bboxWidthPx;
        public float bboxHeightPx;
        public float bboxConfidence;
    }

    [Serializable]
    public sealed class LayoutSpecFloorBounds
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    [Serializable]
    public sealed class LayoutSpecPlacementBoundsEntry
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
        public bool hasImageBounds;
        public float centerPxX;
        public float centerPxY;
        public float bboxWidthPx;
        public float bboxHeightPx;
        public float bboxConfidence;
    }

    [Serializable]
    public sealed class LayoutSpecPlacementEntry
    {
        public string objectId = string.Empty;
        public string imageLabel = string.Empty;
        public string sourceImageId = string.Empty;
        public string laneId = string.Empty;
        public bool hasLaneOrder;
        public int laneOrder;
        public bool hasMinGapToNextCells;
        public float minGapToNextCells;
        // Rejected on parse if present in external JSON.
        public int gridX;
        public int gridZ;
        public bool hasWorldPosition;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
        public string solverPlacementSource = string.Empty;
        public string orientationReason = string.Empty;
        public float anchorDeltaCellsX;
        public float anchorDeltaCellsZ;
        public bool hasImageBounds;
        public float centerPxX;
        public float centerPxY;
        public float bboxWidthPx;
        public float bboxHeightPx;
        public float bboxConfidence;
        public FeatureJsonPayload featureLayout = new FeatureJsonPayload();
    }

    [Serializable]
    public sealed class LayoutSpecEnvironmentEntry
    {
        public string objectId = string.Empty;
        public string designId = string.Empty;
        public string sourceImageId = string.Empty;
        public string kind = string.Empty;
        // Rejected on parse if present in external JSON.
        public int gridX;
        public int gridZ;
        public int widthCells = 1;
        public int depthCells = 1;
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
        public float rotationY;
        public string prefabPath = string.Empty;
        public bool includeInBounds = true;
        public bool singleLayer = true;
        public bool isOuterBoundary;
    }

    [Serializable]
    public sealed class LayoutSpecCustomerPathEntry
    {
        public string targetId = string.Empty;
        public string sourceImageId = string.Empty;
        public LayoutSpecCustomerPathPoint spawnPoint = new LayoutSpecCustomerPathPoint();
        public LayoutSpecCustomerPathPoint leavePoint = new LayoutSpecCustomerPathPoint();
        public LayoutSpecCustomerPathPoint[] queuePoints = new LayoutSpecCustomerPathPoint[0];
        public LayoutSpecCustomerPathPoint[] entryWaypoints = new LayoutSpecCustomerPathPoint[0];
        public LayoutSpecCustomerPathPoint[] exitWaypoints = new LayoutSpecCustomerPathPoint[0];
    }

    [Serializable]
    public sealed class LayoutSpecCustomerPathPoint
    {
        public int gridX;
        public int gridZ;
        public bool hasWorldPosition;
        public float worldX;
        public float worldZ;
    }

    [Serializable]
    public sealed class LayoutIdMappingDocument
    {
        public LayoutIdMappingEntry[] mappings = new LayoutIdMappingEntry[0];
    }

    public static class LayoutAuthoringModeUtility
    {
        public static bool HasAuthoringContent(LayoutSpecDocument layoutSpec)
        {
            if (layoutSpec == null)
                return false;

            if ((layoutSpec.sourceImages ?? new LayoutSpecSourceImageEntry[0]).Length > 0)
                return true;
            if ((layoutSpec.placements ?? new LayoutSpecPlacementEntry[0]).Length > 0)
                return true;
            if ((layoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0]).Length > 0)
                return true;
            if ((layoutSpec.customerPaths ?? new LayoutSpecCustomerPathEntry[0]).Length > 0)
                return true;

            LayoutSpecPlayerStartEntry playerStart = layoutSpec.playerStart ?? new LayoutSpecPlayerStartEntry();
            if (!string.IsNullOrWhiteSpace(playerStart.objectId) ||
                playerStart.hasWorldPosition ||
                playerStart.hasImageBounds)
            {
                return true;
            }

            LayoutSpecFloorBounds floorBounds = layoutSpec.floorBounds ?? new LayoutSpecFloorBounds();
            return floorBounds.hasWorldBounds;
        }
    }

    [Serializable]
    public sealed class LayoutIdMappingEntry
    {
        public string imageLabel = string.Empty;
        public string objectId = string.Empty;
    }

    public static class LayoutSpecGeometryUtility
    {
        public static Dictionary<string, SerializableVector3> BuildPositionLookup(
            ScenarioModelObjectDefinition[] objects,
            LayoutSpecDocument layoutSpec,
            List<string> errors)
        {
            var positions = new Dictionary<string, SerializableVector3>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            if (layoutSpec == null)
            {
                errors?.Add("layoutSpec이 필요합니다.");
                return positions;
            }

            Dictionary<string, LayoutSpecPlacementEntry> placementLookup = BuildPlacementLookup(layoutSpec);
            LayoutSpecPlayerStartEntry playerStart = layoutSpec.playerStart ?? new LayoutSpecPlayerStartEntry();
            string playerStartObjectId = Normalize(playerStart.objectId);

            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                string role = Normalize(value != null ? value.role : string.Empty);
                if (string.IsNullOrEmpty(objectId))
                    continue;

                if (string.Equals(role, PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
                {
                    if (!string.Equals(playerStartObjectId, objectId, StringComparison.Ordinal))
                    {
                        errors?.Add("layoutSpec.playerStart.objectId가 player object '" + objectId + "'와 일치하지 않습니다.");
                        continue;
                    }

                    if (!playerStart.hasWorldPosition)
                    {
                        errors?.Add("layoutSpec.playerStart에는 world position이 필요합니다.");
                        continue;
                    }

                    positions[objectId] = new SerializableVector3(playerStart.worldX, 0f, playerStart.worldZ);
                    continue;
                }

                if (!placementLookup.TryGetValue(objectId, out LayoutSpecPlacementEntry placement) || placement == null)
                {
                    errors?.Add("layoutSpec.placements에 object '" + objectId + "'가 없습니다.");
                    continue;
                }

                if (!placement.hasWorldPosition)
                {
                    errors?.Add("layoutSpec.placements['" + objectId + "']에는 world position이 필요합니다.");
                    continue;
                }

                positions[objectId] = new SerializableVector3(placement.worldX, 0f, placement.worldZ);
            }

            return positions;
        }

        public static Dictionary<string, LayoutSpecPlacementEntry> BuildPlacementLookup(LayoutSpecDocument layoutSpec)
        {
            var lookup = new Dictionary<string, LayoutSpecPlacementEntry>(StringComparer.Ordinal);
            LayoutSpecPlacementEntry[] placements = layoutSpec != null
                ? layoutSpec.placements ?? new LayoutSpecPlacementEntry[0]
                : new LayoutSpecPlacementEntry[0];

            for (int i = 0; i < placements.Length; i++)
            {
                LayoutSpecPlacementEntry entry = placements[i];
                string objectId = Normalize(entry != null ? entry.objectId : string.Empty);
                if (string.IsNullOrEmpty(objectId) || lookup.ContainsKey(objectId))
                    continue;

                lookup.Add(objectId, entry);
            }

            return lookup;
        }

        public static bool TryGetPlacement(LayoutSpecDocument layoutSpec, string objectId, out LayoutSpecPlacementEntry placement)
        {
            placement = null;
            if (layoutSpec == null || string.IsNullOrWhiteSpace(objectId))
                return false;

            return BuildPlacementLookup(layoutSpec).TryGetValue(Normalize(objectId), out placement);
        }

        private static WorldBoundsDefinition ToWorldBounds(LayoutSpecPlacementBoundsEntry bounds)
        {
            return new WorldBoundsDefinition
            {
                hasWorldBounds = bounds != null && bounds.hasWorldBounds,
                worldX = bounds != null ? bounds.worldX : 0f,
                worldZ = bounds != null ? bounds.worldZ : 0f,
                worldWidth = bounds != null ? bounds.worldWidth : 0f,
                worldDepth = bounds != null ? bounds.worldDepth : 0f,
            };
        }

        private static string Normalize(string value)
        {
            return value != null ? value.Trim() : string.Empty;
        }
    }
}

