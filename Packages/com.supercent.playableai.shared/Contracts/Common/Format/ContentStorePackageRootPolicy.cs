using System;
using System.Collections.Generic;
using System.Linq;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class ContentStorePackageRootPolicy
    {
        public string primaryRoot = string.Empty;
        public string featureDefinitionRoot = string.Empty;
        public string sharedPackageRoot = string.Empty;
        public string[] bundledRoots = Array.Empty<string>();
        public string[] allowedZipRoots = Array.Empty<string>();
        public bool isBuiltInPackage;
        public bool isFeaturePackage;
        public bool isStableFeaturePackage;
        public bool isCustomFeaturePackage;
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
            string normalizedBoundary = Normalize(boundary);
            string normalizedFeatureType = FirstNonEmpty(Normalize(featureType), Normalize(objectId));
            string primaryRoot = NormalizeAssetPath(installedPath).TrimEnd('/');
            var roots = new List<string>();
            AddRoot(roots, primaryRoot);

            var policy = new ContentStorePackageRootPolicy
            {
                primaryRoot = primaryRoot,
                isBuiltInPackage = ContentStoreTaxonomyRules.IsBuiltInBoundary(normalizedBoundary),
                isFeaturePackage = ContentStoreTaxonomyRules.IsFeatureBoundary(normalizedBoundary),
                isStableFeaturePackage = ContentStoreTaxonomyRules.IsStableFeatureBoundary(normalizedBoundary),
                isCustomFeaturePackage = ContentStoreTaxonomyRules.IsCustomFeatureBoundary(normalizedBoundary),
                isSharedPackage = ContentStoreTaxonomyRules.IsSharedBoundary(normalizedBoundary),
            };

            if (policy.isStableFeaturePackage && !string.IsNullOrWhiteSpace(normalizedFeatureType))
            {
                policy.featureDefinitionRoot = "Assets/AIPS/Features/stable/" + normalizedFeatureType;
                AddRoot(roots, policy.featureDefinitionRoot);
            }

            if (policy.isSharedPackage)
                policy.sharedPackageRoot = primaryRoot;

            policy.bundledRoots = roots.ToArray();
            policy.allowedZipRoots = policy.bundledRoots;
            return policy;
        }

        public static string[] ResolvePrimaryRootPrefixes(string themeId, string boundary, string category)
        {
            string normalizedBoundary = Normalize(boundary).Trim('/');
            if (ContentStoreTaxonomyRules.IsSharedBoundary(normalizedBoundary))
                return ResolveSharedRootPrefixes(themeId, category);

            string projectBoundaryPath = ContentStoreTaxonomyRules.ResolveBoundaryProjectPath(normalizedBoundary).Trim('/');
            return ResolveThemePathTokens(themeId)
                .SelectMany(value => new[]
                {
                    "Assets/AIPS/Contents/" + value + "/" + projectBoundaryPath + "/",
                    "Assets/AIPS/Contents/" + value + "/" + normalizedBoundary + "/",
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string ResolveStableFeatureDefinitionRoot(string featureType)
        {
            string normalizedFeatureType = Normalize(featureType);
            return string.IsNullOrWhiteSpace(normalizedFeatureType)
                ? string.Empty
                : "Assets/AIPS/Features/stable/" + normalizedFeatureType;
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

        private static string[] ResolveSharedRootPrefixes(string themeId, string category)
        {
            string pathToken = ContentStoreTaxonomyRules.ResolveSharedCategoryPathToken(category);
            string projectPathToken = ContentStoreTaxonomyRules.ResolveSharedCategoryProjectPathToken(category);
            return ResolveThemePathTokens(themeId)
                .SelectMany(value => new[]
                {
                    "Assets/AIPS/Shared/" + value + "/" + projectPathToken + "/",
                    "Assets/AIPS/Shared/" + value + "/" + pathToken + "/",
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] ResolveThemePathTokens(string themeId)
        {
            return ContentStoreTaxonomyRules.ResolveThemePathTokens(themeId);
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
