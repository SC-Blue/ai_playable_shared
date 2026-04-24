using System;

namespace Supercent.PlayableAI.Common.Contracts
{
    [Serializable]
    public sealed class CatalogPrefabMetadata
    {
        public string[] generatedItemStableKeys = new string[0];
        public string[] outputItemStableKeys = new string[0];
        public bool supportsCustomerFeature;
        public bool containsCustomerSingleLine;
        public bool containsSellerFeature;
        public bool containsMoneyHandler;
        public int placementFootprintWidthCells;
        public int placementFootprintDepthCells;
        public float placementFootprintCenterOffsetX;
        public float placementFootprintCenterOffsetZ;
    }
}
