using System;
using System.Collections.Generic;
using System.Linq;

namespace Supercent.PlayableAI.Common.Format
{
    public static class ContentStoreBoundaryIds
    {
        public const string BuiltIn = "built_in";
        public const string StableFeature = "features/stable";
        public const string CustomFeature = "features/custom";
        public const string Shared = "shared";
        public const string LegacyFeature = "feature";
    }

    public static class ContentStoreContentKindIds
    {
        public const string BuiltIn = "built_in";
        public const string Feature = "feature";
        public const string CustomFeature = "custom_feature";
        public const string Shared = "shared";
    }

    public static class ContentStoreSharedCategoryIds
    {
        public const string Font = "font";
        public const string FontsPathToken = "fonts";
    }

    [Serializable]
    public sealed class ContentStoreTaxonomyDefinition
    {
        public int schemaVersion = 1;
        public ContentStoreTaxonomyOption[] themes = Array.Empty<ContentStoreTaxonomyOption>();
        public ContentStoreTaxonomyOption[] contentKinds = Array.Empty<ContentStoreTaxonomyOption>();
        public ContentStoreTaxonomyOption[] builtInGroups = Array.Empty<ContentStoreTaxonomyOption>();
        public ContentStoreTaxonomyOption[] stableFeatureGroups = Array.Empty<ContentStoreTaxonomyOption>();
        public ContentStoreTaxonomyOption[] sharedAssetGroups = Array.Empty<ContentStoreTaxonomyOption>();
        public ContentStoreTaxonomySubcategoryGroup[] builtInSubcategoryGroups = Array.Empty<ContentStoreTaxonomySubcategoryGroup>();
    }

    [Serializable]
    public sealed class ContentStoreTaxonomyOption
    {
        public string id = string.Empty;
        public string label = string.Empty;
        public string iconUrl = string.Empty;
        public string[] aliases = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ContentStoreTaxonomySubcategoryGroup
    {
        public string category = string.Empty;
        public ContentStoreTaxonomyOption[] subcategories = Array.Empty<ContentStoreTaxonomyOption>();
    }

    [Serializable]
    public sealed class ContentStorePackageTaxonomy
    {
        public string kind = string.Empty;
        public string group = string.Empty;
        public string subcategory = string.Empty;
    }

    public static class ContentStoreTaxonomyRules
    {
        private static readonly ContentStoreTaxonomyOption[] ThemeOptions =
        {
            Option("PizzaReady", "PizzaReady", "/assets/themes/pizza_ready.webp", "pizza_ready", "pizza-ready", "pizzaready"),
            Option("SuzysRestaurant", "SuzysRestaurant", "/assets/themes/suzys_restaurant.webp", "suzys_restaurant", "suzy", "suzy_restaurant", "suzys-restaurant", "suzy-restaurant", "suzysrestaurant", "suzyrestaurant"),
        };

        private static readonly ContentStoreTaxonomyOption[] ContentKindOptions =
        {
            Option(ContentStoreContentKindIds.BuiltIn, "Built-in"),
            Option(ContentStoreContentKindIds.Feature, "Feature"),
            Option(ContentStoreContentKindIds.CustomFeature, "Custom Feature"),
            Option(ContentStoreContentKindIds.Shared, "Shared Asset"),
        };

        private static readonly ContentStoreTaxonomyOption[] BuiltInGroupOptions =
        {
            Option(CatalogCategoryIds.UI, "UI"),
            Option(CatalogCategoryIds.CHARACTER, "캐릭터"),
            Option(CatalogCategoryIds.ENVIRONMENT, "환경"),
            Option(CatalogCategoryIds.ITEM, "아이템"),
            Option(CatalogCategoryIds.UNLOCKER, "해금발판"),
        };

        private static readonly ContentStoreTaxonomyOption[] StableFeatureGroupOptions =
        {
            Option(PlayableFeatureTypeIds.Generator, "생성기"),
            Option(PlayableFeatureTypeIds.Converter, "변환기"),
            Option(PlayableFeatureTypeIds.Seller, "판매대"),
            Option(PlayableFeatureTypeIds.Rail, "아이템 레일"),
            Option(PlayableFeatureTypeIds.PhysicsArea, "더미 물리 영역"),
        };

        private static readonly ContentStoreTaxonomyOption[] SharedAssetGroupOptions =
        {
            Option(ContentStoreSharedCategoryIds.Font, "Font", aliases: new[] { ContentStoreSharedCategoryIds.FontsPathToken }),
        };

