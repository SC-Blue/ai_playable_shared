using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("AIPS/Content Options/Environment Variation")]
    public sealed class ContentEnvironmentVariationOption : MonoBehaviour
    {
        public string variationMode = GeneratedContentCatalogContracts.VARIATION_MODE_SINGLE;
        public GameObject straightPrefab;
        public GameObject cornerPrefab;
        public GameObject tJunctionPrefab;
        public GameObject crossPrefab;

        private void OnValidate()
        {
            variationMode = ContentCatalogTokenUtility.Normalize(variationMode);
            if (string.IsNullOrEmpty(variationMode))
                variationMode = GeneratedContentCatalogContracts.VARIATION_MODE_SINGLE;
        }
    }
}
