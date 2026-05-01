namespace Supercent.PlayableAI.Common.Format
{
    public static class PlayableFeatureTypeIds
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [System.Serializable]
    public sealed class PlayableScenarioFeatureOptionDefinition
    {
        public string featureId;
        public string featureType;
        public string targetId;
        public PlayableScenarioFeatureOptions options;
    }

    [System.Serializable]
    public sealed class FeatureJsonPayload
    {
        public string featureType;
        public string targetId;
        public string json;

        public static FeatureJsonPayload Empty(string featureType, string targetId)
        {
            return new FeatureJsonPayload
            {
                featureType = PlayableFeatureTypeIds.Normalize(featureType),
                targetId = PlayableFeatureTypeIds.Normalize(targetId),
                json = "{}",
            };
        }
    }

    [System.Serializable]
    public struct PlayableScenarioPlayerOptions
    {
        public PlayableScenarioFeatureOptions.StackerTuning itemStacker;
    }

    [System.Serializable]
    public struct PlayableScenarioFeatureOptions
    {
        public string featureType;
        public string targetId;
        public string optionsJson;

        [System.Serializable]
        public struct StackerTuning
        {
            public int maxCount;
            public float popIntervalSeconds;
        }

        public PlayableScenarioFeatureOptions NormalizeForFeatureType(string featureType)
        {
            PlayableScenarioFeatureOptions normalized = this;
            normalized.featureType = PlayableFeatureTypeIds.Normalize(
                string.IsNullOrWhiteSpace(normalized.featureType) ? featureType : normalized.featureType);
            normalized.targetId = PlayableFeatureTypeIds.Normalize(normalized.targetId);
            normalized.optionsJson = string.IsNullOrWhiteSpace(normalized.optionsJson)
                ? "{}"
                : normalized.optionsJson.Trim();
            return normalized;
        }
    }
}
