using System;
using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace PlayableAI.AuthoringCore
{
    public static class DraftLayoutAuthoringUtility
    {
        public static LayoutSpecDocument BuildLayoutSpecFromDraftLayout(
            DraftLayoutDocument draftLayout,
            PlayablePromptIntent intent,
            PlayableObjectCatalog catalog)
        {
            var placements = new List<LayoutSpecPlacementEntry>();
            DraftLayoutPlacementEntry[] draftPlacements = draftLayout != null ? draftLayout.placements ?? Array.Empty<DraftLayoutPlacementEntry>() : Array.Empty<DraftLayoutPlacementEntry>();
            for (int i = 0; i < draftPlacements.Length; i++)
            {
                DraftLayoutPlacementEntry entry = draftPlacements[i];
                if (entry == null)
                    continue;

                placements.Add(new LayoutSpecPlacementEntry
                {
                    objectId = Normalize(entry.objectId),
                    laneId = Normalize(entry.laneId),
                    hasLaneOrder = entry.laneOrder.HasValue,
                    laneOrder = entry.laneOrder ?? 0,
                    sharedSlotId = Normalize(entry.sharedSlotId),
                    hasMinGapToNextCells = entry.minGapToNextCells.HasValue,
                    minGapToNextCells = entry.minGapToNextCells ?? 0f,
                    hasWorldPosition = true,
                    worldX = entry.worldX,
                    worldZ = entry.worldZ,
                    hasResolvedYaw = entry.hasResolvedYaw,
                    resolvedYawDegrees = entry.resolvedYawDegrees,
                    solverPlacementSource = "draft_layout",
                    orientationReason = entry.hasResolvedYaw ? "draft_layout" : string.Empty,
                    physicsAreaLayout = TranslatePhysicsAreaLayout(entry),
                    railLayout = TranslateRailLayout(entry),
                });
            }

            DraftLayoutEnvironmentEntry[] draftEnvironment = draftLayout != null ? draftLayout.environment ?? Array.Empty<DraftLayoutEnvironmentEntry>() : Array.Empty<DraftLayoutEnvironmentEntry>();
            var environment = new LayoutSpecEnvironmentEntry[draftEnvironment.Length];
            for (int i = 0; i < draftEnvironment.Length; i++)
            {
                DraftLayoutEnvironmentEntry entry = draftEnvironment[i] ?? new DraftLayoutEnvironmentEntry();
                environment[i] = new LayoutSpecEnvironmentEntry
                {
                    objectId = Normalize(entry.objectId),
                    designId = Normalize(entry.designId),
                    kind = string.Empty,
                    widthCells = entry.widthCells,
                    depthCells = entry.depthCells,
                    hasWorldBounds = entry.hasWorldBounds,
                    worldX = entry.worldX,
                    worldZ = entry.worldZ,
                    worldWidth = entry.worldWidth,
                    worldDepth = entry.worldDepth,
                    rotationY = entry.rotationY,
                    includeInBounds = entry.includeInBounds,
                    singleLayer = entry.singleLayer,
                    isOuterBoundary = entry.isOuterBoundary,
                };
            }

            DraftLayoutCustomerPathEntry[] draftCustomerPaths = draftLayout != null ? draftLayout.customerPaths ?? Array.Empty<DraftLayoutCustomerPathEntry>() : Array.Empty<DraftLayoutCustomerPathEntry>();
            var customerPaths = new LayoutSpecCustomerPathEntry[draftCustomerPaths.Length];
            for (int i = 0; i < draftCustomerPaths.Length; i++)
            {
                DraftLayoutCustomerPathEntry entry = draftCustomerPaths[i] ?? new DraftLayoutCustomerPathEntry();
                customerPaths[i] = new LayoutSpecCustomerPathEntry
                {
                    targetId = CanonicalizeCustomerPathTargetId(entry.targetId),
                    spawnPoint = TranslateCustomerPathPoint(entry.spawnPoint),
                    leavePoint = TranslateCustomerPathPoint(entry.leavePoint),
                    queuePoints = TranslateCustomerPathPoints(entry.queuePoints),
                    entryWaypoints = TranslateCustomerPathPoints(entry.entryWaypoints),
                    exitWaypoints = TranslateCustomerPathPoints(entry.exitWaypoints),
                };
            }

            return new LayoutSpecDocument
            {
                floorBounds = new LayoutSpecFloorBounds
                {
                    hasWorldBounds = draftLayout != null && draftLayout.floorBounds != null && draftLayout.floorBounds.hasWorldBounds,
                    worldX = draftLayout != null && draftLayout.floorBounds != null ? draftLayout.floorBounds.worldX : 0f,
                    worldZ = draftLayout != null && draftLayout.floorBounds != null ? draftLayout.floorBounds.worldZ : 0f,
                    worldWidth = draftLayout != null && draftLayout.floorBounds != null ? draftLayout.floorBounds.worldWidth : 0f,
                    worldDepth = draftLayout != null && draftLayout.floorBounds != null ? draftLayout.floorBounds.worldDepth : 0f,
                },
                placements = placements.ToArray(),
                playerStart = BuildLayoutSpecPlayerStartFromDraftLayout(draftLayout, intent, catalog),
                environment = environment,
                customerPaths = customerPaths,
                sourceImages = Array.Empty<LayoutSpecSourceImageEntry>(),
            };
        }

        public static PlayablePromptIntent ApplyDraftLayoutToIntent(PlayablePromptIntent sourceIntent, DraftLayoutDocument draftLayout)
        {
            PlayablePromptIntent intent = CloneIntent(sourceIntent);
            intent.objects ??= Array.Empty<PromptIntentObjectDefinition>();

            var placementLookup = new Dictionary<string, DraftLayoutPlacementEntry>(StringComparer.Ordinal);
            var roleLookup = new Dictionary<string, string>(StringComparer.Ordinal);
            DraftLayoutPlacementEntry[] draftPlacements = draftLayout != null ? draftLayout.placements ?? Array.Empty<DraftLayoutPlacementEntry>() : Array.Empty<DraftLayoutPlacementEntry>();
            for (int i = 0; i < draftPlacements.Length; i++)
            {
                DraftLayoutPlacementEntry entry = draftPlacements[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.objectId))
                    continue;

                placementLookup[Normalize(entry.objectId)] = entry;
            }

            for (int i = 0; i < intent.objects.Length; i++)
            {
                PromptIntentObjectDefinition value = intent.objects[i];
                if (value == null || string.IsNullOrWhiteSpace(value.id))
                    continue;

                roleLookup[Normalize(value.id)] = Normalize(value.role);
            }

            for (int i = 0; i < intent.objects.Length; i++)
            {
                PromptIntentObjectDefinition value = intent.objects[i];
                if (value == null)
                    continue;

                string objectId = Normalize(value.id);
                value.scenarioOptions = CreateSanitizedScenarioOptions(value.role, value.scenarioOptions);
                value.placement ??= new PromptIntentObjectPlacementDefinition();

                if (string.Equals(Normalize(value.role), PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
                {
                    DraftLayoutPlayerStartEntry playerStart = draftLayout != null ? draftLayout.playerStart ?? new DraftLayoutPlayerStartEntry() : new DraftLayoutPlayerStartEntry();
                    if (string.Equals(objectId, Normalize(playerStart.objectId), StringComparison.Ordinal))
                    {
                        value.placement.hasWorldPosition = true;
                        value.placement.worldX = playerStart.worldX;
                        value.placement.worldZ = playerStart.worldZ;
                        value.placement.hasResolvedYaw = playerStart.hasResolvedYaw;
                        value.placement.resolvedYawDegrees = playerStart.resolvedYawDegrees;
                        value.placement.solverPlacementSource = "draft_layout";
                        value.placement.orientationReason = playerStart.hasResolvedYaw ? "draft_layout" : string.Empty;
                        value.placement.hasImageBounds = false;
                        value.placement.centerPxX = 0f;
                        value.placement.centerPxY = 0f;
                        value.placement.bboxWidthPx = 0f;
                        value.placement.bboxHeightPx = 0f;
                        value.placement.bboxConfidence = 0f;
                    }

                    continue;
                }

                if (!placementLookup.TryGetValue(objectId, out DraftLayoutPlacementEntry placement))
                    continue;

                value.placement.hasWorldPosition = true;
                value.placement.worldX = placement.worldX;
                value.placement.worldZ = placement.worldZ;
                value.placement.hasResolvedYaw = placement.hasResolvedYaw;
                value.placement.resolvedYawDegrees = placement.resolvedYawDegrees;
                value.placement.solverPlacementSource = "draft_layout";
                value.placement.orientationReason = placement.hasResolvedYaw ? "draft_layout" : string.Empty;
                value.placement.anchorDeltaCellsX = 0f;
                value.placement.anchorDeltaCellsZ = 0f;
                value.placement.hasImageBounds = false;
                value.placement.centerPxX = 0f;
                value.placement.centerPxY = 0f;
                value.placement.bboxWidthPx = 0f;
                value.placement.bboxHeightPx = 0f;
                value.placement.bboxConfidence = 0f;

                string normalizedRole = Normalize(value.role);
                value.placement.physicsAreaLayout = string.Equals(normalizedRole, PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal)
                    ? new PhysicsAreaLayoutDefinition
                    {
                        realPhysicsZoneBounds = TranslateWorldBounds(placement.physicsAreaLayout != null ? placement.physicsAreaLayout.realPhysicsZoneBounds : null),
                        fakeSpriteZoneBounds = TranslateWorldBounds(placement.physicsAreaLayout != null ? placement.physicsAreaLayout.fakeSpriteZoneBounds : null),
                        overlapAllowances = Array.Empty<PlacementOverlapAllowanceDefinition>(),
                    }
                    : null;
                value.placement.railLayout = string.Equals(normalizedRole, PromptIntentObjectRoles.RAIL, StringComparison.Ordinal)
                    ? new RailLayoutDefinition
                    {
                        pathCells = CloneRailPathAnchors(placement.railLayout != null ? placement.railLayout.pathCells : null),
                    }
                    : null;
            }

            return intent;
        }

        private static PlayablePromptIntent CloneIntent(PlayablePromptIntent sourceIntent)
        {
            string json = JsonUtility.ToJson(sourceIntent ?? new PlayablePromptIntent());
            return JsonUtility.FromJson<PlayablePromptIntent>(json);
        }

        private static LayoutSpecPlayerStartEntry BuildLayoutSpecPlayerStartFromDraftLayout(
            DraftLayoutDocument draftLayout,
            PlayablePromptIntent intent,
            PlayableObjectCatalog catalog)
        {
            if (draftLayout == null ||
                draftLayout.playerStart == null ||
                string.IsNullOrWhiteSpace(draftLayout.playerStart.objectId))
            {
                return new LayoutSpecPlayerStartEntry();
            }

            DraftLayoutPlayerStartEntry draftPlayerStart = draftLayout.playerStart;
            var playerStart = new LayoutSpecPlayerStartEntry
            {
                objectId = Normalize(draftPlayerStart.objectId),
                hasWorldPosition = true,
                worldX = draftPlayerStart.worldX,
                worldZ = draftPlayerStart.worldZ,
                hasImageBounds = true,
                centerPxX = draftPlayerStart.worldX,
                centerPxY = draftPlayerStart.worldZ,
                bboxWidthPx = 1f,
                bboxHeightPx = 1f,
                bboxConfidence = 1f,
            };

            Dictionary<string, PromptIntentObjectDefinition> objects = BuildIntentObjectLookup(intent);
            if (!objects.TryGetValue(playerStart.objectId, out PromptIntentObjectDefinition objectDefinition))
                return playerStart;

            if (!TryResolveCatalogObjectIdForRole(catalog, Normalize(objectDefinition != null ? objectDefinition.role : string.Empty), out string catalogObjectId))
                return playerStart;

            if (!catalog.TryResolveGameplayPlacementFootprint(
                    catalogObjectId,
                    Normalize(objectDefinition != null ? objectDefinition.designId : string.Empty),
                    out int widthCells,
                    out int depthCells,
                    out _,
                    out _,
                    out _))
            {
                return playerStart;
            }

            playerStart.bboxWidthPx = Math.Max(widthCells, 1);
            playerStart.bboxHeightPx = Math.Max(depthCells, 1);
            return playerStart;
        }

        private static Dictionary<string, PromptIntentObjectDefinition> BuildIntentObjectLookup(PlayablePromptIntent intent)
        {
            var lookup = new Dictionary<string, PromptIntentObjectDefinition>(StringComparer.Ordinal);
            PromptIntentObjectDefinition[] objects = intent != null ? intent.objects ?? Array.Empty<PromptIntentObjectDefinition>() : Array.Empty<PromptIntentObjectDefinition>();
            for (int i = 0; i < objects.Length; i++)
            {
                PromptIntentObjectDefinition value = objects[i];
                string objectId = Normalize(value != null ? value.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) || lookup.ContainsKey(objectId))
                    continue;

                lookup.Add(objectId, value);
            }

            return lookup;
        }

        private static bool TryResolveCatalogObjectIdForRole(PlayableObjectCatalog catalog, string role, out string objectId)
        {
            objectId = string.Empty;
            switch (Normalize(role))
            {
                case PromptIntentObjectRoles.GENERATOR:
                    objectId = "generator";
                    return true;
                case PromptIntentObjectRoles.PROCESSOR:
                    objectId = "converter";
                    return true;
                case PromptIntentObjectRoles.SELLER:
                    objectId = "seller";
                    return true;
                case PromptIntentObjectRoles.RAIL:
                    objectId = "rail";
                    return true;
                case PromptIntentObjectRoles.UNLOCK_PAD:
                    objectId = "unlocker";
                    return true;
                case PromptIntentObjectRoles.PLAYER:
                    return TryResolveUniqueCatalogObjectIdByCategory(catalog, "PlayerModel", out objectId);
                default:
                    return false;
            }
        }

        private static bool TryResolveUniqueCatalogObjectIdByCategory(
            PlayableObjectCatalog catalog,
            string category,
            out string objectId)
        {
            objectId = string.Empty;
            if (catalog == null)
                return false;

            IReadOnlyList<GameplayCatalogEntry> entries = catalog.GetGameplayEntries();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                GameplayCatalogEntry entry = entries[i];
                if (entry == null)
                    continue;

                string entryCategory = Normalize(entry.category);
                string entryObjectId = Normalize(entry.objectId);
                if (string.IsNullOrEmpty(entryObjectId))
                    continue;

                if (string.Equals(entryCategory, Normalize(category), StringComparison.Ordinal))
                    seen.Add(entryObjectId);
            }

            if (seen.Count != 1)
                return false;

            foreach (string value in seen)
            {
                objectId = value;
                return true;
            }

            return false;
        }

        private static LayoutSpecPhysicsAreaLayoutEntry TranslatePhysicsAreaLayout(DraftLayoutPlacementEntry placement)
        {
            DraftLayoutPhysicsAreaLayoutEntry source = placement != null ? placement.physicsAreaLayout ?? new DraftLayoutPhysicsAreaLayoutEntry() : new DraftLayoutPhysicsAreaLayoutEntry();
            return new LayoutSpecPhysicsAreaLayoutEntry
            {
                realPhysicsZoneBounds = TranslatePlacementBounds(source.realPhysicsZoneBounds),
                fakeSpriteZoneBounds = TranslatePlacementBounds(source.fakeSpriteZoneBounds),
                overlapAllowances = Array.Empty<PlacementOverlapAllowanceDefinition>(),
            };
        }

        private static LayoutSpecRailLayoutEntry TranslateRailLayout(DraftLayoutPlacementEntry placement)
        {
            DraftLayoutRailLayoutEntry source = placement != null ? placement.railLayout ?? new DraftLayoutRailLayoutEntry() : new DraftLayoutRailLayoutEntry();
            return new LayoutSpecRailLayoutEntry
            {
                pathCells = CloneRailPathAnchors(source.pathCells),
            };
        }

        private static RailPathAnchorDefinition[] CloneRailPathAnchors(RailPathAnchorDefinition[] source)
        {
            RailPathAnchorDefinition[] safeSource = source ?? Array.Empty<RailPathAnchorDefinition>();
            var translated = new RailPathAnchorDefinition[safeSource.Length];
            for (int i = 0; i < safeSource.Length; i++)
            {
                RailPathAnchorDefinition entry = safeSource[i];
                translated[i] = entry == null
                    ? new RailPathAnchorDefinition()
                    : new RailPathAnchorDefinition
                    {
                        worldX = entry.worldX,
                        worldZ = entry.worldZ,
                    };
            }

            return translated;
        }

        private static LayoutSpecPlacementBoundsEntry TranslatePlacementBounds(DraftLayoutPlacementBoundsEntry source)
        {
            source ??= new DraftLayoutPlacementBoundsEntry();
            return new LayoutSpecPlacementBoundsEntry
            {
                hasWorldBounds = source.hasWorldBounds,
                worldX = source.worldX,
                worldZ = source.worldZ,
                worldWidth = source.worldWidth,
                worldDepth = source.worldDepth,
            };
        }

        private static LayoutSpecCustomerPathPoint TranslateCustomerPathPoint(DraftLayoutCustomerPathPoint source)
        {
            source ??= new DraftLayoutCustomerPathPoint();
            return new LayoutSpecCustomerPathPoint
            {
                hasWorldPosition = true,
                worldX = source.worldX,
                worldZ = source.worldZ,
            };
        }

        private static LayoutSpecCustomerPathPoint[] TranslateCustomerPathPoints(DraftLayoutCustomerPathPoint[] source)
        {
            source ??= Array.Empty<DraftLayoutCustomerPathPoint>();
            var translated = new LayoutSpecCustomerPathPoint[source.Length];
            for (int i = 0; i < source.Length; i++)
                translated[i] = TranslateCustomerPathPoint(source[i]);
            return translated;
        }

        private static string CanonicalizeCustomerPathTargetId(string targetId)
        {
            string normalizedTargetId = Normalize(targetId);
            if (string.IsNullOrEmpty(normalizedTargetId))
                return string.Empty;

            return normalizedTargetId.StartsWith("spawn_", StringComparison.Ordinal)
                ? normalizedTargetId
                : "spawn_" + normalizedTargetId;
        }

        private static PromptIntentObjectScenarioOptions CreateSanitizedScenarioOptions(
            string role,
            PromptIntentObjectScenarioOptions source)
        {
            if (source == null)
                return null;

            string normalizedRole = Normalize(role);
            var sanitized = new PromptIntentObjectScenarioOptions();
            bool hasAnyValue = false;

            if (string.Equals(normalizedRole, PromptIntentObjectRoles.SELLER, StringComparison.Ordinal) &&
                source.customerRequestCount != null &&
                source.customerRequestCount.min > 0 &&
                source.customerRequestCount.max > 0)
            {
                sanitized.customerRequestCount = new PromptIntentCustomerRequestCount
                {
                    min = source.customerRequestCount.min,
                    max = source.customerRequestCount.max,
                };
                hasAnyValue = true;
            }

            if (string.Equals(normalizedRole, PromptIntentObjectRoles.SELLER, StringComparison.Ordinal))
            {
                PromptIntentSellerRequestableItemDefinition[] requestableItems = source.requestableItems ?? Array.Empty<PromptIntentSellerRequestableItemDefinition>();
                if (requestableItems.Length > 0)
                {
                    var sanitizedItems = new List<PromptIntentSellerRequestableItemDefinition>();
                    for (int i = 0; i < requestableItems.Length; i++)
                    {
                        PromptIntentSellerRequestableItemDefinition requestableItem = requestableItems[i];
                        if (requestableItem == null)
                            continue;

                        sanitizedItems.Add(new PromptIntentSellerRequestableItemDefinition
                        {
                            item = ItemRefUtility.Clone(requestableItem.item),
                            startWhen = CloneCondition(requestableItem.startWhen),
                        });
                    }

                    if (sanitizedItems.Count > 0)
                    {
                        sanitized.requestableItems = sanitizedItems.ToArray();
                        hasAnyValue = true;
                    }
                }
            }

            if (string.Equals(normalizedRole, PromptIntentObjectRoles.PROCESSOR, StringComparison.Ordinal))
            {
                if (source.inputCountPerConversion > 0)
                {
                    sanitized.inputCountPerConversion = source.inputCountPerConversion;
                    hasAnyValue = true;
                }

                if (source.conversionIntervalSeconds > 0f)
                {
                    sanitized.conversionIntervalSeconds = source.conversionIntervalSeconds;
                    hasAnyValue = true;
                }

                if (source.inputItemMoveIntervalSeconds > 0f)
                {
                    sanitized.inputItemMoveIntervalSeconds = source.inputItemMoveIntervalSeconds;
                    hasAnyValue = true;
                }
            }

            if (string.Equals(normalizedRole, PromptIntentObjectRoles.GENERATOR, StringComparison.Ordinal) &&
                source.spawnIntervalSeconds > 0f)
            {
                sanitized.spawnIntervalSeconds = source.spawnIntervalSeconds;
                hasAnyValue = true;
            }

            return hasAnyValue ? sanitized : null;
        }

        private static PromptIntentConditionDefinition CloneCondition(PromptIntentConditionDefinition source)
        {
            source ??= new PromptIntentConditionDefinition();
            return new PromptIntentConditionDefinition
            {
                kind = source.kind,
                stageId = source.stageId,
                targetObjectId = source.targetObjectId,
                item = ItemRefUtility.Clone(source.item),
                currencyId = source.currencyId,
                amountValue = source.amountValue,
            };
        }

        private static WorldBoundsDefinition TranslateWorldBounds(DraftLayoutPlacementBoundsEntry source)
        {
            source ??= new DraftLayoutPlacementBoundsEntry();
            return new WorldBoundsDefinition
            {
                hasWorldBounds = source.hasWorldBounds,
                worldX = source.worldX,
                worldZ = source.worldZ,
                worldWidth = source.worldWidth,
                worldDepth = source.worldDepth,
            };
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
