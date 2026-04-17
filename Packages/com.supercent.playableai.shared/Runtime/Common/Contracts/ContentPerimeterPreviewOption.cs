using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayableAI/Content Options/Perimeter Preview")]
    public sealed class ContentPerimeterPreviewOption : MonoBehaviour
    {
        public Texture2D straightTopImage;
        public Texture2D cornerTopImage;
        public Texture2D tJunctionTopImage;
        public Texture2D crossTopImage;
    }
}
