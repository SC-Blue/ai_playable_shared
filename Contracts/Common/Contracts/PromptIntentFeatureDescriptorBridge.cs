using System;
using System.Collections.Generic;

namespace Supercent.PlayableAI.Common.Contracts
{
    internal static class PromptIntentFeatureDescriptorBridge
    {
        private static FeatureDescriptor[] s_activeFeatureDescriptors = new FeatureDescriptor[0];

        public static void SetActiveFeatureDescriptors(FeatureDescriptor[] descriptors)
        {
            s_activeFeatureDescriptors = FeatureDescriptorUtility.CloneArray(descriptors);
        }

        public static void ClearActiveFeatureDescriptors()
        {
            s_activeFeatureDescriptors = new FeatureDescriptor[0];
        }

        public static PromptIntentObjectRoleDescriptor[] MergeObjectRoles(PromptIntentObjectRoleDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentObjectRoleDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseObjectRoles(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureObjectRoleDescriptor[] roles = descriptors[i].objectRoles ?? new FeatureObjectRoleDescriptor[0];
                for (int j = 0; j < roles.Length; j++)
                {
                    FeatureObjectRoleDescriptor role = roles[j];
                    string key = Normalize(role != null ? role.role : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentObjectRoleDescriptor
                        {
                            value = key,
                            summary = role != null ? role.summary ?? string.Empty : string.Empty,
                            catalogBacked = role != null && role.catalogBacked,
                            supportsDesignId = role != null && role.supportsDesignId,
                            supportsFeatureOptions = role != null && role.supportsFeatureOptions,
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentFeatureOptionDescriptor[] MergeFeatureOptions(PromptIntentFeatureOptionDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentFeatureOptionDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(new PromptIntentFeatureOptionDescriptor[0], merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureDescriptor feature = descriptors[i];
                string[] supportedRoles = ExtractFeatureRoles(feature);
                FeatureOptionFieldDescriptor[] fields = feature.optionSchema != null
                    ? feature.optionSchema.fields ?? new FeatureOptionFieldDescriptor[0]
                    : new FeatureOptionFieldDescriptor[0];

                for (int j = 0; j < fields.Length; j++)
                {
                    FeatureOptionFieldDescriptor field = fields[j];
                    string key = Normalize(field != null ? field.fieldId : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentFeatureOptionDescriptor existing))
                    {
                        existing.summary = Prefer(field != null ? field.summary : string.Empty, existing.summary);
                        existing.supportedRoles = UnionStrings(existing.supportedRoles, supportedRoles);
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentFeatureOptionDescriptor
                        {
                            value = key,
                            summary = field != null ? field.summary ?? string.Empty : string.Empty,
                            supportedRoles = CloneStrings(supportedRoles),
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentConditionKindDescriptor[] MergeConditionKinds(PromptIntentConditionKindDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentConditionKindDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseConditionKinds(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureConditionDescriptor[] conditions = descriptors[i].conditionKinds ?? new FeatureConditionDescriptor[0];
                for (int j = 0; j < conditions.Length; j++)
                {
                    FeatureConditionDescriptor condition = conditions[j];
                    string key = Normalize(condition != null ? condition.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentConditionKindDescriptor existing))
                    {
                        existing.summary = Prefer(condition != null ? condition.summary : string.Empty, existing.summary);
                        existing.supportsStageId = existing.supportsStageId || (condition != null && condition.supportsStageId);
                        existing.requiresStageId = existing.requiresStageId || (condition != null && condition.requiresStageId);
                        existing.supportsTargetObjectId = existing.supportsTargetObjectId || (condition != null && condition.supportsTargetObjectId);
                        existing.requiresTargetObjectId = existing.requiresTargetObjectId || (condition != null && condition.requiresTargetObjectId);
                        existing.supportsItem = existing.supportsItem || (condition != null && condition.supportsItem);
                        existing.requiresItem = existing.requiresItem || (condition != null && condition.requiresItem);
                        existing.supportsCurrencyId = existing.supportsCurrencyId || (condition != null && condition.supportsCurrencyId);
                        existing.requiresCurrencyId = existing.requiresCurrencyId || (condition != null && condition.requiresCurrencyId);
                        existing.supportsAmountValue = existing.supportsAmountValue || (condition != null && condition.supportsAmountValue);
                        existing.requiresPositiveAmountValue = existing.requiresPositiveAmountValue || (condition != null && condition.requiresPositiveAmountValue);
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentConditionKindDescriptor
                        {
                            value = key,
                            summary = condition != null ? condition.summary ?? string.Empty : string.Empty,
                            supportsStageId = condition != null && condition.supportsStageId,
                            requiresStageId = condition != null && condition.requiresStageId,
                            supportsTargetObjectId = condition != null && condition.supportsTargetObjectId,
                            requiresTargetObjectId = condition != null && condition.requiresTargetObjectId,
                            supportsItem = condition != null && condition.supportsItem,
                            requiresItem = condition != null && condition.requiresItem,
                            supportsCurrencyId = condition != null && condition.supportsCurrencyId,
                            requiresCurrencyId = condition != null && condition.requiresCurrencyId,
                            supportsAmountValue = condition != null && condition.supportsAmountValue,
                            requiresPositiveAmountValue = condition != null && condition.requiresPositiveAmountValue,
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentObjectiveKindDescriptor[] MergeObjectiveKinds(PromptIntentObjectiveKindDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentObjectiveKindDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseObjectiveKinds(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureObjectiveDescriptor[] objectives = descriptors[i].objectiveKinds ?? new FeatureObjectiveDescriptor[0];
                for (int j = 0; j < objectives.Length; j++)
                {
                    FeatureObjectiveDescriptor objective = objectives[j];
                    string key = Normalize(objective != null ? objective.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentObjectiveKindDescriptor existing))
                    {
                        existing.summary = Prefer(objective != null ? objective.summary : string.Empty, existing.summary);
                        existing.requiresTargetObjectId = existing.requiresTargetObjectId || (objective != null && objective.requiresTargetObjectId);
                        existing.requiresItem = existing.requiresItem || (objective != null && objective.requiresItem);
                        existing.requiresInputItem = existing.requiresInputItem || (objective != null && objective.requiresInputItem);
                        existing.requiresCurrencyId = existing.requiresCurrencyId || (objective != null && objective.requiresCurrencyId);
                        existing.requiresAmountValue = existing.requiresAmountValue || (objective != null && objective.requiresAmountValue);
                        existing.requiresSeconds = existing.requiresSeconds || (objective != null && objective.requiresSeconds);
                        existing.canAbsorbArrow = existing.canAbsorbArrow || (objective != null && (objective.canAbsorbArrow || objective.requiresAbsorbedArrow));
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentObjectiveKindDescriptor
                        {
                            value = key,
                            summary = objective != null ? objective.summary ?? string.Empty : string.Empty,
                            requiresTargetObjectId = objective != null && objective.requiresTargetObjectId,
                            requiresItem = objective != null && objective.requiresItem,
                            requiresInputItem = objective != null && objective.requiresInputItem,
                            requiresCurrencyId = objective != null && objective.requiresCurrencyId,
                            requiresAmountValue = objective != null && objective.requiresAmountValue,
                            requiresSeconds = objective != null && objective.requiresSeconds,
                            canAbsorbArrow = objective != null && (objective.canAbsorbArrow || objective.requiresAbsorbedArrow),
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentEffectKindDescriptor[] MergeEffectKinds(PromptIntentEffectKindDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentEffectKindDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseEffectKinds(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureEffectDescriptor[] effects = descriptors[i].effectKinds ?? new FeatureEffectDescriptor[0];
                for (int j = 0; j < effects.Length; j++)
                {
                    FeatureEffectDescriptor effect = effects[j];
                    string key = Normalize(effect != null ? effect.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentEffectKindDescriptor
                        {
                            value = key,
                            summary = effect != null ? effect.summary ?? string.Empty : string.Empty,
                            requiresTargetObjectId = effect != null && effect.requiresTargetObjectId,
                            supportsTiming = effect != null && effect.supportsTiming,
                            requiresEventKey = effect != null && effect.requiresEventKey,
                            supportsEventKey = effect != null && effect.supportsEventKey,
                            isNonBlockingSystemAction = effect != null && effect.isNonBlockingSystemAction,
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentValueDescriptor[] MergeFlowTargetEventKeys(PromptIntentValueDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentValueDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(builtins, merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureTargetSurfaceDescriptor[] surfaces = descriptors[i].targetSurfaces ?? new FeatureTargetSurfaceDescriptor[0];
                for (int j = 0; j < surfaces.Length; j++)
                {
                    string[] supportedEventKeys = surfaces[j] != null
                        ? surfaces[j].supportedEventKeys ?? new string[0]
                        : new string[0];
                    for (int k = 0; k < supportedEventKeys.Length; k++)
                    {
                        string key = Normalize(supportedEventKeys[k]);
                        if (string.IsNullOrEmpty(key))
                            continue;

                        Upsert(
                            merged,
                            order,
                            key,
                            new PromptIntentValueDescriptor
                            {
                                value = key,
                                summary = key,
                            });
                    }
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentCompiledGameplayRoleDescriptor[] MergeCompiledGameplayRoles(PromptIntentCompiledGameplayRoleDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentCompiledGameplayRoleDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseCompiledGameplayRoles(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureCompiledGameplayRoleDescriptor[] mappings = descriptors[i].compiledGameplayRoleMappings ?? new FeatureCompiledGameplayRoleDescriptor[0];
                for (int j = 0; j < mappings.Length; j++)
                {
                    FeatureCompiledGameplayRoleDescriptor mapping = mappings[j];
                    string key = Normalize(mapping != null ? mapping.gameplayObjectId : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentCompiledGameplayRoleDescriptor
                        {
                            gameplayObjectId = key,
                            role = Normalize(mapping != null ? mapping.role : string.Empty),
                            summary = mapping != null ? mapping.summary ?? string.Empty : string.Empty,
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static string ResolveFeatureTypeForRole(string role)
        {
            string normalizedRole = Normalize(role);
            if (string.IsNullOrEmpty(normalizedRole))
                return string.Empty;

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureDescriptor descriptor = descriptors[i];
                FeatureObjectRoleDescriptor[] roles = descriptor != null ? descriptor.objectRoles ?? new FeatureObjectRoleDescriptor[0] : new FeatureObjectRoleDescriptor[0];
                for (int j = 0; j < roles.Length; j++)
                {
                    if (Normalize(roles[j] != null ? roles[j].role : string.Empty) == normalizedRole)
                        return Normalize(descriptor != null ? descriptor.featureType : string.Empty);
                }
            }

            return string.Empty;
        }

        public static bool ObjectiveDefinesFeatureOutputItem(string featureType, string objectiveKind)
        {
            string normalizedFeatureType = Normalize(featureType);
            string normalizedObjectiveKind = Normalize(objectiveKind);
            if (string.IsNullOrEmpty(normalizedFeatureType) || string.IsNullOrEmpty(normalizedObjectiveKind))
                return false;

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureDescriptor descriptor = descriptors[i];
                if (!string.Equals(Normalize(descriptor != null ? descriptor.featureType : string.Empty), normalizedFeatureType, StringComparison.Ordinal))
                    continue;

                return ObjectiveDefinesFeatureOutputItem(descriptor, normalizedObjectiveKind);
            }

            return false;
        }

        public static bool ObjectiveKindDefinesFeatureOutputItem(string objectiveKind)
        {
            string normalizedObjectiveKind = Normalize(objectiveKind);
            if (string.IsNullOrEmpty(normalizedObjectiveKind))
                return false;

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureDescriptor descriptor = descriptors[i];
                if (ObjectiveDefinesFeatureOutputItem(descriptor, normalizedObjectiveKind))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ObjectiveDefinesFeatureOutputItem(FeatureDescriptor descriptor, string normalizedObjectiveKind)
        {
            if (descriptor == null || string.IsNullOrEmpty(normalizedObjectiveKind))
                return false;

            FeatureInputOutputSemantics semantics = descriptor.inputOutputSemantics;
            if (ContainsString(semantics != null ? semantics.generatedItems : new string[0], normalizedObjectiveKind) ||
                ContainsString(semantics != null ? semantics.outputItems : new string[0], normalizedObjectiveKind))
            {
                return true;
            }

            FeatureObjectiveDescriptor objective = FindObjectiveKind(descriptor, normalizedObjectiveKind);
            if (objective == null ||
                objective.requiresItem ||
                !objective.requiresInputItem)
            {
                return false;
            }

            FeatureGameplaySignalDescriptor completionSignal = FindGameplaySignal(descriptor, objective.completionGameplaySignalId);
            return completionSignal != null &&
                   (completionSignal.requiresItem || completionSignal.supportsItem);
        }

        private static FeatureObjectiveDescriptor FindObjectiveKind(FeatureDescriptor descriptor, string normalizedObjectiveKind)
        {
            FeatureObjectiveDescriptor[] objectives = descriptor != null ? descriptor.objectiveKinds ?? new FeatureObjectiveDescriptor[0] : new FeatureObjectiveDescriptor[0];
            for (int i = 0; i < objectives.Length; i++)
            {
                FeatureObjectiveDescriptor objective = objectives[i];
                if (objective != null &&
                    string.Equals(Normalize(objective.kind), normalizedObjectiveKind, StringComparison.Ordinal))
                {
                    return objective;
                }
            }

            return null;
        }

        private static FeatureGameplaySignalDescriptor FindGameplaySignal(FeatureDescriptor descriptor, string signalId)
        {
            string normalizedSignalId = Normalize(signalId);
            if (string.IsNullOrEmpty(normalizedSignalId))
                return null;

            FeatureGameplaySignalDescriptor[] signals = descriptor != null ? descriptor.gameplaySignals ?? new FeatureGameplaySignalDescriptor[0] : new FeatureGameplaySignalDescriptor[0];
            for (int i = 0; i < signals.Length; i++)
            {
                FeatureGameplaySignalDescriptor signal = signals[i];
                if (signal != null &&
                    string.Equals(Normalize(signal.signalId), normalizedSignalId, StringComparison.Ordinal))
                {
                    return signal;
                }
            }

            return null;
        }

        public static PromptIntentTargetSurfaceRuleDescriptor[] MergeTargetSurfaces(PromptIntentTargetSurfaceRuleDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentTargetSurfaceRuleDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(new PromptIntentTargetSurfaceRuleDescriptor[0], merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureTargetSurfaceDescriptor[] surfaces = descriptors[i].targetSurfaces ?? new FeatureTargetSurfaceDescriptor[0];
                for (int j = 0; j < surfaces.Length; j++)
                {
                    FeatureTargetSurfaceDescriptor surface = surfaces[j];
                    string key = Normalize(surface != null ? surface.role : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    string[] eventKeys = surface != null ? surface.supportedEventKeys ?? new string[0] : new string[0];
                    if (merged.TryGetValue(key, out PromptIntentTargetSurfaceRuleDescriptor existing))
                    {
                        existing.summary = Prefer(surface != null ? surface.summary : string.Empty, existing.summary);
                        existing.supportedEventKeys = UnionStrings(existing.supportedEventKeys, eventKeys);
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentTargetSurfaceRuleDescriptor
                        {
                            role = key,
                            summary = surface != null ? surface.summary ?? string.Empty : string.Empty,
                            supportedEventKeys = CloneStrings(eventKeys),
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentGameplaySignalRuleDescriptor[] MergeGameplaySignals(PromptIntentGameplaySignalRuleDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentGameplaySignalRuleDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(new PromptIntentGameplaySignalRuleDescriptor[0], merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureGameplaySignalDescriptor[] signals = descriptors[i].gameplaySignals ?? new FeatureGameplaySignalDescriptor[0];
                for (int j = 0; j < signals.Length; j++)
                {
                    FeatureGameplaySignalDescriptor signal = signals[j];
                    string key = Normalize(signal != null ? signal.signalId : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentGameplaySignalRuleDescriptor existing))
                    {
                        existing.summary = Prefer(signal != null ? signal.summary : string.Empty, existing.summary);
                        existing.supportsTargetId = existing.supportsTargetId || (signal != null && signal.supportsTargetId);
                        existing.requiresTargetId = existing.requiresTargetId || (signal != null && signal.requiresTargetId);
                        existing.supportsItem = existing.supportsItem || (signal != null && signal.supportsItem);
                        existing.requiresItem = existing.requiresItem || (signal != null && signal.requiresItem);
                        existing.supportsCurrencyId = existing.supportsCurrencyId || (signal != null && signal.supportsCurrencyId);
                        existing.requiresCurrencyId = existing.requiresCurrencyId || (signal != null && signal.requiresCurrencyId);
                        existing.requiredTargetEventKey = Prefer(Normalize(signal != null ? signal.requiredTargetEventKey : string.Empty), existing.requiredTargetEventKey);
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentGameplaySignalRuleDescriptor
                        {
                            signalId = key,
                            summary = signal != null ? signal.summary ?? string.Empty : string.Empty,
                            supportsTargetId = signal != null && signal.supportsTargetId,
                            requiresTargetId = signal != null && signal.requiresTargetId,
                            supportsItem = signal != null && signal.supportsItem,
                            requiresItem = signal != null && signal.requiresItem,
                            supportsCurrencyId = signal != null && signal.supportsCurrencyId,
                            requiresCurrencyId = signal != null && signal.requiresCurrencyId,
                            requiredTargetEventKey = Normalize(signal != null ? signal.requiredTargetEventKey : string.Empty),
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentConditionCapabilityDescriptor[] MergeConditionCapabilities(PromptIntentConditionCapabilityDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentConditionCapabilityDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseConditionCapabilities(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureConditionDescriptor[] conditions = descriptors[i].conditionKinds ?? new FeatureConditionDescriptor[0];
                for (int j = 0; j < conditions.Length; j++)
                {
                    FeatureConditionDescriptor condition = conditions[j];
                    string key = Normalize(condition != null ? condition.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentConditionCapabilityDescriptor existing))
                    {
                        existing.summary = Prefer(condition != null ? condition.summary : string.Empty, existing.summary);
                        existing.supportedTargetRoles = UnionStrings(existing.supportedTargetRoles, condition != null ? condition.supportedTargetRoles : new string[0]);
                        existing.allowAnyTargetRole = existing.allowAnyTargetRole || (condition != null && condition.allowAnyTargetRole);
                        existing.gameplaySignalId = Prefer(Normalize(condition != null ? condition.gameplaySignalId : string.Empty), existing.gameplaySignalId);
                        existing.stepConditionType = Prefer(Normalize(condition != null ? condition.stepConditionType : string.Empty), existing.stepConditionType);
                        existing.reactiveConditionType = Prefer(Normalize(condition != null ? condition.reactiveConditionType : string.Empty), existing.reactiveConditionType);
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentConditionCapabilityDescriptor
                        {
                            kind = key,
                            summary = condition != null ? condition.summary ?? string.Empty : string.Empty,
                            supportedTargetRoles = condition != null ? CloneStrings(condition.supportedTargetRoles) : new string[0],
                            allowAnyTargetRole = condition != null && condition.allowAnyTargetRole,
                            gameplaySignalId = Normalize(condition != null ? condition.gameplaySignalId : string.Empty),
                            stepConditionType = Normalize(condition != null ? condition.stepConditionType : string.Empty),
                            reactiveConditionType = Normalize(condition != null ? condition.reactiveConditionType : string.Empty),
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentObjectiveCapabilityDescriptor[] MergeObjectiveCapabilities(PromptIntentObjectiveCapabilityDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentObjectiveCapabilityDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseObjectiveCapabilities(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureObjectiveDescriptor[] objectives = descriptors[i].objectiveKinds ?? new FeatureObjectiveDescriptor[0];
                for (int j = 0; j < objectives.Length; j++)
                {
                    FeatureObjectiveDescriptor objective = objectives[j];
                    string key = Normalize(objective != null ? objective.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentObjectiveCapabilityDescriptor existing))
                    {
                        existing.summary = Prefer(objective != null ? objective.summary : string.Empty, existing.summary);
                        existing.supportedTargetRoles = UnionStrings(existing.supportedTargetRoles, objective != null ? objective.supportedTargetRoles : new string[0]);
                        existing.allowAnyTargetRole = existing.allowAnyTargetRole || (objective != null && objective.allowAnyTargetRole);
                        existing.completionStepConditionType = Prefer(Normalize(objective != null ? objective.completionStepConditionType : string.Empty), existing.completionStepConditionType);
                        existing.completionGameplaySignalId = Prefer(Normalize(objective != null ? objective.completionGameplaySignalId : string.Empty), existing.completionGameplaySignalId);
                        existing.targetEventKey = Prefer(Normalize(objective != null ? objective.targetEventKey : string.Empty), existing.targetEventKey);
                        existing.requiresAbsorbedArrow = existing.requiresAbsorbedArrow || (objective != null && objective.requiresAbsorbedArrow);
                        existing.requiredArrowEventKey = Prefer(Normalize(objective != null ? objective.requiredArrowEventKey : string.Empty), existing.requiredArrowEventKey);
                        merged[key] = existing;
                        continue;
                    }

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentObjectiveCapabilityDescriptor
                        {
                            kind = key,
                            summary = objective != null ? objective.summary ?? string.Empty : string.Empty,
                            supportedTargetRoles = objective != null ? CloneStrings(objective.supportedTargetRoles) : new string[0],
                            allowAnyTargetRole = objective != null && objective.allowAnyTargetRole,
                            completionStepConditionType = Normalize(objective != null ? objective.completionStepConditionType : string.Empty),
                            completionGameplaySignalId = Normalize(objective != null ? objective.completionGameplaySignalId : string.Empty),
                            targetEventKey = Normalize(objective != null ? objective.targetEventKey : string.Empty),
                            requiresAbsorbedArrow = objective != null && objective.requiresAbsorbedArrow,
                            requiredArrowEventKey = Normalize(objective != null ? objective.requiredArrowEventKey : string.Empty),
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentEffectCapabilityDescriptor[] MergeEffectCapabilities(PromptIntentEffectCapabilityDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentEffectCapabilityDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseEffectCapabilities(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureEffectDescriptor[] effects = descriptors[i].effectKinds ?? new FeatureEffectDescriptor[0];
                for (int j = 0; j < effects.Length; j++)
                {
                    FeatureEffectDescriptor effect = effects[j];
                    string key = Normalize(effect != null ? effect.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentEffectCapabilityDescriptor
                        {
                            kind = key,
                            summary = effect != null ? effect.summary ?? string.Empty : string.Empty,
                            semanticTags = effect != null ? CloneStrings(effect.semanticTags) : new string[0],
                            supportedTargetRoles = effect != null ? CloneStrings(effect.supportedTargetRoles) : new string[0],
                            allowAnyTargetRole = effect != null && effect.allowAnyTargetRole,
                            systemActionId = Normalize(effect != null ? effect.systemActionId : string.Empty),
                            runtimeEventKey = Normalize(effect != null ? effect.runtimeEventKey : string.Empty),
                            buildsSceneActivationTarget = effect != null && effect.buildsSceneActivationTarget,
                            buildsSystemActionTarget = effect != null && effect.buildsSystemActionTarget,
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        private static FeatureDescriptor[] GetEffectiveFeatureDescriptors()
        {
            return FeatureDescriptorUtility.CloneArray(s_activeFeatureDescriptors);
        }

        private static PromptIntentObjectRoleDescriptor[] FilterBaseObjectRoles(PromptIntentObjectRoleDescriptor[] values)
        {
            var filtered = new List<PromptIntentObjectRoleDescriptor>();
            PromptIntentObjectRoleDescriptor[] safeValues = values ?? new PromptIntentObjectRoleDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentConditionKindDescriptor[] FilterBaseConditionKinds(PromptIntentConditionKindDescriptor[] values)
        {
            var filtered = new List<PromptIntentConditionKindDescriptor>();
            PromptIntentConditionKindDescriptor[] safeValues = values ?? new PromptIntentConditionKindDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentObjectiveKindDescriptor[] FilterBaseObjectiveKinds(PromptIntentObjectiveKindDescriptor[] values)
        {
            var filtered = new List<PromptIntentObjectiveKindDescriptor>();
            PromptIntentObjectiveKindDescriptor[] safeValues = values ?? new PromptIntentObjectiveKindDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentEffectKindDescriptor[] FilterBaseEffectKinds(PromptIntentEffectKindDescriptor[] values)
        {
            var filtered = new List<PromptIntentEffectKindDescriptor>();
            PromptIntentEffectKindDescriptor[] safeValues = values ?? new PromptIntentEffectKindDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentCompiledGameplayRoleDescriptor[] FilterBaseCompiledGameplayRoles(PromptIntentCompiledGameplayRoleDescriptor[] values)
        {
            var filtered = new List<PromptIntentCompiledGameplayRoleDescriptor>();
            PromptIntentCompiledGameplayRoleDescriptor[] safeValues = values ?? new PromptIntentCompiledGameplayRoleDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentConditionCapabilityDescriptor[] FilterBaseConditionCapabilities(PromptIntentConditionCapabilityDescriptor[] values)
        {
            var filtered = new List<PromptIntentConditionCapabilityDescriptor>();
            PromptIntentConditionCapabilityDescriptor[] safeValues = values ?? new PromptIntentConditionCapabilityDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentObjectiveCapabilityDescriptor[] FilterBaseObjectiveCapabilities(PromptIntentObjectiveCapabilityDescriptor[] values)
        {
            var filtered = new List<PromptIntentObjectiveCapabilityDescriptor>();
            PromptIntentObjectiveCapabilityDescriptor[] safeValues = values ?? new PromptIntentObjectiveCapabilityDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentEffectCapabilityDescriptor[] FilterBaseEffectCapabilities(PromptIntentEffectCapabilityDescriptor[] values)
        {
            var filtered = new List<PromptIntentEffectCapabilityDescriptor>();
            PromptIntentEffectCapabilityDescriptor[] safeValues = values ?? new PromptIntentEffectCapabilityDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (safeValues[i] != null)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static string[] ExtractFeatureRoles(FeatureDescriptor descriptor)
        {
            FeatureObjectRoleDescriptor[] roles = descriptor != null ? descriptor.objectRoles ?? new FeatureObjectRoleDescriptor[0] : new FeatureObjectRoleDescriptor[0];
            var values = new List<string>();
            for (int i = 0; i < roles.Length; i++)
            {
                string role = Normalize(roles[i] != null ? roles[i].role : string.Empty);
                if (!string.IsNullOrEmpty(role) && !values.Contains(role))
                    values.Add(role);
            }

            return values.ToArray();
        }

        private static void AddBaseDescriptors<TDescriptor>(TDescriptor[] values, Dictionary<string, TDescriptor> merged, List<string> order)
        {
            TDescriptor[] safeValues = values ?? new TDescriptor[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                TDescriptor descriptor = safeValues[i];
                string key = ResolveKey(descriptor);
                if (string.IsNullOrEmpty(key))
                    continue;

                Upsert(merged, order, key, CloneDescriptor(descriptor));
            }
        }

        private static void Upsert<TDescriptor>(Dictionary<string, TDescriptor> merged, List<string> order, string key, TDescriptor descriptor)
        {
            if (!merged.ContainsKey(key))
                order.Add(key);

            merged[key] = descriptor;
        }

        private static TDescriptor[] ToOrderedArray<TDescriptor>(Dictionary<string, TDescriptor> merged, List<string> order)
        {
            var result = new TDescriptor[order.Count];
            for (int i = 0; i < order.Count; i++)
                result[i] = CloneDescriptor(merged[order[i]]);
            return result;
        }

        private static string ResolveKey<TDescriptor>(TDescriptor descriptor)
        {
            object boxed = descriptor;
            switch (boxed)
            {
                case PromptIntentObjectRoleDescriptor value:
                    return Normalize(value.value);
                case PromptIntentFeatureOptionDescriptor value:
                    return Normalize(value.value);
                case PromptIntentConditionKindDescriptor value:
                    return Normalize(value.value);
                case PromptIntentObjectiveKindDescriptor value:
                    return Normalize(value.value);
                case PromptIntentEffectKindDescriptor value:
                    return Normalize(value.value);
                case PromptIntentCompiledGameplayRoleDescriptor value:
                    return Normalize(value.gameplayObjectId);
                case PromptIntentTargetSurfaceRuleDescriptor value:
                    return Normalize(value.role);
                case PromptIntentGameplaySignalRuleDescriptor value:
                    return Normalize(value.signalId);
                case PromptIntentConditionCapabilityDescriptor value:
                    return Normalize(value.kind);
                case PromptIntentObjectiveCapabilityDescriptor value:
                    return Normalize(value.kind);
                case PromptIntentEffectCapabilityDescriptor value:
                    return Normalize(value.kind);
                case PromptIntentValueDescriptor value:
                    return Normalize(value.value);
                default:
                    return string.Empty;
            }
        }

        private static TDescriptor CloneDescriptor<TDescriptor>(TDescriptor descriptor)
        {
            object boxed = descriptor;
            switch (boxed)
            {
                case PromptIntentObjectRoleDescriptor value:
                    return (TDescriptor)(object)new PromptIntentObjectRoleDescriptor
                    {
                        value = Normalize(value.value),
                        summary = value.summary ?? string.Empty,
                        catalogBacked = value.catalogBacked,
                        supportsDesignId = value.supportsDesignId,
                        supportsFeatureOptions = value.supportsFeatureOptions,
                    };
                case PromptIntentFeatureOptionDescriptor value:
                    return (TDescriptor)(object)new PromptIntentFeatureOptionDescriptor
                    {
                        value = Normalize(value.value),
                        summary = value.summary ?? string.Empty,
                        supportedRoles = CloneStrings(value.supportedRoles),
                    };
                case PromptIntentConditionKindDescriptor value:
                    return (TDescriptor)(object)new PromptIntentConditionKindDescriptor
                    {
                        value = Normalize(value.value),
                        summary = value.summary ?? string.Empty,
                        supportsStageId = value.supportsStageId,
                        requiresStageId = value.requiresStageId,
                        supportsTargetObjectId = value.supportsTargetObjectId,
                        requiresTargetObjectId = value.requiresTargetObjectId,
                        supportsItem = value.supportsItem,
                        requiresItem = value.requiresItem,
                        supportsCurrencyId = value.supportsCurrencyId,
                        requiresCurrencyId = value.requiresCurrencyId,
                        supportsAmountValue = value.supportsAmountValue,
                        requiresPositiveAmountValue = value.requiresPositiveAmountValue,
                    };
                case PromptIntentObjectiveKindDescriptor value:
                    return (TDescriptor)(object)new PromptIntentObjectiveKindDescriptor
                    {
                        value = Normalize(value.value),
                        summary = value.summary ?? string.Empty,
                        requiresTargetObjectId = value.requiresTargetObjectId,
                        requiresItem = value.requiresItem,
                        requiresInputItem = value.requiresInputItem,
                        requiresCurrencyId = value.requiresCurrencyId,
                        requiresAmountValue = value.requiresAmountValue,
                        requiresSeconds = value.requiresSeconds,
                        canAbsorbArrow = value.canAbsorbArrow,
                    };
                case PromptIntentEffectKindDescriptor value:
                    return (TDescriptor)(object)new PromptIntentEffectKindDescriptor
                    {
                        value = Normalize(value.value),
                        summary = value.summary ?? string.Empty,
                        requiresTargetObjectId = value.requiresTargetObjectId,
                        supportsTiming = value.supportsTiming,
                        requiresEventKey = value.requiresEventKey,
                        supportsEventKey = value.supportsEventKey,
                        isNonBlockingSystemAction = value.isNonBlockingSystemAction,
                    };
                case PromptIntentCompiledGameplayRoleDescriptor value:
                    return (TDescriptor)(object)new PromptIntentCompiledGameplayRoleDescriptor
                    {
                        gameplayObjectId = Normalize(value.gameplayObjectId),
                        role = Normalize(value.role),
                        summary = value.summary ?? string.Empty,
                    };
                case PromptIntentTargetSurfaceRuleDescriptor value:
                    return (TDescriptor)(object)new PromptIntentTargetSurfaceRuleDescriptor
                    {
                        role = Normalize(value.role),
                        summary = value.summary ?? string.Empty,
                        supportedEventKeys = CloneStrings(value.supportedEventKeys),
                    };
                case PromptIntentGameplaySignalRuleDescriptor value:
                    return (TDescriptor)(object)new PromptIntentGameplaySignalRuleDescriptor
                    {
                        signalId = Normalize(value.signalId),
                        summary = value.summary ?? string.Empty,
                        supportsTargetId = value.supportsTargetId,
                        requiresTargetId = value.requiresTargetId,
                        supportsItem = value.supportsItem,
                        requiresItem = value.requiresItem,
                        supportsCurrencyId = value.supportsCurrencyId,
                        requiresCurrencyId = value.requiresCurrencyId,
                        requiredTargetEventKey = Normalize(value.requiredTargetEventKey),
                    };
                case PromptIntentSystemActionRuleDescriptor value:
                    return (TDescriptor)(object)new PromptIntentSystemActionRuleDescriptor
                    {
                        authoringId = Normalize(value.authoringId),
                        summary = value.summary ?? string.Empty,
                        requiresTargetObjectId = value.requiresTargetObjectId,
                        defaultEventKey = Normalize(value.defaultEventKey),
                    };
                case PromptIntentConditionCapabilityDescriptor value:
                    return (TDescriptor)(object)new PromptIntentConditionCapabilityDescriptor
                    {
                        kind = Normalize(value.kind),
                        summary = value.summary ?? string.Empty,
                        supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                        allowAnyTargetRole = value.allowAnyTargetRole,
                        gameplaySignalId = Normalize(value.gameplaySignalId),
                        stepConditionType = Normalize(value.stepConditionType),
                        reactiveConditionType = Normalize(value.reactiveConditionType),
                    };
                case PromptIntentObjectiveCapabilityDescriptor value:
                    return (TDescriptor)(object)new PromptIntentObjectiveCapabilityDescriptor
                    {
                        kind = Normalize(value.kind),
                        summary = value.summary ?? string.Empty,
                        supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                        allowAnyTargetRole = value.allowAnyTargetRole,
                        completionStepConditionType = Normalize(value.completionStepConditionType),
                        completionGameplaySignalId = Normalize(value.completionGameplaySignalId),
                        targetEventKey = Normalize(value.targetEventKey),
                        requiresAbsorbedArrow = value.requiresAbsorbedArrow,
                        requiredArrowEventKey = Normalize(value.requiredArrowEventKey),
                    };
                case PromptIntentEffectCapabilityDescriptor value:
                    return (TDescriptor)(object)new PromptIntentEffectCapabilityDescriptor
                    {
                        kind = Normalize(value.kind),
                        summary = value.summary ?? string.Empty,
                        semanticTags = CloneStrings(value.semanticTags),
                        supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                        allowAnyTargetRole = value.allowAnyTargetRole,
                        systemActionId = Normalize(value.systemActionId),
                        runtimeEventKey = Normalize(value.runtimeEventKey),
                        buildsSceneActivationTarget = value.buildsSceneActivationTarget,
                        buildsSystemActionTarget = value.buildsSystemActionTarget,
                    };
                case PromptIntentValueDescriptor value:
                    return (TDescriptor)(object)new PromptIntentValueDescriptor
                    {
                        value = Normalize(value.value),
                        summary = value.summary ?? string.Empty,
                    };
                default:
                    return descriptor;
            }
        }

        private static string[] UnionStrings(string[] a, string[] b)
        {
            var values = new List<string>();
            AddUnique(values, a);
            AddUnique(values, b);
            return values.ToArray();
        }

        private static void AddUnique(List<string> values, string[] source)
        {
            string[] safeSource = source ?? new string[0];
            for (int i = 0; i < safeSource.Length; i++)
            {
                string normalized = Normalize(safeSource[i]);
                if (!string.IsNullOrEmpty(normalized) && !values.Contains(normalized))
                    values.Add(normalized);
            }
        }

        private static bool ContainsString(string[] values, string target)
        {
            string normalizedTarget = Normalize(target);
            if (string.IsNullOrEmpty(normalizedTarget))
                return false;

            string[] safeValues = values ?? new string[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (Normalize(safeValues[i]) == normalizedTarget)
                    return true;
            }

            return false;
        }

        private static string[] CloneStrings(string[] values)
        {
            string[] safeValues = values ?? new string[0];
            var clones = new string[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
                clones[i] = Normalize(safeValues[i]);
            return clones;
        }

        private static string Prefer(string preferred, string fallback)
        {
            string normalizedPreferred = preferred ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedPreferred) ? fallback ?? string.Empty : normalizedPreferred;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

