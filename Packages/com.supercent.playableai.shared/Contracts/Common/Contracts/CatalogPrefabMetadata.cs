using System;

namespace Supercent.PlayableAI.Common.Contracts
{
    [Serializable]
    public sealed class CatalogPrefabMetadata
    {
        public string[] generatedItemStableKeys = new string[0];
        public string[] outputItemStableKeys = new string[0];
        public bool supportsCustomerFacility;
        public bool containsCustomerSingleLine;
        public bool containsItemSellFacility;
        public bool containsMoneyHandler;
        public int placementFootprintWidthCells;
        public int placementFootprintDepthCells;
        public float placementFootprintCenterOffsetX;
        public float placementFootprintCenterOffsetZ;
    }
}
