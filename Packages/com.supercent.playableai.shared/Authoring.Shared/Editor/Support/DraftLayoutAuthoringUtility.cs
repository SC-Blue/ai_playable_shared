using System;
using System.Collections.Generic;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;
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
                LayoutSpecEnvironmentEntry normalizedEntry = BuildLayoutSpecEnvironmentEntry(entry, catalog);
                environment[i] = new LayoutSpecEnvironmentEntry
                {
                    objectId = normalizedEntry.objectId,
                    designId = normalizedEntry.designId,
                    kind = normalizedEntry.kind,
                    widthCells = normalizedEntry.widthCells,
                    depthCells = normalizedEntry.depthCells,
                    hasWorldBounds = normalizedEntry.hasWorldBounds,
                    worldX = normalizedEntry.worldX,
                    worldZ = normalizedEntry.worldZ,
                    worldWidth = normalizedEntry.worldWidth,
                    worldDepth = normalizedEntry.worldDepth,
                    rotationY = normalizedEntry.rotationY,
                    includeInBounds = normalizedEntry.includeInBounds,
                    singleLayer = normalizedEntry.singleLayer,
                    isOuterBoundary = normalizedEntry.isOuterBoundary,
                };
            }

            DraftLayoutCustomerPathEntry[] draftCustomerPaths = draftLayout != null ? draftLayout.customerPaths ?? Array.Empty<DraftLayoutCustomerPathEntry>() : Array.Empty<DraftLayoutCustomerPathEntry>();
            var customerPaths = new LayoutSpecCustomerPathEntry[draftCustomerPaths.Length];
            for (int i = 0; i < draftCustomerPaths.Length; i++)
            {
                DraftLayoutCustomerPathEntry entry = draftCustomerPaths[i] ?? new DraftLayoutCustomerPathEntry();
                customerPaths[i] = new LayoutSpecCustomerPathEntry
                {
                    targetId = Normalize(entry.targetId),
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
                hasResolvedYaw = draftPlayerStart.hasResolvedYaw,
                resolvedYawDegrees = draftPlayerStart.resolvedYawDegrees,
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
                    return TryResolveUniqueCatalogObjectIdByCategory(catalog, GameplayCatalog.PLAYER_MODEL_CATEGORY, out objectId);
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

        private static LayoutSpecEnvironmentEntry BuildLayoutSpecEnvironmentEntry(
            DraftLayoutEnvironmentEntry entry,
            PlayableObjectCatalog catalog)
        {
            entry ??= new DraftLayoutEnvironmentEntry();
            string objectId = Normalize(entry.objectId);
            string designId = Normalize(entry.designId);

            float worldWidth = entry.worldWidth;
            float worldDepth = entry.worldDepth;
            int widthCells = entry.widthCells;
            int depthCells = entry.depthCells;
            if (entry.hasWorldBounds &&
                TryResolveEnvironmentTileStep(catalog, objectId, designId, out float tileStep))
            {
                worldWidth = SnapEnvironmentWorldSpan(worldWidth, tileStep);
                worldDepth = SnapEnvironmentWorldSpan(worldDepth, tileStep);
                widthCells = Math.Max(1, ResolveRoundedCellCount(worldWidth));
                depthCells = Math.Max(1, ResolveRoundedCellCount(worldDepth));
            }

            return new LayoutSpecEnvironmentEntry
            {
                objectId = objectId,
                designId = designId,
                kind = string.Empty,
                widthCells = widthCells,
                depthCells = depthCells,
                hasWorldBounds = entry.hasWorldBounds,
                worldX = entry.worldX,
                worldZ = entry.worldZ,
                worldWidth = worldWidth,
                worldDepth = worldDepth,
                rotationY = entry.rotationY,
                includeInBounds = entry.includeInBounds,
                singleLayer = entry.singleLayer,
                isOuterBoundary = entry.isOuterBoundary,
            };
        }

        private static bool TryResolveEnvironmentTileStep(
            PlayableObjectCatalog catalog,
            string objectId,
            string designId,
            out float tileStep)
        {
            tileStep = 0f;
            if (catalog == null || string.IsNullOrEmpty(objectId))
                return false;

            if (!catalog.TryGetEnvironmentDesign(
                    objectId,
                    designId,
                    out EnvironmentDesignVariantEntry design,
                    out _,
                    out _,
                    out _))
            {
                return false;
            }

            if (!TryResolveEnvironmentFootprintCells(design, out int footprintCells))
                return false;

            tileStep = Math.Max(1, footprintCells) * IntentAuthoringUtility.LAYOUT_SPACING;
            return true;
        }

        private static bool TryResolveEnvironmentFootprintCells(EnvironmentDesignVariantEntry design, out int footprintCells)
        {
            footprintCells = 0;
            if (TryReadEnvironmentPrefabFootprintCells(design != null ? design.prefab : null, out footprintCells) ||
                TryReadEnvironmentPrefabFootprintCells(design != null ? design.straightPrefab : null, out footprintCells) ||
                TryReadEnvironmentPrefabFootprintCells(design != null ? design.cornerPrefab : null, out footprintCells) ||
                TryReadEnvironmentPrefabFootprintCells(design != null ? design.tJunctionPrefab : null, out footprintCells) ||
                TryReadEnvironmentPrefabFootprintCells(design != null ? design.crossPrefab : null, out footprintCells))
            {
                return footprintCells > 0;
            }

            return false;
        }

        private static bool TryReadEnvironmentPrefabFootprintCells(GameObject prefab, out int footprintCells)
        {
            footprintCells = 0;
            if (prefab == null)
                return false;

            if (!PortablePrefabMetadataUtility.TryGetMetadata(prefab, out CatalogPrefabMetadata metadata))
                return false;

            int widthCells = metadata.placementFootprintWidthCells > 0 ? metadata.placementFootprintWidthCells : 1;
            int depthCells = metadata.placementFootprintDepthCells > 0 ? metadata.placementFootprintDepthCells : 1;
            if (widthCells != depthCells)
                return false;

            footprintCells = widthCells;
            return true;
        }

        private static float SnapEnvironmentWorldSpan(float worldSpan, float tileStep)
        {
            if (worldSpan <= 0f || tileStep <= 0f)
                return worldSpan;

            int tileCount = Math.Max(1, (int)Math.Round(worldSpan / tileStep, MidpointRounding.AwayFromZero));
            return tileCount * tileStep;
        }

        private static int ResolveRoundedCellCount(float worldSpan)
        {
            if (worldSpan <= 0f)
                return 1;

            return Math.Max(1, (int)Math.Round(worldSpan / IntentAuthoringUtility.LAYOUT_SPACING, MidpointRounding.AwayFromZero));
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
