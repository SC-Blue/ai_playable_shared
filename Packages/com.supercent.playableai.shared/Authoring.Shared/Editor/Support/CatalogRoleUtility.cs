using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;

namespace PlayableAI.AuthoringCore
{
    public static class CatalogRoleUtility
    {
        public static bool IsCatalogBackedRole(string role)
        {
            return PromptIntentContractRegistry.IsCatalogBackedObjectRole(role);
        }

        public static bool TryResolveCatalogObjectIdForRole(
            PlayableObjectCatalog catalog,
            string role,
            out string objectId,
            out string error)
        {
            objectId = string.Empty;
            error = string.Empty;
            string normalizedRole = Normalize(role);
            if (string.Equals(normalizedRole, PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
                return TryResolveUniqueCatalogObjectIdByCategory(catalog, GameplayCatalog.PLAYER_MODEL_CATEGORY, out objectId, out error);

            if (TryResolveRuntimeDescriptorCatalogRole(catalog, normalizedRole, out objectId))
                return true;

            if (TryResolveCatalogFeatureObjectRole(catalog, normalizedRole, out objectId))
                return true;

            if (!PromptIntentContractRegistry.IsSupportedObjectRole(normalizedRole))
            {
                error = "지원되지 않는 object role '" + (role ?? string.Empty) + "'입니다.";
                return false;
            }

            if (!PromptIntentContractRegistry.IsCatalogBackedObjectRole(normalizedRole))
            {
                error = normalizedRole + "는 catalog-backed role이 아닙니다.";
                return false;
            }

            objectId = PromptIntentContractRegistry.ResolveCatalogGameplayObjectIdForRole(normalizedRole);
            if (!string.IsNullOrEmpty(objectId))
                return true;

            error = "role '" + normalizedRole + "'에 대한 catalog objectId를 찾지 못했습니다.";
            return false;
        }

        private static bool TryResolveRuntimeDescriptorCatalogRole(PlayableObjectCatalog catalog, string role, out string objectId)
        {
            objectId = string.Empty;
            if (catalog == null)
                return false;

            string normalizedRole = Normalize(role);
            FeatureDescriptor[] descriptors = catalog.FeatureDescriptors ?? Array.Empty<FeatureDescriptor>();
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                FeatureDescriptor descriptor = descriptors[descriptorIndex];
                if (descriptor == null)
                    continue;

                FeatureCompiledGameplayRoleDescriptor[] mappings =
                    descriptor.compiledGameplayRoleMappings ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
                for (int mappingIndex = 0; mappingIndex < mappings.Length; mappingIndex++)
                {
                    FeatureCompiledGameplayRoleDescriptor mapping = mappings[mappingIndex];
                    if (mapping == null ||
                        !string.Equals(Normalize(mapping.role), normalizedRole, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string mappedObjectId = Normalize(mapping.gameplayObjectId);
                    if (string.IsNullOrEmpty(mappedObjectId))
                        continue;
                    if (!catalog.TryGetGameplayEntry(mappedObjectId, out GameplayCatalogEntry entry) || entry == null)
                        continue;

                    objectId = mappedObjectId;
                    return true;
                }

                FeatureObjectRoleDescriptor[] roles =
                    descriptor.objectRoles ?? Array.Empty<FeatureObjectRoleDescriptor>();
                for (int roleIndex = 0; roleIndex < roles.Length; roleIndex++)
                {
                    FeatureObjectRoleDescriptor objectRole = roles[roleIndex];
                    if (objectRole == null ||
                        !objectRole.catalogBacked ||
                        !string.Equals(Normalize(objectRole.role), normalizedRole, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string descriptorFeatureType = Normalize(descriptor.featureType);
                    if (string.IsNullOrEmpty(descriptorFeatureType))
                        continue;
                    if (!catalog.TryGetGameplayEntry(descriptorFeatureType, out GameplayCatalogEntry entry) || entry == null)
                        continue;

                    objectId = descriptorFeatureType;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveCatalogFeatureObjectRole(PlayableObjectCatalog catalog, string role, out string objectId)
        {
            objectId = string.Empty;
            if (catalog == null)
                return false;

            string normalizedRole = Normalize(role);
            if (string.IsNullOrEmpty(normalizedRole))
                return false;

            if (!catalog.TryGetGameplayEntry(normalizedRole, out GameplayCatalogEntry entry) || entry == null)
                return false;
            if (!string.Equals(Normalize(entry.category), GameplayCatalog.FEATURE_CATEGORY, StringComparison.Ordinal))
                return false;

            objectId = normalizedRole;
            return true;
        }

        public static bool TryResolveUniqueCatalogObjectIdByCategory(
            PlayableObjectCatalog catalog,
            string category,
            out string objectId,
            out string error)
        {
            objectId = string.Empty;
            error = string.Empty;
            if (catalog == null)
            {
                error = "PlayableObjectCatalog가 필요합니다.";
                return false;
            }

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
            {
                error = "category '" + (category ?? string.Empty) + "'에는 정확히 1개의 objectId가 필요합니다.";
                return false;
            }

            foreach (string value in seen)
            {
                objectId = value;
                return true;
            }

            error = "category '" + (category ?? string.Empty) + "'의 objectId를 찾지 못했습니다.";
            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
