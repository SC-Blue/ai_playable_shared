namespace Supercent.PlayableAI.Common.Contracts
{
    public static class ReactiveConditionRules
    {
        public const string BEAT_COMPLETED = "beat_completed";
        public const string SYSTEM_ACTION_ACTIVATED = "system_action_activated";
        public const string MODE_ALL = "all";
        public const string MODE_ANY = "any";

        private static readonly string[] SupportedTypes =
        {
            StepConditionRules.ALWAYS,
            StepConditionRules.CURRENCY_AT_LEAST,
            StepConditionRules.PROJECTED_CURRENCY_AT_LEAST,
            StepConditionRules.UNLOCKER_UNLOCKED,
            StepConditionRules.CAPABILITY_LEVEL_AT_LEAST,
            BEAT_COMPLETED,
            SYSTEM_ACTION_ACTIVATED,
            StepConditionRules.ACTION_STARTED,
            StepConditionRules.ACTION_COMPLETED,
            StepConditionRules.GAMEPLAY_SIGNAL,
        };

        public static string[] GetSupportedTypes()
        {
            var copied = new string[SupportedTypes.Length];
            for (int i = 0; i < SupportedTypes.Length; i++)
                copied[i] = SupportedTypes[i];
            return copied;
        }

        public static string Validate(Supercent.PlayableAI.Common.Format.ReactiveConditionDefinition condition, string label)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.type))
                return label + ".type이 필요합니다.";

            string type = condition.type.Trim();
            switch (type)
            {
                case StepConditionRules.ALWAYS:
                    return null;
                case StepConditionRules.CURRENCY_AT_LEAST:
                    return condition.amount < 0
                        ? label + ".amount는 0 이상이어야 합니다."
                        : null;
                case StepConditionRules.PROJECTED_CURRENCY_AT_LEAST:
                    if (string.IsNullOrWhiteSpace(condition.targetId))
                        return label + ".targetId가 필요합니다.";
                    if (string.IsNullOrWhiteSpace(condition.currencyId))
                        return label + ".currencyId가 필요합니다.";
                    return condition.amount <= 0
                        ? label + ".amount는 0보다 커야 합니다."
                        : null;
                case StepConditionRules.UNLOCKER_UNLOCKED:
                    return string.IsNullOrWhiteSpace(condition.unlockerId)
                        ? label + ".unlockerId가 필요합니다."
                        : null;
                case StepConditionRules.CAPABILITY_LEVEL_AT_LEAST:
                    if (string.IsNullOrWhiteSpace(condition.targetId))
                        return label + ".targetId(capabilityId)가 필요합니다.";
                    return condition.amount < 0
                        ? label + ".amount는 0 이상이어야 합니다."
                        : null;
                case BEAT_COMPLETED:
                case StepConditionRules.ACTION_STARTED:
                case StepConditionRules.ACTION_COMPLETED:
                case SYSTEM_ACTION_ACTIVATED:
                    return string.IsNullOrWhiteSpace(condition.targetId)
                        ? label + ".targetId가 필요합니다."
                        : null;
                case StepConditionRules.GAMEPLAY_SIGNAL:
                    return GameplaySignalRules.Validate(condition.signalId, condition.targetId, condition.item, condition.currencyId, label);
                default:
                    return label + ".type '" + type + "'는 reactive module에서 지원하지 않습니다.";
            }
        }

        public static string ValidateGroup(Supercent.PlayableAI.Common.Format.ReactiveConditionGroupDefinition group, string label)
        {
            if (group == null)
                return label + "가 필요합니다.";

            if (string.IsNullOrWhiteSpace(group.mode))
                return label + ".mode가 필요합니다.";

            if (group.delaySeconds < 0f)
                return label + ".delaySeconds는 0 이상이어야 합니다.";

            string mode = group.mode.Trim();
            if (!string.Equals(mode, MODE_ALL, System.StringComparison.Ordinal) &&
                !string.Equals(mode, MODE_ANY, System.StringComparison.Ordinal))
            {
                return label + ".mode '" + mode + "'는 지원하지 않습니다.";
            }

            Supercent.PlayableAI.Common.Format.ReactiveConditionDefinition[] conditions =
                group.conditions ?? new Supercent.PlayableAI.Common.Format.ReactiveConditionDefinition[0];
            if (conditions.Length == 0)
                return label + ".conditions에는 최소 1개의 condition이 필요합니다.";

            for (int i = 0; i < conditions.Length; i++)
            {
                string error = Validate(conditions[i], label + ".conditions[" + i + "]");
                if (!string.IsNullOrEmpty(error))
                    return error;
            }

            return null;
        }
    }
}
