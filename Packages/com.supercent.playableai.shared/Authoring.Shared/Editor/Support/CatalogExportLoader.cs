#if !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;

namespace PlayableAI.AuthoringCore
{
    public sealed class CatalogExportLoadResult
    {
        public bool IsValid;
        public string Message = string.Empty;
        public string[] Errors = Array.Empty<string>();
        public string ManifestPath = string.Empty;
        public CatalogExportManifestData ManifestData = new CatalogExportManifestData();
        public string[] RequiredShardKinds = Array.Empty<string>();
        public string ExportPath = string.Empty;
        public PlayableCatalogExportData ExportData = new PlayableCatalogExportData();
        public PlayableObjectCatalog Catalog = null;
    }

    public static class CatalogExportLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        public static CatalogExportLoadResult LoadAndValidate(
            string catalogManifestPath,
            string usage = AuthoringCoreSharedContracts.CATALOG_USAGE_VALIDATE)
        {
            var errors = new List<string>();
            string fullManifestPath = Path.GetFullPath(catalogManifestPath ?? string.Empty);
            if (!File.Exists(fullManifestPath))
                return BuildFailure("catalog manifest JSON 파일을 찾지 못했습니다: " + fullManifestPath, errors, fullManifestPath);

            CatalogExportManifestData manifestData;
            try
            {
                string manifestJson = File.ReadAllText(fullManifestPath, Encoding.UTF8);
                manifestData = JsonSerializer.Deserialize<CatalogExportManifestData>(manifestJson, JsonOptions) ?? new CatalogExportManifestData();
            }
            catch (Exception exception)
            {
                return BuildFailure("catalog manifest JSON을 읽지 못했습니다: " + exception.Message, errors, fullManifestPath);
            }

            if (manifestData.schemaVersion < 1 || manifestData.schemaVersion > AuthoringCoreSharedContracts.CATALOG_MANIFEST_SCHEMA_VERSION)
            {
                return BuildFailure(
                    "catalog manifest schemaVersion '" + manifestData.schemaVersion + "'이(가) 지원 범위 '1~" + AuthoringCoreSharedContracts.CATALOG_MANIFEST_SCHEMA_VERSION + "'를 벗어났습니다.",
                    errors,
                    fullManifestPath);
            }

            string[] requiredShardKinds = ResolveRequiredShardKinds(usage);
            if (requiredShardKinds.Length == 0)
            {
                return BuildFailure(
                    "지원되지 않는 catalog usage입니다: " + (usage ?? string.Empty),
                    errors,
                    fullManifestPath,
                    manifestData: manifestData);
            }

            string manifestDirectory = Path.GetDirectoryName(fullManifestPath) ?? string.Empty;
            var shardDataByKind = new Dictionary<string, PlayableCatalogExportData>(StringComparer.Ordinal);
            CatalogShardDescriptor[] safeShards = manifestData.shards ?? new CatalogShardDescriptor[0];
            for (int i = 0; i < requiredShardKinds.Length; i++)
            {
                string requiredKind = requiredShardKinds[i];
                if (!TryFindShardDescriptor(safeShards, requiredKind, out CatalogShardDescriptor descriptor))
                {
                    return BuildFailure(
                        "catalog manifest에 필수 shard '" + requiredKind + "'가 없습니다.",
                        errors,
                        fullManifestPath,
                        manifestData: manifestData,
                        requiredShardKinds: requiredShardKinds);
                }

                if (string.IsNullOrWhiteSpace(descriptor.fileName))
                {
                    return BuildFailure(
                        "catalog manifest shard '" + requiredKind + "'의 fileName이 비어 있습니다.",
                        errors,
                        fullManifestPath,
                        manifestData: manifestData,
                        requiredShardKinds: requiredShardKinds);
                }

                if (descriptor.fileName.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                    descriptor.fileName.IndexOf('/') >= 0 ||
                    descriptor.fileName.IndexOf('\\') >= 0)
                {
                    return BuildFailure(
                        "catalog manifest shard '" + requiredKind + "'의 fileName이 유효하지 않습니다: " + descriptor.fileName,
                        errors,
                        fullManifestPath,
                        manifestData: manifestData,
                        requiredShardKinds: requiredShardKinds);
                }

                string shardPath = Path.GetFullPath(Path.Combine(manifestDirectory, descriptor.fileName));
                if (!File.Exists(shardPath))
                {
                    return BuildFailure(
                        "catalog shard JSON 파일을 찾지 못했습니다: " + shardPath,
                        errors,
                        fullManifestPath,
                        manifestData: manifestData,
                        requiredShardKinds: requiredShardKinds);
                }

                if (string.IsNullOrWhiteSpace(descriptor.contentHash))
                {
                    return BuildFailure(
                        "catalog manifest shard '" + requiredKind + "'의 contentHash가 비어 있습니다.",
                        errors,
                        fullManifestPath,
                        manifestData: manifestData,
                        requiredShardKinds: requiredShardKinds);
                }

                if (!string.IsNullOrWhiteSpace(descriptor.contentHash))
                {
                    string actualHash = ComputeFileContentHash(shardPath);
                    if (!string.Equals(actualHash, descriptor.contentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return BuildFailure(
                            "catalog shard hash가 일치하지 않습니다. shard='" + requiredKind + "', file='" + descriptor.fileName + "'.",
                            errors,
                            fullManifestPath,
                            manifestData: manifestData,
                            requiredShardKinds: requiredShardKinds);
                    }
                }

                try
                {
                    string shardJson = File.ReadAllText(shardPath, Encoding.UTF8);
                    PlayableCatalogExportData shardData = JsonSerializer.Deserialize<PlayableCatalogExportData>(shardJson, JsonOptions) ?? new PlayableCatalogExportData();
                    if (shardData.schemaVersion < 1 || shardData.schemaVersion > AuthoringCoreSharedContracts.CATALOG_EXPORT_SCHEMA_VERSION)
                    {
                        return BuildFailure(
                            "catalog shard schemaVersion '" + shardData.schemaVersion + "'이(가) 지원 범위를 벗어났습니다. file='" + descriptor.fileName + "'.",
                            errors,
                            fullManifestPath,
                            manifestData: manifestData,
                            requiredShardKinds: requiredShardKinds);
                    }

                    shardDataByKind[requiredKind] = shardData;
                }
                catch (Exception exception)
                {
                    return BuildFailure(
                        "catalog shard JSON을 읽지 못했습니다: " + exception.Message + " (file='" + descriptor.fileName + "')",
                        errors,
                        fullManifestPath,
                        manifestData: manifestData,
                        requiredShardKinds: requiredShardKinds);
                }
            }

            PlayableCatalogExportData exportData = MergeShards(manifestData, shardDataByKind, requiredShardKinds);
            PlayableObjectCatalog catalog = BuildCatalog(exportData);
            PlayableObjectCatalogValidationResult validation = PlayableObjectCatalogContractValidator.Validate(catalog);
            if (!validation.IsValid)
            {
                for (int i = 0; i < validation.Errors.Count; i++)
                    errors.Add("catalog contract 오류: " + validation.Errors[i].message);
                return BuildFailure(
                    errors[0],
                    errors,
                    fullManifestPath,
                    exportData,
                    manifestData,
                    requiredShardKinds);
            }

            return new CatalogExportLoadResult
            {
                IsValid = true,
                Message = "유효합니다.",
                Errors = Array.Empty<string>(),
                ManifestPath = fullManifestPath,
                ManifestData = manifestData,
                RequiredShardKinds = requiredShardKinds,
                ExportPath = fullManifestPath,
                ExportData = exportData,
                Catalog = catalog,
            };
        }

        private static string[] ResolveRequiredShardKinds(string usage)
        {
            string normalized = Normalize(usage);
            switch (normalized)
            {
                case AuthoringCoreSharedContracts.CATALOG_USAGE_VALIDATE:
                    return new[]
                    {
                        AuthoringCoreSharedContracts.CATALOG_SHARD_KIND_CORE,
                        AuthoringCoreSharedContracts.CATALOG_SHARD_KIND_STEP2,
                    };
                case AuthoringCoreSharedContracts.CATALOG_USAGE_GENERATE:
                    return new[]
                    {
                        AuthoringCoreSharedContracts.CATALOG_SHARD_KIND_CORE,
                        AuthoringCoreSharedContracts.CATALOG_SHARD_KIND_STEP3,
                    };
                default:
                    return Array.Empty<string>();
            }
        }

        private static bool TryFindShardDescriptor(
            CatalogShardDescriptor[] shards,
            string shardKind,
            out CatalogShardDescriptor descriptor)
        {
            CatalogShardDescriptor[] safeShards = shards ?? new CatalogShardDescriptor[0];
            string normalizedKind = Normalize(shardKind);
            for (int i = 0; i < safeShards.Length; i++)
            {
                CatalogShardDescriptor value = safeShards[i];
                if (value == null)
                    continue;

                if (string.Equals(Normalize(value.shardKind), normalizedKind, StringComparison.Ordinal))
                {
                    descriptor = value;
                    return true;
                }
            }

            descriptor = new CatalogShardDescriptor();
            return false;
        }

        private static PlayableCatalogExportData MergeShards(
            CatalogExportManifestData manifestData,
            Dictionary<string, PlayableCatalogExportData> shardDataByKind,
            string[] requiredShardKinds)
        {
            var merged = new PlayableCatalogExportData
            {
                schemaVersion = AuthoringCoreSharedContracts.CATALOG_EXPORT_SCHEMA_VERSION,
                sourceCatalogAssetPath = manifestData != null ? manifestData.sourceCatalogAssetPath ?? string.Empty : string.Empty,
                themeId = manifestData != null ? manifestData.themeId ?? string.Empty : string.Empty,
                prefabsRootPath = manifestData != null ? manifestData.prefabsRootPath ?? string.Empty : string.Empty,
            };

            Dictionary<string, PlayableCatalogExportData> safeShardData = shardDataByKind ?? new Dictionary<string, PlayableCatalogExportData>(StringComparer.Ordinal);
            string[] safeRequiredKinds = requiredShardKinds ?? Array.Empty<string>();
            for (int i = 0; i < safeRequiredKinds.Length; i++)
            {
                string requiredKind = safeRequiredKinds[i];
                if (!safeShardData.TryGetValue(requiredKind, out PlayableCatalogExportData shardData) || shardData == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(shardData.sourceCatalogAssetPath))
                    merged.sourceCatalogAssetPath = shardData.sourceCatalogAssetPath;
                if (!string.IsNullOrWhiteSpace(shardData.themeId))
                    merged.themeId = shardData.themeId;
                if (!string.IsNullOrWhiteSpace(shardData.prefabsRootPath))
                    merged.prefabsRootPath = shardData.prefabsRootPath;
                if (!string.IsNullOrWhiteSpace(shardData.environmentFloorPrefabAssetPath))
                    merged.environmentFloorPrefabAssetPath = shardData.environmentFloorPrefabAssetPath;
                if (!string.IsNullOrWhiteSpace(shardData.environmentFloorPrefabAssetGuid))
                    merged.environmentFloorPrefabAssetGuid = shardData.environmentFloorPrefabAssetGuid;
                if (shardData.environmentFloorPrefabMetadata != null)
                    merged.environmentFloorPrefabMetadata = shardData.environmentFloorPrefabMetadata;

                CatalogEditorEntryExportData[] editorEntries = shardData.editorBasedEntries ?? new CatalogEditorEntryExportData[0];
                if (editorEntries.Length > 0 && (merged.editorBasedEntries == null || merged.editorBasedEntries.Length == 0))
                    merged.editorBasedEntries = editorEntries;

                CatalogSinglePrefabGameplaySectionExportData[] singlePrefabGameplaySections = shardData.singlePrefabGameplaySections ?? new CatalogSinglePrefabGameplaySectionExportData[0];
                if (singlePrefabGameplaySections.Length > 0 &&
                    (merged.singlePrefabGameplaySections == null || merged.singlePrefabGameplaySections.Length == 0))
                    merged.singlePrefabGameplaySections = singlePrefabGameplaySections;

                CatalogAssembledPathGameplaySectionExportData[] assembledPathGameplaySections = shardData.assembledPathGameplaySections ?? new CatalogAssembledPathGameplaySectionExportData[0];
                if (assembledPathGameplaySections.Length > 0 &&
                    (merged.assembledPathGameplaySections == null || merged.assembledPathGameplaySections.Length == 0))
                    merged.assembledPathGameplaySections = assembledPathGameplaySections;

                CatalogEnvironmentSectionExportData[] environmentSections = shardData.environmentSections ?? new CatalogEnvironmentSectionExportData[0];
                if (environmentSections.Length > 0 &&
                    (merged.environmentSections == null || merged.environmentSections.Length == 0))
                    merged.environmentSections = environmentSections;
            }

            return merged;
        }

        private static PlayableObjectCatalog BuildCatalog(PlayableCatalogExportData exportData)
        {
            var catalog = new PlayableObjectCatalog();
            catalog.SetPrefabsRootPath(exportData.prefabsRootPath ?? string.Empty);
            catalog.SetThemeId(exportData.themeId ?? string.Empty);
            catalog.SetEnvironmentFloorPrefab(BuildPortablePrefab(
                exportData.environmentFloorPrefabAssetPath,
                exportData.environmentFloorPrefabAssetGuid,
                exportData.environmentFloorPrefabMetadata));
            catalog.SetEditorEntries(BuildEditorEntries(exportData.editorBasedEntries));

            GameplayCatalogEntry[] generators = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] rails = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] converters = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] sellers = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] unlockers = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] items = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] playerModels = new GameplayCatalogEntry[0];
            GameplayCatalogEntry[] customers = new GameplayCatalogEntry[0];

            CatalogSinglePrefabGameplaySectionExportData[] singlePrefabGameplaySections = exportData.singlePrefabGameplaySections ?? new CatalogSinglePrefabGameplaySectionExportData[0];
            for (int i = 0; i < singlePrefabGameplaySections.Length; i++)
            {
                CatalogSinglePrefabGameplaySectionExportData section = singlePrefabGameplaySections[i];
                GameplayCatalogEntry[] entries = BuildSinglePrefabGameplayEntries(section != null ? section.entries : null);
                string arrayPath = section != null ? section.arrayPath ?? string.Empty : string.Empty;
                switch (arrayPath)
                {
                    case GameplayCatalog.GENERATORS_ARRAY_PATH:
                        generators = entries;
                        break;
                    case GameplayCatalog.RAILS_ARRAY_PATH:
                        rails = entries;
                        break;
                    case GameplayCatalog.CONVERTERS_ARRAY_PATH:
                        converters = entries;
                        break;
                    case GameplayCatalog.SELLERS_ARRAY_PATH:
                        sellers = entries;
                        break;
                    case GameplayCatalog.UNLOCKERS_ARRAY_PATH:
                        unlockers = entries;
                        break;
                    case GameplayCatalog.ITEMS_ARRAY_PATH:
                        items = entries;
                        break;
                    case GameplayCatalog.PLAYER_MODELS_ARRAY_PATH:
                        playerModels = entries;
                        break;
                    case GameplayCatalog.CUSTOMERS_ARRAY_PATH:
                        customers = entries;
                        break;
                }
            }

            CatalogAssembledPathGameplaySectionExportData[] assembledPathGameplaySections = exportData.assembledPathGameplaySections ?? new CatalogAssembledPathGameplaySectionExportData[0];
            for (int i = 0; i < assembledPathGameplaySections.Length; i++)
            {
                CatalogAssembledPathGameplaySectionExportData section = assembledPathGameplaySections[i];
                GameplayCatalogEntry[] entries = BuildAssembledPathGameplayEntries(section != null ? section.entries : null);
                string arrayPath = section != null ? section.arrayPath ?? string.Empty : string.Empty;
                switch (arrayPath)
                {
                    case GameplayCatalog.GENERATORS_ARRAY_PATH:
                        generators = entries;
                        break;
                    case GameplayCatalog.RAILS_ARRAY_PATH:
                        rails = entries;
                        break;
                    case GameplayCatalog.CONVERTERS_ARRAY_PATH:
                        converters = entries;
                        break;
                    case GameplayCatalog.SELLERS_ARRAY_PATH:
                        sellers = entries;
                        break;
                    case GameplayCatalog.UNLOCKERS_ARRAY_PATH:
                        unlockers = entries;
                        break;
                    case GameplayCatalog.ITEMS_ARRAY_PATH:
                        items = entries;
                        break;
                    case GameplayCatalog.PLAYER_MODELS_ARRAY_PATH:
                        playerModels = entries;
                        break;
                    case GameplayCatalog.CUSTOMERS_ARRAY_PATH:
                        customers = entries;
                        break;
                }
            }

            EnvironmentCatalogEntry[] floors = new EnvironmentCatalogEntry[0];
            EnvironmentCatalogEntry[] walls = new EnvironmentCatalogEntry[0];
            EnvironmentCatalogEntry[] fences = new EnvironmentCatalogEntry[0];
            EnvironmentCatalogEntry[] roads = new EnvironmentCatalogEntry[0];
            CatalogEnvironmentSectionExportData[] environmentSections = exportData.environmentSections ?? new CatalogEnvironmentSectionExportData[0];
            for (int i = 0; i < environmentSections.Length; i++)
            {
                CatalogEnvironmentSectionExportData section = environmentSections[i];
                EnvironmentCatalogEntry[] entries = BuildEnvironmentEntries(section != null ? section.entries : null);
                string arrayPath = section != null ? section.arrayPath ?? string.Empty : string.Empty;
                switch (arrayPath)
                {
                    case EnvironmentCatalog.FLOORS_ARRAY_PATH:
                        floors = entries;
                        break;
                    case EnvironmentCatalog.WALLS_ARRAY_PATH:
                        walls = entries;
                        break;
                    case EnvironmentCatalog.FENCES_ARRAY_PATH:
                        fences = entries;
                        break;
                    case EnvironmentCatalog.ROADS_ARRAY_PATH:
                        roads = entries;
                        break;
                }
            }

            catalog.SetGameplayRoleArrays(generators, rails, converters, sellers, unlockers, items, playerModels, customers);
            catalog.SetEnvironmentRoleArrays(floors, walls, fences, roads);
            return catalog;
        }

        private static EditorBasedCatalog.Entry[] BuildEditorEntries(CatalogEditorEntryExportData[] exportEntries)
        {
            CatalogEditorEntryExportData[] safeEntries = exportEntries ?? new CatalogEditorEntryExportData[0];
            var entries = new EditorBasedCatalog.Entry[safeEntries.Length];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                CatalogEditorEntryExportData entry = safeEntries[i] ?? new CatalogEditorEntryExportData();
                entries[i] = new EditorBasedCatalog.Entry
                {
                    objectId = entry.objectId ?? string.Empty,
                    prefab = BuildPortablePrefab(entry.prefabAssetPath, entry.prefabAssetGuid, entry.prefabMetadata),
                };
            }

            return entries;
        }

        private static GameplayCatalogEntry[] BuildSinglePrefabGameplayEntries(CatalogSinglePrefabGameplayEntryExportData[] exportEntries)
        {
            CatalogSinglePrefabGameplayEntryExportData[] safeEntries = exportEntries ?? new CatalogSinglePrefabGameplayEntryExportData[0];
            var entries = new GameplayCatalogEntry[safeEntries.Length];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                CatalogSinglePrefabGameplayEntryExportData entry = safeEntries[i] ?? new CatalogSinglePrefabGameplayEntryExportData();
                entries[i] = new GameplayCatalogEntry
                {
                    objectId = entry.objectId ?? string.Empty,
                    category = entry.category ?? string.Empty,
                    designMode = GameplayDesignMode.SinglePrefab,
                    designs = BuildSinglePrefabDesignEntries(entry.designs),
                };
            }

            return entries;
        }

        private static GameplayCatalogEntry[] BuildAssembledPathGameplayEntries(CatalogAssembledPathGameplayEntryExportData[] exportEntries)
        {
            CatalogAssembledPathGameplayEntryExportData[] safeEntries = exportEntries ?? new CatalogAssembledPathGameplayEntryExportData[0];
            var entries = new GameplayCatalogEntry[safeEntries.Length];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                CatalogAssembledPathGameplayEntryExportData entry = safeEntries[i] ?? new CatalogAssembledPathGameplayEntryExportData();
                entries[i] = new GameplayCatalogEntry
                {
                    objectId = entry.objectId ?? string.Empty,
                    category = entry.category ?? string.Empty,
                    designMode = GameplayDesignMode.AssembledPath,
                    designs = BuildAssembledPathDesignEntries(entry.designs),
                };
            }

            return entries;
        }

        private static EnvironmentCatalogEntry[] BuildEnvironmentEntries(CatalogEnvironmentEntryExportData[] exportEntries)
        {
            CatalogEnvironmentEntryExportData[] safeEntries = exportEntries ?? new CatalogEnvironmentEntryExportData[0];
            var entries = new EnvironmentCatalogEntry[safeEntries.Length];
            for (int i = 0; i < safeEntries.Length; i++)
            {
                CatalogEnvironmentEntryExportData entry = safeEntries[i] ?? new CatalogEnvironmentEntryExportData();
                entries[i] = new EnvironmentCatalogEntry
                {
                    objectId = entry.objectId ?? string.Empty,
                    category = entry.category ?? string.Empty,
                    placementMode = entry.placementMode ?? string.Empty,
                    variationMode = entry.variationMode ?? string.Empty,
                    designs = BuildEnvironmentDesignEntries(entry.designs),
                };
            }

            return entries;
        }

        private static DesignVariantEntry[] BuildSinglePrefabDesignEntries(CatalogGameplayDesignExportData[] exportDesigns)
        {
            CatalogGameplayDesignExportData[] safeDesigns = exportDesigns ?? new CatalogGameplayDesignExportData[0];
            var designs = new DesignVariantEntry[safeDesigns.Length];
            for (int i = 0; i < safeDesigns.Length; i++)
            {
                CatalogGameplayDesignExportData design = safeDesigns[i] ?? new CatalogGameplayDesignExportData();
                designs[i] = new DesignVariantEntry
                {
                    designId = design.designId ?? string.Empty,
                    description = design.description ?? string.Empty,
                    prefab = BuildPortablePrefab(design.prefabAssetPath, design.prefabAssetGuid, design.prefabMetadata),
                    topImage = BuildPortableTexture(design.topImageAssetPath, design.topImageAssetGuid),
                };
            }

            return designs;
        }

        private static DesignVariantEntry[] BuildAssembledPathDesignEntries(CatalogAssembledPathDesignExportData[] exportDesigns)
        {
            CatalogAssembledPathDesignExportData[] safeDesigns = exportDesigns ?? new CatalogAssembledPathDesignExportData[0];
            var designs = new DesignVariantEntry[safeDesigns.Length];
            for (int i = 0; i < safeDesigns.Length; i++)
            {
                CatalogAssembledPathDesignExportData design = safeDesigns[i] ?? new CatalogAssembledPathDesignExportData();
                CatalogAssembledPathAssetsExportData assets = design.assembledPathAssets ?? new CatalogAssembledPathAssetsExportData();
                designs[i] = new DesignVariantEntry
                {
                    designId = design.designId ?? string.Empty,
                    description = design.description ?? string.Empty,
                    prefab = BuildPortablePrefab(design.prefabAssetPath, design.prefabAssetGuid, design.prefabMetadata),
                    topImage = BuildPortableTexture(design.topImageAssetPath, design.topImageAssetGuid),
                    assembledPathAssets = new AssembledPathDesignAssets
                    {
                        straightTopImage = BuildPortableTexture(assets.straightTopImageAssetPath, assets.straightTopImageAssetGuid),
                        cornerTopImage = BuildPortableTexture(assets.cornerTopImageAssetPath, assets.cornerTopImageAssetGuid),
                        straightPrefab = BuildPortablePrefab(assets.straightPrefabAssetPath, assets.straightPrefabAssetGuid, assets.straightPrefabMetadata),
                        cornerPrefab = BuildPortablePrefab(assets.cornerPrefabAssetPath, assets.cornerPrefabAssetGuid, assets.cornerPrefabMetadata),
                    },
                };
            }

            return designs;
        }

        private static EnvironmentDesignVariantEntry[] BuildEnvironmentDesignEntries(CatalogEnvironmentDesignExportData[] exportDesigns)
        {
            CatalogEnvironmentDesignExportData[] safeDesigns = exportDesigns ?? new CatalogEnvironmentDesignExportData[0];
            var designs = new EnvironmentDesignVariantEntry[safeDesigns.Length];
            for (int i = 0; i < safeDesigns.Length; i++)
            {
                CatalogEnvironmentDesignExportData design = safeDesigns[i] ?? new CatalogEnvironmentDesignExportData();
                designs[i] = new EnvironmentDesignVariantEntry
                {
                    designId = design.designId ?? string.Empty,
                    description = design.description ?? string.Empty,
                    prefab = BuildPortablePrefab(design.prefabAssetPath, design.prefabAssetGuid, design.prefabMetadata),
                    topImage = BuildPortableTexture(design.topImageAssetPath, design.topImageAssetGuid),
                    straightTopImage = BuildPortableTexture(design.straightTopImageAssetPath, design.straightTopImageAssetGuid),
                    cornerTopImage = BuildPortableTexture(design.cornerTopImageAssetPath, design.cornerTopImageAssetGuid),
                    tJunctionTopImage = BuildPortableTexture(design.tJunctionTopImageAssetPath, design.tJunctionTopImageAssetGuid),
                    crossTopImage = BuildPortableTexture(design.crossTopImageAssetPath, design.crossTopImageAssetGuid),
                    straightPrefab = BuildPortablePrefab(design.straightPrefabAssetPath, design.straightPrefabAssetGuid, design.straightPrefabMetadata),
                    cornerPrefab = BuildPortablePrefab(design.cornerPrefabAssetPath, design.cornerPrefabAssetGuid, design.cornerPrefabMetadata),
                    tJunctionPrefab = BuildPortablePrefab(design.tJunctionPrefabAssetPath, design.tJunctionPrefabAssetGuid, design.tJunctionPrefabMetadata),
                    crossPrefab = BuildPortablePrefab(design.crossPrefabAssetPath, design.crossPrefabAssetGuid, design.crossPrefabMetadata),
                };
            }

            return designs;
        }

        private static PortableGameObject BuildPortablePrefab(string assetPath, string assetGuid, CatalogPrefabMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(assetPath) && string.IsNullOrWhiteSpace(assetGuid))
                return null;

            return new PortableGameObject
            {
                name = Path.GetFileNameWithoutExtension(assetPath ?? string.Empty),
                assetPath = assetPath ?? string.Empty,
                assetGuid = assetGuid ?? string.Empty,
                metadata = metadata ?? new CatalogPrefabMetadata(),
            };
        }

        private static PortableTexture2D BuildPortableTexture(string assetPath, string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetPath) && string.IsNullOrWhiteSpace(assetGuid))
                return null;

            return new PortableTexture2D
            {
                name = Path.GetFileNameWithoutExtension(assetPath ?? string.Empty),
                assetPath = assetPath ?? string.Empty,
                assetGuid = assetGuid ?? string.Empty,
            };
        }

        private static string ComputeFileContentHash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static CatalogExportLoadResult BuildFailure(
            string message,
            List<string> errors,
            string manifestPath,
            PlayableCatalogExportData exportData = null,
            CatalogExportManifestData manifestData = null,
            string[] requiredShardKinds = null)
        {
            return new CatalogExportLoadResult
            {
                IsValid = false,
                Message = message ?? string.Empty,
                Errors = errors.Count == 0 ? new[] { message } : errors.ToArray(),
                ManifestPath = manifestPath ?? string.Empty,
                ManifestData = manifestData ?? new CatalogExportManifestData(),
                RequiredShardKinds = requiredShardKinds ?? Array.Empty<string>(),
                ExportPath = manifestPath ?? string.Empty,
                ExportData = exportData ?? new PlayableCatalogExportData(),
                Catalog = null,
            };
        }
    }
}
#endif
