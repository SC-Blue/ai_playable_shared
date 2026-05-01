using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class FeatureOptionRules
    {
        public static string[] GetSupportedPromptFeatureOptionNames(string role)
        {
            return PromptIntentContractRegistry.GetSupportedFeatureOptionNames(role);
        }

        public static string DescribeSupportedPromptFeatureOptions(string role)
        {
            return PromptIntentContractRegistry.DescribeSupportedFeatureOptions(role);
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
