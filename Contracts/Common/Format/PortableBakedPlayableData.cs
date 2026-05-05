using System;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class PortableBakedPlayableData
    {
        public string themeId;
        public ObjectDesignSelectionDefinition[] objectDesigns = new ObjectDesignSelectionDefinition[0];
        public ContentSelectionDefinition[] contentSelections = new ContentSelectionDefinition[0];
        public PortableBakedSpawnData[] spawns = new PortableBakedSpawnData[0];
        public PortableBakedFeatureData[] features = new PortableBakedFeatureData[0];
        public CurrencyDefinition[] currencies = new CurrencyDefinition[0];
        public ItemPriceDefinition[] itemPrices = new ItemPriceDefinition[0];
        public FeatureAcceptedItemDefinition[] featureAcceptedItems = new FeatureAcceptedItemDefinition[0];
        public FeatureOutputItemDefinition[] featureOutputItems = new FeatureOutputItemDefinition[0];
        public UnlockDefinition[] unlocks = new UnlockDefinition[0];
    }

    [Serializable]
    public sealed class PortableBakedSpawnData
    {
        public string spawnKey;
        public string objectId;
        public int designIndex = -1;
        public bool startActive = true;
        public SerializableVector3 localPosition;
        public bool hasLocalRotation;
        public SerializableQuaternion localRotation;
    }

    [Serializable]
    public sealed class PortableBakedFeatureData
    {
        public string featureType;
        public string targetId;
        public string objectId;
        public string spawnKey;
        public bool startActive = true;
        public SerializableVector3 localPosition;
        public bool hasLocalRotation;
        public SerializableQuaternion localRotation;
        public string optionsJson = "{}";
        public string layoutJson = "{}";
    }
}
