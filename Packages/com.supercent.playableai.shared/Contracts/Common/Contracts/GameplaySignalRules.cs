namespace Supercent.PlayableAI.Common.Contracts
{
    public static class GameplaySignalRules
    {
        public static string Validate(string signalId, string targetId, Supercent.PlayableAI.Common.Format.ItemRef item, string currencyId, string label)
        {
            if (string.IsNullOrWhiteSpace(signalId))
                return label + ".signalId가 필요합니다.";

            string normalized = signalId.Trim();
            if (!PromptIntentCapabilityRegistry.IsSupportedGameplaySignalId(normalized))
                return label + ".signalId '" + normalized + "'는 지원하지 않습니다.";

            if (PromptIntentCapabilityRegistry.GameplaySignalRequiresTargetId(normalized) &&
                string.IsNullOrWhiteSpace(targetId))
            {
                return "signal '" + normalized + "'에는 " + label + ".targetId가 필요합니다.";
            }

            if (!PromptIntentCapabilityRegistry.GameplaySignalSupportsTargetId(normalized) &&
                !string.IsNullOrWhiteSpace(targetId))
            {
                return "signal '" + normalized + "'에는 " + label + ".targetId를 넣을 수 없습니다.";
            }

            if (PromptIntentCapabilityRegistry.GameplaySignalRequiresItem(normalized) &&
                !Supercent.PlayableAI.Common.Format.ItemRefUtility.IsValid(item))
            {
                return "signal '" + normalized + "'에는 " + label + ".item이 필요합니다.";
            }

            if (!PromptIntentCapabilityRegistry.GameplaySignalSupportsItem(normalized) &&
                !Supercent.PlayableAI.Common.Format.ItemRefUtility.IsEmpty(item))
            {
                return "signal '" + normalized + "'에는 " + label + ".item을 넣을 수 없습니다.";
            }

            if (PromptIntentCapabilityRegistry.GameplaySignalRequiresCurrencyId(normalized) &&
                string.IsNullOrWhiteSpace(currencyId))
            {
                return "signal '" + normalized + "'에는 " + label + ".currencyId가 필요합니다.";
            }

            if (!PromptIntentCapabilityRegistry.GameplaySignalSupportsCurrencyId(normalized) &&
                !string.IsNullOrWhiteSpace(currencyId))
            {
                return "signal '" + normalized + "'에는 " + label + ".currencyId를 넣을 수 없습니다.";
            }

            return null;
        }
    }
}
