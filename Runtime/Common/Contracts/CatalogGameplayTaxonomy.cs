using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class CatalogGameplayTaxonomy
    {
        public const string SourceCategoryFeature = CatalogCategoryIds.FEATURE;
        public const string SourceCategoryUnlocker = GameplayCatalog.UNLOCKER_CATEGORY;
        public const string SourceCategoryCharacter = CatalogCategoryIds.CHARACTER;
        public const string SourceCategoryItem = CatalogCategoryIds.ITEM;

        public const string RoleUnlockPad = GameplayRoleIds.UNLOCK_PAD;
        public const string RoleItem = GameplayRoleIds.ITEM;
        public const string RolePlayer = GameplayRoleIds.PLAYER;
        public const string RoleCustomer = GameplayRoleIds.CUSTOMER;

        public static bool IsSupportedSourceGameplayCategory(string category)
        {
            string normalizedCategory = Normalize(category);
            return string.Equals(normalizedCategory, SourceCategoryFeature, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedCategory, SourceCategoryUnlocker, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedCategory, SourceCategoryCharacter, System.StringComparison.Ordinal) ||
                   string.Equals(normalizedCategory, SourceCategoryItem, System.StringComparison.Ordinal);
        }

        public static bool IsSupportedGameplayRole(string role)
        {
            return !string.IsNullOrWhiteSpace(ResolveFinalGameplayCategory(role));
        }

        public static bool TryResolveGameplayRole(
            string sourceCategory,
            string objectId,
            string assetPath,
            string designMode,
            string placementMode,
            out string role)
        {
            role = string.Empty;

            string normalizedCategory = Normalize(sourceCategory);
            string normalizedObjectId = Normalize(objectId);
            string normalizedAssetPath = NormalizePath(assetPath);
            if (string.Equals(normalizedCategory, SourceCategoryItem, System.StringComparison.Ordinal))
            {
                role = RoleItem;
                return true;
            }

            if (string.Equals(normalizedCategory, SourceCategoryUnlocker, System.StringComparison.Ordinal))
            {
                role = RoleUnlockPad;
                return true;
            }

            if (string.Equals(normalizedCategory, SourceCategoryCharacter, System.StringComparison.Ordinal))
            {
                if (string.Equals(normalizedObjectId, RoleCustomer, System.StringComparison.Ordinal) ||
                    normalizedAssetPath.IndexOf("/customer/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    role = RoleCustomer;
                    return true;
                }

                if (string.Equals(normalizedObjectId, "player model", System.StringComparison.Ordinal) ||
                    string.Equals(normalizedObjectId, "player_model", System.StringComparison.Ordinal) ||
                    string.Equals(normalizedObjectId, "player-model", System.StringComparison.Ordinal) ||
                    normalizedAssetPath.IndexOf("/playermodel/", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalizedAssetPath.IndexOf("/player_model/", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalizedAssetPath.IndexOf("/player_models/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    role = RolePlayer;
                    return true;
                }

                return false;
            }

            if (!string.Equals(normalizedCategory, SourceCategoryFeature, System.StringComparison.Ordinal))
                return false;

            if (PromptIntentContractRegistry.IsSupportedObjectRole(normalizedObjectId) &&
                !string.Equals(normalizedObjectId, RolePlayer, System.StringComparison.Ordinal) &&
                !string.Equals(normalizedObjectId, RoleUnlockPad, System.StringComparison.Ordinal))
            {
                role = normalizedObjectId;
                return true;
            }

            return false;
        }

        public static string ResolveFinalGameplayCategory(string role)
        {
            switch (Normalize(role))
            {
                case RoleUnlockPad:
                    return GameplayCatalog.UNLOCKER_CATEGORY;
                case RoleItem:
                    return GameplayCatalog.ITEM_CATEGORY;
                case RolePlayer:
                    return GameplayCatalog.PLAYER_MODEL_CATEGORY;
                case RoleCustomer:
                    return GameplayCatalog.CUSTOMER_CATEGORY;
                default:
                    string normalizedRole = Normalize(role);
                    return PromptIntentContractRegistry.IsSupportedObjectRole(normalizedRole) &&
                           !string.Equals(normalizedRole, RolePlayer, System.StringComparison.Ordinal) &&
                           !string.Equals(normalizedRole, RoleUnlockPad, System.StringComparison.Ordinal)
                        ? GameplayCatalog.FEATURE_CATEGORY
                        : string.Empty;
            }
        }

        public static string ResolveExpectedSectionCategory(string role)
        {
            return ResolveFinalGameplayCategory(role);
        }

        public static bool MatchesFinalGameplayCategory(string role, string category)
        {
            string expectedCategory = ResolveFinalGameplayCategory(role);
            return !string.IsNullOrWhiteSpace(expectedCategory) &&
                   string.Equals(expectedCategory, Normalize(category), System.StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizePath(string value)
        {
            return Normalize(value).Replace('\\', '/');
        }
    }
}