        private static readonly ContentStoreTaxonomySubcategoryGroup[] BuiltInSubcategoryGroups =
        {
            Group(CatalogCategoryIds.UI,
                Option("endcard", "엔드카드"),
                Option("joystick", "조이스틱"),
                Option("arrow", "화살표"),
                Option("currency_hud", "재화 HUD"),
                Option("drag_to_move", "드래그 유도")),
            Group(CatalogCategoryIds.CHARACTER,
                Option("player_model", "플레이어"),
                Option("customer", "손님")),
            Group(CatalogCategoryIds.ENVIRONMENT,
                Option(EnvironmentRoleIds.FLOOR, "바닥"),
                Option(EnvironmentRoleIds.WALL, "벽"),
                Option(EnvironmentRoleIds.ROAD, "도로"),
                Option(EnvironmentRoleIds.FENCE, "울타리")),
            Group(CatalogCategoryIds.ITEM,
                Option("pizza", "피자"),
                Option("cola", "콜라"),
                Option(CatalogIdentityRules.MONEY_OBJECT_ID, "돈"),
                Option("flour", "밀가루"),
                Option("dish", "접시"),
                Option("meat", "고기"),
                Option("patty", "패티"),
                Option("steak", "스테이크")),
            Group(CatalogCategoryIds.UNLOCKER,
                Option("unlocker", "해금발판")),
        };

        public static ContentStoreTaxonomyDefinition CreateDefinition()
        {
            return new ContentStoreTaxonomyDefinition
            {
                themes = CloneOptions(ThemeOptions),
                contentKinds = CloneOptions(ContentKindOptions),
                builtInGroups = CloneOptions(BuiltInGroupOptions),
                stableFeatureGroups = CloneOptions(StableFeatureGroupOptions),
                sharedAssetGroups = CloneOptions(SharedAssetGroupOptions),
                builtInSubcategoryGroups = BuiltInSubcategoryGroups.Select(CloneGroup).ToArray(),
            };
        }

        public static ContentStorePackageTaxonomy ResolvePackageTaxonomy(string boundary, string category, string featureType, string objectId)
        {
            string normalizedBoundary = Normalize(boundary);
            string normalizedCategory = Normalize(category);
            string normalizedFeatureType = Normalize(featureType);
            string normalizedObjectId = Normalize(objectId);
            string kind = ResolveContentKind(normalizedBoundary, normalizedCategory, normalizedFeatureType);

            if (string.Equals(kind, ContentStoreContentKindIds.Shared, StringComparison.Ordinal))
            {
                return new ContentStorePackageTaxonomy
                {
                    kind = kind,
                    group = normalizedCategory,
                    subcategory = normalizedObjectId,
                };
            }

            if (string.Equals(kind, ContentStoreContentKindIds.BuiltIn, StringComparison.Ordinal))
            {
                return new ContentStorePackageTaxonomy
                {
                    kind = kind,
                    group = normalizedCategory,
                    subcategory = normalizedObjectId,
                };
            }

            if (string.Equals(kind, ContentStoreContentKindIds.CustomFeature, StringComparison.Ordinal))
            {
                string group = FirstNonEmpty(normalizedFeatureType, normalizedCategory == CatalogCategoryIds.FEATURE ? string.Empty : normalizedCategory);
                return new ContentStorePackageTaxonomy
                {
                    kind = kind,
                    group = group,
                    subcategory = string.Equals(normalizedObjectId, group, StringComparison.Ordinal) ? string.Empty : normalizedObjectId,
                };
            }

            return new ContentStorePackageTaxonomy
            {
                kind = ContentStoreContentKindIds.Feature,
                group = normalizedFeatureType,
                subcategory = string.Empty,
            };
        }

        public static string ResolveContentKind(string boundary, string category, string featureType)
        {
            string normalizedBoundary = Normalize(boundary);
            string normalizedCategory = Normalize(category);
            string normalizedFeatureType = Normalize(featureType);
            if (IsSharedBoundary(normalizedBoundary))
                return ContentStoreContentKindIds.Shared;
            if (IsBuiltInBoundary(normalizedBoundary))
                return ContentStoreContentKindIds.BuiltIn;
            if (IsCustomFeatureBoundary(normalizedBoundary))
                return ContentStoreContentKindIds.CustomFeature;
            if (IsStableFeatureBoundary(normalizedBoundary) || string.Equals(normalizedBoundary, ContentStoreBoundaryIds.LegacyFeature, StringComparison.Ordinal))
                return ContentStoreContentKindIds.Feature;
            if (IsBuiltInCategory(normalizedCategory))
                return ContentStoreContentKindIds.BuiltIn;
            if (string.Equals(normalizedCategory, CatalogCategoryIds.FEATURE, StringComparison.Ordinal) || !string.IsNullOrEmpty(normalizedFeatureType))
                return ContentStoreContentKindIds.Feature;
            return ContentStoreContentKindIds.Feature;
        }

