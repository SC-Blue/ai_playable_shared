namespace Supercent.PlayableAI.Common.Format
{
    public static class PlayableScenarioFeatureDefaults
    {
        public const int ItemStackMaxCount = 10;
        public const float ItemStackPopIntervalSeconds = 0f;

        public static PlayableScenarioFeatureOptions CreateRoleOptions(string role)
        {
            return new PlayableScenarioFeatureOptions
            {
                featureType = PlayableFeatureTypeIds.Normalize(role),
                optionsJson = "{}",
            };
        }

        public static void ApplyRoleDefaults(string role, ref PlayableScenarioFeatureOptions options)
        {
            options = options.NormalizeForFeatureType(role);
        }
    }
}
