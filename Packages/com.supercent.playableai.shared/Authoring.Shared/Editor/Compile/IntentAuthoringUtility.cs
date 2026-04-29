using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Generation.Editor.Compile
{
#pragma warning disable 0649
    public static class IntentAuthoringUtility
    {
        private sealed class GameplayResolutionGuidance
        {
            public bool IsItemKey;
            public string SuggestedObjectId = string.Empty;
            public string SuggestedDesignId = string.Empty;
            public string[] AvailableObjectIds = new string[0];
            public string[] AvailableDesignIds = new string[0];
        }

        private sealed class PackedPlacementEntry
        {
            public string ObjectId = string.Empty;
            public string Role = string.Empty;
            public string SourceEndpointTargetObjectId = string.Empty;
            public string SourceEndpointSide = string.Empty;
            public string SinkEndpointTargetObjectId = string.Empty;
            public string SinkEndpointSide = string.Empty;
            public HashSet<string> UnlockTargetReferenceIds = new HashSet<string>(System.StringComparer.Ordinal);
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
            public float CenterX;
            public float CenterZ;
            public bool HasResolvedYaw;
            public float ResolvedYawDegrees;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

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
            return global::PlayableAI.AuthoringCore.CatalogRoleUtility.TryResolveCatalogObjectIdForRole(catalog, role, out objectId, out error);
        }

        public static bool IsCatalogBackedRole(string role)
        {
            return !string.IsNullOrEmpty(Normalize(role));
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
            string requestedDesignId = normalizedDesignId;
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
                    errors.Add(BuildGameplayDesignResolutionGuidance(
                        catalog,
                        normalizedObjectId,
                        requestedDesignId,
                        label));
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
            return BuildDeterministicPositions(objects, null, catalog, errors);
        }

        private static string BuildGameplayDesignResolutionGuidance(
            PlayableObjectCatalog catalog,
            string normalizedObjectId,
            string requestedDesignId,
            string label)
        {
            string missingDesignLabel = string.IsNullOrEmpty(requestedDesignId) ? "(empty)" : requestedDesignId;
            GameplayResolutionGuidance guidance = BuildGameplayResolutionGuidance(catalog, normalizedObjectId, requestedDesignId);
            return label + "에서 objectId '" + normalizedObjectId + "'의 designId '" + missingDesignLabel +
                   "'를 catalog design으로 해석하지 못했습니다." + BuildGameplayResolutionCandidateBlock(guidance);
        }

        private static string TryGetAvailableDesignIds(PlayableObjectCatalog catalog, string objectId)
        {
            return JoinCandidateValues(TryGetAvailableDesignIdValues(catalog, objectId));
        }

        private static GameplayResolutionGuidance BuildGameplayResolutionGuidance(
            PlayableObjectCatalog catalog,
            string normalizedObjectId,
            string requestedDesignId)
        {
            var guidance = new GameplayResolutionGuidance();
            guidance.AvailableDesignIds = TryGetAvailableDesignIdValues(catalog, normalizedObjectId);

            if (ItemRefUtility.TryParseItemKey(normalizedObjectId, out ItemRef itemRef) && itemRef != null)
            {
                guidance.IsItemKey = true;
                guidance.SuggestedObjectId = Normalize(itemRef.familyId);
                guidance.SuggestedDesignId = Normalize(itemRef.variantId);
                guidance.AvailableObjectIds = BuildGameplayObjectIdCandidates(catalog, guidance.SuggestedObjectId);
                if (guidance.AvailableObjectIds.Length == 0 && !string.IsNullOrEmpty(guidance.SuggestedObjectId))
                    guidance.AvailableObjectIds = new[] { guidance.SuggestedObjectId };

                string guidanceObjectId = string.IsNullOrEmpty(guidance.SuggestedObjectId)
                    ? normalizedObjectId
                    : guidance.SuggestedObjectId;
                guidance.AvailableDesignIds = TryGetAvailableDesignIdValues(catalog, guidanceObjectId);
                if (string.IsNullOrEmpty(guidance.SuggestedDesignId) && guidance.AvailableDesignIds.Length > 0)
                    guidance.SuggestedDesignId = guidance.AvailableDesignIds[0];
                return guidance;
            }

            guidance.AvailableObjectIds = BuildGameplayObjectIdCandidates(catalog, normalizedObjectId);
            if (guidance.AvailableDesignIds.Length > 0 && string.IsNullOrEmpty(guidance.SuggestedDesignId))
            {
                guidance.SuggestedDesignId = SelectSuggestedDesignId(requestedDesignId, guidance.AvailableDesignIds);
            }

            return guidance;
        }

        private static string BuildGameplayResolutionCandidateBlock(GameplayResolutionGuidance guidance)
        {
            if (guidance == null)
                return " -> 수정 가이드: catalog의 objectId와 designId를 확인해서 수정하세요.";

            var segments = new List<string>();
            if (guidance.IsItemKey)
                segments.Add("이 값은 item entry id입니다");

            string recommendedChange = BuildRecommendedChange(guidance);
            if (!string.IsNullOrEmpty(recommendedChange))
                segments.Add("추천 수정: " + recommendedChange);

            if (guidance.AvailableObjectIds != null && guidance.AvailableObjectIds.Length > 0)
                segments.Add("사용 가능한 objectId: [" + JoinCandidateValues(guidance.AvailableObjectIds) + "]");

            if (guidance.AvailableDesignIds != null && guidance.AvailableDesignIds.Length > 0)
                segments.Add("사용 가능한 designId: [" + JoinCandidateValues(guidance.AvailableDesignIds) + "]");

            if (segments.Count == 0)
                return " -> 수정 가이드: catalog의 objectId와 designId를 확인해서 수정하세요.";

            return " -> 수정 가이드: " + string.Join("; ", segments) + ".";
        }

        private static string BuildRecommendedChange(GameplayResolutionGuidance guidance)
        {
            if (guidance == null)
                return string.Empty;

            var components = new List<string>();
            if (!string.IsNullOrEmpty(guidance.SuggestedObjectId))
                components.Add("objectId '" + guidance.SuggestedObjectId + "'");
            if (!string.IsNullOrEmpty(guidance.SuggestedDesignId))
                components.Add("designId '" + guidance.SuggestedDesignId + "'");

            if (components.Count > 0)
                return string.Join(", ", components);

            if (guidance.AvailableDesignIds != null && guidance.AvailableDesignIds.Length > 0)
                return "designId '" + guidance.AvailableDesignIds[0] + "'";

            if (guidance.AvailableObjectIds != null && guidance.AvailableObjectIds.Length > 0)
                return "objectId '" + guidance.AvailableObjectIds[0] + "'";

            return string.Empty;
        }

        private static string[] BuildGameplayObjectIdCandidates(PlayableObjectCatalog catalog, string requestedObjectId)
        {
            string normalizedRequestedObjectId = Normalize(requestedObjectId);
            if (catalog == null || string.IsNullOrEmpty(normalizedRequestedObjectId))
                return new string[0];

            var exactMatches = new HashSet<string>(StringComparer.Ordinal);
            var fuzzyMatches = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<GameplayCatalogEntry> entries = catalog.GetGameplayEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                GameplayCatalogEntry entry = entries[i];
                string candidateObjectId = entry != null ? Normalize(entry.objectId) : string.Empty;
                if (string.IsNullOrEmpty(candidateObjectId))
                    continue;

                if (string.Equals(candidateObjectId, normalizedRequestedObjectId, StringComparison.Ordinal))
                {
                    exactMatches.Add(candidateObjectId);
                    continue;
                }

                if (candidateObjectId.IndexOf(normalizedRequestedObjectId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalizedRequestedObjectId.IndexOf(candidateObjectId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fuzzyMatches.Add(candidateObjectId);
                }
            }

            if (exactMatches.Count > 0)
                return exactMatches.OrderBy(value => value, StringComparer.Ordinal).ToArray();

            return fuzzyMatches
                .OrderBy(value => value, StringComparer.Ordinal)
                .Take(5)
                .ToArray();
        }

        private static string[] TryGetAvailableDesignIdValues(PlayableObjectCatalog catalog, string objectId)
        {
            if (catalog == null || !catalog.TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry) || entry == null)
                return new string[0];

            return (entry.designs ?? new DesignVariantEntry[0])
                .Where(value => value != null && !string.IsNullOrWhiteSpace(Normalize(value.designId)))
                .Select(value => Normalize(value.designId))
                .Distinct(System.StringComparer.Ordinal)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
        }

        private static string JoinCandidateValues(IEnumerable<string> values)
        {
            if (values == null)
                return string.Empty;

            return string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string SelectSuggestedDesignId(string requestedDesignId, string[] availableDesignIds)
        {
            string normalizedRequestedDesignId = Normalize(requestedDesignId);
            if (availableDesignIds == null || availableDesignIds.Length == 0)
                return string.Empty;

            if (!string.IsNullOrEmpty(normalizedRequestedDesignId))
            {
                for (int i = 0; i < availableDesignIds.Length; i++)
                {
                    string availableDesignId = Normalize(availableDesignIds[i]);
                    if (string.IsNullOrEmpty(availableDesignId))
                        continue;

                    if (availableDesignId.IndexOf(normalizedRequestedDesignId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        normalizedRequestedDesignId.IndexOf(availableDesignId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return availableDesignId;
                    }
                }
            }

            return availableDesignIds[0];
        }

        public static Dictionary<string, SerializableVector3> BuildDeterministicPositions(
            ScenarioModelObjectDefinition[] objects,
            ScenarioModelStageDefinition[] stages,
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            var positions = new Dictionary<string, SerializableVector3>(System.StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            Dictionary<string, HashSet<string>> unlockTargetLookup = GameplayOverlapAllowanceRules.BuildUnlockTargetReferenceLookup(stages);
            List<PackedPlacementEntry> packedPlacements = BuildPackedPlacements(safeObjects, unlockTargetLookup, catalog, errors);
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

            int nextUnlockGridX = 0;
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
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            _ = objects;
            _ = unlockTargetLookup;
            _ = catalog;
            _ = errors;
            return new List<PackedPlacementEntry>();
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

            if (!TryResolvePlacementFootprint(catalog, value.role, value.designId, null, out widthCells, out depthCells, out string error))
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

            if (!TryResolveCatalogObjectId(catalog, role, out string gameplayObjectId, out error))
                return false;

            if (catalog == null)
            {
                error = "PlayableObjectCatalog가 필요합니다.";
                return false;
            }

            if (TryResolveDescriptorPlacementFootprint(
                    catalog,
                    gameplayObjectId,
                    placement,
                    out widthCells,
                    out depthCells,
                    out centerOffsetX,
                    out centerOffsetZ,
                    out string descriptorFootprintError))
            {
                return true;
            }

            bool resolvedFromCatalog = catalog.TryResolveGameplayPlacementFootprintFromCatalogMetadata(
                gameplayObjectId,
                designId,
                out widthCells,
                out depthCells,
                out centerOffsetX,
                out centerOffsetZ,
                out error);
            if (resolvedFromCatalog)
                return true;

            if (!string.IsNullOrEmpty(descriptorFootprintError))
                error = descriptorFootprintError;

            return false;
        }

        private static bool TryResolveDescriptorPlacementFootprint(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
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

            if (catalog == null ||
                string.IsNullOrWhiteSpace(gameplayObjectId) ||
                !TryResolveFeatureDescriptorForGameplayObjectId(catalog, gameplayObjectId, out FeatureDescriptor descriptor) ||
                descriptor.layoutRequirements == null ||
                descriptor.layoutRequirements.catalogBacked)
            {
                return false;
            }

            if (placement != null &&
                placement.hasImageBounds &&
                placement.bboxWidthPx > 0f &&
                placement.bboxHeightPx > 0f)
            {
                widthCells = Math.Max(1, (int)Math.Ceiling(placement.bboxWidthPx));
                depthCells = Math.Max(1, (int)Math.Ceiling(placement.bboxHeightPx));
                return true;
            }

            if (TryResolveFeatureLayoutFootprint(placement != null ? placement.featureLayout : null, out widthCells, out depthCells))
                return true;

            error = "descriptor-owned feature '" + gameplayObjectId + "'는 catalog design footprint를 사용하지 않습니다. featureLayout에 worldWidth/worldDepth 또는 widthCells/depthCells가 필요합니다.";
            return false;
        }

        private static bool TryResolveFeatureDescriptorForGameplayObjectId(
            PlayableObjectCatalog catalog,
            string gameplayObjectId,
            out FeatureDescriptor descriptor)
        {
            descriptor = null;
            FeatureDescriptor[] descriptors = catalog != null ? catalog.FeatureDescriptors ?? Array.Empty<FeatureDescriptor>() : Array.Empty<FeatureDescriptor>();
            string normalizedGameplayObjectId = PlayableFeatureTypeIds.Normalize(gameplayObjectId);
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                FeatureDescriptor value = descriptors[descriptorIndex];
                if (value == null)
                    continue;

                if (string.Equals(PlayableFeatureTypeIds.Normalize(value.featureType), normalizedGameplayObjectId, StringComparison.Ordinal))
                {
                    descriptor = value;
                    return true;
                }

                FeatureCompiledGameplayRoleDescriptor[] mappings =
                    value.compiledGameplayRoleMappings ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
                for (int mappingIndex = 0; mappingIndex < mappings.Length; mappingIndex++)
                {
                    FeatureCompiledGameplayRoleDescriptor mapping = mappings[mappingIndex];
                    if (mapping != null &&
                        string.Equals(PlayableFeatureTypeIds.Normalize(mapping.gameplayObjectId), normalizedGameplayObjectId, StringComparison.Ordinal))
                    {
                        descriptor = value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveFeatureLayoutFootprint(FeatureJsonPayload featureLayout, out int widthCells, out int depthCells)
        {
            widthCells = 0;
            depthCells = 0;
            string json = featureLayout != null ? featureLayout.json : string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            if (TryReadMaxJsonNumber(json, "widthCells", out float widthByCells) &&
                TryReadMaxJsonNumber(json, "depthCells", out float depthByCells))
            {
                widthCells = Math.Max(1, (int)Math.Ceiling(widthByCells));
                depthCells = Math.Max(1, (int)Math.Ceiling(depthByCells));
                return true;
            }

            if (TryReadMaxJsonNumber(json, "worldWidth", out float widthByWorld) &&
                TryReadMaxJsonNumber(json, "worldDepth", out float depthByWorld))
            {
                widthCells = Math.Max(1, (int)Math.Ceiling(widthByWorld));
                depthCells = Math.Max(1, (int)Math.Ceiling(depthByWorld));
                return true;
            }

            if (TryReadMaxJsonNumber(json, "bboxWidthPx", out float widthByBounds) &&
                TryReadMaxJsonNumber(json, "bboxHeightPx", out float depthByBounds))
            {
                widthCells = Math.Max(1, (int)Math.Ceiling(widthByBounds));
                depthCells = Math.Max(1, (int)Math.Ceiling(depthByBounds));
                return true;
            }

            return false;
        }

        private static bool TryReadMaxJsonNumber(string json, string propertyName, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
                return false;

            string pattern = "\\\"" + Regex.Escape(propertyName) + "\\\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)";
            MatchCollection matches = Regex.Matches(json, pattern);
            bool found = false;
            for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
            {
                if (!float.TryParse(matches[matchIndex].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ||
                    parsed <= 0f)
                {
                    continue;
                }

                value = found ? Math.Max(value, parsed) : parsed;
                found = true;
            }

            return found;
        }

        public static void ValidatePlacementGrid(
            PromptIntentObjectDefinition[] objects,
            PlayableObjectCatalog catalog,
            List<string> errors)
        {
            _ = objects;
            _ = catalog;
            _ = errors;
        }

        private static bool ShouldSwapPlacementFootprintAxesForValidation(PromptIntentObjectDefinition value)
        {
            return ShouldSwapPlacementFootprintAxesForValidation(
                value != null ? value.role : string.Empty,
                null);
        }

        private static bool ShouldSwapPlacementFootprintAxesForValidation(
            string roleValue,
            PromptIntentObjectPlacementDefinition placement)
        {
            if (placement == null)
                return false;

            string role = Normalize(roleValue);
            if (placement.hasResolvedYaw)
                return IsQuarterTurnOddYaw(placement.resolvedYawDegrees);
            return false;
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

        public static FeatureAcceptedItemDefinition[] BuildFeatureAcceptedItems(
            PlayableScenarioModel model,
            Dictionary<string, string> spawnKeys,
            List<string> errors)
        {
            var itemsByTargetId = new Dictionary<string, List<ItemRef>>(System.StringComparer.Ordinal);
            if (model == null)
                return new FeatureAcceptedItemDefinition[0];

            Dictionary<string, string> featureTypeByObjectId = BuildFeatureTypeByObjectId(model);
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
                    bool usesItem = PromptIntentContractRegistry.ObjectiveRequiresItem(kind);
                    bool usesInputItem = PromptIntentContractRegistry.ObjectiveRequiresInputItem(kind);
                    if (!usesItem && !usesInputItem)
                        continue;

                    string featureType = ResolveObjectiveFeatureType(featureTypeByObjectId, objective.targetObjectId);
                    if (!usesInputItem &&
                        PromptIntentContractRegistry.ObjectiveDefinesFeatureOutputItem(featureType, kind))
                    {
                        continue;
                    }

                    ItemRef acceptedItem = usesInputItem ? objective.inputItem : objective.item;
                    if (!ItemRefUtility.IsValid(acceptedItem))
                    {
                        if (errors != null)
                            errors.Add("stages[" + stageIndex + "].objectives[" + objectiveIndex + "]의 " + kind + "에는 " + (usesInputItem ? "inputItem" : "item") + "이 필요합니다.");
                        continue;
                    }

                    RegisterFeatureItem(
                        itemsByTargetId,
                        ResolveTargetId(spawnKeys, objective.targetObjectId, errors, kind + ".targetObjectId"),
                        acceptedItem);
                }
            }

            int definitionCount = 0;
            foreach (KeyValuePair<string, List<ItemRef>> pair in itemsByTargetId)
                definitionCount += pair.Value != null ? pair.Value.Count : 0;

            var definitions = new FeatureAcceptedItemDefinition[definitionCount];
            int writeIndex = 0;
            foreach (KeyValuePair<string, List<ItemRef>> pair in itemsByTargetId)
            {
                List<ItemRef> items = pair.Value ?? new List<ItemRef>();
                for (int laneIndex = 0; laneIndex < items.Count; laneIndex++)
                {
                    definitions[writeIndex++] = new FeatureAcceptedItemDefinition
                    {
                        targetId = pair.Key,
                        item = ItemRefUtility.Clone(items[laneIndex]),
                        laneIndex = laneIndex,
                    };
                }
            }

            return definitions;
        }

        public static FeatureOutputItemDefinition[] BuildFeatureOutputItems(
            PlayableScenarioModel model,
            Dictionary<string, string> spawnKeys,
            List<string> errors)
        {
            var itemsByTargetId = new Dictionary<string, ItemRef>(StringComparer.Ordinal);
            var targetOrder = new List<string>();
            Dictionary<string, string> featureTypeByObjectId = BuildFeatureTypeByObjectId(model);
            ScenarioModelStageDefinition[] stages = model != null ? model.stages ?? new ScenarioModelStageDefinition[0] : new ScenarioModelStageDefinition[0];
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
                    string featureType = ResolveObjectiveFeatureType(featureTypeByObjectId, objective.targetObjectId);
                    if (!PromptIntentContractRegistry.ObjectiveDefinesFeatureOutputItem(featureType, kind))
                        continue;

                    if (!ItemRefUtility.IsValid(objective.item))
                    {
                        if (errors != null)
                            errors.Add("stages[" + stageIndex + "].objectives[" + objectiveIndex + "]의 " + kind + "에는 output item으로 사용할 item이 필요합니다.");
                        continue;
                    }

                    string targetId = ResolveTargetId(spawnKeys, objective.targetObjectId, errors, kind + ".targetObjectId");
                    if (string.IsNullOrEmpty(targetId))
                        continue;

                    ItemRef outputItem = ItemRefUtility.Clone(objective.item);
                    if (itemsByTargetId.TryGetValue(targetId, out ItemRef existing))
                    {
                        if (!ItemRefUtility.Equals(existing, outputItem) && errors != null)
                        {
                            errors.Add(
                                "feature '" + targetId + "'에 서로 다른 output item objective가 선언되었습니다: '" +
                                ItemRefUtility.ToItemKey(existing) + "' vs '" + ItemRefUtility.ToItemKey(outputItem) + "'.");
                        }
                        continue;
                    }

                    itemsByTargetId.Add(targetId, outputItem);
                    targetOrder.Add(targetId);
                }
            }

            var definitions = new FeatureOutputItemDefinition[targetOrder.Count];
            for (int i = 0; i < targetOrder.Count; i++)
            {
                string targetId = targetOrder[i];
                definitions[i] = new FeatureOutputItemDefinition
                {
                    targetId = targetId,
                    item = ItemRefUtility.Clone(itemsByTargetId[targetId]),
                };
            }

            return definitions;
        }

        private static Dictionary<string, string> BuildFeatureTypeByObjectId(PlayableScenarioModel model)
        {
            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] objects = model != null ? model.objects ?? new ScenarioModelObjectDefinition[0] : new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < objects.Length; i++)
            {
                ScenarioModelObjectDefinition obj = objects[i];
                string objectId = Normalize(obj != null ? obj.id : string.Empty);
                if (string.IsNullOrEmpty(objectId))
                    continue;

                string featureType = Normalize(obj != null ? obj.featureOptions.featureType : string.Empty);
                if (string.IsNullOrEmpty(featureType))
                    featureType = PromptIntentContractRegistry.ResolveFeatureTypeForRole(obj != null ? obj.role : string.Empty);

                if (!string.IsNullOrEmpty(featureType))
                    lookup[objectId] = featureType;
            }

            return lookup;
        }

        private static string ResolveObjectiveFeatureType(Dictionary<string, string> featureTypeByObjectId, string targetObjectId)
        {
            string normalizedTargetObjectId = Normalize(targetObjectId);
            if (string.IsNullOrEmpty(normalizedTargetObjectId) || featureTypeByObjectId == null)
                return string.Empty;

            return featureTypeByObjectId.TryGetValue(normalizedTargetObjectId, out string featureType)
                ? Normalize(featureType)
                : string.Empty;
        }

        private static void RegisterFeatureItem(
            Dictionary<string, List<ItemRef>> itemsByTargetId,
            string targetId,
            ItemRef item)
        {
            string normalizedTargetId = Normalize(targetId);
            string itemKey = ItemRefUtility.ToItemKey(item);
            if (string.IsNullOrEmpty(normalizedTargetId) || string.IsNullOrEmpty(itemKey))
                return;

            if (!itemsByTargetId.TryGetValue(normalizedTargetId, out List<ItemRef> items))
            {
                items = new List<ItemRef>();
                itemsByTargetId.Add(normalizedTargetId, items);
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (ItemRefUtility.Equals(items[i], item))
                    return;
            }

            items.Add(ItemRefUtility.Clone(item));
        }

        private static string ResolveTargetId(
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
