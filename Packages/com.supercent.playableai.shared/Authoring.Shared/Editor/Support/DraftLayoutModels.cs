using System;
using Supercent.PlayableAI.Common.Format;

namespace PlayableAI.AuthoringCore
{
    public static class DraftLayoutContracts
    {
        public const int SCHEMA_VERSION = 1;
    }

    public sealed class DraftLayoutDocument
    {
        public int schemaVersion = DraftLayoutContracts.SCHEMA_VERSION;
        public DraftLayoutFloorBounds floorBounds = new DraftLayoutFloorBounds();
        public DraftLayoutPlacementEntry[] placements = Array.Empty<DraftLayoutPlacementEntry>();
        public DraftLayoutPlayerStartEntry playerStart = new DraftLayoutPlayerStartEntry();
        public DraftLayoutEnvironmentEntry[] environment = Array.Empty<DraftLayoutEnvironmentEntry>();
        public DraftLayoutCustomerPathEntry[] customerPaths = Array.Empty<DraftLayoutCustomerPathEntry>();
    }

    public sealed class DraftLayoutFloorBounds
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    public sealed class DraftLayoutPlacementBoundsEntry
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    public sealed class DraftLayoutPhysicsAreaLayoutEntry
    {
        public DraftLayoutPlacementBoundsEntry realPhysicsZoneBounds = new DraftLayoutPlacementBoundsEntry();
        public DraftLayoutPlacementBoundsEntry fakeSpriteZoneBounds = new DraftLayoutPlacementBoundsEntry();
    }

    public sealed class DraftLayoutRailLayoutEntry
    {
        public RailPathAnchorDefinition[] pathCells = Array.Empty<RailPathAnchorDefinition>();
    }

    public sealed class DraftLayoutPlacementEntry
    {
        public string objectId = string.Empty;
        public string laneId = string.Empty;
        public int? laneOrder;
        public float? minGapToNextCells;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
        public DraftLayoutPhysicsAreaLayoutEntry physicsAreaLayout = new DraftLayoutPhysicsAreaLayoutEntry();
        public DraftLayoutRailLayoutEntry railLayout = new DraftLayoutRailLayoutEntry();
    }

    public sealed class DraftLayoutPlayerStartEntry
    {
        public string objectId = string.Empty;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
    }

    public sealed class DraftLayoutEnvironmentEntry
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

    public sealed class DraftLayoutCustomerPathPoint
    {
        public float worldX;
        public float worldZ;
    }

    public sealed class DraftLayoutCustomerPathEntry
    {
        public string targetId = string.Empty;
        public DraftLayoutCustomerPathPoint spawnPoint = new DraftLayoutCustomerPathPoint();
        public DraftLayoutCustomerPathPoint leavePoint = new DraftLayoutCustomerPathPoint();
        public DraftLayoutCustomerPathPoint[] queuePoints = Array.Empty<DraftLayoutCustomerPathPoint>();
        public DraftLayoutCustomerPathPoint[] entryWaypoints = Array.Empty<DraftLayoutCustomerPathPoint>();
        public DraftLayoutCustomerPathPoint[] exitWaypoints = Array.Empty<DraftLayoutCustomerPathPoint>();
    }
}
