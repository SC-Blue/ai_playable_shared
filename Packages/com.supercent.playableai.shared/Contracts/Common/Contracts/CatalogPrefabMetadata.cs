using System;

namespace Supercent.PlayableAI.Common.Contracts
{
    [Serializable]
    public sealed class CatalogFootprintOverlapAllowanceMetadata
    {
        public string counterpartRole = string.Empty;
        public int widthCells = 1;
        public int depthCells = 1;
        public float centerOffsetX;
        public float centerOffsetZ;
    }

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
        public CatalogFootprintOverlapAllowanceMetadata[] placementOverlapAllowances = new CatalogFootprintOverlapAllowanceMetadata[0];
    }
}
