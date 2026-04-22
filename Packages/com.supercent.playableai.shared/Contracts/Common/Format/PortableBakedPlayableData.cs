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
        public PortableBakedPhysicsAreaDefinition[] physicsAreas = new PortableBakedPhysicsAreaDefinition[0];
        public PortableBakedRailDefinition[] rails = new PortableBakedRailDefinition[0];
        public CurrencyDefinition[] currencies = new CurrencyDefinition[0];
        public ItemPriceDefinition[] itemPrices = new ItemPriceDefinition[0];
        public FacilityAcceptedItemDefinition[] facilityAcceptedItems = new FacilityAcceptedItemDefinition[0];
        public FacilityOutputItemDefinition[] facilityOutputItems = new FacilityOutputItemDefinition[0];
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
    }

    [Serializable]
    public sealed class PortableBakedPhysicsAreaDefinition
    {
        public string objectId;
        public string spawnKey;
        public bool startActive = true;
        public SerializableVector3 localPosition;
        public PhysicsAreaOptionsDefinition options = new PhysicsAreaOptionsDefinition();
        public PhysicsAreaLayoutDefinition layout = new PhysicsAreaLayoutDefinition();
    }

    [Serializable]
    public sealed class PortableBakedRailDefinition
    {
        public string objectId;
        public string spawnKey;
        public RailOptionsDefinition options = new RailOptionsDefinition();
        public RailLayoutDefinition layout = new RailLayoutDefinition();
        public RailPathDefinition path = new RailPathDefinition();
    }
}
