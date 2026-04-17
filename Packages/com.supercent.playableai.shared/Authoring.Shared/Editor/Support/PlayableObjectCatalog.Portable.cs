#if !UNITY_EDITOR
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Runtime.Gameplay;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    public sealed class PortableGameObject : GameObject
    {
        public string assetPath = string.Empty;
        public string assetGuid = string.Empty;
        public CatalogPrefabMetadata metadata = new CatalogPrefabMetadata();

        public override T GetComponentInChildren<T>(bool includeInactive = false)
        {
            if (typeof(T) == typeof(PlayablePlacementFootprint))
            {
                PlayablePlacementFootprint footprint = PlayablePlacementFootprint.FromMetadata(metadata);
                if (footprint == null)
                    return null;
                return footprint as T;
            }

            return base.GetComponentInChildren<T>(includeInactive);
        }
    }

    public sealed class PortableTexture2D : Texture2D
    {
        public string assetPath = string.Empty;
        public string assetGuid = string.Empty;
    }

    internal static class PortablePrefabMetadataUtility
    {
        public static bool TryGetMetadata(GameObject prefab, out CatalogPrefabMetadata metadata)
        {
            if (prefab is PortableGameObject portable)
            {
                metadata = portable.metadata ?? new CatalogPrefabMetadata();
                return true;
            }

            metadata = new CatalogPrefabMetadata();
            return false;
        }
    }
}
#endif
