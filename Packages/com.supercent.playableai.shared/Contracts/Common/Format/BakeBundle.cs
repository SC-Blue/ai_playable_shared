using System;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class BakeBundleCatalogIdentity
    {
        public string themeId = string.Empty;
        public string manifestContentHash = string.Empty;
    }

    [Serializable]
    public sealed class BakeBundleGenerationMetadata
    {
        public string sourceInputPath = string.Empty;
        public string createdAtUtc = string.Empty;
        public string mode = string.Empty;
    }

    [Serializable]
    public sealed class BakeBundleCoreResultData
    {
        public bool success;
        public string stage = string.Empty;
        public string failureCode = string.Empty;
        public string message = string.Empty;
        public string[] errors = Array.Empty<string>();
        public string[] warnings = Array.Empty<string>();
    }

    [Serializable]
    public sealed class BakeBundle
    {
        public const int CURRENT_BUNDLE_VERSION = 3;

        public int bundleVersion = CURRENT_BUNDLE_VERSION;
        public string sessionId = string.Empty;
        public string playableRecipeId = string.Empty;
        public BakeBundleCatalogIdentity catalogIdentity = new BakeBundleCatalogIdentity();
        public BakeBundleGenerationMetadata generationMetadata = new BakeBundleGenerationMetadata();
        public PlayablePromptIntent intent = new PlayablePromptIntent();
        public CompiledPlayablePlan compiledPlan = new CompiledPlayablePlan();
        public BakeBundleCoreResultData coreResult = new BakeBundleCoreResultData();
        public PortableDraftLayoutDocument draftLayout = new PortableDraftLayoutDocument();
        public PortableLayoutSpecDocument layoutSpec = new PortableLayoutSpecDocument();
        public PortableLayoutIdMappingDocument idMapping = new PortableLayoutIdMappingDocument();
    }

    [Serializable]
    public sealed class PortableDraftLayoutDocument
    {
        public int schemaVersion = 1;
        public PortableDraftLayoutFloorBounds floorBounds = new PortableDraftLayoutFloorBounds();
        public PortableDraftLayoutPlacementEntry[] placements = Array.Empty<PortableDraftLayoutPlacementEntry>();
        public PortableDraftLayoutPlayerStartEntry playerStart = new PortableDraftLayoutPlayerStartEntry();
        public PortableDraftLayoutEnvironmentEntry[] environment = Array.Empty<PortableDraftLayoutEnvironmentEntry>();
        public PortableDraftLayoutCustomerPathEntry[] customerPaths = Array.Empty<PortableDraftLayoutCustomerPathEntry>();
    }

    [Serializable]
    public sealed class PortableDraftLayoutFloorBounds
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    [Serializable]
    public sealed class PortableDraftLayoutPlacementBoundsEntry
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    [Serializable]
    public sealed class PortableDraftLayoutPhysicsAreaLayoutEntry
    {
        public PortableDraftLayoutPlacementBoundsEntry realPhysicsZoneBounds = new PortableDraftLayoutPlacementBoundsEntry();
        public PortableDraftLayoutPlacementBoundsEntry fakeSpriteZoneBounds = new PortableDraftLayoutPlacementBoundsEntry();
        public PlacementOverlapAllowanceDefinition[] overlapAllowances = Array.Empty<PlacementOverlapAllowanceDefinition>();
    }

    [Serializable]
    public sealed class PortableDraftLayoutRailLayoutEntry
    {
        public RailPathAnchorDefinition[] pathCells = Array.Empty<RailPathAnchorDefinition>();
    }

    [Serializable]
    public sealed class PortableDraftLayoutPlacementEntry
    {
        public string objectId = string.Empty;
        public string laneId = string.Empty;
        public int? laneOrder;
        public string sharedSlotId = string.Empty;
        public float? minGapToNextCells;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
        public PortableDraftLayoutPhysicsAreaLayoutEntry physicsAreaLayout = new PortableDraftLayoutPhysicsAreaLayoutEntry();
        public PortableDraftLayoutRailLayoutEntry railLayout = new PortableDraftLayoutRailLayoutEntry();
    }

    [Serializable]
    public sealed class PortableDraftLayoutPlayerStartEntry
    {
        public string objectId = string.Empty;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
    }

    [Serializable]
    public sealed class PortableDraftLayoutEnvironmentEntry
    {
        public string objectId = string.Empty;
        public string designId = string.Empty;
        public string kind = string.Empty;
        public int widthCells = 1;
        public int depthCells = 1;
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
        public float rotationY;
        public bool includeInBounds = true;
        public bool singleLayer = true;
        public bool isOuterBoundary;
    }

    [Serializable]
    public sealed class PortableDraftLayoutCustomerPathPoint
    {
        public float worldX;
        public float worldZ;
    }

    [Serializable]
    public sealed class PortableDraftLayoutCustomerPathEntry
    {
        public string targetId = string.Empty;
        public PortableDraftLayoutCustomerPathPoint spawnPoint = new PortableDraftLayoutCustomerPathPoint();
        public PortableDraftLayoutCustomerPathPoint leavePoint = new PortableDraftLayoutCustomerPathPoint();
        public PortableDraftLayoutCustomerPathPoint[] queuePoints = Array.Empty<PortableDraftLayoutCustomerPathPoint>();
        public PortableDraftLayoutCustomerPathPoint[] entryWaypoints = Array.Empty<PortableDraftLayoutCustomerPathPoint>();
        public PortableDraftLayoutCustomerPathPoint[] exitWaypoints = Array.Empty<PortableDraftLayoutCustomerPathPoint>();
    }

    [Serializable]
    public sealed class PortableLayoutSpecDocument
    {
        public PortableLayoutSpecSourceImageEntry[] sourceImages = Array.Empty<PortableLayoutSpecSourceImageEntry>();
        public PortableLayoutSpecPlayerStartEntry playerStart = new PortableLayoutSpecPlayerStartEntry();
        public PortableLayoutSpecPlacementEntry[] placements = Array.Empty<PortableLayoutSpecPlacementEntry>();
        public PortableLayoutSpecEnvironmentEntry[] environment = Array.Empty<PortableLayoutSpecEnvironmentEntry>();
        public PortableLayoutSpecCustomerPathEntry[] customerPaths = Array.Empty<PortableLayoutSpecCustomerPathEntry>();
        public PortableLayoutSpecFloorBounds floorBounds = new PortableLayoutSpecFloorBounds();
    }

    [Serializable]
    public sealed class PortableLayoutSpecSourceImageEntry
    {
        public string sourceImageId = string.Empty;
        public string description = string.Empty;
    }

    [Serializable]
    public sealed class PortableLayoutSpecPlayerStartEntry
    {
        public string objectId = string.Empty;
        public string sourceImageId = string.Empty;
        public bool hasWorldPosition;
        public float worldX;
        public float worldZ;
        public bool hasImageBounds;
        public float centerPxX;
        public float centerPxY;
        public float bboxWidthPx;
        public float bboxHeightPx;
        public float bboxConfidence;
    }

    [Serializable]
    public sealed class PortableLayoutSpecFloorBounds
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    [Serializable]
    public sealed class PortableLayoutSpecPlacementBoundsEntry
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
    public sealed class PortableLayoutSpecPhysicsAreaLayoutEntry
    {
        public PortableLayoutSpecPlacementBoundsEntry realPhysicsZoneBounds = new PortableLayoutSpecPlacementBoundsEntry();
        public PortableLayoutSpecPlacementBoundsEntry fakeSpriteZoneBounds = new PortableLayoutSpecPlacementBoundsEntry();
        public int itemsPerBlock = 1;
        public PlacementOverlapAllowanceDefinition[] overlapAllowances = Array.Empty<PlacementOverlapAllowanceDefinition>();
    }

    [Serializable]
    public sealed class PortableLayoutSpecRailLayoutEntry
    {
        public RailPathAnchorDefinition[] pathCells = Array.Empty<RailPathAnchorDefinition>();
    }

    [Serializable]
    public sealed class PortableLayoutSpecPlacementEntry
    {
        public string objectId = string.Empty;
        public string imageLabel = string.Empty;
        public string sourceImageId = string.Empty;
        public string laneId = string.Empty;
        public bool hasLaneOrder;
        public int laneOrder;
        public string sharedSlotId = string.Empty;
        public bool hasMinGapToNextCells;
        public float minGapToNextCells;
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
        public PortableLayoutSpecPhysicsAreaLayoutEntry physicsAreaLayout = new PortableLayoutSpecPhysicsAreaLayoutEntry();
        public PortableLayoutSpecRailLayoutEntry railLayout = new PortableLayoutSpecRailLayoutEntry();
    }

    [Serializable]
    public sealed class PortableLayoutSpecEnvironmentEntry
    {
        public string objectId = string.Empty;
        public string designId = string.Empty;
        public string sourceImageId = string.Empty;
        public string kind = string.Empty;
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
    public sealed class PortableLayoutSpecCustomerPathEntry
    {
        public string targetId = string.Empty;
        public string sourceImageId = string.Empty;
        public PortableLayoutSpecCustomerPathPoint spawnPoint = new PortableLayoutSpecCustomerPathPoint();
        public PortableLayoutSpecCustomerPathPoint leavePoint = new PortableLayoutSpecCustomerPathPoint();
        public PortableLayoutSpecCustomerPathPoint[] queuePoints = Array.Empty<PortableLayoutSpecCustomerPathPoint>();
        public PortableLayoutSpecCustomerPathPoint[] entryWaypoints = Array.Empty<PortableLayoutSpecCustomerPathPoint>();
        public PortableLayoutSpecCustomerPathPoint[] exitWaypoints = Array.Empty<PortableLayoutSpecCustomerPathPoint>();
    }

    [Serializable]
    public sealed class PortableLayoutSpecCustomerPathPoint
    {
        public int gridX;
        public int gridZ;
        public bool hasWorldPosition;
        public float worldX;
        public float worldZ;
    }

    [Serializable]
    public sealed class PortableLayoutIdMappingDocument
    {
        public PortableLayoutIdMappingEntry[] mappings = Array.Empty<PortableLayoutIdMappingEntry>();
    }

    [Serializable]
    public sealed class PortableLayoutIdMappingEntry
    {
        public string imageLabel = string.Empty;
        public string objectId = string.Empty;
    }

    [Serializable]
    public sealed class BakeOperationResult
    {
        public string status = string.Empty;
        public string message = string.Empty;
        public string bakedDataPath = string.Empty;
        public string scenePath = string.Empty;
        public string[] diagnostics = Array.Empty<string>();
    }
}
