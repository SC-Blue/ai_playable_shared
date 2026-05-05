using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class StepConditionRules
    {
        public const string ALWAYS = "always";
        public const string BEAT_COMPLETED = "beat_completed";
        public const string ACTION_STARTED = "action_started";
        public const string CURRENCY_AT_LEAST = "currency_at_least";
        public const string PROJECTED_CURRENCY_AT_LEAST = "projected_currency_at_least";
        public const string UNLOCKER_UNLOCKED = "unlocker_unlocked";
        public const string CAPABILITY_LEVEL_AT_LEAST = "capability_level_at_least";
        public const string TIMEOUT = "timeout";
        public const string ACTION_COMPLETED = "action_completed";
        public const string GAMEPLAY_SIGNAL = "gameplay_signal";

        private static readonly string[] SupportedTypes =
        {
            ALWAYS,
            BEAT_COMPLETED,
            ACTION_STARTED,
            CURRENCY_AT_LEAST,
            PROJECTED_CURRENCY_AT_LEAST,
            UNLOCKER_UNLOCKED,
            CAPABILITY_LEVEL_AT_LEAST,
            ACTION_COMPLETED,
            GAMEPLAY_SIGNAL,
            TIMEOUT,
        };

        public static string[] GetSupportedTypes()
        {
            var copied = new string[SupportedTypes.Length];
            for (int i = 0; i < SupportedTypes.Length; i++)
                copied[i] = SupportedTypes[i];
            return copied;
        }

        public static string Validate(StepConditionDefinition condition, string label)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.type))
                return null;

            string type = condition.type.Trim();
            switch (type)
            {
                case ALWAYS:
                    return null;
                case BEAT_COMPLETED:
                    return string.IsNullOrWhiteSpace(condition.targetId) ? label + ".targetId가 필요합니다." : null;
                case ACTION_STARTED:
                    return string.IsNullOrWhiteSpace(condition.targetId) ? label + ".targetId가 필요합니다." : null;
                case CURRENCY_AT_LEAST:
                    return condition.amount < 0 ? label + ".amount는 0 이상이어야 합니다." : null;
                case PROJECTED_CURRENCY_AT_LEAST:
                    if (string.IsNullOrWhiteSpace(condition.targetId))
                        return label + ".targetId가 필요합니다.";
                    if (string.IsNullOrWhiteSpace(condition.currencyId))
                        return label + ".currencyId가 필요합니다.";
                    return condition.amount <= 0 ? label + ".amount는 0보다 커야 합니다." : null;
                case UNLOCKER_UNLOCKED:
                    return string.IsNullOrWhiteSpace(condition.unlockerId) ? label + ".unlockerId가 필요합니다." : null;
                case CAPABILITY_LEVEL_AT_LEAST:
                    if (string.IsNullOrWhiteSpace(condition.targetId))
                        return label + ".targetId(capabilityId)가 필요합니다.";
                    return condition.amount < 0 ? label + ".amount는 0 이상이어야 합니다." : null;
                case TIMEOUT:
                    return condition.seconds < 0f ? label + ".seconds는 0 이상이어야 합니다." : null;
                case ACTION_COMPLETED:
                    return string.IsNullOrWhiteSpace(condition.targetId) ? label + ".targetId가 필요합니다." : null;
                case GAMEPLAY_SIGNAL:
                    return GameplaySignalRules.Validate(condition.signalId, condition.targetId, condition.item, condition.currencyId, label);
                default:
                    return label + ".type '" + type + "'는 beat flow에서 지원하지 않습니다.";
            }
        }
    }
}
