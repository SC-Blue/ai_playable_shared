namespace Supercent.PlayableAI.Common.Format
{
    [System.Serializable]
    public sealed class PlayableScenarioFacilityOptionDefinition
    {
        public string facilityId;
        public PlayableScenarioFacilityOptions options;
    }

    [System.Serializable]
    public struct PlayableScenarioPlayerOptions
    {
        public PlayableScenarioFacilityOptions.StackerTuning itemStacker;
    }

    [System.Serializable]
    public struct PlayableScenarioFacilityOptions
    {
        public float conversionInterval;
        public int inputCountPerConversion;
        public float inputItemMoveInterval;
        public float itemTweenDuration;
        public float itemTweenParabolaHeight;
        public bool playPopAnimation;
        public float itemStackingSoundVolume;
        public int pitchInitialItemCountRef;
        public UnityEngine.Vector3 moneyRotationOffset;
        public float playerDropInterval;
        public float playerGetItemInterval;
        public float spawnInterval;
        public float customerSellingInterval;
        public int costPerMoneyPile;
        public int customerReqMin;
        public int customerReqMax;

        [System.Serializable]
        public struct StackerTuning
        {
            public int maxCount;
            public float popIntervalSeconds;
        }

        public StackerTuning itemStacker;
        public StackerTuning moneyStacker;
    }
}
