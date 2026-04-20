using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayableAI/Content Metadata")]
    public sealed class ContentMetadata : MonoBehaviour
    {
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
            objectId = ContentCatalogTokenUtility.Normalize(objectId);
            designId = ContentCatalogTokenUtility.Normalize(designId);
            displayName = ContentCatalogTokenUtility.Normalize(displayName);
            description = ContentCatalogTokenUtility.Normalize(description);
        }
    }
}
