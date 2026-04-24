using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class FeatureScenarioOptionRules
    {
        public const string CUSTOMER_REQUEST_COUNT = PromptIntentContractRegistry.SCENARIO_OPTION_CUSTOMER_REQUEST_COUNT;
        public const string REQUESTABLE_ITEMS = PromptIntentContractRegistry.SCENARIO_OPTION_REQUESTABLE_ITEMS;
        public const string INPUT_COUNT_PER_CONVERSION = PromptIntentContractRegistry.SCENARIO_OPTION_INPUT_COUNT_PER_CONVERSION;
        public const string CONVERSION_INTERVAL_SECONDS = PromptIntentContractRegistry.SCENARIO_OPTION_CONVERSION_INTERVAL_SECONDS;
        public const string INPUT_ITEM_MOVE_INTERVAL_SECONDS = PromptIntentContractRegistry.SCENARIO_OPTION_INPUT_ITEM_MOVE_INTERVAL_SECONDS;
        public const string SPAWN_INTERVAL_SECONDS = PromptIntentContractRegistry.SCENARIO_OPTION_SPAWN_INTERVAL_SECONDS;

        public static bool SupportsCustomerRequestCount(string role)
        {
            return PromptIntentContractRegistry.SupportsScenarioOption(role, CUSTOMER_REQUEST_COUNT);
        }

        public static bool SupportsInputCountPerConversion(string role)
        {
            return PromptIntentContractRegistry.SupportsScenarioOption(role, INPUT_COUNT_PER_CONVERSION);
        }

        public static bool SupportsConversionIntervalSeconds(string role)
        {
            return PromptIntentContractRegistry.SupportsScenarioOption(role, CONVERSION_INTERVAL_SECONDS);
        }

        public static bool SupportsInputItemMoveIntervalSeconds(string role)
        {
            return PromptIntentContractRegistry.SupportsScenarioOption(role, INPUT_ITEM_MOVE_INTERVAL_SECONDS);
        }

        public static bool SupportsSpawnIntervalSeconds(string role)
        {
            return PromptIntentContractRegistry.SupportsScenarioOption(role, SPAWN_INTERVAL_SECONDS);
        }

        public static string[] GetSupportedPromptScenarioOptionNames(string role)
        {
            return PromptIntentContractRegistry.GetSupportedScenarioOptionNames(role);
        }

        public static string DescribeSupportedPromptScenarioOptions(string role)
        {
            return PromptIntentContractRegistry.DescribeSupportedScenarioOptions(role);
        }

        public static PlayableScenarioFeatureOptions CreateRoleDefaultFeatureOptions(string role)
        {
            return PromptIntentContractRegistry.CreateRoleDefaultFeatureOptions(role);
        }

        public static void ApplyRoleDefaults(string role, ref PlayableScenarioFeatureOptions options)
        {
            PromptIntentContractRegistry.ApplyRoleDefaultFeatureOptions(role, ref options);
        }
    }
}
