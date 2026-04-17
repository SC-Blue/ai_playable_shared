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
        public string designId = GeneratedContentCatalogContracts.DEFAULT_DESIGN_ID;
        public string displayName = string.Empty;
        [TextArea] public string description = string.Empty;
        public ContentCatalogCategory category = ContentCatalogCategory.Unknown;
        public ContentCatalogSubscriptionType subscriptionType = ContentCatalogSubscriptionType.Object;
        public ContentCatalogPlacementMode placementMode = ContentCatalogPlacementMode.None;

        private void OnValidate()
        {
            contentId = ContentCatalogTokenUtility.Normalize(contentId);
            objectId = ContentCatalogTokenUtility.Normalize(objectId);
            designId = ContentCatalogTokenUtility.Normalize(designId);
            displayName = ContentCatalogTokenUtility.Normalize(displayName);
            description = ContentCatalogTokenUtility.Normalize(description);
            if (string.IsNullOrEmpty(designId))
                designId = GeneratedContentCatalogContracts.DEFAULT_DESIGN_ID;
        }
    }
}
