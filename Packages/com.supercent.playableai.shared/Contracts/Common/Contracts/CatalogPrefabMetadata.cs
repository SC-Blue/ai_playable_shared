using System;

namespace Supercent.PlayableAI.Common.Contracts
{
    [Serializable]
    public sealed class CatalogPrefabMetadata
    {
        public string[] generatedItemKeys = new string[0];
        public string[] outputItemKeys = new string[0];
        public bool supportsCustomerFeature;
        public bool containsCustomerSingleLine;
        public bool containsMoneyHandler;
        public int placementFootprintWidthCells;
        public int placementFootprintDepthCells;
        public float placementFootprintCenterOffsetX;
        public float placementFootprintCenterOffsetZ;
    }
}
