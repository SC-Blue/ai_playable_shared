using System;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class CompiledPlayablePlan
    {
        public const int FLOW_SCHEMA_VERSION = 2;

        // Authoring compile output uses the same beat/action flow model consumed by post-bake/runtime.
        public string themeId;
        public ObjectDesignSelectionDefinition[] objectDesigns = new ObjectDesignSelectionDefinition[0];
        public CompiledSpawnData[] spawns = new CompiledSpawnData[0];
        public CompiledPhysicsAreaDefinition[] physicsAreas = new CompiledPhysicsAreaDefinition[0];
        public CompiledRailDefinition[] rails = new CompiledRailDefinition[0];
        public CurrencyDefinition[] currencies = new CurrencyDefinition[0];
        public ItemPriceDefinition[] itemPrices = new ItemPriceDefinition[0];
        public FacilityAcceptedItemDefinition[] facilityAcceptedItems = new FacilityAcceptedItemDefinition[0];
        public FacilityOutputItemDefinition[] facilityOutputItems = new FacilityOutputItemDefinition[0];
        public PlayableScenarioPlayerOptions playerOptions;
        public PlayableScenarioFacilityOptionDefinition[] facilityOptions = new PlayableScenarioFacilityOptionDefinition[0];
        public UnlockDefinition[] unlocks = new UnlockDefinition[0];
        public int flowSchemaVersion = FLOW_SCHEMA_VERSION;
        public FlowBeatDefinition[] beats = new FlowBeatDefinition[0];
        public FlowActionDefinition[] actions = new FlowActionDefinition[0];
        /// <summary>First beat id per stage (same order as model.stages). Used for entry spawn_customer arrival audit.</summary>
        public string[] stageFirstBeatIds = new string[0];
    }

    [Serializable]
    public sealed class CompiledSpawnData
    {
        public string spawnKey;
        public string objectId;
        public int designIndex = -1;
        public string parentRef;
        public bool startActive = true;
        public SerializableVector3 localPosition;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
        public string solverPlacementSource;
        public string orientationReason;
        public float anchorDeltaCellsX;
        public float anchorDeltaCellsZ;
    }

    [Serializable]
    public sealed class CompiledPhysicsAreaDefinition
    {
        public string objectId;
        public string spawnKey;
        public bool startActive = true;
        public SerializableVector3 localPosition;
        public PhysicsAreaOptionsDefinition options = new PhysicsAreaOptionsDefinition();
        public PhysicsAreaLayoutDefinition layout = new PhysicsAreaLayoutDefinition();
    }

    [Serializable]
    public sealed class CompiledRailDefinition
    {
        public string objectId;
        public string spawnKey;
        public RailOptionsDefinition options = new RailOptionsDefinition();
        public RailLayoutDefinition layout = new RailLayoutDefinition();
    }
}