        public static bool IsSupportedUploadBoundary(string boundary)
        {
            string normalized = Normalize(boundary);
            return IsBuiltInBoundary(normalized) ||
                IsStableFeatureBoundary(normalized) ||
                IsCustomFeatureBoundary(normalized) ||
                IsSharedBoundary(normalized);
        }

        public static bool IsFeatureBoundary(string boundary)
        {
            string normalized = Normalize(boundary);
            return IsStableFeatureBoundary(normalized) || IsCustomFeatureBoundary(normalized);
        }

        public static bool IsBuiltInBoundary(string boundary)
        {
            return string.Equals(Normalize(boundary), ContentStoreBoundaryIds.BuiltIn, StringComparison.Ordinal);
        }

        public static bool IsStableFeatureBoundary(string boundary)
        {
            return string.Equals(Normalize(boundary), ContentStoreBoundaryIds.StableFeature, StringComparison.Ordinal);
        }

        public static bool IsCustomFeatureBoundary(string boundary)
        {
            return string.Equals(Normalize(boundary), ContentStoreBoundaryIds.CustomFeature, StringComparison.Ordinal);
        }

        public static bool IsSharedBoundary(string boundary)
        {
            return string.Equals(Normalize(boundary), ContentStoreBoundaryIds.Shared, StringComparison.Ordinal);
        }

        public static bool IsSharedCategory(string category)
        {
            string normalized = Normalize(category);
            return SharedAssetGroupOptions.Any(option =>
                string.Equals(option.id, normalized, StringComparison.Ordinal) ||
                (option.aliases ?? Array.Empty<string>()).Any(alias => string.Equals(alias, normalized, StringComparison.Ordinal)));
        }

        public static string ResolveSharedCategoryPathToken(string category)
        {
            string normalized = Normalize(category);
            if (string.Equals(normalized, ContentStoreSharedCategoryIds.Font, StringComparison.Ordinal) ||
                string.Equals(normalized, ContentStoreSharedCategoryIds.FontsPathToken, StringComparison.Ordinal))
                return ContentStoreSharedCategoryIds.FontsPathToken;
            return normalized;
        }

        public static string ResolveProjectPathToken(string token)
        {
            string normalized = Normalize(token);
            switch (normalized)
            {
                case ContentStoreBoundaryIds.BuiltIn:
                    return "BuiltIn";
                case "features":
                    return "Features";
                case "stable":
                    return "Stable";
                case "custom":
                    return "Custom";
                case CatalogCategoryIds.UI:
                    return "UI";
                case CatalogCategoryIds.CHARACTER:
                    return "Character";
                case CatalogCategoryIds.ENVIRONMENT:
                    return "Environment";
                case CatalogCategoryIds.ITEM:
                    return "Item";
                case CatalogCategoryIds.UNLOCKER:
                    return "Unlocker";
                case PlayableFeatureTypeIds.Generator:
                    return "Generator";
                case PlayableFeatureTypeIds.Converter:
                    return "Converter";
                case PlayableFeatureTypeIds.Seller:
                    return "Seller";
                case PlayableFeatureTypeIds.Rail:
                    return "Rail";
                case PlayableFeatureTypeIds.PhysicsArea:
                    return "PhysicsArea";
                case ContentStoreSharedCategoryIds.FontsPathToken:
                    return "Fonts";
                default:
                    return normalized;
            }
        }

        public static string ResolveBoundaryProjectPath(string boundary)
        {
            string normalized = Normalize(boundary);
            if (IsBuiltInBoundary(normalized))
                return ResolveProjectPathToken(ContentStoreBoundaryIds.BuiltIn);
            if (IsStableFeatureBoundary(normalized))
                return ResolveProjectPathToken("features") + "/" + ResolveProjectPathToken("stable");
            if (IsCustomFeatureBoundary(normalized))
                return ResolveProjectPathToken("features") + "/" + ResolveProjectPathToken("custom");
            return normalized;
        }

