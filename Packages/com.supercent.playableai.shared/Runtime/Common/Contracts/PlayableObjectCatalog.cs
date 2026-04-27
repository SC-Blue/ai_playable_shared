using System;
using System.Collections.Generic;
using System.Reflection;
using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    public enum GameplayDesignMode
    {
        SinglePrefab = 0,
        AssembledPath = 1,
    }

    internal enum PlacementFootprintResolutionMode
    {
        CatalogMetadataOnly = 0,
        RuntimePrefabThenMetadata = 1,
    }

    internal static class CatalogAssetReferenceUtility
    {
        private const BindingFlags PUBLIC_INSTANCE = BindingFlags.Instance | BindingFlags.Public;
        private const BindingFlags ANY_INSTANCE = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string PLAYABLE_PLACEMENT_FOOTPRINT_TYPE_NAME = "PlayablePlacementFootprint";

        public static string ResolvePrefabAssetPath(GameObject prefab)
        {
            return ResolveStringField(prefab, "assetPath");
        }

        public static string ResolvePrefabAssetGuid(GameObject prefab)
        {
            return ResolveStringField(prefab, "assetGuid");
        }

        public static bool TryReadPlacementFootprintFromCatalogMetadata(
            GameObject prefab,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ)
        {
            widthCells = 0;
            depthCells = 0;
            centerOffsetX = 0f;
            centerOffsetZ = 0f;
            if (prefab == null)
                return false;

            if (!TryResolveCatalogPrefabMetadata(prefab, out CatalogPrefabMetadata metadata))
                return false;

            if (metadata == null ||
                metadata.placementFootprintWidthCells <= 0 ||
                metadata.placementFootprintDepthCells <= 0)
            {
                widthCells = 0;
                depthCells = 0;
                centerOffsetX = 0f;
                centerOffsetZ = 0f;
                return false;
            }

            widthCells = metadata.placementFootprintWidthCells;
            depthCells = metadata.placementFootprintDepthCells;
            centerOffsetX = metadata.placementFootprintCenterOffsetX;
            centerOffsetZ = metadata.placementFootprintCenterOffsetZ;
            return widthCells > 0 && depthCells > 0;
        }

        public static bool TryReadPlacementFootprintFromPrefabComponent(
            GameObject prefab,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ)
        {
            widthCells = 0;
            depthCells = 0;
            centerOffsetX = 0f;
            centerOffsetZ = 0f;
            if (prefab == null)
                return false;

            Component footprint = FindPlacementFootprint(prefab);
            if (footprint == null)
                return false;

            if (!TryReadIntProperty(footprint, "WidthCells", out widthCells) ||
                !TryReadIntProperty(footprint, "DepthCells", out depthCells) ||
                !TryReadVector3Property(footprint, "LocalCenterOffset", out Vector3 localCenterOffset))
            {
                widthCells = 0;
                depthCells = 0;
                centerOffsetX = 0f;
                centerOffsetZ = 0f;
                return false;
            }

            centerOffsetX = localCenterOffset.x;
            centerOffsetZ = localCenterOffset.z;
            return widthCells > 0 && depthCells > 0;
        }

        public static bool TryReadPlacementFootprintForRuntime(
            GameObject prefab,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ)
        {
            if (TryReadPlacementFootprintFromPrefabComponent(prefab, out widthCells, out depthCells, out centerOffsetX, out centerOffsetZ))
                return true;

            return TryReadPlacementFootprintFromCatalogMetadata(prefab, out widthCells, out depthCells, out centerOffsetX, out centerOffsetZ);
        }

        internal static Component FindPlacementFootprint(GameObject prefab)
        {
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type componentType = component.GetType();
                if (componentType == null)
                    continue;

                if (string.Equals(componentType.Name, PLAYABLE_PLACEMENT_FOOTPRINT_TYPE_NAME, System.StringComparison.Ordinal))
                    return component;
            }

            return null;
        }

        internal static bool TryResolveCatalogPrefabMetadata(GameObject prefab, out CatalogPrefabMetadata metadata)
        {
            metadata = new CatalogPrefabMetadata();
            if (prefab == null)
                return false;

            Type prefabType = prefab.GetType();
            FieldInfo metadataField = prefabType.GetField("metadata", ANY_INSTANCE);
            if (metadataField != null && typeof(CatalogPrefabMetadata).IsAssignableFrom(metadataField.FieldType))
            {
                metadata = metadataField.GetValue(prefab) as CatalogPrefabMetadata ?? new CatalogPrefabMetadata();
                return true;
            }

            PropertyInfo metadataProperty = prefabType.GetProperty("metadata", ANY_INSTANCE);
            if (metadataProperty != null && metadataProperty.CanRead && typeof(CatalogPrefabMetadata).IsAssignableFrom(metadataProperty.PropertyType))
            {
                metadata = metadataProperty.GetValue(prefab) as CatalogPrefabMetadata ?? new CatalogPrefabMetadata();
                return true;
            }

            return false;
        }

        internal static bool TryReadIntProperty(object target, string propertyName, out int value)
        {
            value = 0;
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            PropertyInfo property = target.GetType().GetProperty(propertyName, PUBLIC_INSTANCE);
            if (property == null || property.PropertyType != typeof(int))
                return false;

            object rawValue = property.GetValue(target);
            if (rawValue is not int intValue)
                return false;

            value = intValue;
            return true;
        }

        internal static bool TryReadVector3Property(object target, string propertyName, out Vector3 value)
        {
            value = Vector3.zero;
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            PropertyInfo property = target.GetType().GetProperty(propertyName, PUBLIC_INSTANCE);
            if (property == null || property.PropertyType != typeof(Vector3))
                return false;

            object rawValue = property.GetValue(target);
            if (rawValue is not Vector3 vectorValue)
                return false;

            value = vectorValue;
            return true;
        }

        private static string ResolveStringField(object target, string fieldName)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
                return string.Empty;

            FieldInfo field = target.GetType().GetField(fieldName, PUBLIC_INSTANCE);
            if (field == null || field.FieldType != typeof(string))
                return string.Empty;

            return Normalize(field.GetValue(target) as string);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [System.Serializable]
    public sealed class AssembledPathDesignAssets
    {
        public GameObject straightPrefab;
        public GameObject cornerPrefab;

        public string straightPrefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(straightPrefab);
        public string straightPrefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(straightPrefab);
        public string cornerPrefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(cornerPrefab);
        public string cornerPrefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(cornerPrefab);
    }

    [System.Serializable]
    public sealed class DesignVariantEntry
    {
        public string designId;
        public GameObject prefab;
        public AssembledPathDesignAssets assembledPathAssets = new AssembledPathDesignAssets();
        [TextArea] public string description;

        public string prefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(prefab);
        public string prefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(prefab);
    }

    [System.Serializable]
    public sealed class GameplayCatalogEntry
    {
        public string objectId;
        public string category;
        public GameplayDesignMode designMode = GameplayDesignMode.SinglePrefab;
        public DesignVariantEntry[] designs = new DesignVariantEntry[0];
    }

    public sealed class GameplayCatalogSectionDefinition
    {
        public string arrayPath;
        public string label;
        public string expectedCategory;
        public GameplayDesignMode designMode;
        public GameplayCatalogEntry[] entries = new GameplayCatalogEntry[0];
    }

    [System.Serializable]
    public sealed class EnvironmentDesignVariantEntry
    {
        public string designId;
        public GameObject prefab;
        public GameObject straightPrefab;
        public GameObject cornerPrefab;
        public GameObject tJunctionPrefab;
        public GameObject crossPrefab;
        [TextArea] public string description;

        public string prefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(prefab);
        public string prefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(prefab);
        public string straightPrefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(straightPrefab);
        public string straightPrefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(straightPrefab);
        public string cornerPrefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(cornerPrefab);
        public string cornerPrefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(cornerPrefab);
        public string tJunctionPrefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(tJunctionPrefab);
        public string tJunctionPrefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(tJunctionPrefab);
        public string crossPrefabAssetPath => CatalogAssetReferenceUtility.ResolvePrefabAssetPath(crossPrefab);
        public string crossPrefabAssetGuid => CatalogAssetReferenceUtility.ResolvePrefabAssetGuid(crossPrefab);
    }

    [System.Serializable]
    public sealed class EnvironmentCatalogEntry
    {
        public string objectId;
        public string category;
        public string placementMode;
        public string variationMode;
        public EnvironmentDesignVariantEntry[] designs = new EnvironmentDesignVariantEntry[0];
    }

    public sealed class EnvironmentCatalogSectionDefinition
    {
        public string arrayPath = string.Empty;
        public string label = string.Empty;
        public string expectedCategory = string.Empty;
        public string placementMode = string.Empty;
        public EnvironmentCatalogEntry[] entries = new EnvironmentCatalogEntry[0];
    }

    public sealed class PlayableObjectCatalogValidationIssue
    {
        public string sectionPath = string.Empty;
        public int entryIndex = -1;
        public int designIndex = -1;
        public string message = string.Empty;
    }

    public sealed class PlayableObjectCatalogValidationResult
    {
        public readonly List<PlayableObjectCatalogValidationIssue> Errors = new List<PlayableObjectCatalogValidationIssue>();
        public readonly List<PlayableObjectCatalogValidationIssue> Warnings = new List<PlayableObjectCatalogValidationIssue>();
        public readonly List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();

        public bool IsValid => Errors.Count == 0;
        public string Message => IsValid ? "유효합니다." : Errors[0].message;
    }

    /// <summary>에디터/시스템이 관리하는 오브젝트 (Core, UI). LLM에 전달하지 않음. 생성 시 EditorBased 항목으로 인스턴스 생성.</summary>
    [System.Serializable]
    public sealed class EditorBasedCatalog
    {
        [System.Serializable]
        public sealed class Entry
        {
            public string objectId;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];

        public IReadOnlyList<Entry> GetEntries()
        {
            if (_entries == null || _entries.Length == 0)
                return new Entry[0];

            var copy = new Entry[_entries.Length];
            for (int i = 0; i < _entries.Length; i++)
                copy[i] = _entries[i];
            return copy;
        }

        public bool TryGetPrefab(string objectId, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(objectId) || _entries == null)
                return false;

            string normalized = objectId.Trim();
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.objectId) || entry.prefab == null)
                    continue;
                if (!string.Equals(entry.objectId.Trim(), normalized, System.StringComparison.Ordinal))
                    continue;

                prefab = entry.prefab;
                return true;
            }

            return false;
        }

        public void SetEntries(Entry[] entries)
        {
            _entries = entries ?? new Entry[0];
        }
    }

    /// <summary>배치 없이 design만 선택하는 content 카탈로그. ui/core/guide가 여기에 포함됩니다.</summary>
    [System.Serializable]
    public sealed class ContentSelectionCatalog
    {
        [System.Serializable]
        public sealed class Entry
        {
            public string objectId;
            public string category;
            public DesignVariantEntry[] designs = new DesignVariantEntry[0];
        }

        [SerializeField] private Entry[] _entries = new Entry[0];

        public IReadOnlyList<Entry> GetEntries()
        {
            if (_entries == null || _entries.Length == 0)
                return new Entry[0];

            var copy = new Entry[_entries.Length];
            for (int i = 0; i < _entries.Length; i++)
                copy[i] = _entries[i];
            return copy;
        }

        public bool TryGetEntry(string objectId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(objectId) || _entries == null)
                return false;

            string normalizedObjectId = objectId.Trim();
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry candidate = _entries[i];
                if (candidate == null ||
                    string.IsNullOrWhiteSpace(candidate.objectId) ||
                    !HasAnyValidDesign(candidate.designs))
                    continue;

                if (!string.Equals(candidate.objectId.Trim(), normalizedObjectId, System.StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        public bool TryResolvePrefab(string objectId, int requestedDesignIndex, out GameObject prefab, out int resolvedDesignIndex)
        {
            prefab = null;
            resolvedDesignIndex = -1;
            if (!TryGetEntry(objectId, out Entry entry) || entry == null)
                return false;

            if (!TryResolveDesign(entry.designs, requestedDesignIndex, out DesignVariantEntry design, out resolvedDesignIndex))
                return false;

            prefab = design.prefab;
            return prefab != null;
        }

        public bool TryResolveDesign(string objectId, int requestedDesignIndex, out DesignVariantEntry design, out int resolvedDesignIndex)
        {
            design = null;
            resolvedDesignIndex = -1;
            if (!TryGetEntry(objectId, out Entry entry) || entry == null)
                return false;

            return TryResolveDesign(entry.designs, requestedDesignIndex, out design, out resolvedDesignIndex);
        }

        public bool TryResolveDesignIndex(string objectId, string designId, out int resolvedDesignIndex)
        {
            resolvedDesignIndex = -1;
            if (!TryGetEntry(objectId, out Entry entry) || entry == null)
                return false;

            return TryResolveDesignIndex(entry.designs, designId, out resolvedDesignIndex);
        }

        public bool IsValidDesignId(string objectId, string designId)
        {
            return TryResolveDesignIndex(objectId, designId, out _);
        }

        public bool IsValidDesignIndex(string objectId, int designIndex)
        {
            if (!TryGetEntry(objectId, out Entry entry) || entry == null)
                return false;

            return IsValidDesignIndex(entry.designs, designIndex);
        }

        public bool IsSupportedObject(string objectId)
        {
            return TryGetEntry(objectId, out _);
        }

        public void SetEntries(Entry[] entries)
        {
            _entries = entries ?? new Entry[0];
        }

        private static bool HasAnyValidDesign(DesignVariantEntry[] designs)
        {
            if (designs == null)
                return false;

            for (int i = 0; i < designs.Length; i++)
            {
                if (designs[i] != null && designs[i].prefab != null)
                    return true;
            }

            return false;
        }

        private static bool TryResolveDesign(DesignVariantEntry[] designs, int requestedDesignIndex, out DesignVariantEntry design, out int resolvedDesignIndex)
        {
            design = null;
            resolvedDesignIndex = requestedDesignIndex;
            if (!IsValidDesignIndex(designs, resolvedDesignIndex))
                return false;

            design = designs[resolvedDesignIndex];
            return design != null && design.prefab != null;
        }

        private static bool TryResolveDesignIndex(DesignVariantEntry[] designs, string designId, out int resolvedDesignIndex)
        {
            resolvedDesignIndex = -1;
            if (designs == null || string.IsNullOrWhiteSpace(designId))
                return false;

            string normalizedDesignId = Normalize(designId);
            for (int i = 0; i < designs.Length; i++)
            {
                DesignVariantEntry candidate = designs[i];
                if (candidate == null || candidate.prefab == null)
                    continue;

                if (!string.Equals(Normalize(candidate.designId), normalizedDesignId, System.StringComparison.Ordinal))
                    continue;

                resolvedDesignIndex = i;
                return true;
            }

            return false;
        }

        private static bool IsValidDesignIndex(DesignVariantEntry[] designs, int designIndex)
        {
            return designs != null &&
                   designIndex >= 0 &&
                   designIndex < designs.Length &&
                   designs[designIndex] != null &&
                   designs[designIndex].prefab != null;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    /// <summary>시나리오 스펙이 참조하는 gameplay 오브젝트 (Feature, Unlocker, Item, PlayerModel, Customer).</summary>
    [System.Serializable]
    public sealed class GameplayCatalog
    {
        public const string GENERATORS_ARRAY_PATH = "_gameplayCatalog._generators";
        public const string RAILS_ARRAY_PATH = "_gameplayCatalog._rails";
        public const string CONVERTERS_ARRAY_PATH = "_gameplayCatalog._converters";
        public const string SELLERS_ARRAY_PATH = "_gameplayCatalog._sellers";
        public const string UNLOCKERS_ARRAY_PATH = "_gameplayCatalog._unlockers";
        public const string ITEMS_ARRAY_PATH = "_gameplayCatalog._items";
        public const string PLAYER_MODELS_ARRAY_PATH = "_gameplayCatalog._playerModels";
        public const string CUSTOMERS_ARRAY_PATH = "_gameplayCatalog._customers";
        public const string CUSTOM_FEATURES_ARRAY_PATH = "_gameplayCatalog._customFeatures";
        public const string FEATURE_CATEGORY = "feature";
        public const string UNLOCKER_CATEGORY = "unlocker";
        public const string ITEM_CATEGORY = "item";
        public const string PLAYER_MODEL_CATEGORY = "playermodel";
        public const string CUSTOMER_CATEGORY = "customer";

        [SerializeField] private GameplayCatalogEntry[] _generators = new GameplayCatalogEntry[0];
        [SerializeField] private GameplayCatalogEntry[] _rails = new GameplayCatalogEntry[0];
        [SerializeField] private GameplayCatalogEntry[] _converters = new GameplayCatalogEntry[0];
        [SerializeField] private GameplayCatalogEntry[] _sellers = new GameplayCatalogEntry[0];

        [SerializeField] private GameplayCatalogEntry[] _unlockers = new GameplayCatalogEntry[0];

        [SerializeField] private GameplayCatalogEntry[] _items = new GameplayCatalogEntry[0];

        [SerializeField] private GameplayCatalogEntry[] _playerModels = new GameplayCatalogEntry[0];

        [SerializeField] private GameplayCatalogEntry[] _customers = new GameplayCatalogEntry[0];
        [SerializeField] private GameplayCatalogEntry[] _customFeatures = new GameplayCatalogEntry[0];

        public IReadOnlyList<GameplayCatalogEntry> GetGameplayEntries()
        {
            var list = new List<GameplayCatalogEntry>();
            AddGameplayEntries(list, _generators);
            AddGameplayEntries(list, _rails);
            AddGameplayEntries(list, _converters);
            AddGameplayEntries(list, _sellers);
            AddGameplayEntries(list, _unlockers);
            AddGameplayEntries(list, _items);
            AddGameplayEntries(list, _playerModels);
            AddGameplayEntries(list, _customers);
            AddGameplayEntries(list, _customFeatures);
            return list;
        }

        public IReadOnlyList<GameplayCatalogSectionDefinition> GetGameplaySections()
        {
            return new GameplayCatalogSectionDefinition[]
            {
                CreateSection(GENERATORS_ARRAY_PATH, "생성기", FEATURE_CATEGORY, GameplayDesignMode.SinglePrefab, _generators),
                CreateSection(RAILS_ARRAY_PATH, "레일", FEATURE_CATEGORY, GameplayDesignMode.AssembledPath, _rails),
                CreateSection(CONVERTERS_ARRAY_PATH, "변환기", FEATURE_CATEGORY, GameplayDesignMode.SinglePrefab, _converters),
                CreateSection(SELLERS_ARRAY_PATH, "판매기", FEATURE_CATEGORY, GameplayDesignMode.SinglePrefab, _sellers),
                CreateSection(UNLOCKERS_ARRAY_PATH, "언락 패드", UNLOCKER_CATEGORY, GameplayDesignMode.SinglePrefab, _unlockers),
                CreateSection(ITEMS_ARRAY_PATH, "아이템", ITEM_CATEGORY, GameplayDesignMode.SinglePrefab, _items),
                CreateSection(PLAYER_MODELS_ARRAY_PATH, "플레이어 모델", PLAYER_MODEL_CATEGORY, GameplayDesignMode.SinglePrefab, _playerModels),
                CreateSection(CUSTOMERS_ARRAY_PATH, "손님", CUSTOMER_CATEGORY, GameplayDesignMode.SinglePrefab, _customers),
                CreateSection(CUSTOM_FEATURES_ARRAY_PATH, "커스텀 Feature", FEATURE_CATEGORY, GameplayDesignMode.SinglePrefab, _customFeatures),
            };
        }

        public bool TryGetGameplayEntry(string objectId, out GameplayCatalogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(objectId))
                return false;

            string normalized = objectId.Trim();
            return TryFindGameplayEntry(normalized, _generators, out entry) ||
                   TryFindGameplayEntry(normalized, _rails, out entry) ||
                   TryFindGameplayEntry(normalized, _converters, out entry) ||
                   TryFindGameplayEntry(normalized, _sellers, out entry) ||
                   TryFindGameplayEntry(normalized, _unlockers, out entry) ||
                   TryFindGameplayEntry(normalized, _items, out entry) ||
                   TryFindGameplayEntry(normalized, _playerModels, out entry) ||
                   TryFindGameplayEntry(normalized, _customers, out entry) ||
                   TryFindGameplayEntry(normalized, _customFeatures, out entry);
        }

        public bool TryResolveGameplayPrefab(string objectId, int requestedDesignIndex, out GameObject prefab, out int resolvedDesignIndex)
        {
            prefab = null;
            resolvedDesignIndex = -1;
            if (!TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry))
                return false;

            if (!TryResolveDesign(entry.designs, requestedDesignIndex, out DesignVariantEntry design, out resolvedDesignIndex))
                return false;

            prefab = design.prefab;
            return prefab != null;
        }

        public bool TryResolveGameplayDesign(string objectId, int requestedDesignIndex, out DesignVariantEntry design, out int resolvedDesignIndex)
        {
            design = null;
            resolvedDesignIndex = -1;
            if (!TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry))
                return false;

            return TryResolveDesign(entry.designs, requestedDesignIndex, out design, out resolvedDesignIndex);
        }

        public bool TryResolveRailPrefabSet(
            string objectId,
            int requestedDesignIndex,
            out GameObject prefab,
            out GameObject straightPrefab,
            out GameObject cornerPrefab,
            out int resolvedDesignIndex)
        {
            prefab = null;
            straightPrefab = null;
            cornerPrefab = null;
            resolvedDesignIndex = -1;

            if (!TryResolveGameplayDesign(objectId, requestedDesignIndex, out DesignVariantEntry design, out resolvedDesignIndex) || design == null)
                return false;

            if (!TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry) ||
                entry == null ||
                entry.designMode != GameplayDesignMode.AssembledPath)
            {
                return false;
            }

            AssembledPathDesignAssets assets = design.assembledPathAssets ?? new AssembledPathDesignAssets();
            prefab = design.prefab;
            straightPrefab = assets.straightPrefab;
            cornerPrefab = assets.cornerPrefab;
            return prefab != null && straightPrefab != null && cornerPrefab != null;
        }

        public bool TryResolveGameplayDesignIndex(string objectId, string designId, out int resolvedDesignIndex)
        {
            resolvedDesignIndex = -1;
            if (!TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry) || entry == null)
                return false;

            return TryResolveDesignIndex(entry.designs, designId, out resolvedDesignIndex);
        }

        public bool IsValidGameplayDesignIndex(string objectId, int designIndex)
        {
            if (!TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry))
                return false;

            return IsValidDesignIndex(entry.designs, designIndex);
        }

        public bool IsValidGameplayDesignId(string objectId, string designId)
        {
            return TryResolveGameplayDesignIndex(objectId, designId, out _);
        }

        public bool IsSupportedGameplayObject(string objectId)
        {
            return TryGetGameplayEntry(objectId, out _);
        }

        public void SetRoleArrays(
            GameplayCatalogEntry[] generators,
            GameplayCatalogEntry[] rails,
            GameplayCatalogEntry[] converters,
            GameplayCatalogEntry[] sellers,
            GameplayCatalogEntry[] unlockers,
            GameplayCatalogEntry[] items,
            GameplayCatalogEntry[] playerModels,
            GameplayCatalogEntry[] customers,
            GameplayCatalogEntry[] customFeatures = null)
        {
            _generators = generators ?? new GameplayCatalogEntry[0];
            _rails = rails ?? new GameplayCatalogEntry[0];
            _converters = converters ?? new GameplayCatalogEntry[0];
            _sellers = sellers ?? new GameplayCatalogEntry[0];
            _unlockers = unlockers ?? new GameplayCatalogEntry[0];
            _items = items ?? new GameplayCatalogEntry[0];
            _playerModels = playerModels ?? new GameplayCatalogEntry[0];
            _customers = customers ?? new GameplayCatalogEntry[0];
            _customFeatures = customFeatures ?? new GameplayCatalogEntry[0];
        }

        private static GameplayCatalogSectionDefinition CreateSection(string arrayPath, string label, string expectedCategory, GameplayDesignMode designMode, GameplayCatalogEntry[] entries)
        {
            return new GameplayCatalogSectionDefinition
            {
                arrayPath = arrayPath,
                label = label,
                expectedCategory = expectedCategory,
                designMode = designMode,
                entries = entries ?? new GameplayCatalogEntry[0],
            };
        }

        private static void AddGameplayEntries(List<GameplayCatalogEntry> list, GameplayCatalogEntry[] entries)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (IsValidGameplayEntry(entries[i]))
                    list.Add(entries[i]);
            }
        }

        private static bool TryFindGameplayEntry(string objectId, GameplayCatalogEntry[] entries, out GameplayCatalogEntry entry)
        {
            entry = null;
            if (entries == null)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                GameplayCatalogEntry candidate = entries[i];
                if (!IsValidGameplayEntry(candidate))
                    continue;
                if (!string.Equals(candidate.objectId.Trim(), objectId, System.StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        private static bool TryResolveDesign(DesignVariantEntry[] designs, int requestedDesignIndex, out DesignVariantEntry design, out int resolvedDesignIndex)
        {
            design = null;
            resolvedDesignIndex = requestedDesignIndex;
            if (!IsValidDesignIndex(designs, resolvedDesignIndex))
                return false;

            design = designs[resolvedDesignIndex];
            return design != null && design.prefab != null;
        }

        private static bool TryResolveDesignIndex(DesignVariantEntry[] designs, string designId, out int resolvedDesignIndex)
        {
            resolvedDesignIndex = -1;
            if (designs == null || string.IsNullOrWhiteSpace(designId))
                return false;

            string normalizedDesignId = Normalize(designId);
            for (int i = 0; i < designs.Length; i++)
            {
                DesignVariantEntry candidate = designs[i];
                if (candidate == null || candidate.prefab == null)
                    continue;

                if (!string.Equals(Normalize(candidate.designId), normalizedDesignId, System.StringComparison.Ordinal))
                    continue;

                resolvedDesignIndex = i;
                return true;
            }

            return false;
        }

        private static bool IsValidDesignIndex(DesignVariantEntry[] designs, int designIndex)
        {
            return designs != null &&
                   designIndex >= 0 &&
                   designIndex < designs.Length &&
                   designs[designIndex] != null &&
                   designs[designIndex].prefab != null;
        }

        private static bool IsValidGameplayEntry(GameplayCatalogEntry entry)
        {
            return entry != null &&
                   !string.IsNullOrWhiteSpace(entry.objectId) &&
                   HasAnyValidDesign(entry.designs);
        }

        private static bool HasAnyValidDesign(DesignVariantEntry[] designs)
        {
            if (designs == null)
                return false;

            for (int i = 0; i < designs.Length; i++)
            {
                if (designs[i] != null && designs[i].prefab != null)
                    return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

    }

    [System.Serializable]
    public sealed class EnvironmentCatalog
    {
        public const string FLOORS_ARRAY_PATH = "_environmentCatalog._floors";
        public const string WALLS_ARRAY_PATH = "_environmentCatalog._walls";
        public const string FENCES_ARRAY_PATH = "_environmentCatalog._fences";
        public const string ROADS_ARRAY_PATH = "_environmentCatalog._roads";
        public const string FLOOR_CATEGORY = "floor";
        public const string WALL_CATEGORY = "wall";
        public const string FENCE_CATEGORY = "fence";
        public const string ROAD_CATEGORY = "road";
        public const string PLACEMENT_MODE_PERIMETER = "perimeter";
        public const string PLACEMENT_MODE_FILL = "fill";
        public const string VARIATION_MODE_SINGLE = "single";
        public const string VARIATION_MODE_CONNECTED3 = "connected3";

        [SerializeField] private GameObject _floorPrefab;
        [SerializeField] private EnvironmentCatalogEntry[] _floors = new EnvironmentCatalogEntry[0];
        [SerializeField] private EnvironmentCatalogEntry[] _walls = new EnvironmentCatalogEntry[0];
        [SerializeField] private EnvironmentCatalogEntry[] _fences = new EnvironmentCatalogEntry[0];
        [SerializeField] private EnvironmentCatalogEntry[] _roads = new EnvironmentCatalogEntry[0];

        public GameObject FloorPrefab
        {
            get
            {
                if (TryResolveFloorPrefab(string.Empty, out GameObject prefab, out _))
                    return prefab;
                return _floorPrefab;
            }
        }

        public IReadOnlyList<EnvironmentCatalogEntry> GetEnvironmentEntries()
        {
            var list = new List<EnvironmentCatalogEntry>();
            AddEnvironmentEntries(list, _floors);
            AddEnvironmentEntries(list, _walls);
            AddEnvironmentEntries(list, _fences);
            AddEnvironmentEntries(list, _roads);
            return list;
        }

        public IReadOnlyList<EnvironmentCatalogSectionDefinition> GetEnvironmentSections()
        {
            return new EnvironmentCatalogSectionDefinition[]
            {
                CreateSection(FLOORS_ARRAY_PATH, "바닥", FLOOR_CATEGORY, PLACEMENT_MODE_FILL, _floors),
                CreateSection(WALLS_ARRAY_PATH, "벽", WALL_CATEGORY, PLACEMENT_MODE_PERIMETER, _walls),
                CreateSection(FENCES_ARRAY_PATH, "울타리", FENCE_CATEGORY, PLACEMENT_MODE_PERIMETER, _fences),
                CreateSection(ROADS_ARRAY_PATH, "도로", ROAD_CATEGORY, PLACEMENT_MODE_FILL, _roads),
            };
        }

        public bool TryGetFloorDesign(string designId, out EnvironmentDesignVariantEntry design, out int resolvedDesignIndex)
        {
            design = null;
            resolvedDesignIndex = -1;
            string requestedDesignId = Normalize(designId);
            if (!TryFindEnvironmentEntryByCategory(FLOOR_CATEGORY, _floors, out EnvironmentCatalogEntry floorEntry) || floorEntry == null)
                return false;
            EnvironmentDesignVariantEntry[] floorDesigns = floorEntry.designs ?? new EnvironmentDesignVariantEntry[0];
            if (string.IsNullOrEmpty(requestedDesignId))
            {
                for (int index = 0; index < floorDesigns.Length; index++)
                {
                    EnvironmentDesignVariantEntry candidate = floorDesigns[index];
                    if (candidate == null || candidate.prefab == null || string.IsNullOrEmpty(Normalize(candidate.designId)))
                        continue;

                    resolvedDesignIndex = index;
                    design = candidate;
                    return true;
                }

                return false;
            }

            if (!TryResolveEnvironmentDesignIndex(floorDesigns, requestedDesignId, out resolvedDesignIndex))
                return false;

            if (resolvedDesignIndex < 0 || resolvedDesignIndex >= floorDesigns.Length)
                return false;
            design = floorDesigns[resolvedDesignIndex];
            return design != null && design.prefab != null;
        }

        public bool TryResolveFloorPrefab(string designId, out GameObject prefab, out int resolvedDesignIndex)
        {
            prefab = null;
            resolvedDesignIndex = -1;
            if (!TryGetFloorDesign(designId, out EnvironmentDesignVariantEntry design, out resolvedDesignIndex) || design == null)
                return false;

            prefab = design.prefab;
            return prefab != null;
        }

        public bool TryGetEnvironmentEntry(string objectId, out EnvironmentCatalogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(objectId))
                return false;

            string normalized = objectId.Trim();
            return TryFindEnvironmentEntry(normalized, _floors, out entry) ||
                   TryFindEnvironmentEntry(normalized, _walls, out entry) ||
                   TryFindEnvironmentEntry(normalized, _fences, out entry) ||
                   TryFindEnvironmentEntry(normalized, _roads, out entry);
        }

        public bool TryResolveEnvironmentDesignIndex(string objectId, string designId, out int resolvedDesignIndex)
        {
            resolvedDesignIndex = -1;
            if (!TryGetEnvironmentEntry(objectId, out EnvironmentCatalogEntry entry) || entry == null)
                return false;

            return TryResolveEnvironmentDesignIndex(entry.designs, designId, out resolvedDesignIndex);
        }

        public bool TryGetEnvironmentDesign(
            string objectId,
            string designId,
            out EnvironmentDesignVariantEntry design,
            out string placementMode,
            out int resolvedDesignIndex)
        {
            return TryGetEnvironmentDesign(
                objectId,
                designId,
                out design,
                out placementMode,
                out _,
                out resolvedDesignIndex);
        }

        public bool TryGetEnvironmentDesign(
            string objectId,
            string designId,
            out EnvironmentDesignVariantEntry design,
            out string placementMode,
            out string variationMode,
            out int resolvedDesignIndex)
        {
            design = null;
            placementMode = string.Empty;
            variationMode = string.Empty;
            resolvedDesignIndex = -1;

            string normalizedObjectId = Normalize(objectId);
            string requestedDesignId = Normalize(designId);
            if (!TryGetEnvironmentEntry(normalizedObjectId, out EnvironmentCatalogEntry entry) || entry == null)
                return false;

            if (!TryResolveEnvironmentPolicies(normalizedObjectId, out placementMode, out variationMode))
                return false;

            if (!TryResolveEnvironmentDesignIndex(entry.designs, requestedDesignId, out resolvedDesignIndex))
                return false;

            EnvironmentDesignVariantEntry[] designs = entry.designs ?? new EnvironmentDesignVariantEntry[0];
            if (resolvedDesignIndex < 0 || resolvedDesignIndex >= designs.Length)
                return false;

            design = designs[resolvedDesignIndex];
            return design != null && design.prefab != null;
        }

        public bool TryResolvePlacementMode(string objectId, out string placementMode)
        {
            placementMode = string.Empty;
            string normalized = Normalize(objectId);
            if (string.IsNullOrEmpty(normalized))
                return false;

            return TryResolveEnvironmentPolicies(normalized, out placementMode, out _);
        }

        public bool TryResolveVariationMode(string objectId, out string variationMode)
        {
            variationMode = string.Empty;
            string normalized = Normalize(objectId);
            if (string.IsNullOrEmpty(normalized))
                return false;

            return TryResolveEnvironmentPolicies(normalized, out _, out variationMode);
        }

        public bool TryResolveEnvironmentPrefabs(
            string objectId,
            string designId,
            out GameObject prefab,
            out GameObject cornerPrefab,
            out string placementMode,
            out int resolvedDesignIndex)
        {
            prefab = null;
            cornerPrefab = null;
            placementMode = string.Empty;
            resolvedDesignIndex = -1;

            if (!TryGetEnvironmentDesign(objectId, designId, out EnvironmentDesignVariantEntry design, out placementMode, out resolvedDesignIndex) || design == null)
                return false;

            prefab = design.prefab;
            cornerPrefab = design.cornerPrefab;
            return prefab != null && cornerPrefab != null;
        }

        public void SetFloorPrefab(GameObject prefab)
        {
            _floorPrefab = prefab;
        }

        public void SetRoleArrays(EnvironmentCatalogEntry[] walls, EnvironmentCatalogEntry[] fences, EnvironmentCatalogEntry[] roads)
        {
            SetRoleArrays(new EnvironmentCatalogEntry[0], walls, fences, roads);
        }

        public void SetRoleArrays(EnvironmentCatalogEntry[] floors, EnvironmentCatalogEntry[] walls, EnvironmentCatalogEntry[] fences, EnvironmentCatalogEntry[] roads)
        {
            _floors = floors ?? new EnvironmentCatalogEntry[0];
            _walls = walls ?? new EnvironmentCatalogEntry[0];
            _fences = fences ?? new EnvironmentCatalogEntry[0];
            _roads = roads ?? new EnvironmentCatalogEntry[0];
        }

        private static EnvironmentCatalogSectionDefinition CreateSection(string arrayPath, string label, string expectedCategory, string placementMode, EnvironmentCatalogEntry[] entries)
        {
            return new EnvironmentCatalogSectionDefinition
            {
                arrayPath = arrayPath,
                label = label,
                expectedCategory = expectedCategory,
                placementMode = placementMode,
                entries = entries ?? new EnvironmentCatalogEntry[0],
            };
        }

        private static void AddEnvironmentEntries(List<EnvironmentCatalogEntry> list, EnvironmentCatalogEntry[] entries)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (IsValidEnvironmentEntry(entries[i]))
                    list.Add(entries[i]);
            }
        }

        private static bool TryFindEnvironmentEntry(string objectId, EnvironmentCatalogEntry[] entries, out EnvironmentCatalogEntry entry)
        {
            entry = null;
            if (entries == null)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                EnvironmentCatalogEntry candidate = entries[i];
                if (!IsValidEnvironmentEntry(candidate))
                    continue;
                if (!string.Equals(Normalize(candidate.objectId), objectId, System.StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        private static bool TryFindEnvironmentEntryByCategory(string category, EnvironmentCatalogEntry[] entries, out EnvironmentCatalogEntry entry)
        {
            entry = null;
            if (entries == null)
                return false;

            string normalizedCategory = Normalize(category);
            for (int i = 0; i < entries.Length; i++)
            {
                EnvironmentCatalogEntry candidate = entries[i];
                if (!IsValidEnvironmentEntry(candidate))
                    continue;
                if (!string.Equals(Normalize(candidate.category), normalizedCategory, System.StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        private static bool TryResolveEnvironmentDesignIndex(EnvironmentDesignVariantEntry[] designs, string designId, out int resolvedDesignIndex)
        {
            resolvedDesignIndex = -1;
            if (designs == null || string.IsNullOrWhiteSpace(designId))
                return false;

            string normalizedDesignId = Normalize(designId);
            for (int i = 0; i < designs.Length; i++)
            {
                EnvironmentDesignVariantEntry candidate = designs[i];
                if (candidate == null || candidate.prefab == null)
                    continue;

                if (!string.Equals(Normalize(candidate.designId), normalizedDesignId, System.StringComparison.Ordinal))
                    continue;

                resolvedDesignIndex = i;
                return true;
            }

            return false;
        }

        private bool TryResolveEnvironmentPolicies(
            string objectId,
            out string placementMode,
            out string variationMode)
        {
            placementMode = string.Empty;
            variationMode = string.Empty;
            string normalizedObjectId = Normalize(objectId);
            if (string.IsNullOrEmpty(normalizedObjectId))
                return false;

            if (TryFindEnvironmentEntry(normalizedObjectId, _floors, out EnvironmentCatalogEntry floorEntry))
            {
                return TryResolvePlacementMode(floorEntry, out placementMode) &&
                       TryResolveVariationMode(floorEntry, out variationMode);
            }

            if (TryFindEnvironmentEntry(normalizedObjectId, _walls, out EnvironmentCatalogEntry wallEntry))
            {
                return TryResolvePlacementMode(wallEntry, out placementMode) &&
                       TryResolveVariationMode(wallEntry, out variationMode);
            }

            if (TryFindEnvironmentEntry(normalizedObjectId, _fences, out EnvironmentCatalogEntry fenceEntry))
            {
                return TryResolvePlacementMode(fenceEntry, out placementMode) &&
                       TryResolveVariationMode(fenceEntry, out variationMode);
            }

            if (TryFindEnvironmentEntry(normalizedObjectId, _roads, out EnvironmentCatalogEntry roadEntry))
            {
                return TryResolvePlacementMode(roadEntry, out placementMode) &&
                       TryResolveVariationMode(roadEntry, out variationMode);
            }

            return false;
        }

        private static bool TryResolvePlacementMode(EnvironmentCatalogEntry entry, out string placementMode)
        {
            placementMode = Normalize(entry != null ? entry.placementMode : string.Empty);
            return IsSupportedPlacementMode(placementMode);
        }

        private static bool TryResolveVariationMode(EnvironmentCatalogEntry entry, out string variationMode)
        {
            variationMode = Normalize(entry != null ? entry.variationMode : string.Empty);
            return IsSupportedVariationMode(variationMode);
        }

        private static bool IsSupportedPlacementMode(string placementMode)
        {
            return string.Equals(placementMode, PLACEMENT_MODE_PERIMETER, System.StringComparison.Ordinal) ||
                   string.Equals(placementMode, PLACEMENT_MODE_FILL, System.StringComparison.Ordinal);
        }

        private static bool IsSupportedVariationMode(string variationMode)
        {
            return string.Equals(variationMode, VARIATION_MODE_SINGLE, System.StringComparison.Ordinal) ||
                   string.Equals(variationMode, VARIATION_MODE_CONNECTED3, System.StringComparison.Ordinal);
        }

        private static bool IsValidEnvironmentEntry(EnvironmentCatalogEntry entry)
        {
            return entry != null &&
                   !string.IsNullOrWhiteSpace(entry.objectId) &&
                   HasAnyValidEnvironmentDesign(entry.designs);
        }

        private static bool HasAnyValidEnvironmentDesign(EnvironmentDesignVariantEntry[] designs)
        {
            if (designs == null)
                return false;

            for (int i = 0; i < designs.Length; i++)
            {
                if (designs[i] != null && designs[i].prefab != null)
                    return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    /// <summary>EditorBasedCatalog + ContentSelectionCatalog + GameplayCatalog + EnvironmentCatalog.</summary>
    [CreateAssetMenu(fileName = "playable_object_catalog", menuName = "AIPS/Settings/Object Catalog (오브젝트 카탈로그)")]
    public sealed class PlayableObjectCatalog : ScriptableObject
    {
        [Tooltip("스캔 루트. 예: Assets/Supercent/PlayableAI/Concepts/PizzaReady/Prefabs")]
        [SerializeField] private string _prefabsRootPath = string.Empty;

        [Tooltip("에디터/시스템용 (Core, UI). 생성 시 먼저 인스턴스화합니다.")]
        [SerializeField] private EditorBasedCatalog _editorBased = new EditorBasedCatalog();

        [Tooltip("배치 없이 design만 선택하는 content. ui/core/guide 등 agent가 고르는 비배치 오브젝트를 담습니다.")]
        [SerializeField] private ContentSelectionCatalog _contentSelections = new ContentSelectionCatalog();

        [Tooltip("시나리오 스펙이 참조하는 gameplay 오브젝트. Feature/Unlocker/PlayerModel/Customer는 gameplay placements, runtime-owned Item/Customer는 objectDesign selection에 사용합니다.")]
        [SerializeField] private GameplayCatalog _gameplayCatalog = new GameplayCatalog();

        [Tooltip("Step 3 image layout에서 사용하는 environment 오브젝트. wall/road/fence 등의 explicit layout 배치를 담당합니다.")]
        [SerializeField] private EnvironmentCatalog _environmentCatalog = new EnvironmentCatalog();

        [Tooltip("시나리오 스펙의 themeId 고정값.")]
        [SerializeField] private string _themeId = string.Empty;

        [Tooltip("현재 세션에서 사용 가능한 feature descriptor authority.")]
        [SerializeField] private FeatureDescriptor[] _featureDescriptors = Array.Empty<FeatureDescriptor>();

        public string PrefabsRootPath => _prefabsRootPath ?? string.Empty;
        public EditorBasedCatalog EditorBased => _editorBased ?? new EditorBasedCatalog();
        public ContentSelectionCatalog ContentSelections => _contentSelections ?? new ContentSelectionCatalog();
        public GameplayCatalog Gameplay => _gameplayCatalog ?? new GameplayCatalog();
        public EnvironmentCatalog Environment => _environmentCatalog ?? new EnvironmentCatalog();
        public string ThemeId => _themeId ?? string.Empty;
        public GameObject EnvironmentFloorPrefab => Environment.FloorPrefab;
        public FeatureDescriptor[] FeatureDescriptors => FeatureDescriptorUtility.CloneArray(_featureDescriptors);

        public void SetThemeId(string themeId)
        {
            _themeId = themeId ?? string.Empty;
        }

        public void SetFeatureDescriptors(FeatureDescriptor[] descriptors)
        {
            _featureDescriptors = FeatureDescriptorUtility.CloneArray(descriptors);
        }

        public bool IsSupportedFeatureType(string featureType)
        {
            string normalizedFeatureType = FeatureDescriptorUtility.Normalize(featureType);
            if (string.IsNullOrEmpty(normalizedFeatureType))
                return false;

            FeatureDescriptor[] safeDescriptors = _featureDescriptors ?? Array.Empty<FeatureDescriptor>();
            for (int i = 0; i < safeDescriptors.Length; i++)
            {
                FeatureDescriptor descriptor = safeDescriptors[i];
                if (descriptor == null)
                    continue;

                if (string.Equals(
                        FeatureDescriptorUtility.Normalize(descriptor.featureType),
                        normalizedFeatureType,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFeatureDescriptor(string featureType, out FeatureDescriptor descriptor)
        {
            string normalizedFeatureType = FeatureDescriptorUtility.Normalize(featureType);
            FeatureDescriptor[] safeDescriptors = _featureDescriptors ?? Array.Empty<FeatureDescriptor>();
            for (int i = 0; i < safeDescriptors.Length; i++)
            {
                FeatureDescriptor candidate = safeDescriptors[i];
                if (candidate == null)
                    continue;

                if (!string.Equals(
                        FeatureDescriptorUtility.Normalize(candidate.featureType),
                        normalizedFeatureType,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                descriptor = FeatureDescriptorUtility.Clone(candidate);
                return true;
            }

            descriptor = new FeatureDescriptor();
            return false;
        }

        public IReadOnlyList<GameplayCatalogEntry> GetGameplayEntries()
        {
            return Gameplay.GetGameplayEntries();
        }

        public IReadOnlyList<GameplayCatalogSectionDefinition> GetGameplaySections()
        {
            return Gameplay.GetGameplaySections();
        }

        public IReadOnlyList<EnvironmentCatalogEntry> GetEnvironmentEntries()
        {
            return Environment.GetEnvironmentEntries();
        }

        public IReadOnlyList<EnvironmentCatalogSectionDefinition> GetEnvironmentSections()
        {
            return Environment.GetEnvironmentSections();
        }

        public bool TryGetGameplayEntry(string objectId, out GameplayCatalogEntry entry)
        {
            return Gameplay.TryGetGameplayEntry(objectId, out entry);
        }

        public bool TryResolveGameplayPrefab(string objectId, int requestedDesignIndex, out GameObject prefab, out int resolvedDesignIndex)
        {
            return Gameplay.TryResolveGameplayPrefab(objectId, requestedDesignIndex, out prefab, out resolvedDesignIndex);
        }

        public bool TryResolveRailPrefabSet(
            string objectId,
            int requestedDesignIndex,
            out GameObject prefab,
            out GameObject straightPrefab,
            out GameObject cornerPrefab,
            out int resolvedDesignIndex)
        {
            return Gameplay.TryResolveRailPrefabSet(
                objectId,
                requestedDesignIndex,
                out prefab,
                out straightPrefab,
                out cornerPrefab,
                out resolvedDesignIndex);
        }

        public bool TryResolveEnvironmentPrefabs(string objectId, string designId, out GameObject prefab, out GameObject cornerPrefab, out string placementMode, out int resolvedDesignIndex)
        {
            return Environment.TryResolveEnvironmentPrefabs(objectId, designId, out prefab, out cornerPrefab, out placementMode, out resolvedDesignIndex);
        }

        public bool TryGetEditorPrefab(string objectId, out GameObject prefab)
        {
            return EditorBased.TryGetPrefab(objectId, out prefab);
        }

        public IReadOnlyList<ContentSelectionCatalog.Entry> GetContentSelectionEntries()
        {
            return ContentSelections.GetEntries();
        }

        public bool TryGetContentSelectionEntry(string objectId, out ContentSelectionCatalog.Entry entry)
        {
            return ContentSelections.TryGetEntry(objectId, out entry);
        }

        public bool TryResolveContentSelectionPrefab(string objectId, int requestedDesignIndex, out GameObject prefab, out int resolvedDesignIndex)
        {
            return ContentSelections.TryResolvePrefab(objectId, requestedDesignIndex, out prefab, out resolvedDesignIndex);
        }

        public bool TryResolveContentSelectionDesign(string objectId, int requestedDesignIndex, out DesignVariantEntry design, out int resolvedDesignIndex)
        {
            return ContentSelections.TryResolveDesign(objectId, requestedDesignIndex, out design, out resolvedDesignIndex);
        }

        public bool TryResolveContentSelectionDesignIndex(string objectId, string designId, out int resolvedDesignIndex)
        {
            return ContentSelections.TryResolveDesignIndex(objectId, designId, out resolvedDesignIndex);
        }

        public bool IsValidContentSelectionDesignIndex(string objectId, int designIndex)
        {
            return ContentSelections.IsValidDesignIndex(objectId, designIndex);
        }

        public bool IsValidContentSelectionDesignId(string objectId, string designId)
        {
            return ContentSelections.IsValidDesignId(objectId, designId);
        }

        public bool IsSupportedContentSelectionObject(string objectId)
        {
            return ContentSelections.IsSupportedObject(objectId);
        }

        public bool TryGetEnvironmentEntry(string objectId, out EnvironmentCatalogEntry entry)
        {
            return Environment.TryGetEnvironmentEntry(objectId, out entry);
        }

        public bool IsSupportedGameplayObject(string objectId)
        {
            return Gameplay.IsSupportedGameplayObject(objectId);
        }

        public bool IsSupportedEnvironmentObject(string objectId)
        {
            return Environment.TryGetEnvironmentEntry(objectId, out _);
        }

        public bool IsSupportedItemStableKey(string itemStableKey)
        {
            if (!Gameplay.TryGetGameplayEntry(itemStableKey, out GameplayCatalogEntry entry) || entry == null)
                return false;

            string normalizedObjectId = itemStableKey != null ? itemStableKey.Trim() : string.Empty;
            return string.Equals(entry.category, GameplayCatalog.ITEM_CATEGORY, System.StringComparison.Ordinal) &&
                   !string.Equals(normalizedObjectId, "money", System.StringComparison.Ordinal);
        }

        public bool IsValidGameplayDesignIndex(string objectId, int designIndex)
        {
            return Gameplay.IsValidGameplayDesignIndex(objectId, designIndex);
        }

        public bool IsValidGameplayDesignId(string objectId, string designId)
        {
            return Gameplay.IsValidGameplayDesignId(objectId, designId);
        }

        public bool TryGetEnvironmentDesign(
            string objectId,
            string designId,
            out EnvironmentDesignVariantEntry design,
            out string placementMode,
            out int resolvedDesignIndex)
        {
            return Environment.TryGetEnvironmentDesign(objectId, designId, out design, out placementMode, out resolvedDesignIndex);
        }

        public bool TryResolveEnvironmentPlacementMode(string objectId, out string placementMode)
        {
            return Environment.TryResolvePlacementMode(objectId, out placementMode);
        }

        public bool TryResolveEnvironmentVariationMode(string objectId, out string variationMode)
        {
            return Environment.TryResolveVariationMode(objectId, out variationMode);
        }

        public bool TryGetEnvironmentDesign(
            string objectId,
            string designId,
            out EnvironmentDesignVariantEntry design,
            out string placementMode,
            out string variationMode,
            out int resolvedDesignIndex)
        {
            return Environment.TryGetEnvironmentDesign(objectId, designId, out design, out placementMode, out variationMode, out resolvedDesignIndex);
        }

        public bool TryResolveGameplayDesignIndex(string objectId, string designId, out int resolvedDesignIndex)
        {
            return Gameplay.TryResolveGameplayDesignIndex(objectId, designId, out resolvedDesignIndex);
        }

        public bool TryResolveEnvironmentDesignIndex(string objectId, string designId, out int resolvedDesignIndex)
        {
            return Environment.TryResolveEnvironmentDesignIndex(objectId, designId, out resolvedDesignIndex);
        }

        public bool TryResolveGameplayPlacementFootprint(string objectId, string designId, out int widthCells, out int depthCells, out string error)
        {
            return TryResolveGameplayPlacementFootprint(
                objectId,
                designId,
                PlacementFootprintResolutionMode.RuntimePrefabThenMetadata,
                out widthCells,
                out depthCells,
                out _,
                out _,
                out error);
        }

        public bool TryResolveGameplayPlacementFootprint(
            string objectId,
            string designId,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ,
            out string error)
        {
            return TryResolveGameplayPlacementFootprint(
                objectId,
                designId,
                PlacementFootprintResolutionMode.RuntimePrefabThenMetadata,
                out widthCells,
                out depthCells,
                out centerOffsetX,
                out centerOffsetZ,
                out error);
        }

        public bool TryResolveGameplayPlacementFootprintFromCatalogMetadata(
            string objectId,
            string designId,
            out int widthCells,
            out int depthCells,
            out string error)
        {
            return TryResolveGameplayPlacementFootprintFromCatalogMetadata(
                objectId,
                designId,
                out widthCells,
                out depthCells,
                out _,
                out _,
                out error);
        }

        public bool TryResolveGameplayPlacementFootprintFromCatalogMetadata(
            string objectId,
            string designId,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ,
            out string error)
        {
            return TryResolveGameplayPlacementFootprint(
                objectId,
                designId,
                PlacementFootprintResolutionMode.CatalogMetadataOnly,
                out widthCells,
                out depthCells,
                out centerOffsetX,
                out centerOffsetZ,
                out error);
        }

        private bool TryResolveGameplayPlacementFootprint(
            string objectId,
            string designId,
            PlacementFootprintResolutionMode resolutionMode,
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

            string normalizedObjectId = Normalize(objectId);
            string normalizedDesignId = Normalize(designId);
            string requestedDesignId = normalizedDesignId;
            if (!Gameplay.TryGetGameplayEntry(normalizedObjectId, out GameplayCatalogEntry entry) || entry == null)
            {
                error = BuildGameplayDesignResolutionError(normalizedObjectId, requestedDesignId);
                return false;
            }

            if (!Gameplay.TryResolveGameplayDesignIndex(normalizedObjectId, requestedDesignId, out int resolvedDesignIndex))
            {
                error = BuildGameplayDesignResolutionError(normalizedObjectId, requestedDesignId);
                return false;
            }

            DesignVariantEntry[] designs = entry.designs ?? new DesignVariantEntry[0];
            if (resolvedDesignIndex < 0 || resolvedDesignIndex >= designs.Length)
            {
                error = "objectId '" + normalizedObjectId + "'의 designIndex '" + resolvedDesignIndex + "'가 유효하지 않습니다.";
                return false;
            }

            DesignVariantEntry design = designs[resolvedDesignIndex];
            if (design == null || design.prefab == null)
            {
                error = "objectId '" + normalizedObjectId + "'의 designId '" + requestedDesignId + "' prefab이 없습니다.";
                return false;
            }

            if (TryReadPlacementFootprint(design.prefab, resolutionMode, out widthCells, out depthCells, out centerOffsetX, out centerOffsetZ))
                return true;

            string normalizedCategory = Normalize(entry.category);
            if (string.Equals(normalizedCategory, GameplayCatalog.UNLOCKER_CATEGORY, System.StringComparison.Ordinal) ||
                string.Equals(normalizedCategory, GameplayCatalog.PLAYER_MODEL_CATEGORY, System.StringComparison.Ordinal))
            {
                widthCells = 1;
                depthCells = 1;
                centerOffsetX = 0f;
                centerOffsetZ = 0f;
                return true;
            }

            string footprintSource = resolutionMode == PlacementFootprintResolutionMode.CatalogMetadataOnly
                ? "prefab metadata"
                : "prefab component 또는 metadata";
            error = "objectId '" + normalizedObjectId + "'의 designId '" + requestedDesignId + "' " + footprintSource + "에 placement footprint가 없습니다.";
            return false;
        }

        public void SetPrefabsRootPath(string path)
        {
            _prefabsRootPath = path ?? string.Empty;
        }

        public void SetEditorEntries(EditorBasedCatalog.Entry[] entries)
        {
            if (_editorBased == null)
                _editorBased = new EditorBasedCatalog();

            _editorBased.SetEntries(entries);
        }

        public void SetContentSelectionEntries(ContentSelectionCatalog.Entry[] entries)
        {
            if (_contentSelections == null)
                _contentSelections = new ContentSelectionCatalog();

            _contentSelections.SetEntries(entries);
        }

        public void SetGameplayRoleArrays(
            GameplayCatalogEntry[] generators,
            GameplayCatalogEntry[] rails,
            GameplayCatalogEntry[] converters,
            GameplayCatalogEntry[] sellers,
            GameplayCatalogEntry[] unlockers,
            GameplayCatalogEntry[] items,
            GameplayCatalogEntry[] playerModels,
            GameplayCatalogEntry[] customers,
            GameplayCatalogEntry[] customFeatures = null)
        {
            if (_gameplayCatalog == null)
                _gameplayCatalog = new GameplayCatalog();

            _gameplayCatalog.SetRoleArrays(generators, rails, converters, sellers, unlockers, items, playerModels, customers, customFeatures);
        }

        public void SetEnvironmentRoleArrays(EnvironmentCatalogEntry[] walls, EnvironmentCatalogEntry[] fences, EnvironmentCatalogEntry[] roads)
        {
            SetEnvironmentRoleArrays(new EnvironmentCatalogEntry[0], walls, fences, roads);
        }

        public void SetEnvironmentRoleArrays(EnvironmentCatalogEntry[] floors, EnvironmentCatalogEntry[] walls, EnvironmentCatalogEntry[] fences, EnvironmentCatalogEntry[] roads)
        {
            if (_environmentCatalog == null)
                _environmentCatalog = new EnvironmentCatalog();

            _environmentCatalog.SetRoleArrays(floors, walls, fences, roads);
        }

        public void SetEnvironmentFloorPrefab(GameObject prefab)
        {
            if (_environmentCatalog == null)
                _environmentCatalog = new EnvironmentCatalog();

            _environmentCatalog.SetFloorPrefab(prefab);
        }

        private static bool TryReadPlacementFootprint(
            GameObject prefab,
            PlacementFootprintResolutionMode resolutionMode,
            out int widthCells,
            out int depthCells,
            out float centerOffsetX,
            out float centerOffsetZ)
        {
            switch (resolutionMode)
            {
                case PlacementFootprintResolutionMode.RuntimePrefabThenMetadata:
                    return CatalogAssetReferenceUtility.TryReadPlacementFootprintForRuntime(prefab, out widthCells, out depthCells, out centerOffsetX, out centerOffsetZ);
                case PlacementFootprintResolutionMode.CatalogMetadataOnly:
                default:
                    return CatalogAssetReferenceUtility.TryReadPlacementFootprintFromCatalogMetadata(prefab, out widthCells, out depthCells, out centerOffsetX, out centerOffsetZ);
            }
        }

        private string BuildGameplayDesignResolutionError(string objectId, string designId)
        {
            string normalizedObjectId = Normalize(objectId);
            string normalizedDesignId = Normalize(designId);
            string missingDesignLabel = string.IsNullOrEmpty(normalizedDesignId) ? "(empty)" : normalizedDesignId;
            var segments = new List<string>();

            string[] availableObjectIds = BuildGameplayObjectIdCandidates(normalizedObjectId);
            if (availableObjectIds.Length > 0)
                segments.Add("사용 가능한 objectId: [" + string.Join(", ", availableObjectIds) + "]");

            string[] availableDesignIds = TryGetAvailableGameplayDesignIds(normalizedObjectId);
            if (availableDesignIds.Length > 0)
                segments.Add("사용 가능한 designId: [" + string.Join(", ", availableDesignIds) + "]");

            string guidance = segments.Count > 0
                ? " -> 수정 가이드: " + string.Join("; ", segments) + "."
                : " -> 수정 가이드: catalog의 objectId와 designId를 확인하세요.";
            return "objectId '" + normalizedObjectId + "'의 designId '" + missingDesignLabel + "'를 catalog gameplay design으로 해석하지 못했습니다." + guidance;
        }

        private string[] BuildGameplayObjectIdCandidates(string requestedObjectId)
        {
            if (string.IsNullOrEmpty(requestedObjectId))
                return new string[0];

            var exactMatches = new HashSet<string>(System.StringComparer.Ordinal);
            var fuzzyMatches = new HashSet<string>(System.StringComparer.Ordinal);
            IReadOnlyList<GameplayCatalogEntry> entries = Gameplay.GetGameplayEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                GameplayCatalogEntry entry = entries[i];
                string candidateObjectId = entry != null ? Normalize(entry.objectId) : string.Empty;
                if (string.IsNullOrEmpty(candidateObjectId))
                    continue;

                if (string.Equals(candidateObjectId, requestedObjectId, System.StringComparison.Ordinal))
                {
                    exactMatches.Add(candidateObjectId);
                    continue;
                }

                if (candidateObjectId.IndexOf(requestedObjectId, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    requestedObjectId.IndexOf(candidateObjectId, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fuzzyMatches.Add(candidateObjectId);
                }
            }

            if (exactMatches.Count > 0)
                return new List<string>(exactMatches).ToArray();

            var values = new List<string>(fuzzyMatches);
            values.Sort(System.StringComparer.Ordinal);
            if (values.Count > 5)
                values.RemoveRange(5, values.Count - 5);
            return values.ToArray();
        }

        private string[] TryGetAvailableGameplayDesignIds(string objectId)
        {
            if (!Gameplay.TryGetGameplayEntry(objectId, out GameplayCatalogEntry entry) || entry == null)
                return new string[0];

            DesignVariantEntry[] designs = entry.designs ?? new DesignVariantEntry[0];
            var values = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < designs.Length; i++)
            {
                string designId = designs[i] != null ? Normalize(designs[i].designId) : string.Empty;
                if (string.IsNullOrEmpty(designId) || !seen.Add(designId))
                    continue;

                values.Add(designId);
            }

            values.Sort(System.StringComparer.Ordinal);
            return values.ToArray();
        }


        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public static class PlayableObjectCatalogContractValidator
    {
        public const string DEFAULT_DESIGN_ID = GeneratedContentCatalogContracts.DEFAULT_DESIGN_ID;

        public static PlayableObjectCatalogValidationResult Validate(PlayableObjectCatalog catalog)
        {
            var result = new PlayableObjectCatalogValidationResult();
            if (catalog == null)
            {
                AddError(result, string.Empty, -1, -1, "PlayableObjectCatalog가 필요합니다.");
                return result;
            }

            IReadOnlyList<GameplayCatalogSectionDefinition> sections = catalog.GetGameplaySections();
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
                ValidateGameplaySection(sections[sectionIndex], result);

            IReadOnlyList<EnvironmentCatalogSectionDefinition> environmentSections = catalog.GetEnvironmentSections();
            for (int sectionIndex = 0; sectionIndex < environmentSections.Count; sectionIndex++)
                ValidateEnvironmentSection(environmentSections[sectionIndex], result);

            return result;
        }

        private static void ValidateGameplaySection(GameplayCatalogSectionDefinition section, PlayableObjectCatalogValidationResult result)
        {
            GameplayCatalogEntry[] entries = section != null ? section.entries : null;
            if (entries == null)
                return;

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                ValidateGameplayEntry(section, entries[entryIndex], entryIndex, result);
        }

        private static void ValidateGameplayEntry(GameplayCatalogSectionDefinition section, GameplayCatalogEntry entry, int entryIndex, PlayableObjectCatalogValidationResult result)
        {
            string sectionPath = section != null ? section.arrayPath : string.Empty;
            string expectedCategory = Normalize(section != null ? section.expectedCategory : string.Empty);
            if (entry == null)
            {
                AddError(result, sectionPath, entryIndex, -1, BuildEntryLabel(section, entryIndex, string.Empty) + "가 null입니다.");
                return;
            }

            string objectId = Normalize(entry.objectId);
            string category = Normalize(entry.category);
            string entryLabel = BuildEntryLabel(section, entryIndex, objectId);

            if (string.IsNullOrEmpty(objectId))
            {
                AddError(result, sectionPath, entryIndex, -1, new ValidationIssueRecord(
                    ValidationRuleId.CATALOG_OBJECT_ID_MISSING,
                    ValidationSeverity.Blocker,
                    entryLabel + "의 objectId는 비어 있을 수 없습니다.",
                    "CatalogContract")
                    .WithRelated("sectionPath", sectionPath ?? string.Empty)
                    .WithRelated("expectedCategory", expectedCategory ?? string.Empty)
                    .WithFixHint("section '" + (sectionPath ?? string.Empty) + "' entry[" + entryIndex + "]에 objectId를 채워라."));
            }

            if (string.IsNullOrEmpty(category))
            {
                AddError(result, sectionPath, entryIndex, -1, new ValidationIssueRecord(
                    ValidationRuleId.CATALOG_SECTION_CATEGORY_MISMATCH,
                    ValidationSeverity.Blocker,
                    entryLabel + "의 category는 비어 있을 수 없습니다.",
                    "CatalogContract")
                    .WithRelated("objectId", objectId ?? string.Empty)
                    .WithRelated("actualCategory", string.Empty)
                    .WithRelated("expectedCategory", expectedCategory ?? string.Empty)
                    .WithRelated("sectionPath", sectionPath ?? string.Empty)
                    .WithFixHint("entry의 category 필드를 '" + expectedCategory + "'로 채워라."));
            }
            else if (!string.Equals(category, expectedCategory, System.StringComparison.Ordinal))
            {
                string sectionExpected = section != null ? section.expectedCategory : string.Empty;
                AddError(result, sectionPath, entryIndex, -1, new ValidationIssueRecord(
                    ValidationRuleId.CATALOG_SECTION_CATEGORY_MISMATCH,
                    ValidationSeverity.Blocker,
                    entryLabel + "의 category '" + entry.category + "'는 섹션 category '" + sectionExpected + "'와 정확히 일치해야 합니다.",
                    "CatalogContract")
                    .WithRelated("objectId", objectId ?? string.Empty)
                    .WithRelated("actualCategory", entry.category ?? string.Empty)
                    .WithRelated("expectedCategory", sectionExpected ?? string.Empty)
                    .WithRelated("sectionPath", sectionPath ?? string.Empty)
                    .WithFixHint("Catalog entry category를 '" + sectionExpected + "'로 바꾸거나, 이 entry를 정확한 section으로 옮겨라."));
            }

            DesignVariantEntry[] designs = entry.designs ?? new DesignVariantEntry[0];
            if (designs.Length == 0)
            {
                AddError(result, sectionPath, entryIndex, -1, new ValidationIssueRecord(
                    ValidationRuleId.CATALOG_DESIGN_LIST_EMPTY,
                    ValidationSeverity.Blocker,
                    entryLabel + "에는 최소 1개의 design이 필요합니다.",
                    "CatalogContract")
                    .WithRelated("objectId", objectId ?? string.Empty)
                    .WithRelated("sectionPath", sectionPath ?? string.Empty)
                    .WithFixHint("entry의 designs[]에 최소 1개의 design을 추가하라."));
                return;
            }

            var seenDesignIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int designIndex = 0; designIndex < designs.Length; designIndex++)
            {
                DesignVariantEntry design = designs[designIndex];
                string designLabel = entryLabel + " design[" + designIndex + "]";
                if (design == null)
                {
                    AddError(result, sectionPath, entryIndex, designIndex, designLabel + "가 null입니다.");
                    continue;
                }

                string designId = Normalize(design.designId);
                if (string.IsNullOrEmpty(designId))
                {
                    AddError(result, sectionPath, entryIndex, designIndex, designLabel + "의 designId는 비어 있을 수 없습니다.");
                }
                else
                {
                    if (!seenDesignIds.Add(designId))
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "의 designId '" + design.designId + "'가 같은 entry 안에서 중복됩니다.");
                }

                if (design.prefab == null)
                    AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 prefab이 필요합니다.");

                if (entry.designMode == GameplayDesignMode.AssembledPath)
                {
                    AssembledPathDesignAssets assets = design.assembledPathAssets ?? new AssembledPathDesignAssets();
                    if (design.prefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 rail prefab(root controller)이 필요합니다.");
                    if (assets.straightPrefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 rail straightPrefab이 필요합니다.");
                    else
                        ValidateRailTilePrefab(sectionPath, entryIndex, designIndex, designLabel + ".assembledPathAssets.straightPrefab", assets.straightPrefab, result);
                    if (assets.cornerPrefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 rail cornerPrefab이 필요합니다.");
                    else
                        ValidateRailTilePrefab(sectionPath, entryIndex, designIndex, designLabel + ".assembledPathAssets.cornerPrefab", assets.cornerPrefab, result);
                }
            }

        }

        private static void ValidateEnvironmentSection(EnvironmentCatalogSectionDefinition section, PlayableObjectCatalogValidationResult result)
        {
            EnvironmentCatalogEntry[] entries = section != null ? section.entries : null;
            if (entries == null)
                return;

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                ValidateEnvironmentEntry(section, entries[entryIndex], entryIndex, result);
        }

        private static void ValidateEnvironmentEntry(EnvironmentCatalogSectionDefinition section, EnvironmentCatalogEntry entry, int entryIndex, PlayableObjectCatalogValidationResult result)
        {
            string sectionPath = section != null ? section.arrayPath : string.Empty;
            string expectedCategory = Normalize(section != null ? section.expectedCategory : string.Empty);

            if (entry == null)
            {
                AddError(result, sectionPath, entryIndex, -1, BuildEnvironmentEntryLabel(section, entryIndex, string.Empty) + "가 null입니다.");
                return;
            }

            string objectId = Normalize(entry.objectId);
            string category = Normalize(entry.category);
            string placementMode = Normalize(entry.placementMode);
            string variationMode = Normalize(entry.variationMode);
            string entryLabel = BuildEnvironmentEntryLabel(section, entryIndex, objectId);

            if (string.IsNullOrEmpty(objectId))
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 objectId는 비어 있을 수 없습니다.");

            if (string.IsNullOrEmpty(category))
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 category는 비어 있을 수 없습니다.");
            else if (!string.Equals(category, expectedCategory, System.StringComparison.Ordinal))
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 category '" + entry.category + "'는 섹션 category '" + (section != null ? section.expectedCategory : string.Empty) + "'와 정확히 일치해야 합니다.");

            if (string.IsNullOrEmpty(placementMode))
            {
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 placementMode는 비어 있을 수 없습니다.");
            }
            else if (!IsSupportedEnvironmentPlacementMode(placementMode))
            {
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 placementMode '" + entry.placementMode + "'는 'fill' 또는 'perimeter'여야 합니다.");
            }

            if (string.IsNullOrEmpty(variationMode))
            {
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 variationMode는 비어 있을 수 없습니다.");
            }
            else if (!IsSupportedEnvironmentVariationMode(variationMode))
            {
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "의 variationMode '" + entry.variationMode + "'는 'single' 또는 'connected3'여야 합니다.");
            }

            string resolvedVariationMode = variationMode;

            EnvironmentDesignVariantEntry[] designs = entry.designs ?? new EnvironmentDesignVariantEntry[0];
            if (designs.Length == 0)
            {
                AddError(result, sectionPath, entryIndex, -1, entryLabel + "에는 최소 1개의 design이 필요합니다.");
                return;
            }

            var seenDesignIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int designIndex = 0; designIndex < designs.Length; designIndex++)
            {
                EnvironmentDesignVariantEntry design = designs[designIndex];
                string designLabel = entryLabel + " design[" + designIndex + "]";
                if (design == null)
                {
                    AddError(result, sectionPath, entryIndex, designIndex, designLabel + "가 null입니다.");
                    continue;
                }

                int expectedSquareSizeCells = 0;

                string designId = Normalize(design.designId);
                if (string.IsNullOrEmpty(designId))
                {
                    AddError(result, sectionPath, entryIndex, designIndex, designLabel + "의 designId는 비어 있을 수 없습니다.");
                }
                else
                {
                    if (!seenDesignIds.Add(designId))
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "의 designId '" + design.designId + "'가 같은 entry 안에서 중복됩니다.");
                }

                if (design.prefab == null)
                    AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 prefab이 필요합니다.");
                else
                    ValidateEnvironmentPrefabFootprintRules(sectionPath, entryIndex, designIndex, designLabel + ".prefab", design.prefab, ref expectedSquareSizeCells, result);

                if (design.tJunctionPrefab != null &&
                    !string.Equals(resolvedVariationMode, EnvironmentCatalog.VARIATION_MODE_CONNECTED3, System.StringComparison.Ordinal))
                    ValidateEnvironmentPrefabFootprintRules(sectionPath, entryIndex, designIndex, designLabel + ".tJunctionPrefab", design.tJunctionPrefab, ref expectedSquareSizeCells, result);

                if (string.Equals(resolvedVariationMode, EnvironmentCatalog.VARIATION_MODE_CONNECTED3, System.StringComparison.Ordinal))
                {
                    if (design.straightPrefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 variationMode 'connected3'용 straightPrefab이 필요합니다.");
                    else
                        ValidateEnvironmentPrefabFootprintRules(sectionPath, entryIndex, designIndex, designLabel + ".straightPrefab", design.straightPrefab, ref expectedSquareSizeCells, result);
                    if (design.cornerPrefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 variationMode 'connected3'용 cornerPrefab이 필요합니다.");
                    else
                        ValidateEnvironmentPrefabFootprintRules(sectionPath, entryIndex, designIndex, designLabel + ".cornerPrefab", design.cornerPrefab, ref expectedSquareSizeCells, result);
                    if (design.tJunctionPrefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 variationMode 'connected3'용 tJunctionPrefab이 필요합니다.");
                    else
                        ValidateEnvironmentPrefabFootprintRules(sectionPath, entryIndex, designIndex, designLabel + ".tJunctionPrefab", design.tJunctionPrefab, ref expectedSquareSizeCells, result);
                    if (design.crossPrefab == null)
                        AddError(result, sectionPath, entryIndex, designIndex, designLabel + "에는 variationMode 'connected3'용 crossPrefab이 필요합니다.");
                    else
                        ValidateEnvironmentPrefabFootprintRules(sectionPath, entryIndex, designIndex, designLabel + ".crossPrefab", design.crossPrefab, ref expectedSquareSizeCells, result);
                }
            }

        }

        private static string BuildEntryLabel(GameplayCatalogSectionDefinition section, int entryIndex, string objectId)
        {
            string label = section != null && !string.IsNullOrWhiteSpace(section.label) ? section.label : "gameplay entry";
            return string.IsNullOrEmpty(objectId) ? label + "[" + entryIndex + "]" : label + " '" + objectId + "'";
        }

        private static string BuildEnvironmentEntryLabel(EnvironmentCatalogSectionDefinition section, int entryIndex, string objectId)
        {
            string label = section != null && !string.IsNullOrWhiteSpace(section.label) ? section.label : "environment entry";
            return string.IsNullOrEmpty(objectId) ? label + "[" + entryIndex + "]" : label + " '" + objectId + "'";
        }

        private static void AddError(PlayableObjectCatalogValidationResult result, string sectionPath, int entryIndex, int designIndex, string message)
        {
            AddError(result, sectionPath, entryIndex, designIndex, new ValidationIssueRecord(
                ValidationRuleId.CATALOG_GENERIC,
                ValidationSeverity.Blocker,
                message,
                "CatalogContract"));
        }

        private static void AddError(PlayableObjectCatalogValidationResult result, string sectionPath, int entryIndex, int designIndex, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return;

            result.Errors.Add(new PlayableObjectCatalogValidationIssue
            {
                sectionPath = sectionPath ?? string.Empty,
                entryIndex = entryIndex,
                designIndex = designIndex,
                message = issue.message ?? string.Empty,
            });

            if (string.IsNullOrEmpty(issue.targetPath))
                issue.WithTargetPath(BuildIssueTargetPath(sectionPath, entryIndex, designIndex));

            result.Issues.Add(issue);
        }

        private static string BuildIssueTargetPath(string sectionPath, int entryIndex, int designIndex)
        {
            string basePath = string.IsNullOrEmpty(sectionPath) ? string.Empty : sectionPath;
            if (entryIndex < 0)
                return basePath;

            string entryPart = basePath + "[" + entryIndex + "]";
            if (designIndex < 0)
                return entryPart;

            return entryPart + ".designs[" + designIndex + "]";
        }

        private static bool IsSupportedEnvironmentPlacementMode(string placementMode)
        {
            return string.Equals(placementMode, EnvironmentCatalog.PLACEMENT_MODE_FILL, System.StringComparison.Ordinal) ||
                   string.Equals(placementMode, EnvironmentCatalog.PLACEMENT_MODE_PERIMETER, System.StringComparison.Ordinal);
        }

        private static bool IsSupportedEnvironmentVariationMode(string variationMode)
        {
            return string.Equals(variationMode, EnvironmentCatalog.VARIATION_MODE_SINGLE, System.StringComparison.Ordinal) ||
                   string.Equals(variationMode, EnvironmentCatalog.VARIATION_MODE_CONNECTED3, System.StringComparison.Ordinal);
        }

        private static void ValidateEnvironmentPrefabFootprintRules(
            string sectionPath,
            int entryIndex,
            int designIndex,
            string prefabLabel,
            GameObject prefab,
            ref int expectedSquareSizeCells,
            PlayableObjectCatalogValidationResult result)
        {
            if (prefab == null)
                return;

            if (!TryReadPrefabFootprintForValidation(prefab, out int widthCells, out int depthCells))
            {
                AddError(
                    result,
                    sectionPath,
                    entryIndex,
                    designIndex,
                    prefabLabel + "에는 placement footprint metadata가 필요합니다.");
                return;
            }

            if (widthCells < 1 || depthCells < 1)
            {
                AddError(
                    result,
                    sectionPath,
                    entryIndex,
                    designIndex,
                    prefabLabel + " footprint는 width/depth가 1 이상이어야 합니다.");
                return;
            }

            if (widthCells != depthCells)
            {
                AddError(
                    result,
                    sectionPath,
                    entryIndex,
                    designIndex,
                    prefabLabel + " footprint는 정사각형이어야 합니다. 현재 " + widthCells + "x" + depthCells + "입니다.");
                return;
            }

            if (expectedSquareSizeCells == 0)
            {
                expectedSquareSizeCells = widthCells;
                return;
            }

            if (expectedSquareSizeCells != widthCells)
            {
                AddError(
                    result,
                    sectionPath,
                    entryIndex,
                    designIndex,
                    prefabLabel + " footprint는 같은 design 안의 다른 environment variant와 동일한 정사각 크기여야 합니다. 기준 " +
                    expectedSquareSizeCells + "x" + expectedSquareSizeCells + ", 현재 " + widthCells + "x" + depthCells + "입니다.");
            }
        }

        private static void ValidateRailTilePrefab(
            string sectionPath,
            int entryIndex,
            int designIndex,
            string prefabLabel,
            GameObject prefab,
            PlayableObjectCatalogValidationResult result)
        {
            if (prefab == null)
                return;

            if (!TryReadPrefabFootprintForValidation(prefab, out int widthCells, out int depthCells))
            {
                AddError(result, sectionPath, entryIndex, designIndex, prefabLabel + "에는 placement footprint metadata가 필요합니다.");
                return;
            }

            if (widthCells != 2 || depthCells != 2)
                AddError(result, sectionPath, entryIndex, designIndex, prefabLabel + " footprint는 2x2 cells여야 합니다.");
        }

        private static bool TryReadPrefabFootprintForValidation(GameObject prefab, out int widthCells, out int depthCells)
        {
            widthCells = 0;
            depthCells = 0;
            if (prefab == null)
                return false;

            return CatalogAssetReferenceUtility.TryReadPlacementFootprintFromCatalogMetadata(prefab, out widthCells, out depthCells, out _, out _);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
