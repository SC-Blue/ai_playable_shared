using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayableAI/Content Options/Path Preview")]
    public sealed class ContentPathPreviewOption : MonoBehaviour
    {
        public Texture2D straightTopImage;
        public Texture2D cornerTopImage;
    }
}