        public static string ResolveSharedCategoryProjectPathToken(string category)
        {
            return ResolveProjectPathToken(ResolveSharedCategoryPathToken(category));
        }

        public static bool IsBuiltInCategory(string category)
        {
            string normalized = Normalize(category);
            return BuiltInGroupOptions.Any(option => string.Equals(option.id, normalized, StringComparison.Ordinal));
        }

        public static bool IsStableFeatureType(string featureType)
        {
            string normalized = Normalize(featureType);
            return StableFeatureGroupOptions.Any(option => string.Equals(option.id, normalized, StringComparison.Ordinal));
        }

        public static bool IsBuiltInSubcategory(string category, string objectId)
        {
            string normalizedCategory = Normalize(category);
            string normalizedObjectId = Normalize(objectId);
            ContentStoreTaxonomySubcategoryGroup group = BuiltInSubcategoryGroups.FirstOrDefault(value => string.Equals(value.category, normalizedCategory, StringComparison.Ordinal));
            return group != null && group.subcategories.Any(option => string.Equals(option.id, normalizedObjectId, StringComparison.Ordinal));
        }

        public static bool IsReservedStableFeatureToken(string value)
        {
            return IsStableFeatureType(value);
        }

        public static bool IsSupportedThemeId(string themeId)
        {
            string canonical = CanonicalizeThemeId(themeId);
            return ThemeOptions.Any(option => string.Equals(option.id, canonical, StringComparison.Ordinal));
        }

        public static string CanonicalizeThemeId(string themeId)
        {
            string normalized = Normalize(themeId);
            string token = CanonicalToken(normalized);
            foreach (ContentStoreTaxonomyOption option in ThemeOptions)
            {
                if (string.Equals(CanonicalToken(option.id), token, StringComparison.Ordinal))
                    return option.id;
                if ((option.aliases ?? Array.Empty<string>()).Any(alias => string.Equals(CanonicalToken(alias), token, StringComparison.Ordinal)))
                    return option.id;
            }

            return normalized;
        }

        public static string ResolveThemeContentPathToken(string themeId)
        {
            string canonical = CanonicalizeThemeId(themeId);
            if (string.Equals(canonical, "PizzaReady", StringComparison.Ordinal))
                return "pizza_ready";
            if (string.Equals(canonical, "SuzysRestaurant", StringComparison.Ordinal))
                return "suzys_restaurant";
            return Normalize(canonical);
        }

        public static string[] ResolveThemePathTokens(string themeId)
        {
            string canonical = CanonicalizeThemeId(themeId);
            string pathToken = ResolveThemeContentPathToken(canonical);
            return new[] { pathToken, canonical }.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static ContentStoreTaxonomyOption Option(string id, string label, string iconUrl = "", params string[] aliases)
        {
            return new ContentStoreTaxonomyOption
            {
                id = id,
                label = label,
                iconUrl = iconUrl,
                aliases = aliases ?? Array.Empty<string>(),
            };
        }

        private static ContentStoreTaxonomySubcategoryGroup Group(string category, params ContentStoreTaxonomyOption[] subcategories)
        {
            return new ContentStoreTaxonomySubcategoryGroup
            {
                category = category,
                subcategories = subcategories ?? Array.Empty<ContentStoreTaxonomyOption>(),
            };
        }

        private static ContentStoreTaxonomyOption CloneOption(ContentStoreTaxonomyOption source)
        {
            return new ContentStoreTaxonomyOption
            {
                id = source.id,
                label = source.label,
                iconUrl = source.iconUrl,
                aliases = (source.aliases ?? Array.Empty<string>()).ToArray(),
            };
        }

        private static ContentStoreTaxonomyOption[] CloneOptions(IEnumerable<ContentStoreTaxonomyOption> source)
        {
            return source.Select(CloneOption).ToArray();
        }

        private static ContentStoreTaxonomySubcategoryGroup CloneGroup(ContentStoreTaxonomySubcategoryGroup source)
        {
            return new ContentStoreTaxonomySubcategoryGroup
            {
                category = source.category,
                subcategories = CloneOptions(source.subcategories ?? Array.Empty<ContentStoreTaxonomyOption>()),
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                string normalized = Normalize(value);
                if (!string.IsNullOrEmpty(normalized))
                    return normalized;
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string CanonicalToken(string value)
        {
            return Normalize(value)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
        }
    }
}
