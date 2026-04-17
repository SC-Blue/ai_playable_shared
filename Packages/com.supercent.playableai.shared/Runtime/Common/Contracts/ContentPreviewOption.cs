using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PlayableAI/Content Options/Preview")]
    public sealed class ContentPreviewOption : MonoBehaviour
    {
        public Texture2D topImage;
    }
}
