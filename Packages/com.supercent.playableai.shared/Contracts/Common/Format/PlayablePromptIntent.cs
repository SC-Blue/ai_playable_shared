using System;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class WorldBoundsDefinition
    {
        public bool hasWorldBounds;
        public float worldX;
        public float worldZ;
        public float worldWidth;
        public float worldDepth;
    }

    [Serializable]
    public sealed class FeaturePathAnchorDefinition
    {
        public float worldX;
        public float worldZ;
    }

    public static class FeaturePathElementKinds
    {
        public const string STRAIGHT = "straight";
        public const string CORNER = "corner";
    }

    [Serializable]
    public sealed class FeaturePathCellDefinition
    {
        public int gridX;
        public int gridZ;
        public string elementKind;
        public int rotationQuarterTurns;
    }

    [Serializable]
    public sealed class FeaturePathDefinition
    {
        public string sourceSide;
        public string sinkSide;
        public FeaturePathCellDefinition[] cells = new FeaturePathCellDefinition[0];
        public SerializableVector3[] worldPoints = new SerializableVector3[0];
    }

    [Serializable]
    public sealed class PromptIntentCurrencyDefinition
    {
        public string currencyId;
        public int unitValue;
        public int startingAmountValue;
        public string startVisualMode;
    }

    [Serializable]
    public sealed class PromptIntentSaleValueDefinition
    {
        public ItemRef item = new ItemRef();
        public string currencyId;
        public int amountValue;
    }

    [Serializable]
    public sealed class PromptIntentObjectDefinition
    {
        public string id;
        public string role;
        public string designId;
        public PlayableScenarioFeatureOptions featureOptions;
    }

    [Serializable]
    public sealed class PromptIntentConditionDefinition
    {
        public string kind;
        public string stageId;
        public string targetObjectId;
        public ItemRef item = new ItemRef();
        public string currencyId;
        public int amountValue;
    }

    [Serializable]
    public sealed class PromptIntentObjectiveDefinition
    {
        public string kind;
        public string targetObjectId;
        public ItemRef item = new ItemRef();
        public ItemRef inputItem = new ItemRef();
        public string currencyId;
        public int amountValue;
        public float seconds;
    }

    [Serializable]
    public sealed class PromptIntentEffectDefinition
    {
        public string kind;
        public string timing;
        public string targetObjectId;
        public string eventKey;
        public ItemRef item = new ItemRef();
        public string currencyId;
        public int amountValue;
        public float seconds;
    }

    [Serializable]
    public sealed class PromptIntentStageDefinition
    {
        public string id;
        public PromptIntentConditionDefinition enterWhen = new PromptIntentConditionDefinition();
        public PromptIntentEffectDefinition[] onEnter = new PromptIntentEffectDefinition[0];
        public PromptIntentObjectiveDefinition[] objectives = new PromptIntentObjectiveDefinition[0];
        public PromptIntentEffectDefinition[] onComplete = new PromptIntentEffectDefinition[0];
    }

    [Serializable]
    public sealed class PromptIntentObjectPlacementDefinition
    {
        // Image-first placement payload.
        public bool hasWorldPosition;
        public float worldX;
        public float worldZ;
        public bool hasResolvedYaw;
        public float resolvedYawDegrees;
        public string solverPlacementSource;
        public string orientationReason;
        public float anchorDeltaCellsX;
        public float anchorDeltaCellsZ;
        public bool hasImageBounds;
        public float centerPxX;
        public float centerPxY;
        public float bboxWidthPx;
        public float bboxHeightPx;
        public float bboxConfidence;
        public FeatureJsonPayload featureLayout;
    }

    [Serializable]
    public sealed class PromptIntentPlayerOptions
    {
        public int itemStackMaxCount;
    }

    [Serializable]
    public sealed class PlayablePromptIntent
    {
        public string themeId;
        public PromptIntentCurrencyDefinition[] currencies = new PromptIntentCurrencyDefinition[0];
        public PromptIntentSaleValueDefinition[] saleValues = new PromptIntentSaleValueDefinition[0];
        public PromptIntentObjectDefinition[] objects = new PromptIntentObjectDefinition[0];
        public ContentSelectionDefinition[] contentSelections = new ContentSelectionDefinition[0];
        public PromptIntentStageDefinition[] stages = new PromptIntentStageDefinition[0];
        public PromptIntentPlayerOptions playerOptions;
    }

    [Serializable]
    public sealed class ScenarioModelCurrencyDefinition
    {
        public string currencyId;
        public int unitValue;
        public int startingAmount;
        public string startVisualMode;
    }

    [Serializable]
    public sealed class ScenarioModelSaleValueDefinition
    {
        public ItemRef item = new ItemRef();
        public string currencyId;
        public int amount;
        public int amountValue;
    }

    [Serializable]
    public sealed class ScenarioModelObjectDefinition
    {
        public string id;
        public string role;
        public string designId;
        public PlayableScenarioFeatureOptions featureOptions;
        public bool startsPresent = true;
        public bool startsActive = true;
        public string firstPresentingStageId;
        public string firstActivatingStageId;
    }

    [Serializable]
    public sealed class ScenarioModelConditionDefinition
    {
        public string kind;
        public string stageId;
        public string targetObjectId;
        public ItemRef item = new ItemRef();
        public string currencyId;
        public int amount;
        public int amountValue;
    }

    [Serializable]
    public sealed class ScenarioModelObjectiveDefinition
    {
        public string id;
        public string kind;
        public string targetObjectId;
        public string arrowTargetObjectId;
        public string arrowEventKey;
        public string arrowTiming;
        public bool arrowOnFocusArrival;
        public ItemRef item = new ItemRef();
        public ItemRef inputItem = new ItemRef();
        public string currencyId;
        public int amount;
        public int amountValue;
        public float seconds;
        public bool absorbsArrow;
    }

    [Serializable]
    public sealed class ScenarioModelEffectDefinition
    {
        public string kind;
        public string timing;
        public string targetObjectId;
        public string eventKey;
        public ItemRef item = new ItemRef();
        public string currencyId;
        public int amount;
        public int amountValue;
        public float seconds;
    }

    [Serializable]
    public sealed class ScenarioModelStageDefinition
    {
        public string id;
        public ScenarioModelConditionDefinition enterCondition = new ScenarioModelConditionDefinition();
        public ScenarioModelEffectDefinition[] entryEffects = new ScenarioModelEffectDefinition[0];
        public ScenarioModelObjectiveDefinition[] objectives = new ScenarioModelObjectiveDefinition[0];
        public ScenarioModelEffectDefinition[] completionEffects = new ScenarioModelEffectDefinition[0];
    }

    [Serializable]
    public sealed class PlayableScenarioModel
    {
        public string themeId;
        public ScenarioModelCurrencyDefinition[] currencies = new ScenarioModelCurrencyDefinition[0];
        public ScenarioModelSaleValueDefinition[] saleValues = new ScenarioModelSaleValueDefinition[0];
        public ScenarioModelObjectDefinition[] objects = new ScenarioModelObjectDefinition[0];
        public ContentSelectionDefinition[] contentSelections = new ContentSelectionDefinition[0];
        public ScenarioModelStageDefinition[] stages = new ScenarioModelStageDefinition[0];
        public PlayableScenarioPlayerOptions playerOptions;
    }
}
