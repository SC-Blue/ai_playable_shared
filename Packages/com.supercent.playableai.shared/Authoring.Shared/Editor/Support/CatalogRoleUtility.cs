using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;

namespace PlayableAI.AuthoringCore
{
    public static class CatalogRoleUtility
    {
        public static bool IsCatalogBackedRole(string role)
        {
            return !string.Equals(Normalize(role), PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal);
        }

        public static bool TryResolveCatalogObjectIdForRole(
            PlayableObjectCatalog catalog,
            string role,
            out string objectId,
            out string error)
        {
            objectId = string.Empty;
            error = string.Empty;
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
                    return TryResolveUniqueCatalogObjectIdByCategory(catalog, "PlayerModel", out objectId, out error);
                case PromptIntentObjectRoles.PHYSICS_AREA:
                    error = "physics_area는 catalog-backed role이 아닙니다.";
                    return false;
                default:
                    error = "지원되지 않는 object role '" + (role ?? string.Empty) + "'입니다.";
                    return false;
            }
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
