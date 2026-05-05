#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Serialization;

namespace Supercent.PlayableAI.Common.Contracts
{
    [DisallowMultipleComponent]
    [AddComponentMenu("AIPS/Item Advanced Metadata")]
    public sealed class ItemAdvancedMetadata : MonoBehaviour
    {
        [FormerlySerializedAs("_sprites")]
        [SerializeField] private Sprite[] _dummyImages = new Sprite[0];

        public Sprite[] DummyImages => _dummyImages ?? new Sprite[0];

        public bool HasDummyImages()
        {
            Sprite[] dummyImages = DummyImages;
            for (int i = 0; i < dummyImages.Length; i++)
            {
                if (dummyImages[i] != null)
                    return true;
            }

            return false;
        }

        public Sprite[] GetDummyImagesSnapshot()
        {
            return (Sprite[])DummyImages.Clone();
        }
    }
}
#endif
