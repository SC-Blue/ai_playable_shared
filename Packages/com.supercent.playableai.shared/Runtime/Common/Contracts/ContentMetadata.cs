using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("AIPS/Content Metadata")]
    public sealed class ContentMetadata : MonoBehaviour
    {
        public string themeId = string.Empty;
        public string contentId = string.Empty;
        public string objectId = string.Empty;
        public string designId = string.Empty;
        public string displayName = string.Empty;
        [TextArea] public string description = string.Empty;
        public ContentCatalogCategory category = ContentCatalogCategory.unknown;
        public ContentCatalogSubscriptionType subscriptionType = ContentCatalogSubscriptionType.@object;
        public ContentCatalogPlacementMode placementMode = ContentCatalogPlacementMode.none;

        private void OnValidate()
        {
            contentId = ContentCatalogTokenUtility.Normalize(contentId);
            if (!string.IsNullOrWhiteSpace(contentId))
            {
                int delimiterIndex = contentId.IndexOf('/');
                if (delimiterIndex > 0)
                {
                    string contentThemeId = ContentCatalogTokenUtility.Normalize(contentId.Substring(0, delimiterIndex));
                    string localContentId = ContentCatalogTokenUtility.Normalize(contentId.Substring(delimiterIndex + 1));
                    if (!string.IsNullOrWhiteSpace(contentThemeId) && string.IsNullOrWhiteSpace(themeId))
                        themeId = contentThemeId;
                    contentId = localContentId;
                }
            }

            themeId = ContentStoreTaxonomyRules.CanonicalizeThemeId(themeId);
            objectId = ContentCatalogTokenUtility.Normalize(objectId);
            designId = ContentCatalogTokenUtility.Normalize(designId);
            displayName = ContentCatalogTokenUtility.Normalize(displayName);
            description = ContentCatalogTokenUtility.Normalize(description);
        }
    }
}
