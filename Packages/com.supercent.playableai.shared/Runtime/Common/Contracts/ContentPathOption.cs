using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("AIPS/Content Options/Path")]
    public sealed class ContentPathOption : MonoBehaviour
    {
        public GameObject straightPrefab;
        public GameObject cornerPrefab;
    }
}
