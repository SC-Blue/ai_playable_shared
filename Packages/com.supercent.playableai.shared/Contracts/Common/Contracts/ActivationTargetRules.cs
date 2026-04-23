using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public static class ActivationTargetRules
    {
        public static string Validate(ActivationTargetDefinition target, string label)
        {
            if (target == null)
                return label + "가 null입니다.";

            if (string.IsNullOrWhiteSpace(target.kind))
                return label + ".kind가 필요합니다.";

            if (string.IsNullOrWhiteSpace(target.id))
                return label + ".id가 필요합니다.";

            string kind = target.kind.Trim();
            switch (kind)
            {
                case ActivationTargetKinds.SCENE_REF:
                    return null;
                case ActivationTargetKinds.SYSTEM_ACTION:
                    return SystemActionIds.IsSupportedRuntimeTargetId(target.id)
                        ? null
                        : label + ".id '" + target.id + "'는 지원하지 않는 system action입니다.";
                default:
                    return label + ".kind '" + kind + "'는 지원하지 않습니다.";
            }
        }
    }

    public static class GameplayOverlapAllowanceRules
    {
        public static class RailEndpointSideRules
        {
            public const string LEFT = PromptIntentContractRegistry.RAIL_ENDPOINT_SIDE_LEFT;
            public const string RIGHT = PromptIntentContractRegistry.RAIL_ENDPOINT_SIDE_RIGHT;
            public const string TOP = PromptIntentContractRegistry.RAIL_ENDPOINT_SIDE_TOP;
            public const string BOTTOM = PromptIntentContractRegistry.RAIL_ENDPOINT_SIDE_BOTTOM;

            public static bool IsSupported(string side)
            {
                return PromptIntentContractRegistry.IsSupportedRailEndpointSide(side);
            }
        }

        public static Dictionary<string, HashSet<string>> BuildUnlockTargetReferenceLookup(PromptIntentStageDefinition[] stages)
        {
            var lookup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            PromptIntentStageDefinition[] safeStages = stages ?? new PromptIntentStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                PromptIntentStageDefinition stage = safeStages[i];
                if (stage == null)
                    continue;

                string unlockerId = ResolvePromptUnlockerId(stage.objectives);
                if (string.IsNullOrEmpty(unlockerId))
                    continue;

                PromptIntentEffectDefinition[] effects = stage.onComplete ?? new PromptIntentEffectDefinition[0];
                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    if (!TryResolvePromptSpatialTargetId(effects[effectIndex], out string targetId))
                        continue;

                    AddLookupTarget(lookup, unlockerId, targetId);
                }
            }

            return ExpandTransitiveUnlockTargetReferenceLookup(lookup);
        }

        public static Dictionary<string, HashSet<string>> BuildUnlockTargetReferenceLookup(ScenarioModelStageDefinition[] stages)
        {
            var lookup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            ScenarioModelStageDefinition[] safeStages = stages ?? new ScenarioModelStageDefinition[0];
            for (int i = 0; i < safeStages.Length; i++)
            {
                ScenarioModelStageDefinition stage = safeStages[i];
                if (stage == null)
                    continue;

                string unlockerId = ResolveScenarioUnlockerId(stage.objectives);
                if (string.IsNullOrEmpty(unlockerId))
                    continue;

                ScenarioModelEffectDefinition[] effects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];
                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    if (!TryResolveScenarioSpatialTargetId(effects[effectIndex], out string targetId))
                        continue;

                    AddLookupTarget(lookup, unlockerId, targetId);
                }
            }

            return ExpandTransitiveUnlockTargetReferenceLookup(lookup);
        }

        public static Dictionary<string, HashSet<string>> BuildUnlockTargetReferenceLookup(UnlockDefinition[] unlocks)
        {
            var lookup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            UnlockDefinition[] safeUnlocks = unlocks ?? new UnlockDefinition[0];
            for (int i = 0; i < safeUnlocks.Length; i++)
            {
                UnlockDefinition unlock = safeUnlocks[i];
                string unlockerId = Normalize(unlock != null ? unlock.unlockerId : string.Empty);
                if (string.IsNullOrEmpty(unlockerId))
                    continue;

                ActivationTargetDefinition[] targets = unlock.targets ?? new ActivationTargetDefinition[0];
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    ActivationTargetDefinition target = targets[targetIndex];
                    if (target == null ||
                        !string.Equals(Normalize(target.kind), ActivationTargetKinds.SCENE_REF, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddLookupTarget(lookup, unlockerId, target.id);
                }
            }

            return ExpandTransitiveUnlockTargetReferenceLookup(lookup);
        }

        public static string ResolveCompiledGameplayRole(string gameplayObjectId)
        {
            return PromptIntentContractRegistry.ResolveCompiledGameplayRole(gameplayObjectId);
        }

        private static string ResolvePromptUnlockerId(PromptIntentObjectiveDefinition[] objectives)
        {
            PromptIntentObjectiveDefinition[] safeObjectives = objectives ?? new PromptIntentObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                PromptIntentObjectiveDefinition objective = safeObjectives[i];
                if (!string.Equals(Normalize(objective != null ? objective.kind : string.Empty), PromptIntentObjectiveKinds.UNLOCK_OBJECT, StringComparison.Ordinal))
                    continue;

                return Normalize(objective.targetObjectId);
            }

            return string.Empty;
        }

        private static string ResolveScenarioUnlockerId(ScenarioModelObjectiveDefinition[] objectives)
        {
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = safeObjectives[i];
                if (!string.Equals(Normalize(objective != null ? objective.kind : string.Empty), PromptIntentObjectiveKinds.UNLOCK_OBJECT, StringComparison.Ordinal))
                    continue;

                return Normalize(objective.targetObjectId);
            }

            return string.Empty;
        }

        private static bool TryResolvePromptSpatialTargetId(PromptIntentEffectDefinition effect, out string targetId)
        {
            targetId = string.Empty;
            string kind = Normalize(effect != null ? effect.kind : string.Empty);
            if (!string.Equals(kind, PromptIntentEffectKinds.REVEAL_OBJECT, StringComparison.Ordinal) &&
                !string.Equals(kind, PromptIntentEffectKinds.ACTIVATE_OBJECT, StringComparison.Ordinal))
            {
                return false;
            }

            targetId = Normalize(effect.targetObjectId);
            return !string.IsNullOrEmpty(targetId);
        }

        private static bool TryResolveScenarioSpatialTargetId(ScenarioModelEffectDefinition effect, out string targetId)
        {
            targetId = string.Empty;
            string kind = Normalize(effect != null ? effect.kind : string.Empty);
            if (!string.Equals(kind, PromptIntentEffectKinds.REVEAL_OBJECT, StringComparison.Ordinal) &&
                !string.Equals(kind, PromptIntentEffectKinds.ACTIVATE_OBJECT, StringComparison.Ordinal))
            {
                return false;
            }

            targetId = Normalize(effect.targetObjectId);
            return !string.IsNullOrEmpty(targetId);
        }

        private static void AddLookupTarget(
            Dictionary<string, HashSet<string>> lookup,
            string unlockerId,
            string targetId)
        {
            string normalizedUnlockerId = Normalize(unlockerId);
            string normalizedTargetId = Normalize(targetId);
            if (string.IsNullOrEmpty(normalizedUnlockerId) || string.IsNullOrEmpty(normalizedTargetId))
                return;

            if (!lookup.TryGetValue(normalizedUnlockerId, out HashSet<string> targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                lookup.Add(normalizedUnlockerId, targets);
            }

            targets.Add(normalizedTargetId);
        }

        private static Dictionary<string, HashSet<string>> ExpandTransitiveUnlockTargetReferenceLookup(
            Dictionary<string, HashSet<string>> directLookup)
        {
            if (directLookup == null || directLookup.Count == 0)
                return directLookup ?? new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            var expanded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<string>> pair in directLookup)
            {
                string unlockerId = Normalize(pair.Key);
                var closure = new HashSet<string>(StringComparer.Ordinal);
                var stack = new Stack<string>();
                HashSet<string> directTargets = pair.Value ?? new HashSet<string>(StringComparer.Ordinal);
                foreach (string targetId in directTargets)
                {
                    string normalizedTargetId = Normalize(targetId);
                    if (string.IsNullOrEmpty(normalizedTargetId) ||
                        string.Equals(normalizedTargetId, unlockerId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (closure.Add(normalizedTargetId))
                        stack.Push(normalizedTargetId);
                }

                while (stack.Count > 0)
                {
                    string currentId = Normalize(stack.Pop());
                    if (string.IsNullOrEmpty(currentId) ||
                        !directLookup.TryGetValue(currentId, out HashSet<string> nextTargets) ||
                        nextTargets == null)
                    {
                        continue;
                    }

                    foreach (string nextTargetId in nextTargets)
                    {
                        string normalizedNextTargetId = Normalize(nextTargetId);
                        if (string.IsNullOrEmpty(normalizedNextTargetId) ||
                            string.Equals(normalizedNextTargetId, unlockerId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (closure.Add(normalizedNextTargetId))
                            stack.Push(normalizedNextTargetId);
                    }
                }

                expanded[unlockerId] = closure;
            }

            return expanded;
        }
        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
