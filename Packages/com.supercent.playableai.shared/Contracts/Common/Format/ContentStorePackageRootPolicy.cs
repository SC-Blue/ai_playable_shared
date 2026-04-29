using System;
using System.Collections.Generic;
using System.Linq;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class ContentStoreRevisionDependencyContract
    {
        public string packageId = string.Empty;
        public string revisionId = string.Empty;
        public string dependencyKind = string.Empty;
    }

    [Serializable]
    public sealed class ContentStorePackageRootPolicy
    {
        public string packageId = string.Empty;
        public string packageKind = string.Empty;
        public string primaryRoot = string.Empty;
        public string featureDefinitionRoot = string.Empty;
        public string featureRuntimeRoot = string.Empty;
        public string sharedPackageRoot = string.Empty;
        public string markerRoot = string.Empty;
        public string featureType = string.Empty;
        public string themeId = string.Empty;
        public string designId = string.Empty;
        public string[] bundledRoots = Array.Empty<string>();
        public string[] allowedZipRoots = Array.Empty<string>();
        public string[] ownedRoots = Array.Empty<string>();
        public string[] managedReferenceRoots = Array.Empty<string>();
        public bool isBuiltInPackage;
        public bool isFeaturePackage;
        public bool isFeatureRuntimePackage;
        public bool isFeatureVariantPackage;
        public bool isSharedPackage;

        public static ContentStorePackageRootPolicy Resolve(
            string themeId,
            string boundary,
            string category,
            string objectId,
            string designId,
            string featureType,
            string installedPath)
        {
            string normalizedBoundary = Normalize(boundary).Trim('/');
            string normalizedThemeId = ContentStoreTaxonomyRules.ResolveThemeContentPathToken(themeId);
            string normalizedFeatureType = FirstNonEmpty(Normalize(featureType), Normalize(objectId));
            string normalizedObjectId = Normalize(objectId);
            string normalizedDesignId = FirstNonEmpty(Normalize(designId), "basic");
            string packageId = ResolvePackageId(normalizedThemeId, normalizedBoundary, category, normalizedObjectId, normalizedDesignId, normalizedFeatureType);
            string primaryRoot = FirstNonEmpty(NormalizeAssetPath(installedPath).TrimEnd('/'), ResolvePrimaryRoot(normalizedThemeId, normalizedBoundary, normalizedObjectId, normalizedDesignId, normalizedFeatureType));
            var roots = new List<string>();
            var referenceRoots = new List<string>();
            AddRoot(roots, primaryRoot);

            var policy = new ContentStorePackageRootPolicy
            {
                packageId = packageId,
                primaryRoot = primaryRoot,
                featureType = normalizedFeatureType,
                themeId = normalizedThemeId,
                designId = normalizedDesignId,
                isBuiltInPackage = ContentStoreTaxonomyRules.IsBuiltInBoundary(normalizedBoundary),
                isFeatureRuntimePackage = ContentStoreTaxonomyRules.IsFeatureRuntimeBoundary(normalizedBoundary),
                isFeatureVariantPackage = ContentStoreTaxonomyRules.IsFeatureBoundary(normalizedBoundary),
                isSharedPackage = ContentStoreTaxonomyRules.IsSharedBoundary(normalizedBoundary),
            };
            policy.isFeaturePackage = policy.isFeatureRuntimePackage || policy.isFeatureVariantPackage;
            policy.packageKind = policy.isSharedPackage
                ? ContentStorePackageKindIds.SharedAsset
                : policy.isFeatureRuntimePackage
                    ? ContentStorePackageKindIds.FeatureRuntime
                    : policy.isFeatureVariantPackage
                        ? ContentStorePackageKindIds.FeatureVariant
                        : policy.isBuiltInPackage
                            ? ContentStorePackageKindIds.BuiltIn
                            : normalizedBoundary;

            if (policy.isFeatureRuntimePackage)
            {
                policy.featureRuntimeRoot = primaryRoot;
                policy.featureDefinitionRoot = primaryRoot;
                AddRoot(referenceRoots, primaryRoot);
            }

            if (policy.isFeatureVariantPackage && !string.IsNullOrWhiteSpace(normalizedFeatureType))
            {
                policy.featureRuntimeRoot = "Assets/AIPS/Features/runtime/" + normalizedFeatureType;
                policy.featureDefinitionRoot = policy.featureRuntimeRoot;
                AddRoot(referenceRoots, policy.featureRuntimeRoot);
            }

            if (policy.isSharedPackage)
            {
                policy.sharedPackageRoot = primaryRoot;
                AddRoot(referenceRoots, policy.sharedPackageRoot);
            }

            policy.bundledRoots = roots.ToArray();
            policy.allowedZipRoots = policy.bundledRoots;
            policy.ownedRoots = policy.bundledRoots;
            policy.managedReferenceRoots = referenceRoots.ToArray();
            policy.markerRoot = ResolveMarkerRoot(packageId);
            return policy;
        }

        public static string ResolvePackageId(string themeId, string boundary, string category, string objectId, string designId)
        {
            return ResolvePackageId(themeId, boundary, category, objectId, designId, string.Empty);
        }

        public static string ResolvePackageId(string themeId, string boundary, string category, string objectId, string designId, string featureType)
        {
            string normalizedBoundary = Normalize(boundary).Trim('/');
            string normalizedThemeId = ContentStoreTaxonomyRules.ResolveThemeContentPathToken(themeId);
            string normalizedObjectId = Normalize(objectId).Trim('/');
            string normalizedDesignId = FirstNonEmpty(Normalize(designId).Trim('/'), "basic");
            string normalizedFeatureType = FirstNonEmpty(Normalize(featureType), normalizedObjectId);

            if (ContentStoreTaxonomyRules.IsFeatureRuntimeBoundary(normalizedBoundary))
                return "feature_runtime/" + normalizedFeatureType;

            if (ContentStoreTaxonomyRules.IsSharedBoundary(normalizedBoundary))
                return "shared/" + normalizedThemeId + "/assets/theme/all";

            if (ContentStoreTaxonomyRules.IsFeatureBoundary(normalizedBoundary))
            {
                return normalizedThemeId + "/features/" + normalizedFeatureType + "/" + normalizedDesignId;
            }

            return normalizedThemeId + "/" +
                ContentStoreBoundaryIds.BuiltIn + "/" +
                normalizedObjectId + "/" +
                normalizedDesignId;
        }

        public static string NormalizePackageId(string packageId)
        {
            string normalized = NormalizeAssetPath(packageId).Trim('/');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.StartsWith("feature_runtime/", StringComparison.Ordinal) ||
                normalized.StartsWith("shared/", StringComparison.Ordinal))
            {
                return normalized;
            }

            int delimiterIndex = normalized.IndexOf('/');
            if (delimiterIndex <= 0)
                return ContentStoreTaxonomyRules.ResolveThemeContentPathToken(normalized);

            return ContentStoreTaxonomyRules.ResolveThemeContentPathToken(normalized.Substring(0, delimiterIndex)) +
                normalized.Substring(delimiterIndex);
        }

        public static string ResolveMarkerRoot(string packageId)
        {
            string normalizedPackageId = NormalizePackageId(packageId);
            if (normalizedPackageId.StartsWith("feature_runtime/", StringComparison.Ordinal))
            {
                string featureType = ResolveRuntimeFeatureType(normalizedPackageId);
                if (string.IsNullOrWhiteSpace(featureType))
                    throw new InvalidOperationException("feature runtime packageId에 featureType이 필요합니다: " + normalizedPackageId);
                return "Assets/AIPS/Features/runtime/" + featureType + "/.store_installed";
            }

            if (normalizedPackageId.StartsWith("shared/", StringComparison.Ordinal))
            {
                string sharedThemeId = ResolvePackageThemeToken(normalizedPackageId);
                if (string.IsNullOrWhiteSpace(sharedThemeId))
                    throw new InvalidOperationException("shared packageId에 themeId가 필요합니다: " + normalizedPackageId);
                return "Assets/AIPS/Shared/" + sharedThemeId + "/.store_installed";
            }

            string themeId = ResolvePackageThemeToken(normalizedPackageId);
            if (string.IsNullOrWhiteSpace(themeId))
                throw new InvalidOperationException("content packageId에 themeId가 필요합니다: " + normalizedPackageId);
            return "Assets/AIPS/Contents/" + themeId + "/.store_installed";
        }

        public static string ResolveMarkerAssetPath(string packageId)
        {
            string normalizedPackageId = NormalizePackageId(packageId);
            return ResolveMarkerRoot(normalizedPackageId) + "/" + MakePackageIdSafe(normalizedPackageId) + ".json";
        }

        public static string ResolvePackageThemeToken(string packageId)
        {
            string normalizedPackageId = NormalizePackageId(packageId);
            string[] parts = normalizedPackageId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && string.Equals(parts[0], "shared", StringComparison.Ordinal))
                return ContentStoreTaxonomyRules.ResolveThemeContentPathToken(parts[1]);
            if (parts.Length >= 1 && !string.Equals(parts[0], "shared", StringComparison.Ordinal) && !string.Equals(parts[0], "feature_runtime", StringComparison.Ordinal))
                return ContentStoreTaxonomyRules.ResolveThemeContentPathToken(parts[0]);
            return string.Empty;
        }

        public static bool IsSharedPackageId(string packageId)
        {
            return NormalizePackageId(packageId).StartsWith("shared/", StringComparison.Ordinal);
        }

        public static bool IsFeatureRuntimePackageId(string packageId)
        {
            return NormalizePackageId(packageId).StartsWith("feature_runtime/", StringComparison.Ordinal);
        }

        public static string ResolveRuntimeFeatureType(string packageId)
        {
            string[] parts = NormalizePackageId(packageId).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && string.Equals(parts[0], "feature_runtime", StringComparison.Ordinal) ? parts[1] : string.Empty;
        }

        public static string MakePackageIdSafe(string packageId)
        {
            string normalized = NormalizePackageId(packageId);
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }

            return builder.Length == 0 ? "package" : builder.ToString();
        }

        public static string[] ResolvePrimaryRootPrefixes(string themeId, string boundary, string category)
        {
            string normalizedBoundary = Normalize(boundary).Trim('/');
            if (ContentStoreTaxonomyRules.IsFeatureRuntimeBoundary(normalizedBoundary))
                return new[] { "Assets/AIPS/Features/runtime/" };
            if (ContentStoreTaxonomyRules.IsSharedBoundary(normalizedBoundary))
                return ResolveThemePathTokens(themeId)
                    .Select(value => "Assets/AIPS/Shared/" + value + "/")
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            if (ContentStoreTaxonomyRules.IsFeatureBoundary(normalizedBoundary))
            {
                return ResolveThemePathTokens(themeId)
                    .Select(value => "Assets/AIPS/Contents/" + value + "/features/")
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return ResolveThemePathTokens(themeId)
                .SelectMany(value => new[]
                {
                    "Assets/AIPS/Contents/" + value + "/BuiltIn/",
                    "Assets/AIPS/Contents/" + value + "/built_in/",
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool IsSameOrChildAssetPath(string assetPath, string rootPath)
        {
            string normalizedPath = NormalizeAssetPath(assetPath).TrimEnd('/');
            string normalizedRoot = NormalizeAssetPath(rootPath).TrimEnd('/');
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.Ordinal) ||
                normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal);
        }

        public static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }

        private static string ResolvePrimaryRoot(string themeId, string boundary, string objectId, string designId, string featureType)
        {
            if (ContentStoreTaxonomyRules.IsFeatureRuntimeBoundary(boundary))
                return "Assets/AIPS/Features/runtime/" + featureType;
            if (ContentStoreTaxonomyRules.IsSharedBoundary(boundary))
                return "Assets/AIPS/Shared/" + themeId;
            if (ContentStoreTaxonomyRules.IsFeatureBoundary(boundary))
                return "Assets/AIPS/Contents/" + themeId + "/features/" + featureType + "/" + designId;
            return "Assets/AIPS/Contents/" + themeId + "/built_in/" + objectId + "/" + designId;
        }

        private static string[] ResolveThemePathTokens(string themeId)
        {
            string pathToken = ContentStoreTaxonomyRules.ResolveThemeContentPathToken(themeId);
            return string.IsNullOrWhiteSpace(pathToken) ? Array.Empty<string>() : new[] { pathToken };
        }

        private static void AddRoot(List<string> roots, string root)
        {
            string normalized = NormalizeAssetPath(root).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized))
                return;
            if (!roots.Any(value => string.Equals(value, normalized, StringComparison.Ordinal)))
                roots.Add(normalized);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string value = Normalize(values[i]);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }
    }
}
