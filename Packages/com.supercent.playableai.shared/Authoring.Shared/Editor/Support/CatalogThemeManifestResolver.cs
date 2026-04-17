#if !UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Supercent.PlayableAI.AuthoringCore;

namespace PlayableAI.AuthoringCore
{
    public static class CatalogThemeManifestResolver
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };

        public static string ResolveManifestPath(
            string workspaceRoot,
            string requestedManifestPath,
            string intentJsonPath = null,
            string explicitThemeId = null)
        {
            if (!string.IsNullOrWhiteSpace(requestedManifestPath))
                return Path.GetFullPath(requestedManifestPath.Trim());

            string resolvedWorkspaceRoot = Path.GetFullPath(workspaceRoot ?? Directory.GetCurrentDirectory());
            string referencesRoot = Path.Combine(resolvedWorkspaceRoot, "references");
            string indexPath = Path.Combine(referencesRoot, AuthoringCoreSharedContracts.CATALOG_INDEX_FILE_NAME);
            string fullIndexPath = Path.GetFullPath(indexPath);

            if (!File.Exists(fullIndexPath))
                throw new InvalidOperationException("catalog index를 찾지 못했습니다: " + fullIndexPath);

            string themeId = Normalize(explicitThemeId);
            if (string.IsNullOrWhiteSpace(themeId) && !string.IsNullOrWhiteSpace(intentJsonPath))
                themeId = TryReadThemeIdFromIntentFile(intentJsonPath);
            if (string.IsNullOrWhiteSpace(themeId))
                themeId = TryReadDefaultThemeIdFromIndex(fullIndexPath);

            if (!string.IsNullOrWhiteSpace(themeId) &&
                TryResolveManifestPathFromIndex(fullIndexPath, themeId, out string resolvedManifestPath))
            {
                return resolvedManifestPath;
            }

            throw new InvalidOperationException("themeId '" + themeId + "'에 해당하는 catalog manifest를 index에서 찾지 못했습니다: " + fullIndexPath);
        }

        public static bool TryResolveManifestPathFromIndex(string indexPath, string themeId, out string manifestPath)
        {
            manifestPath = string.Empty;
            string normalizedThemeId = Normalize(themeId);
            string fullIndexPath = Path.GetFullPath(indexPath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedThemeId) || !File.Exists(fullIndexPath))
                return false;

            try
            {
                string json = File.ReadAllText(fullIndexPath, Encoding.UTF8);
                CatalogManifestIndexData index = JsonSerializer.Deserialize<CatalogManifestIndexData>(json, JsonOptions) ?? new CatalogManifestIndexData();
                CatalogThemeManifestEntry[] themes = index.themes ?? Array.Empty<CatalogThemeManifestEntry>();
                for (int i = 0; i < themes.Length; i++)
                {
                    CatalogThemeManifestEntry entry = themes[i];
                    if (entry == null || !string.Equals(Normalize(entry.themeId), normalizedThemeId, StringComparison.Ordinal))
                        continue;

                    string relativePath = entry.manifestRelativePath ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(relativePath))
                        return false;

                    string fullManifestPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullIndexPath) ?? string.Empty, relativePath));
                    if (!File.Exists(fullManifestPath))
                        return false;

                    manifestPath = fullManifestPath;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static string TryReadThemeIdFromIntentFile(string intentJsonPath)
        {
            string fullIntentPath = Path.GetFullPath(intentJsonPath ?? string.Empty);
            if (!File.Exists(fullIntentPath))
                return string.Empty;

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullIntentPath, Encoding.UTF8));
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return string.Empty;

                if (!document.RootElement.TryGetProperty("themeId", out JsonElement themeIdElement))
                    return string.Empty;

                return Normalize(themeIdElement.GetString());
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string TryReadDefaultThemeIdFromIndex(string indexPath)
        {
            string fullIndexPath = Path.GetFullPath(indexPath ?? string.Empty);
            if (!File.Exists(fullIndexPath))
                return string.Empty;

            try
            {
                string json = File.ReadAllText(fullIndexPath, Encoding.UTF8);
                CatalogManifestIndexData index = JsonSerializer.Deserialize<CatalogManifestIndexData>(json, JsonOptions) ?? new CatalogManifestIndexData();
                return Normalize(index.defaultThemeId);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
#endif
