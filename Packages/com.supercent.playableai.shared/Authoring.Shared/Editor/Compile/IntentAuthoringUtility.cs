using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Generation.Editor.Compile
{
    public static class IntentAuthoringUtility
    {
        private sealed class PackedPlacementEntry
        {
            public string ObjectId = string.Empty;
            public string Role = string.Empty;
            public string SourceEndpointTargetObjectId = string.Empty;
            public string SourceEndpointSide = string.Empty;
            public string SinkEndpointTargetObjectId = string.Empty;
            public string SinkEndpointSide = string.Empty;
            public HashSet<string> UnlockTargetReferenceIds = new HashSet<string>(System.StringComparer.Ordinal);
            public HashSet<string> SharedSlotReferenceIds = new HashSet<string>(System.StringComparer.Ordinal);
            public GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] OverlapAllowanceDescriptors = new GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[0];
            public bool HasWorldPosition;
            public float WorldX;
            public float WorldZ;
            public bool HasResolvedYaw;
            public float ResolvedYawDegrees;
            public int GridX;
            public int GridZ;
            public int WidthCells = 1;
            public int DepthCells = 1;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        private sealed class WorldPlacementBounds
        {
            public string ObjectId = string.Empty;
            public string Role = string.Empty;
            public string SourceEndpointTargetObjectId = string.Empty;
            public string SourceEndpointSide = string.Empty;
            public string SinkEndpointTargetObjectId = string.Empty;
            public string SinkEndpointSide = string.Empty;
            public HashSet<string> UnlockTargetReferenceIds = new HashSet<string>(System.StringComparer.Ordinal);
            public HashSet<string> SharedSlotReferenceIds = new HashSet<string>(System.StringComparer.Ordinal);
            public GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] OverlapAllowanceDescriptors = new GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[0];
            public float CenterX;
            public float CenterZ;
            public bool HasResolvedYaw;
            public float ResolvedYawDegrees;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        public const string DEFAULT_DESIGN_ID = "default";
        public const float LAYOUT_SPACING = 1f;
        public const float UNLOCK_PAD_ROW_Z = -1f;
        private const int PLAYER_PLACEMENT_SEARCH_RADIUS_CELLS = 64;

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool HasEntryFocusBeforeIndex(PromptIntentEffectDefinition[] effects, int index)
        {
            if (effects == null || index <= 0)
                return false;

            for (int i = 0; i < index && i < effects.Length; i++)
            {
                PromptIntentEffectDefinition effect = effects[i];
                if (effect != null &&
                    string.Equals(Normalize(effect.kind), PromptIntentEffectKinds.FOCUS_CAMERA, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasEntryFocusBeforeIndex(ScenarioModelEffectDefinition[] effects, int index)
        {
            if (effects == null || index <= 0)
                return false;

            for (int i = 0; i < index && i < effects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = effects[i];
                if (effect != null &&
                    string.Equals(Normalize(effect.kind), PromptIntentEffectKinds.FOCUS_CAMERA, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static string BuildSpawnKey(string objectId)
        {
            return string.IsNullOrEmpty(objectId) ? string.Empty : "spawn_" + objectId;
        }

        public static bool TryResolveCatalogObjectId(PlayableObjectCatalog catalog, string role, out string objectId, out string error)
        {
            objectId = string.Empty;
            error = string.Empty;
            if (catalog == null)
            {
                error = "PlayableObjectCatalog가 필요합니다.";
                return false;
            }

            switch (Normalize(role))
            {
                case PromptIntentObjectRoles.GENERATOR:
                    objectId = "generator";
                    break;
                case PromptIntentObjectRoles.PROCESSOR:
                    objectId = "converter";
                    break;
                case PromptIntentObjectRoles.SELLER:
                    objectId = "seller";
                    break;
                case PromptIntentObjectRoles.UNLOCK_PAD:
                    objectId = "unlocker";
                    break;
                case PromptIntentObjectRoles.RAIL:
                    objectId = "rail";
                    break;
                case PromptIntentObjectRoles.PLAYER:
                    return TryResolveUniqueCatalogObjectIdByCategory(catalog, "PlayerModel", out objectId, out error);
                case PromptIntentObjectRoles.PHYSICS_AREA:
                    error = "physics_area는 catalog-backed role이 아닙니다.";
                    return false;
                default:
                    error = "지원되지 않는 object role '" + role + "'입니다.";
                    return false;
            }

            if (catalog.IsSupportedGameplayObject(objectId))
                return true;

            error = "Catalog에 role '" + role + "'에 대응하는 gameplay objectId '" + objectId + "'가 없습니다.";
            return false;
        }

        public static bool IsCatalogBackedRole(string role)
        {
            return Normalize(role) != PromptIntentObjectRoles.PHYSICS_AREA;
        }

        public static bool TryResolveUniqueCatalogObjectIdByCategory(PlayableObjectCatalog catalog, string category, out string objectId, out string error)
        {
            objectId = string.Empty;
            error = string.Empty;
            if (catalog == null)
            {
                error = "PlayableObjectCatalog가 필요합니다.";
                return false;
            }

            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            IReadOnlyList<GameplayCatalogEntry> entries = catalog.GetGameplayEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                GameplayCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.objectId))
                    continue;
                if (!string.Equals(Normalize(entry.category), Normalize(category), System.StringComparison.Ordinal))
                    continue;

                seen.Add(entry.objectId.Trim());
            }

            if (seen.Count != 1)
            {
                error = "category '" + category + "'에는 정확히 1개의 objectId가 필요합니다.";
                return false;
            }

            foreach (string value in seen)
            {
                objectId = value;
                return true;
            }

            error = "category '" + category + "'에 대한 objectId를 찾지 못했습니다.";
            return false;
        }

        public static int ResolveGameplayDesignIndex(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
            string designId,
            System.Collections.Generic.List<string> errors,
            string label)
        {
            string normalizedObjectId = Normalize(gameplayObjectId);
            string normalizedDesignId = Normalize(designId);
            string requestedDesignId = string.IsNullOrEmpty(normalizedDesignId) ? DEFAULT_DESIGN_ID : normalizedDesignId;
            if (catalog == null)
            {
                if (errors != null)
                    errors.Add(label + "를 해석하려면 PlayableObjectCatalog가 필요합니다.");
                return -1;
            }

            if (!catalog.TryResolveGameplayDesignIndex(normalizedObjectId, requestedDesignId, out int resolvedDesignIndex))
            {
                if (errors != null)
                {
                    errors.Add(label + "에서 objectId '" + normalizedObjectId + "'의 designId '" + requestedDesignId + "'를 catalog design으로 해석하지 못했습니다.");
                }

                return -1;
            }

            return resolvedDesignIndex;
        }

        public static Dictionary<string, SerializableVector3> BuildDeterministicPositions(
            ScenarioModelObjectDefinition[] objects,
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            return BuildDeterministicPositions(objects, null, catalog, errors, null);
        }

        public static Dictionary<string, SerializableVector3> BuildDeterministicPositions(
            ScenarioModelObjectDefinition[] objects,
            ScenarioModelStageDefinition[] stages,
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            return BuildDeterministicPositions(objects, stages, catalog, errors, null);
        }

        public static Dictionary<string, SerializableVector3> BuildDeterministicPositions(
            ScenarioModelObjectDefinition[] objects,
            ScenarioModelStageDefinition[] stages,
            PlayableObjectCatalog catalog,
            List<string> errors,
            Dictionary<string, HashSet<string>> sharedSlotLookup)
        {
            var positions = new Dictionary<string, SerializableVector3>(System.StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            Dictionary<string, HashSet<string>> unlockTargetLookup = GameplayOverlapAllowanceRules.BuildUnlockTargetReferenceLookup(stages);
            List<PackedPlacementEntry> packedPlacements = BuildPackedPlacements(safeObjects, unlockTargetLookup, sharedSlotLookup, catalog, errors);
            HashSet<string> occupiedCells = BuildOccupiedCellSet(packedPlacements, errors);

            for (int i = 0; i < packedPlacements.Count; i++)
            {
                PackedPlacementEntry entry = packedPlacements[i];
                if (entry == null || string.IsNullOrEmpty(entry.ObjectId) || positions.ContainsKey(entry.ObjectId))
                    continue;

                positions.Add(
                    entry.ObjectId,
                    new SerializableVector3(
                        entry.HasWorldPosition ? entry.WorldX : entry.GridX * LAYOUT_SPACING,
                        0f,
                        entry.HasWorldPosition ? entry.WorldZ : entry.GridZ * LAYOUT_SPACING));
            }

            int nextMainGridX = 0;
            string[] mainRoles =
            {
                PromptIntentObjectRoles.GENERATOR,
                PromptIntentObjectRoles.PROCESSOR,
                PromptIntentObjectRoles.SELLER,
            };

            for (int roleIndex = 0; roleIndex < mainRoles.Length; roleIndex++)
            {
                string role = mainRoles[roleIndex];
                for (int i = 0; i < safeObjects.Length; i++)
                {
                    ScenarioModelObjectDefinition value = safeObjects[i];
                    string objectId = Normalize(value != null ? value.id : string.Empty);
                    if (string.IsNullOrEmpty(objectId) ||
                        Normalize(value.role) != role ||
                        positions.ContainsKey(objectId))
                    {
                        continue;
                    }

                    ResolveObjectFootprintOrDefault(value, catalog, errors, "objects[" + i + "]", out int widthCells, out int depthCells);
                    TryPlaceWithoutOverlap(
                        positions,
                        occupiedCells,
                        objectId,
                        nextMainGridX,
                        0,
                        widthCells,
                        depthCells,
                        errors,
                        "objects[" + i + "] auto-placement(main)");
                    nextMainGridX += widthCells;
                }
            }

            SerializableVector3 basePlayerCenter = ResolveBasePlayerCenter(safeObjects, positions);
            int basePlayerGridX = WorldToGridCoordinate(basePlayerCenter.x);
            int basePlayerGridZ = WorldToGridCoordinate(basePlayerCenter.z);
            int nextPlayerOffsetCells = 0;
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) ||
                    Normalize(value.role) != PromptIntentObjectRoles.PLAYER ||
                    positions.ContainsKey(objectId))
                {
                    continue;
                }

                ResolveObjectFootprintOrDefault(value, catalog, errors, "objects[" + i + "]", out int widthCells, out int depthCells);
                int playerGridX = basePlayerGridX + nextPlayerOffsetCells;
                TryPlacePlayerWithSearch(
                    positions,
                    occupiedCells,
                    objectId,
                    playerGridX,
                    basePlayerGridZ,
                    widthCells,
                    depthCells,
                    errors,
                    "objects[" + i + "] auto-placement(player)");
                nextPlayerOffsetCells -= widthCells;
            }

            int nextUnlockGridX = nextMainGridX;
            int unlockGridZ = WorldToGridCoordinate(UNLOCK_PAD_ROW_Z);
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) ||
                    Normalize(value.role) != PromptIntentObjectRoles.UNLOCK_PAD ||
                    positions.ContainsKey(objectId))
                {
                    continue;
                }

                ResolveObjectFootprintOrDefault(value, catalog, errors, "objects[" + i + "]", out int widthCells, out int depthCells);
                TryPlaceWithoutOverlap(
                    positions,
                    occupiedCells,
                    objectId,
                    nextUnlockGridX,
                    unlockGridZ,
                    widthCells,
                    depthCells,
                    errors,
                    "objects[" + i + "] auto-placement(unlocker)");
                nextUnlockGridX += widthCells;
            }

            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) || positions.ContainsKey(objectId))
                    continue;

                ResolveObjectFootprintOrDefault(value, catalog, errors, "objects[" + i + "]", out int widthCells, out int depthCells);
                TryPlaceWithoutOverlap(
                    positions,
                    occupiedCells,
                    objectId,
                    nextUnlockGridX,
                    0,
                    widthCells,
                    depthCells,
                    errors,
                    "objects[" + i + "] auto-placement(default)");
                nextUnlockGridX += widthCells;
            }

            return positions;
        }

        private static SerializableVector3 ResolveBasePlayerCenter(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, SerializableVector3> positions)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            if (positions == null || positions.Count == 0)
                return new SerializableVector3(0f, 0f, 0f);

            bool hasAny = false;
            float minX = 0f;
            float maxX = 0f;
            float minZ = 0f;
            float maxZ = 0f;
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                if (string.Equals(Normalize(value.role), PromptIntentObjectRoles.PLAYER, System.StringComparison.Ordinal))
                    continue;

                string objectId = Normalize(value.id);
                if (string.IsNullOrEmpty(objectId) || !positions.TryGetValue(objectId, out SerializableVector3 position))
                    continue;

                if (!hasAny)
                {
                    minX = position.x;
                    maxX = position.x;
                    minZ = position.z;
                    maxZ = position.z;
                    hasAny = true;
                    continue;
                }

                if (position.x < minX)
                    minX = position.x;
                if (position.x > maxX)
                    maxX = position.x;
                if (position.z < minZ)
                    minZ = position.z;
                if (position.z > maxZ)
                    maxZ = position.z;
            }

            if (!hasAny)
                return new SerializableVector3(0f, 0f, 0f);

            return new SerializableVector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        }

        private static List<PackedPlacementEntry> BuildPackedPlacements(
            ScenarioModelObjectDefinition[] objects,
            Dictionary<string, HashSet<string>> unlockTargetLookup,
            Dictionary<string, HashSet<string>> sharedSlotLookup,
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            var entries = new List<PackedPlacementEntry>();
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) || !HasPlacement(value != null ? value.placement : null))
                    continue;

                int widthCells = 1;
                int depthCells = 1;
                if (!TryResolvePlacementFootprint(catalog, value.role, value.designId, value.placement, out widthCells, out depthCells, out string error))
                {
                    if (errors != null)
                        errors.Add("objects[" + i + "].placement footprint를 해석하지 못해 1x1로 처리합니다: " + error);
                    widthCells = 1;
                    depthCells = 1;
                }

                if (ShouldSwapPlacementFootprintAxesForValidation(value.role, value.placement))
                {
                    int temp = widthCells;
                    widthCells = depthCells;
                    depthCells = temp;
                }

                bool hasWorldPosition = HasWorldPlacement(value.placement);
                float halfWidth = (widthCells > 0 ? widthCells : 1) * LAYOUT_SPACING * 0.5f;
                float halfDepth = (depthCells > 0 ? depthCells : 1) * LAYOUT_SPACING * 0.5f;
                GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] overlapAllowanceDescriptors = ResolvePlacementOverlapAllowanceDescriptors(
                    catalog,
                    value.role,
                    value.designId,
                    value.placement);
                entries.Add(new PackedPlacementEntry
                {
                    ObjectId = objectId,
                    Role = Normalize(value.role),
                    SinkEndpointTargetObjectId = Normalize(value.railOptions != null ? value.railOptions.sinkEndpointTargetObjectId : string.Empty),
                    UnlockTargetReferenceIds = ResolveUnlockTargetReferenceIds(unlockTargetLookup, objectId),
                    SharedSlotReferenceIds = ResolveUnlockTargetReferenceIds(sharedSlotLookup, objectId),
                    OverlapAllowanceDescriptors = overlapAllowanceDescriptors,
                    HasWorldPosition = hasWorldPosition,
                    WorldX = value.placement.worldX,
                    WorldZ = value.placement.worldZ,
                    HasResolvedYaw = value.placement != null && value.placement.hasResolvedYaw,
                    ResolvedYawDegrees = value.placement != null ? value.placement.resolvedYawDegrees : 0f,
                    GridX = WorldToGridCoordinate(value.placement.worldX),
                    GridZ = WorldToGridCoordinate(value.placement.worldZ),
                    WidthCells = widthCells > 0 ? widthCells : 1,
                    DepthCells = depthCells > 0 ? depthCells : 1,
                    MinX = value.placement.worldX - halfWidth,
                    MaxX = value.placement.worldX + halfWidth,
                    MinZ = value.placement.worldZ - halfDepth,
                    MaxZ = value.placement.worldZ + halfDepth,
                });
            }

            if (entries.Count == 0)
                return entries;

            return entries;
        }

        private static void ResolveObjectFootprintOrDefault(
            ScenarioModelObjectDefinition value,
            PlayableObjectCatalog catalog,
            List<string> errors,
            string label,
            out int widthCells,
            out int depthCells)
        {
            widthCells = 1;
            depthCells = 1;
            if (value == null)
                return;

            if (!TryResolvePlacementFootprint(catalog, value.role, value.designId, value.placement, out widthCells, out depthCells, out string error))
            {
                if (errors != null)
                    errors.Add(label + " placement footprint를 해석하지 못해 1x1로 처리합니다: " + error);
                widthCells = 1;
                depthCells = 1;
            }

            if (widthCells < 1)
                widthCells = 1;
            if (depthCells < 1)
                depthCells = 1;
        }

        private static HashSet<string> BuildOccupiedCellSet(List<PackedPlacementEntry> entries, List<string> errors)
        {
            var occupied = new HashSet<string>(System.StringComparer.Ordinal);
            var ownersByCell = new Dictionary<string, List<PackedPlacementEntry>>(System.StringComparer.Ordinal);
            List<PackedPlacementEntry> safeEntries = entries ?? new List<PackedPlacementEntry>();
            for (int i = 0; i < safeEntries.Count; i++)
            {
                PackedPlacementEntry entry = safeEntries[i];
                if (entry == null)
                    continue;

                ResolvePackedPlacementCellRange(entry, out int minCellX, out int maxCellX, out int minCellZ, out int maxCellZ);
                for (int x = minCellX; x < maxCellX; x++)
                {
                    for (int z = minCellZ; z < maxCellZ; z++)
                    {
                        string cellKey = BuildCellKey(x, z);
                        if (ownersByCell.TryGetValue(cellKey, out List<PackedPlacementEntry> existingOwners))
                        {
                            for (int ownerIndex = 0; ownerIndex < existingOwners.Count; ownerIndex++)
                            {
                                PackedPlacementEntry existingOwner = existingOwners[ownerIndex];
                                if (entry.HasWorldPosition &&
                                    existingOwner != null &&
                                    existingOwner.HasWorldPosition &&
                                    !AreWorldBoundsOverlapping(
                                        existingOwner.MinX,
                                        existingOwner.MaxX,
                                        existingOwner.MinZ,
                                        existingOwner.MaxZ,
                                        entry.MinX,
                                        entry.MaxX,
                                        entry.MinZ,
                                        entry.MaxZ))
                                {
                                    continue;
                                }

                                if (IsPackedPlacementOverlapAllowed(existingOwner, entry, out string overlapError))
                                    continue;
                            }

                            existingOwners.Add(entry);
                            occupied.Add(cellKey);
                            continue;
                        }

                        ownersByCell.Add(cellKey, new List<PackedPlacementEntry> { entry });
                        occupied.Add(cellKey);
                    }
                }
            }

            return occupied;
        }

        private static bool TryPlaceWithoutOverlap(
            Dictionary<string, SerializableVector3> positions,
            HashSet<string> occupiedCells,
            string objectId,
            int gridX,
            int gridZ,
            int widthCells,
            int depthCells,
            List<string> errors,
            string label)
        {
            if (string.IsNullOrEmpty(objectId) || positions == null)
                return false;

            if (IsAreaOccupied(occupiedCells, gridX, gridZ, widthCells, depthCells))
            {
                if (errors != null)
                {
                    errors.Add(
                        label + " 배치가 기존 점유와 겹칩니다. fail-fast로 중단합니다. " +
                        "objectId='" + objectId + "', grid=(" + gridX + ", " + gridZ + "), footprint=" +
                        widthCells + "x" + depthCells + ".");
                }

                return false;
            }

            positions[objectId] = new SerializableVector3(gridX * LAYOUT_SPACING, 0f, gridZ * LAYOUT_SPACING);
            MarkOccupiedCells(occupiedCells, gridX, gridZ, widthCells, depthCells);
            return true;
        }

        private static bool TryPlacePlayerWithSearch(
            Dictionary<string, SerializableVector3> positions,
            HashSet<string> occupiedCells,
            string objectId,
            int preferredGridX,
            int preferredGridZ,
            int widthCells,
            int depthCells,
            List<string> errors,
            string label)
        {
            if (string.IsNullOrEmpty(objectId) || positions == null)
                return false;

            if (!IsAreaOccupied(occupiedCells, preferredGridX, preferredGridZ, widthCells, depthCells))
            {
                return TryPlaceWithoutOverlap(
                    positions,
                    occupiedCells,
                    objectId,
                    preferredGridX,
                    preferredGridZ,
                    widthCells,
                    depthCells,
                    errors,
                    label);
            }

            for (int distance = 1; distance <= PLAYER_PLACEMENT_SEARCH_RADIUS_CELLS; distance++)
            {
                int leftCandidateX = preferredGridX - distance;
                if (!IsAreaOccupied(occupiedCells, leftCandidateX, preferredGridZ, widthCells, depthCells))
                {
                    return TryPlaceWithoutOverlap(
                        positions,
                        occupiedCells,
                        objectId,
                        leftCandidateX,
                        preferredGridZ,
                        widthCells,
                        depthCells,
                        errors,
                        label);
                }

                int rightCandidateX = preferredGridX + distance;
                if (!IsAreaOccupied(occupiedCells, rightCandidateX, preferredGridZ, widthCells, depthCells))
                {
                    return TryPlaceWithoutOverlap(
                        positions,
                        occupiedCells,
                        objectId,
                        rightCandidateX,
                        preferredGridZ,
                        widthCells,
                        depthCells,
                        errors,
                        label);
                }
            }

            if (errors != null)
            {
                errors.Add(
                    label + " 배치 가능한 빈 공간을 찾지 못했습니다. " +
                    "기준 grid=(" + preferredGridX + ", " + preferredGridZ + "), " +
                    "검색 반경=" + PLAYER_PLACEMENT_SEARCH_RADIUS_CELLS + ", " +
                    "footprint=" + widthCells + "x" + depthCells + ".");
            }

            return false;
        }

        private static bool IsAreaOccupied(HashSet<string> occupiedCells, int minX, int minZ, int widthCells, int depthCells)
        {
            if (occupiedCells == null)
                return false;

            int safeWidth = widthCells < 1 ? 1 : widthCells;
            int safeDepth = depthCells < 1 ? 1 : depthCells;
            for (int x = minX; x < minX + safeWidth; x++)
            {
                for (int z = minZ; z < minZ + safeDepth; z++)
                {
                    if (occupiedCells.Contains(BuildCellKey(x, z)))
                        return true;
                }
            }

            return false;
        }

        private static void MarkOccupiedCells(HashSet<string> occupiedCells, int minX, int minZ, int widthCells, int depthCells)
        {
            if (occupiedCells == null)
                return;

            int safeWidth = widthCells < 1 ? 1 : widthCells;
            int safeDepth = depthCells < 1 ? 1 : depthCells;
            for (int x = minX; x < minX + safeWidth; x++)
            {
                for (int z = minZ; z < minZ + safeDepth; z++)
                    occupiedCells.Add(BuildCellKey(x, z));
            }
        }

        private static string BuildCellKey(int x, int z)
        {
            return x.ToString() + ":" + z.ToString();
        }

        private static bool AreWorldBoundsOverlapping(
            float minAX,
            float maxAX,
            float minAZ,
            float maxAZ,
            float minBX,
            float maxBX,
            float minBZ,
            float maxBZ)
        {
            const float EPSILON = 0.0001f;
            bool overlapX = minAX < maxBX - EPSILON && maxAX > minBX + EPSILON;
            bool overlapZ = minAZ < maxBZ - EPSILON && maxAZ > minBZ + EPSILON;
            return overlapX && overlapZ;
        }

        private static int WorldToGridCoordinate(float worldCoordinate)
        {
            return (int)System.Math.Round(worldCoordinate / LAYOUT_SPACING, System.MidpointRounding.AwayFromZero);
        }

        public static bool HasPlacement(PromptIntentObjectPlacementDefinition placement)
        {
            return HasWorldPlacement(placement);
        }

        public static bool HasWorldPlacement(PromptIntentObjectPlacementDefinition placement)
        {
            return placement != null && placement.hasWorldPosition;
        }

        public static bool TryResolvePlacementFootprint(
            PlayableObjectCatalog catalog,
            string role,
            string designId,
            PromptIntentObjectPlacementDefinition placement,
            out int widthCells,
            out int depthCells,
            out string error)
        {
            return TryResolvePlacementFootprint(catalog, role, designId, placement, out widthCells, out depthCells, out _, out _, out error);
        }

        public static bool TryResolvePlacementFootprint(
            PlayableObjectCatalog catalog,
            string role,
            string designId,
            out int widthCells,
            out int depthCells,
            out string error)
        {
            return TryResolvePlacementFootprint(catalog, role, designId, null, out widthCells, out depthCells, out _, out _, out error);
        }

        public static bool TryResolvePlacementFootprint(
            PlayableObjectCatalog catalog,
            string role,
            string designId,
            PromptIntentObjectPlacementDefinition placement,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ,
            out string error)
        {
            widthCells = 0;
            depthCells = 0;
            centerOffsetX = 0f;
            centerOffsetZ = 0f;
            error = string.Empty;

            string normalizedRole = Normalize(role);
            if (normalizedRole == PromptIntentObjectRoles.PHYSICS_AREA)
                return TryResolvePhysicsAreaFootprint(placement, out widthCells, out depthCells, out error);

            if (normalizedRole == PromptIntentObjectRoles.RAIL &&
                TryResolveRailTrackFootprint(placement, out widthCells, out depthCells, out error))
            {
                return true;
            }

            if (!TryResolveCatalogObjectId(catalog, role, out string gameplayObjectId, out error))
                return false;

            if (catalog == null)
            {
                error = "PlayableObjectCatalog가 필요합니다.";
                return false;
            }

            return catalog.TryResolveGameplayPlacementFootprint(
                gameplayObjectId,
                designId,
                out widthCells,
                out depthCells,
                out centerOffsetX,
                out centerOffsetZ,
                out error);
        }

        public static void ValidatePlacementGrid(
            PromptIntentObjectDefinition[] objects,
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            ValidatePlacementGrid(objects, null, catalog, errors, null);
        }

        public static void ValidatePlacementGrid(
            PromptIntentObjectDefinition[] objects,
            PromptIntentStageDefinition[] stages,
            PlayableObjectCatalog catalog,
            List<string> errors,
            Dictionary<string, HashSet<string>> sharedSlotLookup = null)
        {
            var occupiedWorldBounds = new List<WorldPlacementBounds>();
            Dictionary<string, HashSet<string>> unlockTargetLookup = GameplayOverlapAllowanceRules.BuildUnlockTargetReferenceLookup(stages);
            PromptIntentObjectDefinition[] safeObjects = objects ?? new PromptIntentObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                PromptIntentObjectDefinition value = safeObjects[i];
                if (value == null || !HasPlacement(value.placement))
                    continue;

                string objectId = Normalize(value.id);
                if (string.IsNullOrEmpty(objectId))
                    continue;

                if (!HasWorldPlacement(value.placement))
                {
                    if (errors != null)
                        errors.Add("objects[" + i + "].placement는 gridX/gridZ를 사용할 수 없습니다. worldX/worldZ가 필요합니다.");
                    continue;
                }

                if (!TryResolvePlacementFootprint(
                        catalog,
                        value.role,
                        value.designId,
                        value.placement,
                        out int widthCells,
                        out int depthCells,
                        out _,
                        out _,
                        out string error))
                {
                    if (errors != null)
                        errors.Add("objects[" + i + "].placement를 검증하려면 footprint가 필요합니다: " + error);
                    continue;
                }

                if (ShouldSwapPlacementFootprintAxesForValidation(value.role, value.placement))
                {
                    int temp = widthCells;
                    widthCells = depthCells;
                    depthCells = temp;
                }

                float centerX = value.placement.worldX;
                float centerZ = value.placement.worldZ;
                string role = Normalize(value.role);
                float halfWidth = widthCells * LAYOUT_SPACING * 0.5f;
                float halfDepth = depthCells * LAYOUT_SPACING * 0.5f;
                float minX = centerX - halfWidth;
                float maxX = centerX + halfWidth;
                float minZ = centerZ - halfDepth;
                float maxZ = centerZ + halfDepth;
                GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] overlapAllowanceDescriptors = ResolvePlacementOverlapAllowanceDescriptors(
                    catalog,
                    value.role,
                    value.designId,
                    value.placement);

                for (int existingIndex = 0; existingIndex < occupiedWorldBounds.Count; existingIndex++)
                {
                    WorldPlacementBounds existing = occupiedWorldBounds[existingIndex];
                    if (!AreWorldBoundsOverlapping(minX, maxX, minZ, maxZ, existing.MinX, existing.MaxX, existing.MinZ, existing.MaxZ))
                        continue;

                    if (IsWorldPlacementOverlapAllowed(
                            new WorldPlacementBounds
                            {
                                ObjectId = objectId,
                                Role = role,
                                SinkEndpointTargetObjectId = Normalize(value.railOptions != null ? value.railOptions.sinkEndpointTargetObjectId : string.Empty),
                                UnlockTargetReferenceIds = ResolveUnlockTargetReferenceIds(unlockTargetLookup, objectId),
                                SharedSlotReferenceIds = ResolveUnlockTargetReferenceIds(sharedSlotLookup, objectId),
                                OverlapAllowanceDescriptors = overlapAllowanceDescriptors,
                                CenterX = centerX,
                                CenterZ = centerZ,
                                HasResolvedYaw = value.placement != null && value.placement.hasResolvedYaw,
                                ResolvedYawDegrees = value.placement != null ? value.placement.resolvedYawDegrees : 0f,
                                MinX = minX,
                                MaxX = maxX,
                                MinZ = minZ,
                                MaxZ = maxZ,
                            },
                            existing,
                            out string overlapError))
                    {
                        continue;
                    }

                    break;
                }

                occupiedWorldBounds.Add(new WorldPlacementBounds
                {
                    ObjectId = objectId,
                    Role = role,
                    SinkEndpointTargetObjectId = Normalize(value.railOptions != null ? value.railOptions.sinkEndpointTargetObjectId : string.Empty),
                    UnlockTargetReferenceIds = ResolveUnlockTargetReferenceIds(unlockTargetLookup, objectId),
                    SharedSlotReferenceIds = ResolveUnlockTargetReferenceIds(sharedSlotLookup, objectId),
                    OverlapAllowanceDescriptors = overlapAllowanceDescriptors,
                    CenterX = centerX,
                    CenterZ = centerZ,
                    HasResolvedYaw = value.placement != null && value.placement.hasResolvedYaw,
                    ResolvedYawDegrees = value.placement != null ? value.placement.resolvedYawDegrees : 0f,
                    MinX = minX,
                    MaxX = maxX,
                    MinZ = minZ,
                    MaxZ = maxZ,
                });
            }
        }

        private static bool ShouldSwapPlacementFootprintAxesForValidation(PromptIntentObjectDefinition value)
        {
            return ShouldSwapPlacementFootprintAxesForValidation(
                value != null ? value.role : string.Empty,
                value != null ? value.placement : null);
        }

        private static bool ShouldSwapPlacementFootprintAxesForValidation(
            string roleValue,
            PromptIntentObjectPlacementDefinition placement)
        {
            if (placement == null)
                return false;

            string role = Normalize(roleValue);
            if (role == PromptIntentObjectRoles.PHYSICS_AREA || role == PromptIntentObjectRoles.RAIL)
                return false;

            if (placement.hasResolvedYaw)
                return IsQuarterTurnOddYaw(placement.resolvedYawDegrees);
            return false;
        }

        private static bool TryResolvePhysicsAreaFootprint(
            PromptIntentObjectPlacementDefinition placement,
            out int widthCells,
            out int depthCells,
            out string error)
        {
            widthCells = 0;
            depthCells = 0;
            error = string.Empty;
            if (placement == null)
            {
                widthCells = 1;
                depthCells = 1;
                return true;
            }

            if (placement.physicsAreaLayout == null)
            {
                error = "physics_area는 placement.physicsAreaLayout이 필요합니다.";
                return false;
            }

            WorldBoundsDefinition real = placement.physicsAreaLayout.realPhysicsZoneBounds;
            WorldBoundsDefinition fake = placement.physicsAreaLayout.fakeSpriteZoneBounds;
            if (!HasWorldBounds(real) || !HasWorldBounds(fake))
            {
                error = "physics_area는 real/fake zone world bounds가 모두 필요합니다.";
                return false;
            }

            float minX = System.Math.Min(real.worldX - real.worldWidth * 0.5f, fake.worldX - fake.worldWidth * 0.5f);
            float maxX = System.Math.Max(real.worldX + real.worldWidth * 0.5f, fake.worldX + fake.worldWidth * 0.5f);
            float minZ = System.Math.Min(real.worldZ - real.worldDepth * 0.5f, fake.worldZ - fake.worldDepth * 0.5f);
            float maxZ = System.Math.Max(real.worldZ + real.worldDepth * 0.5f, fake.worldZ + fake.worldDepth * 0.5f);
            widthCells = ResolveWorldSizeToCells(maxX - minX);
            depthCells = ResolveWorldSizeToCells(maxZ - minZ);
            return true;
        }

        private static bool TryResolveRailTrackFootprint(
            PromptIntentObjectPlacementDefinition placement,
            out int widthCells,
            out int depthCells,
            out string error)
        {
            widthCells = 0;
            depthCells = 0;
            error = string.Empty;
            if (placement == null)
            {
                widthCells = 1;
                depthCells = 1;
                return true;
            }

            RailPathAnchorDefinition[] pathCells = placement.railLayout != null
                ? placement.railLayout.pathCells ?? new RailPathAnchorDefinition[0]
                : new RailPathAnchorDefinition[0];
            if (!RailPathAuthoringUtility.TryBuildTrackBounds(pathCells, out WorldBoundsDefinition trackBounds, out _))
            {
                error = "rail은 placement.railLayout.pathCells가 필요합니다.";
                return false;
            }

            widthCells = ResolveWorldSizeToCells(trackBounds.worldWidth);
            depthCells = ResolveWorldSizeToCells(trackBounds.worldDepth);
            return true;
        }

        private static int ResolveWorldSizeToCells(float worldSize)
        {
            if (worldSize <= 0f)
                return 1;

            return System.Math.Max(1, (int)System.Math.Round(worldSize / LAYOUT_SPACING, System.MidpointRounding.AwayFromZero));
        }

        private static bool HasWorldBounds(WorldBoundsDefinition bounds)
        {
            return bounds != null &&
                   bounds.hasWorldBounds &&
                   bounds.worldWidth > 0f &&
                   bounds.worldDepth > 0f;
        }

        private static HashSet<string> ResolveUnlockTargetReferenceIds(
            Dictionary<string, HashSet<string>> unlockTargetLookup,
            string objectId)
        {
            string normalizedObjectId = Normalize(objectId);
            if (unlockTargetLookup == null ||
                string.IsNullOrEmpty(normalizedObjectId) ||
                !unlockTargetLookup.TryGetValue(normalizedObjectId, out HashSet<string> targets) ||
                targets == null)
            {
                return new HashSet<string>(System.StringComparer.Ordinal);
            }

            return new HashSet<string>(targets, System.StringComparer.Ordinal);
        }

        private static bool IsPackedPlacementOverlapAllowed(
            PackedPlacementEntry left,
            PackedPlacementEntry right,
            out string overlapError)
        {
            GameplayOverlapAllowanceRules.Participant leftParticipant = BuildOverlapParticipant(
                left != null ? left.ObjectId : string.Empty,
                left != null ? left.ObjectId : string.Empty,
                left != null ? left.Role : string.Empty,
                left != null ? left.SourceEndpointTargetObjectId : string.Empty,
                left != null ? left.SourceEndpointSide : string.Empty,
                left != null ? left.SinkEndpointTargetObjectId : string.Empty,
                left != null ? left.SinkEndpointSide : string.Empty,
                left != null ? left.UnlockTargetReferenceIds : null,
                left != null ? left.SharedSlotReferenceIds : null,
                left != null ? left.OverlapAllowanceDescriptors : null,
                left != null ? left.WorldX : 0f,
                left != null ? left.WorldZ : 0f,
                left != null && left.HasResolvedYaw,
                left != null ? left.ResolvedYawDegrees : 0f,
                left != null ? left.MinX : 0f,
                left != null ? left.MaxX : 0f,
                left != null ? left.MinZ : 0f,
                left != null ? left.MaxZ : 0f);
            GameplayOverlapAllowanceRules.Participant rightParticipant = BuildOverlapParticipant(
                right != null ? right.ObjectId : string.Empty,
                right != null ? right.ObjectId : string.Empty,
                right != null ? right.Role : string.Empty,
                right != null ? right.SourceEndpointTargetObjectId : string.Empty,
                right != null ? right.SourceEndpointSide : string.Empty,
                right != null ? right.SinkEndpointTargetObjectId : string.Empty,
                right != null ? right.SinkEndpointSide : string.Empty,
                right != null ? right.UnlockTargetReferenceIds : null,
                right != null ? right.SharedSlotReferenceIds : null,
                right != null ? right.OverlapAllowanceDescriptors : null,
                right != null ? right.WorldX : 0f,
                right != null ? right.WorldZ : 0f,
                right != null && right.HasResolvedYaw,
                right != null ? right.ResolvedYawDegrees : 0f,
                right != null ? right.MinX : 0f,
                right != null ? right.MaxX : 0f,
                right != null ? right.MinZ : 0f,
                right != null ? right.MaxZ : 0f);
            return GameplayOverlapAllowanceRules.IsAllowedOverlap(leftParticipant, rightParticipant, out overlapError);
        }

        private static bool IsWorldPlacementOverlapAllowed(
            WorldPlacementBounds left,
            WorldPlacementBounds right,
            out string overlapError)
        {
            GameplayOverlapAllowanceRules.Participant leftParticipant = BuildOverlapParticipant(
                left != null ? left.ObjectId : string.Empty,
                left != null ? left.ObjectId : string.Empty,
                left != null ? left.Role : string.Empty,
                left != null ? left.SourceEndpointTargetObjectId : string.Empty,
                left != null ? left.SourceEndpointSide : string.Empty,
                left != null ? left.SinkEndpointTargetObjectId : string.Empty,
                left != null ? left.SinkEndpointSide : string.Empty,
                left != null ? left.UnlockTargetReferenceIds : null,
                left != null ? left.SharedSlotReferenceIds : null,
                left != null ? left.OverlapAllowanceDescriptors : null,
                left != null ? left.CenterX : 0f,
                left != null ? left.CenterZ : 0f,
                left != null && left.HasResolvedYaw,
                left != null ? left.ResolvedYawDegrees : 0f,
                left != null ? left.MinX : 0f,
                left != null ? left.MaxX : 0f,
                left != null ? left.MinZ : 0f,
                left != null ? left.MaxZ : 0f);
            GameplayOverlapAllowanceRules.Participant rightParticipant = BuildOverlapParticipant(
                right != null ? right.ObjectId : string.Empty,
                right != null ? right.ObjectId : string.Empty,
                right != null ? right.Role : string.Empty,
                right != null ? right.SourceEndpointTargetObjectId : string.Empty,
                right != null ? right.SourceEndpointSide : string.Empty,
                right != null ? right.SinkEndpointTargetObjectId : string.Empty,
                right != null ? right.SinkEndpointSide : string.Empty,
                right != null ? right.UnlockTargetReferenceIds : null,
                right != null ? right.SharedSlotReferenceIds : null,
                right != null ? right.OverlapAllowanceDescriptors : null,
                right != null ? right.CenterX : 0f,
                right != null ? right.CenterZ : 0f,
                right != null && right.HasResolvedYaw,
                right != null ? right.ResolvedYawDegrees : 0f,
                right != null ? right.MinX : 0f,
                right != null ? right.MaxX : 0f,
                right != null ? right.MinZ : 0f,
                right != null ? right.MaxZ : 0f);
            return GameplayOverlapAllowanceRules.IsAllowedOverlap(leftParticipant, rightParticipant, out overlapError);
        }

        private static GameplayOverlapAllowanceRules.Participant BuildOverlapParticipant(
            string referenceId,
            string sceneObjectId,
            string role,
            string sourceEndpointTargetObjectId,
            string sourceEndpointSide,
            string sinkEndpointTargetObjectId,
            string sinkEndpointSide,
            HashSet<string> unlockTargetReferenceIds,
            HashSet<string> sharedSlotReferenceIds,
            GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] overlapAllowanceDescriptors,
            float centerX,
            float centerZ,
            bool hasResolvedYaw,
            float resolvedYawDegrees,
            float minX,
            float maxX,
            float minZ,
            float maxZ)
        {
            var participant = new GameplayOverlapAllowanceRules.Participant
            {
                ReferenceId = Normalize(referenceId),
                SceneObjectId = Normalize(sceneObjectId),
                Role = Normalize(role),
                SourceEndpointTargetObjectId = Normalize(sourceEndpointTargetObjectId),
                SourceEndpointSide = Normalize(sourceEndpointSide),
                SinkEndpointTargetObjectId = Normalize(sinkEndpointTargetObjectId),
                SinkEndpointSide = Normalize(sinkEndpointSide),
                MinX = minX,
                MaxX = maxX,
                MinZ = minZ,
                MaxZ = maxZ,
            };

            if (unlockTargetReferenceIds != null)
                participant.UnlockTargetReferenceIds = new HashSet<string>(unlockTargetReferenceIds, System.StringComparer.Ordinal);
            if (sharedSlotReferenceIds != null)
                participant.SharedSlotReferenceIds = new HashSet<string>(sharedSlotReferenceIds, System.StringComparer.Ordinal);

            participant.OverlapAllowanceRects = GameplayOverlapAllowanceRules.BuildWorldOverlapAllowanceRects(
                overlapAllowanceDescriptors,
                centerX,
                centerZ,
                hasResolvedYaw ? resolvedYawDegrees : 0f);

            return participant;
        }

        private static GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] ResolvePlacementOverlapAllowanceDescriptors(
            PlayableObjectCatalog catalog,
            string role,
            string designId,
            PromptIntentObjectPlacementDefinition placement)
        {
            string normalizedRole = Normalize(role);
            if (string.Equals(normalizedRole, PromptIntentObjectRoles.PHYSICS_AREA, System.StringComparison.Ordinal))
            {
                PhysicsAreaLayoutDefinition physicsAreaLayout = placement != null ? placement.physicsAreaLayout : null;
                return GameplayOverlapAllowanceRules.BuildOverlapAllowanceDescriptors(
                    physicsAreaLayout != null ? physicsAreaLayout.overlapAllowances : null,
                    LAYOUT_SPACING);
            }

            if (catalog == null)
                return new GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[0];

            if (!TryResolveCatalogObjectId(catalog, role, out string gameplayObjectId, out _))
                return new GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[0];

            if (!catalog.TryResolveGameplayPlacementOverlapAllowanceDescriptors(gameplayObjectId, designId, out GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[] descriptors, out _))
                return new GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[0];

            return descriptors ?? new GameplayOverlapAllowanceRules.OverlapAllowanceDescriptor[0];
        }

        private static void ResolvePackedPlacementCellRange(
            PackedPlacementEntry entry,
            out int minCellX,
            out int maxCellX,
            out int minCellZ,
            out int maxCellZ)
        {
            minCellX = 0;
            maxCellX = 0;
            minCellZ = 0;
            maxCellZ = 0;
            if (entry == null)
                return;

            if (!entry.HasWorldPosition)
            {
                int safeWidth = entry.WidthCells < 1 ? 1 : entry.WidthCells;
                int safeDepth = entry.DepthCells < 1 ? 1 : entry.DepthCells;
                minCellX = entry.GridX;
                maxCellX = entry.GridX + safeWidth;
                minCellZ = entry.GridZ;
                maxCellZ = entry.GridZ + safeDepth;
                return;
            }

            minCellX = (int)System.Math.Floor(entry.MinX / LAYOUT_SPACING);
            maxCellX = (int)System.Math.Ceiling(entry.MaxX / LAYOUT_SPACING);
            minCellZ = (int)System.Math.Floor(entry.MinZ / LAYOUT_SPACING);
            maxCellZ = (int)System.Math.Ceiling(entry.MaxZ / LAYOUT_SPACING);

            if (maxCellX <= minCellX)
                maxCellX = minCellX + 1;
            if (maxCellZ <= minCellZ)
                maxCellZ = minCellZ + 1;
        }

        public static bool IsQuarterTurnOddYaw(float yaw)
        {
            float wrappedYaw = yaw % 360f;
            if (wrappedYaw < 0f)
                wrappedYaw += 360f;

            float snapped = (float)System.Math.Round(wrappedYaw / 90f, System.MidpointRounding.AwayFromZero) * 90f;
            if (System.Math.Abs(wrappedYaw - snapped) > 0.01f)
                return false;

            int quarterTurns = ((int)System.Math.Round(snapped / 90f, System.MidpointRounding.AwayFromZero)) % 4;
            if (quarterTurns < 0)
                quarterTurns += 4;

            return quarterTurns == 1 || quarterTurns == 3;
        }

        public static string BuildRuntimeSystemActionTargetId(string effectKind, string targetSpawnKey)
        {
            string normalizedEffectKind = Normalize(effectKind);
            if (!PromptIntentCapabilityRegistry.TryGetEffectSystemActionAuthoringId(normalizedEffectKind, out string systemActionId))
                return string.Empty;

            return SystemActionIds.BuildRuntimeTargetId(
                systemActionId,
                targetSpawnKey,
                PromptIntentCapabilityRegistry.GetEffectRuntimeEventKey(normalizedEffectKind));
        }

        public static FacilityAcceptedItemDefinition[] BuildFacilityAcceptedItems(
            PlayableScenarioModel model,
            Dictionary<string, string> spawnKeys,
            List<string> errors)
        {
            var itemsByFacilityId = new Dictionary<string, List<ItemRef>>(System.StringComparer.Ordinal);
            if (model == null)
                return new FacilityAcceptedItemDefinition[0];

            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                ScenarioModelStageDefinition stage = stages[stageIndex];
                ScenarioModelObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new ScenarioModelObjectiveDefinition[0] : new ScenarioModelObjectiveDefinition[0];
                for (int objectiveIndex = 0; objectiveIndex < objectives.Length; objectiveIndex++)
                {
                    ScenarioModelObjectiveDefinition objective = objectives[objectiveIndex];
                    if (objective == null)
                        continue;

                    string kind = Normalize(objective.kind);
                    if (string.Equals(kind, PromptIntentObjectiveKinds.SELL_ITEM, System.StringComparison.Ordinal))
                    {
                        RegisterFacilityItem(
                            itemsByFacilityId,
                            ResolveFacilityId(spawnKeys, objective.targetObjectId, errors, "sell_item.targetObjectId"),
                            objective.item,
                            "seller",
                            errors);
                        continue;
                    }

                    if (!string.Equals(kind, PromptIntentObjectiveKinds.CONVERT_ITEM, System.StringComparison.Ordinal))
                        continue;

                    ItemRef inputItem = objective.inputItem;
                    if (!ItemRefUtility.IsValid(inputItem))
                    {
                        if (errors != null)
                            errors.Add("stages[" + stageIndex + "].objectives[" + objectiveIndex + "]의 convert_item에는 inputItem이 필요합니다.");
                        continue;
                    }

                    RegisterFacilityItem(
                        itemsByFacilityId,
                        ResolveFacilityId(spawnKeys, objective.targetObjectId, errors, "convert_item.targetObjectId"),
                        inputItem,
                        "processor",
                        errors);
                }
            }

            int definitionCount = 0;
            foreach (KeyValuePair<string, List<ItemRef>> pair in itemsByFacilityId)
                definitionCount += pair.Value != null ? pair.Value.Count : 0;

            var definitions = new FacilityAcceptedItemDefinition[definitionCount];
            int writeIndex = 0;
            foreach (KeyValuePair<string, List<ItemRef>> pair in itemsByFacilityId)
            {
                List<ItemRef> items = pair.Value ?? new List<ItemRef>();
                for (int laneIndex = 0; laneIndex < items.Count; laneIndex++)
                {
                    definitions[writeIndex++] = new FacilityAcceptedItemDefinition
                    {
                        facilityId = pair.Key,
                        item = ItemRefUtility.Clone(items[laneIndex]),
                        laneIndex = laneIndex,
                    };
                }
            }

            return definitions;
        }

        public static FacilityOutputItemDefinition[] BuildFacilityOutputItems(
            PlayableScenarioModel model,
            Dictionary<string, string> spawnKeys,
            List<string> errors)
        {
            var outputItemByFacilityId = new Dictionary<string, ItemRef>(System.StringComparer.Ordinal);
            if (model == null)
                return new FacilityOutputItemDefinition[0];

            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                ScenarioModelStageDefinition stage = stages[stageIndex];
                ScenarioModelObjectiveDefinition[] objectives = stage != null ? stage.objectives ?? new ScenarioModelObjectiveDefinition[0] : new ScenarioModelObjectiveDefinition[0];
                for (int objectiveIndex = 0; objectiveIndex < objectives.Length; objectiveIndex++)
                {
                    ScenarioModelObjectiveDefinition objective = objectives[objectiveIndex];
                    if (objective == null)
                        continue;

                    string kind = Normalize(objective.kind);
                    if (!string.Equals(kind, PromptIntentObjectiveKinds.CONVERT_ITEM, System.StringComparison.Ordinal))
                        continue;

                    ItemRef outputItem = objective.item;
                    if (!ItemRefUtility.IsValid(outputItem))
                    {
                        if (errors != null)
                            errors.Add("stages[" + stageIndex + "].objectives[" + objectiveIndex + "]의 convert_item에는 output item이 필요합니다.");
                        continue;
                    }

                    string facilityId = ResolveFacilityId(spawnKeys, objective.targetObjectId, errors, "convert_item.targetObjectId");
                    if (string.IsNullOrEmpty(facilityId))
                        continue;

                    if (outputItemByFacilityId.TryGetValue(facilityId, out ItemRef existingOutputItem))
                    {
                        if (!ItemRefUtility.Equals(existingOutputItem, outputItem) && errors != null)
                        {
                            errors.Add(
                                "convert_item.targetObjectId '" + facilityId + "'에는 단일 output item만 허용됩니다: '" +
                                ItemRefUtility.ToStableKey(existingOutputItem) + "' vs '" + ItemRefUtility.ToStableKey(outputItem) + "'.");
                        }

                        continue;
                    }

                    outputItemByFacilityId.Add(facilityId, ItemRefUtility.Clone(outputItem));
                }
            }

            var definitions = new FacilityOutputItemDefinition[outputItemByFacilityId.Count];
            int writeIndex = 0;
            foreach (KeyValuePair<string, ItemRef> pair in outputItemByFacilityId)
            {
                definitions[writeIndex++] = new FacilityOutputItemDefinition
                {
                    facilityId = pair.Key,
                    item = ItemRefUtility.Clone(pair.Value),
                };
            }

            return definitions;
        }

        private static void RegisterFacilityItem(
            Dictionary<string, List<ItemRef>> itemsByFacilityId,
            string facilityId,
            ItemRef item,
            string roleLabel,
            List<string> errors)
        {
            string normalizedFacilityId = Normalize(facilityId);
            string itemKey = ItemRefUtility.ToStableKey(item);
            if (string.IsNullOrEmpty(normalizedFacilityId) || string.IsNullOrEmpty(itemKey))
                return;

            if (!itemsByFacilityId.TryGetValue(normalizedFacilityId, out List<ItemRef> items))
            {
                items = new List<ItemRef>();
                itemsByFacilityId.Add(normalizedFacilityId, items);
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (ItemRefUtility.Equals(items[i], item))
                    return;
            }

            if (string.Equals(roleLabel, "processor", System.StringComparison.Ordinal) &&
                items.Count > 0)
            {
                if (errors != null)
                {
                    errors.Add(
                        roleLabel + " '" + normalizedFacilityId + "'에 서로 다른 item('" +
                        ItemRefUtility.ToDisplayString(items[0]) + "', '" + itemKey + "')가 매핑되어 있습니다.");
                }
                return;
            }

            items.Add(ItemRefUtility.Clone(item));
        }

        private static string ResolveFacilityId(
            Dictionary<string, string> spawnKeys,
            string targetObjectId,
            List<string> errors,
            string label)
        {
            string normalizedTargetObjectId = Normalize(targetObjectId);
            if (string.IsNullOrEmpty(normalizedTargetObjectId))
                return string.Empty;

            if (spawnKeys != null && spawnKeys.TryGetValue(normalizedTargetObjectId, out string spawnKey))
                return spawnKey;

            if (errors != null)
                errors.Add(label + "에서 알 수 없는 object id '" + normalizedTargetObjectId + "'를 참조하고 있습니다.");
            return string.Empty;
        }

    }
}
