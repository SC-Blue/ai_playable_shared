using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class FeaturePathAuthoringUtility
    {
        private const string SIDE_LEFT = "left";
        private const string SIDE_RIGHT = "right";
        private const string SIDE_TOP = "top";
        private const string SIDE_BOTTOM = "bottom";

        private const float EPSILON = 0.0001f;
        private const float CELL_WORLD_SIZE = 1f;
        private const int TILE_SIZE_CELLS = 2;
        private const int TILE_COORDINATE_STRIDE = TILE_SIZE_CELLS * 2;
        private const float HALF_TILE_WORLD_SIZE = TILE_SIZE_CELLS * CELL_WORLD_SIZE * 0.5f;

        public readonly struct OrderedPathResult
        {
            public OrderedPathResult(int[][] orderedCells, string startSide, string sinkSide)
            {
                OrderedCells = orderedCells ?? new int[0][];
                StartSide = startSide ?? string.Empty;
                SinkSide = sinkSide ?? string.Empty;
            }

            public int[][] OrderedCells { get; }
            public string StartSide { get; }
            public string SinkSide { get; }
        }

        public static bool TryBuildTrackBounds(
            FeaturePathAnchorDefinition[] pathCells,
            out WorldBoundsDefinition bounds,
            out string errorMessage)
        {
            bounds = new WorldBoundsDefinition();
            errorMessage = string.Empty;
            if (!TryBuildQuantizedCells(pathCells, out List<Cell> quantizedCells, out errorMessage))
                return false;

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;
            for (int i = 0; i < quantizedCells.Count; i++)
            {
                Cell cell = quantizedCells[i];
                if (cell.X < minX)
                    minX = cell.X;
                if (cell.X > maxX)
                    maxX = cell.X;
                if (cell.Z < minZ)
                    minZ = cell.Z;
                if (cell.Z > maxZ)
                    maxZ = cell.Z;
            }

            bounds = new WorldBoundsDefinition
            {
                hasWorldBounds = true,
                worldX = DoubleCoordinateToWorld(minX + maxX) * 0.5f,
                worldZ = DoubleCoordinateToWorld(minZ + maxZ) * 0.5f,
                worldWidth = DoubleCoordinateToWorld(maxX - minX) + (TILE_SIZE_CELLS * CELL_WORLD_SIZE),
                worldDepth = DoubleCoordinateToWorld(maxZ - minZ) + (TILE_SIZE_CELLS * CELL_WORLD_SIZE),
            };
            return true;
        }

        public static bool TryResolveOrderedPath(
            FeaturePathAnchorDefinition[] pathCells,
            WorldBoundsDefinition sinkBounds,
            out OrderedPathResult orderedPath,
            out string errorMessage)
        {
            orderedPath = new OrderedPathResult(new int[0][], string.Empty, string.Empty);
            errorMessage = string.Empty;
            if (!TryBuildQuantizedCells(pathCells, out List<Cell> quantizedCells, out errorMessage))
                return false;

            if (!TryBuildTerminalOrderedPath(quantizedCells, out List<Cell> orderedCellPath, out errorMessage))
                return false;

            float sinkCenterX = sinkBounds != null ? sinkBounds.worldX : 0f;
            float sinkCenterZ = sinkBounds != null ? sinkBounds.worldZ : 0f;
            float distanceFirst = DistanceSquaredToSink(orderedCellPath[0], sinkCenterX, sinkCenterZ);
            float distanceLast = DistanceSquaredToSink(orderedCellPath[orderedCellPath.Count - 1], sinkCenterX, sinkCenterZ);
            if (Math.Abs(distanceFirst - distanceLast) <= EPSILON)
            {
                errorMessage = "path terminal 두 점이 sink와 같은 거리여서 end를 결정할 수 없습니다.";
                return false;
            }

            if (distanceFirst < distanceLast)
                orderedCellPath.Reverse();

            var orderedCells = new List<int[]>(orderedCellPath.Count);
            for (int i = 0; i < orderedCellPath.Count; i++)
                orderedCells.Add(new[] { orderedCellPath[i].X, orderedCellPath[i].Z });

            if (!TryValidateSupportedShape(orderedCells, out errorMessage))
                return false;

            orderedPath = new OrderedPathResult(
                orderedCells.ToArray(),
                ResolveStartSide(orderedCells),
                ResolveSinkSide(orderedCells));
            return true;
        }

        public static bool TryResolveConnectedPathCells(
            FeaturePathAnchorDefinition[] pathCells,
            out int[][] orderedCells,
            out string errorMessage)
        {
            orderedCells = new int[0][];
            errorMessage = string.Empty;
            if (!TryBuildQuantizedCells(pathCells, out List<Cell> quantizedCells, out errorMessage))
                return false;

            if (!TryBuildTerminalOrderedPath(quantizedCells, out List<Cell> orderedCellPath, out errorMessage))
                return false;

            var resolved = new List<int[]>(orderedCellPath.Count);
            for (int i = 0; i < orderedCellPath.Count; i++)
                resolved.Add(new[] { orderedCellPath[i].X, orderedCellPath[i].Z });

            if (!TryValidateSupportedShape(resolved, out errorMessage))
                return false;

            orderedCells = resolved.ToArray();
            return true;
        }

        public static bool TryBuildResolvedPath(
            FeaturePathAnchorDefinition[] pathCells,
            WorldBoundsDefinition sinkBounds,
            out FeaturePathDefinition path,
            out string errorMessage)
        {
            path = new FeaturePathDefinition();
            errorMessage = string.Empty;
            if (!TryResolveOrderedPath(pathCells, sinkBounds, out OrderedPathResult orderedPath, out errorMessage))
                return false;

            int[][] orderedCells = orderedPath.OrderedCells;
            var cells = new FeaturePathCellDefinition[orderedCells.Length];
            for (int i = 0; i < orderedCells.Length; i++)
            {
                cells[i] = BuildPathCell(orderedCells, i);
            }

            var worldPoints = new List<SerializableVector3>(orderedCells.Length + 2);
            Cell startCell = new Cell(orderedCells[0][0], orderedCells[0][1]);
            Cell endCell = new Cell(orderedCells[orderedCells.Length - 1][0], orderedCells[orderedCells.Length - 1][1]);
            AppendWorldPoint(worldPoints, ResolveTileSideCenter(startCell, orderedPath.StartSide));
            for (int i = 0; i < orderedCells.Length; i++)
                AppendWorldPoint(worldPoints, new SerializableVector3(DoubleCoordinateToWorld(orderedCells[i][0]), 0.75f, DoubleCoordinateToWorld(orderedCells[i][1])));
            AppendWorldPoint(worldPoints, ResolveTileSideCenter(endCell, orderedPath.SinkSide));

            path = new FeaturePathDefinition
            {
                sourceSide = orderedPath.StartSide,
                sinkSide = orderedPath.SinkSide,
                cells = cells,
                worldPoints = worldPoints.ToArray(),
            };
            return true;
        }

        public static float DoubleCoordinateToWorld(int doubledCoordinate)
        {
            return doubledCoordinate * 0.5f * CELL_WORLD_SIZE;
        }

        public static float TileWorldSize => TILE_SIZE_CELLS * CELL_WORLD_SIZE;

        private static bool TryBuildQuantizedCells(
            FeaturePathAnchorDefinition[] pathCells,
            out List<Cell> cells,
            out string errorMessage)
        {
            cells = new List<Cell>();
            errorMessage = string.Empty;
            FeaturePathAnchorDefinition[] safeCells = pathCells ?? new FeaturePathAnchorDefinition[0];
            if (safeCells.Length == 0)
            {
                errorMessage = "pathCells가 비어 있습니다.";
                return false;
            }

            var seen = new HashSet<Cell>();
            for (int i = 0; i < safeCells.Length; i++)
            {
                FeaturePathAnchorDefinition cell = safeCells[i];
                int doubledX = QuantizeDoubleCoordinate(cell != null ? cell.worldX : 0f);
                int doubledZ = QuantizeDoubleCoordinate(cell != null ? cell.worldZ : 0f);
                if (Math.Abs(DoubleCoordinateToWorld(doubledX) - (cell != null ? cell.worldX : 0f)) > EPSILON ||
                    Math.Abs(DoubleCoordinateToWorld(doubledZ) - (cell != null ? cell.worldZ : 0f)) > EPSILON)
                {
                    errorMessage = "pathCells는 grid 중심 좌표만 허용합니다.";
                    return false;
                }

                var quantized = new Cell(doubledX, doubledZ);
                if (!seen.Add(quantized))
                {
                    errorMessage = "pathCells에 중복 cell이 있습니다.";
                    return false;
                }

                cells.Add(quantized);
            }

            return true;
        }

        private static float DistanceSquaredToSink(Cell cell, float sinkCenterX, float sinkCenterZ)
        {
            float deltaX = DoubleCoordinateToWorld(cell.X) - sinkCenterX;
            float deltaZ = DoubleCoordinateToWorld(cell.Z) - sinkCenterZ;
            return (deltaX * deltaX) + (deltaZ * deltaZ);
        }

        private static bool TryBuildTerminalOrderedPath(
            List<Cell> quantizedCells,
            out List<Cell> orderedCells,
            out string errorMessage)
        {
            orderedCells = new List<Cell>();
            errorMessage = string.Empty;
            if (quantizedCells == null || quantizedCells.Count < 2)
            {
                errorMessage = "pathCells는 최소 2개가 필요합니다.";
                return false;
            }

            var neighbors = new Dictionary<Cell, List<Cell>>();
            for (int i = 0; i < quantizedCells.Count; i++)
                neighbors[quantizedCells[i]] = new List<Cell>();

            for (int i = 0; i < quantizedCells.Count; i++)
            {
                for (int j = i + 1; j < quantizedCells.Count; j++)
                {
                    Cell left = quantizedCells[i];
                    Cell right = quantizedCells[j];
                    int deltaX = Math.Abs(left.X - right.X);
                    int deltaZ = Math.Abs(left.Z - right.Z);
                    bool isNeighbor = (deltaX == TILE_COORDINATE_STRIDE && deltaZ == 0) ||
                                      (deltaZ == TILE_COORDINATE_STRIDE && deltaX == 0);
                    if (!isNeighbor)
                        continue;

                    neighbors[left].Add(right);
                    neighbors[right].Add(left);
                }
            }

            var terminals = new List<Cell>();
            foreach (KeyValuePair<Cell, List<Cell>> pair in neighbors)
            {
                int degree = pair.Value.Count;
                if (degree == 1)
                {
                    terminals.Add(pair.Key);
                    continue;
                }

                if (degree == 2)
                    continue;

                errorMessage = degree == 0
                    ? "pathCells에 끊긴 단일 tile이 있습니다."
                    : "pathCells는 branch를 지원하지 않습니다.";
                return false;
            }

            if (terminals.Count != 2)
            {
                errorMessage = "pathCells는 terminal endpoint가 정확히 2개여야 합니다.";
                return false;
            }

            var visited = new HashSet<Cell>();
            var queue = new Queue<Cell>();
            queue.Enqueue(terminals[0]);
            visited.Add(terminals[0]);
            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();
                List<Cell> currentNeighbors = neighbors[current];
                for (int i = 0; i < currentNeighbors.Count; i++)
                {
                    Cell next = currentNeighbors[i];
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            if (visited.Count != quantizedCells.Count)
            {
                errorMessage = "pathCells가 하나의 connected path가 아닙니다.";
                return false;
            }

            orderedCells.Add(terminals[0]);
            Cell previous = default;
            Cell cursor = terminals[0];
            bool hasPrevious = false;
            while (true)
            {
                List<Cell> currentNeighbors = neighbors[cursor];
                Cell next = default;
                bool foundNext = false;
                for (int i = 0; i < currentNeighbors.Count; i++)
                {
                    Cell candidate = currentNeighbors[i];
                    if (hasPrevious && candidate.Equals(previous))
                        continue;

                    next = candidate;
                    foundNext = true;
                    break;
                }

                if (!foundNext)
                    break;

                orderedCells.Add(next);
                previous = cursor;
                cursor = next;
                hasPrevious = true;
            }

            if (orderedCells.Count != quantizedCells.Count)
            {
                errorMessage = "pathCells 순서를 구성하지 못했습니다.";
                return false;
            }

            return true;
        }

        private static bool TryValidateSupportedShape(IReadOnlyList<int[]> orderedCells, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (orderedCells == null || orderedCells.Count < 2)
            {
                errorMessage = "pathCells는 최소 2개가 필요합니다.";
                return false;
            }

            int previousDeltaX = orderedCells[1][0] - orderedCells[0][0];
            int previousDeltaZ = orderedCells[1][1] - orderedCells[0][1];
            int turnCount = 0;
            for (int i = 1; i < orderedCells.Count - 1; i++)
            {
                int currentDeltaX = orderedCells[i + 1][0] - orderedCells[i][0];
                int currentDeltaZ = orderedCells[i + 1][1] - orderedCells[i][1];
                if (currentDeltaX != previousDeltaX || currentDeltaZ != previousDeltaZ)
                    turnCount++;

                previousDeltaX = currentDeltaX;
                previousDeltaZ = currentDeltaZ;
            }

            if (turnCount > 1)
            {
                errorMessage = "pathCells는 현재 직선 또는 ㄴ자 경로만 지원합니다.";
                return false;
            }

            return true;
        }

        private static string ResolveStartSide(IReadOnlyList<int[]> orderedCells)
        {
            int[] first = orderedCells[0];
            int[] next = orderedCells[1];
            int deltaX = next[0] - first[0];
            int deltaZ = next[1] - first[1];
            if (deltaX > 0)
                return SIDE_LEFT;
            if (deltaX < 0)
                return SIDE_RIGHT;
            if (deltaZ > 0)
                return SIDE_BOTTOM;
            return SIDE_TOP;
        }

        private static string ResolveSinkSide(IReadOnlyList<int[]> orderedCells)
        {
            int[] previous = orderedCells[orderedCells.Count - 2];
            int[] last = orderedCells[orderedCells.Count - 1];
            int deltaX = last[0] - previous[0];
            int deltaZ = last[1] - previous[1];
            if (deltaX > 0)
                return SIDE_RIGHT;
            if (deltaX < 0)
                return SIDE_LEFT;
            if (deltaZ > 0)
                return SIDE_TOP;
            return SIDE_BOTTOM;
        }

        private static FeaturePathCellDefinition BuildPathCell(IReadOnlyList<int[]> cells, int index)
        {
            int[] current = cells[index];
            if (index == 0)
            {
                int[] next = cells[index + 1];
                return new FeaturePathCellDefinition
                {
                    gridX = current[0],
                    gridZ = current[1],
                    elementKind = FeaturePathElementKinds.STRAIGHT,
                    rotationQuarterTurns = ResolveStraightQuarterTurns(next[0] - current[0], next[1] - current[1]),
                };
            }

            if (index == cells.Count - 1)
            {
                int[] previous = cells[index - 1];
                return new FeaturePathCellDefinition
                {
                    gridX = current[0],
                    gridZ = current[1],
                    elementKind = FeaturePathElementKinds.STRAIGHT,
                    rotationQuarterTurns = ResolveStraightQuarterTurns(previous[0] - current[0], previous[1] - current[1]),
                };
            }

            int incomingX = current[0] - cells[index - 1][0];
            int incomingZ = current[1] - cells[index - 1][1];
            int outgoingX = cells[index + 1][0] - current[0];
            int outgoingZ = cells[index + 1][1] - current[1];
            if (incomingX == outgoingX || incomingZ == outgoingZ)
            {
                return new FeaturePathCellDefinition
                {
                    gridX = current[0],
                    gridZ = current[1],
                    elementKind = FeaturePathElementKinds.STRAIGHT,
                    rotationQuarterTurns = ResolveStraightQuarterTurns(incomingX, incomingZ),
                };
            }

            return new FeaturePathCellDefinition
            {
                gridX = current[0],
                gridZ = current[1],
                elementKind = FeaturePathElementKinds.CORNER,
                rotationQuarterTurns = ResolveCornerQuarterTurns(incomingX, incomingZ, outgoingX, outgoingZ),
            };
        }

        private static int ResolveStraightQuarterTurns(int deltaX, int deltaZ)
        {
            return deltaX != 0 ? 1 : 0;
        }

        private static int ResolveCornerQuarterTurns(int incomingX, int incomingZ, int outgoingX, int outgoingZ)
        {
            bool hasTop = incomingZ > 0 || outgoingZ > 0;
            bool hasBottom = incomingZ < 0 || outgoingZ < 0;
            bool hasRight = incomingX > 0 || outgoingX > 0;
            bool hasLeft = incomingX < 0 || outgoingX < 0;
            if (hasBottom && hasLeft)
                return 0;
            if (hasLeft && hasTop)
                return 1;
            if (hasTop && hasRight)
                return 2;
            if (hasRight && hasBottom)
                return 3;
            return 0;
        }

        private static SerializableVector3 ResolveTileSideCenter(Cell cell, string side)
        {
            float centerX = DoubleCoordinateToWorld(cell.X);
            float centerZ = DoubleCoordinateToWorld(cell.Z);
            if (string.Equals(side, SIDE_LEFT, StringComparison.Ordinal))
                return new SerializableVector3(centerX - HALF_TILE_WORLD_SIZE, 0.75f, centerZ);
            if (string.Equals(side, SIDE_RIGHT, StringComparison.Ordinal))
                return new SerializableVector3(centerX + HALF_TILE_WORLD_SIZE, 0.75f, centerZ);
            if (string.Equals(side, SIDE_TOP, StringComparison.Ordinal))
                return new SerializableVector3(centerX, 0.75f, centerZ + HALF_TILE_WORLD_SIZE);
            return new SerializableVector3(centerX, 0.75f, centerZ - HALF_TILE_WORLD_SIZE);
        }

        private static void AppendWorldPoint(List<SerializableVector3> points, SerializableVector3 worldPoint)
        {
            if (points.Count > 0)
            {
                SerializableVector3 previous = points[points.Count - 1];
                float deltaX = previous.x - worldPoint.x;
                float deltaY = previous.y - worldPoint.y;
                float deltaZ = previous.z - worldPoint.z;
                if ((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ) <= EPSILON)
                    return;
            }

            points.Add(worldPoint);
        }

        private static int QuantizeDoubleCoordinate(float worldValue)
        {
            return (int)Math.Round((worldValue / CELL_WORLD_SIZE) * 2f, MidpointRounding.AwayFromZero);
        }

        private readonly struct Cell : IEquatable<Cell>
        {
            public Cell(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }

            public bool Equals(Cell other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is Cell other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X, Z);
            }
        }
    }
}
