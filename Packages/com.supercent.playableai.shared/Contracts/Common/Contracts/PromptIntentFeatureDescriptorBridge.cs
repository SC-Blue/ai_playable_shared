using System;
using System.Collections.Generic;
using System.Threading;

namespace Supercent.PlayableAI.Common.Contracts
{
    internal static class PromptIntentFeatureDescriptorBridge
    {
        private static readonly AsyncLocal<FeatureDescriptor[]> s_activeFeatureDescriptors = new AsyncLocal<FeatureDescriptor[]>();

        public static void SetActiveFeatureDescriptors(FeatureDescriptor[] descriptors)
        {
            s_activeFeatureDescriptors.Value = FeatureDescriptorUtility.CloneArray(descriptors);
        }

        public static void ClearActiveFeatureDescriptors()
        {
            s_activeFeatureDescriptors.Value = Array.Empty<FeatureDescriptor>();
        }

        public static PromptIntentObjectRoleDescriptor[] MergeObjectRoles(PromptIntentObjectRoleDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentObjectRoleDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(FilterBaseObjectRoles(builtins), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureObjectRoleDescriptor[] roles = descriptors[i].objectRoles ?? Array.Empty<FeatureObjectRoleDescriptor>();
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
                            supportsScenarioOptions = role != null && role.supportsScenarioOptions,
                            supportsRailSinkTarget = role != null && role.supportsRailSinkTarget,
                        });
                }
            }

            return ToOrderedArray(merged, order);
        }

        public static PromptIntentScenarioOptionDescriptor[] MergeScenarioOptions(PromptIntentScenarioOptionDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentScenarioOptionDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(Array.Empty<PromptIntentScenarioOptionDescriptor>(), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureDescriptor feature = descriptors[i];
                string[] supportedRoles = ExtractFeatureRoles(feature);
                FeatureScenarioOptionFieldDescriptor[] fields = feature.scenarioOptionSchema != null
                    ? feature.scenarioOptionSchema.fields ?? Array.Empty<FeatureScenarioOptionFieldDescriptor>()
                    : Array.Empty<FeatureScenarioOptionFieldDescriptor>();

                for (int j = 0; j < fields.Length; j++)
                {
                    FeatureScenarioOptionFieldDescriptor field = fields[j];
                    string key = Normalize(field != null ? field.fieldId : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (merged.TryGetValue(key, out PromptIntentScenarioOptionDescriptor existing))
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
                        new PromptIntentScenarioOptionDescriptor
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
                FeatureConditionDescriptor[] conditions = descriptors[i].conditionKinds ?? Array.Empty<FeatureConditionDescriptor>();
                for (int j = 0; j < conditions.Length; j++)
                {
                    FeatureConditionDescriptor condition = conditions[j];
                    string key = Normalize(condition != null ? condition.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

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
                FeatureObjectiveDescriptor[] objectives = descriptors[i].objectiveKinds ?? Array.Empty<FeatureObjectiveDescriptor>();
                for (int j = 0; j < objectives.Length; j++)
                {
                    FeatureObjectiveDescriptor objective = objectives[j];
                    string key = Normalize(objective != null ? objective.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

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
                            canAbsorbArrow = objective != null && objective.canAbsorbArrow,
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
                FeatureEffectDescriptor[] effects = descriptors[i].effectKinds ?? Array.Empty<FeatureEffectDescriptor>();
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
                FeatureTargetSurfaceDescriptor[] surfaces = descriptors[i].targetSurfaces ?? Array.Empty<FeatureTargetSurfaceDescriptor>();
                for (int j = 0; j < surfaces.Length; j++)
                {
                    string[] supportedEventKeys = surfaces[j] != null
                        ? surfaces[j].supportedEventKeys ?? Array.Empty<string>()
                        : Array.Empty<string>();
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
                FeatureCompiledGameplayRoleDescriptor[] mappings = descriptors[i].compiledGameplayRoleMappings ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
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
                FeatureObjectRoleDescriptor[] roles = descriptor != null ? descriptor.objectRoles ?? Array.Empty<FeatureObjectRoleDescriptor>() : Array.Empty<FeatureObjectRoleDescriptor>();
                for (int j = 0; j < roles.Length; j++)
                {
                    if (Normalize(roles[j] != null ? roles[j].role : string.Empty) == normalizedRole)
                        return Normalize(descriptor != null ? descriptor.featureType : string.Empty);
                }
            }

            return string.Empty;
        }

        public static PromptIntentTargetSurfaceRuleDescriptor[] MergeTargetSurfaces(PromptIntentTargetSurfaceRuleDescriptor[] builtins)
        {
            var merged = new Dictionary<string, PromptIntentTargetSurfaceRuleDescriptor>(StringComparer.Ordinal);
            var order = new List<string>();

            AddBaseDescriptors(Array.Empty<PromptIntentTargetSurfaceRuleDescriptor>(), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureTargetSurfaceDescriptor[] surfaces = descriptors[i].targetSurfaces ?? Array.Empty<FeatureTargetSurfaceDescriptor>();
                for (int j = 0; j < surfaces.Length; j++)
                {
                    FeatureTargetSurfaceDescriptor surface = surfaces[j];
                    string key = Normalize(surface != null ? surface.role : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    string[] eventKeys = surface != null ? surface.supportedEventKeys ?? Array.Empty<string>() : Array.Empty<string>();
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

            AddBaseDescriptors(Array.Empty<PromptIntentGameplaySignalRuleDescriptor>(), merged, order);

            FeatureDescriptor[] descriptors = GetEffectiveFeatureDescriptors();
            for (int i = 0; i < descriptors.Length; i++)
            {
                FeatureGameplaySignalDescriptor[] signals = descriptors[i].gameplaySignals ?? Array.Empty<FeatureGameplaySignalDescriptor>();
                for (int j = 0; j < signals.Length; j++)
                {
                    FeatureGameplaySignalDescriptor signal = signals[j];
                    string key = Normalize(signal != null ? signal.signalId : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

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
                FeatureConditionDescriptor[] conditions = descriptors[i].conditionKinds ?? Array.Empty<FeatureConditionDescriptor>();
                for (int j = 0; j < conditions.Length; j++)
                {
                    FeatureConditionDescriptor condition = conditions[j];
                    string key = Normalize(condition != null ? condition.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentConditionCapabilityDescriptor
                        {
                            kind = key,
                            summary = condition != null ? condition.summary ?? string.Empty : string.Empty,
                            supportedTargetRoles = condition != null ? CloneStrings(condition.supportedTargetRoles) : Array.Empty<string>(),
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
                FeatureObjectiveDescriptor[] objectives = descriptors[i].objectiveKinds ?? Array.Empty<FeatureObjectiveDescriptor>();
                for (int j = 0; j < objectives.Length; j++)
                {
                    FeatureObjectiveDescriptor objective = objectives[j];
                    string key = Normalize(objective != null ? objective.kind : string.Empty);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    Upsert(
                        merged,
                        order,
                        key,
                        new PromptIntentObjectiveCapabilityDescriptor
                        {
                            kind = key,
                            summary = objective != null ? objective.summary ?? string.Empty : string.Empty,
                            supportedTargetRoles = objective != null ? CloneStrings(objective.supportedTargetRoles) : Array.Empty<string>(),
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
                FeatureEffectDescriptor[] effects = descriptors[i].effectKinds ?? Array.Empty<FeatureEffectDescriptor>();
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
                            semanticTags = effect != null ? CloneStrings(effect.semanticTags) : Array.Empty<string>(),
                            supportedTargetRoles = effect != null ? CloneStrings(effect.supportedTargetRoles) : Array.Empty<string>(),
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
            return FeatureDescriptorUtility.CloneArray(s_activeFeatureDescriptors.Value);
        }

        private static PromptIntentObjectRoleDescriptor[] FilterBaseObjectRoles(PromptIntentObjectRoleDescriptor[] values)
        {
            var filtered = new List<PromptIntentObjectRoleDescriptor>();
            PromptIntentObjectRoleDescriptor[] safeValues = values ?? Array.Empty<PromptIntentObjectRoleDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = Normalize(safeValues[i] != null ? safeValues[i].value : string.Empty);
                if (value == PromptIntentObjectRoles.GENERATOR ||
                    value == PromptIntentObjectRoles.PROCESSOR ||
                    value == PromptIntentObjectRoles.SELLER ||
                    value == PromptIntentObjectRoles.RAIL ||
                    value == PromptIntentObjectRoles.PHYSICS_AREA)
                {
                    continue;
                }

                filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentConditionKindDescriptor[] FilterBaseConditionKinds(PromptIntentConditionKindDescriptor[] values)
        {
            var filtered = new List<PromptIntentConditionKindDescriptor>();
            PromptIntentConditionKindDescriptor[] safeValues = values ?? Array.Empty<PromptIntentConditionKindDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (!IsFeatureConditionKind(Normalize(safeValues[i] != null ? safeValues[i].value : string.Empty)))
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentObjectiveKindDescriptor[] FilterBaseObjectiveKinds(PromptIntentObjectiveKindDescriptor[] values)
        {
            var filtered = new List<PromptIntentObjectiveKindDescriptor>();
            PromptIntentObjectiveKindDescriptor[] safeValues = values ?? Array.Empty<PromptIntentObjectiveKindDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = Normalize(safeValues[i] != null ? safeValues[i].value : string.Empty);
                if (value == PromptIntentObjectiveKinds.COLLECT_ITEM ||
                    value == PromptIntentObjectiveKinds.CONVERT_ITEM ||
                    value == PromptIntentObjectiveKinds.SELL_ITEM ||
                    value == PromptIntentObjectiveKinds.COLLECT_CURRENCY)
                {
                    continue;
                }

                filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentEffectKindDescriptor[] FilterBaseEffectKinds(PromptIntentEffectKindDescriptor[] values)
        {
            var filtered = new List<PromptIntentEffectKindDescriptor>();
            PromptIntentEffectKindDescriptor[] safeValues = values ?? Array.Empty<PromptIntentEffectKindDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = Normalize(safeValues[i] != null ? safeValues[i].value : string.Empty);
                if (value != PromptIntentEffectKinds.SPAWN_CUSTOMER)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentCompiledGameplayRoleDescriptor[] FilterBaseCompiledGameplayRoles(PromptIntentCompiledGameplayRoleDescriptor[] values)
        {
            var filtered = new List<PromptIntentCompiledGameplayRoleDescriptor>();
            PromptIntentCompiledGameplayRoleDescriptor[] safeValues = values ?? Array.Empty<PromptIntentCompiledGameplayRoleDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string role = Normalize(safeValues[i] != null ? safeValues[i].role : string.Empty);
                if (role == PromptIntentObjectRoles.GENERATOR ||
                    role == PromptIntentObjectRoles.PROCESSOR ||
                    role == PromptIntentObjectRoles.SELLER ||
                    role == PromptIntentObjectRoles.RAIL ||
                    role == PromptIntentObjectRoles.PHYSICS_AREA)
                {
                    continue;
                }

                filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentConditionCapabilityDescriptor[] FilterBaseConditionCapabilities(PromptIntentConditionCapabilityDescriptor[] values)
        {
            var filtered = new List<PromptIntentConditionCapabilityDescriptor>();
            PromptIntentConditionCapabilityDescriptor[] safeValues = values ?? Array.Empty<PromptIntentConditionCapabilityDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                if (!IsFeatureConditionKind(Normalize(safeValues[i] != null ? safeValues[i].kind : string.Empty)))
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentObjectiveCapabilityDescriptor[] FilterBaseObjectiveCapabilities(PromptIntentObjectiveCapabilityDescriptor[] values)
        {
            var filtered = new List<PromptIntentObjectiveCapabilityDescriptor>();
            PromptIntentObjectiveCapabilityDescriptor[] safeValues = values ?? Array.Empty<PromptIntentObjectiveCapabilityDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = Normalize(safeValues[i] != null ? safeValues[i].kind : string.Empty);
                if (value == PromptIntentObjectiveKinds.COLLECT_ITEM ||
                    value == PromptIntentObjectiveKinds.CONVERT_ITEM ||
                    value == PromptIntentObjectiveKinds.SELL_ITEM ||
                    value == PromptIntentObjectiveKinds.COLLECT_CURRENCY)
                {
                    continue;
                }

                filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static PromptIntentEffectCapabilityDescriptor[] FilterBaseEffectCapabilities(PromptIntentEffectCapabilityDescriptor[] values)
        {
            var filtered = new List<PromptIntentEffectCapabilityDescriptor>();
            PromptIntentEffectCapabilityDescriptor[] safeValues = values ?? Array.Empty<PromptIntentEffectCapabilityDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = Normalize(safeValues[i] != null ? safeValues[i].kind : string.Empty);
                if (value != PromptIntentEffectKinds.SPAWN_CUSTOMER)
                    filtered.Add(safeValues[i]);
            }

            return filtered.ToArray();
        }

        private static bool IsFeatureConditionKind(string value)
        {
            return value == PromptIntentConditionKinds.ITEM_GENERATED ||
                   value == PromptIntentConditionKinds.ITEM_COLLECTED ||
                   value == PromptIntentConditionKinds.ITEM_CONVERTED ||
                   value == PromptIntentConditionKinds.RAIL_ITEM_ARRIVED ||
                   value == PromptIntentConditionKinds.SALE_COMPLETED ||
                   value == PromptIntentConditionKinds.MONEY_COLLECTED ||
                   value == PromptIntentConditionKinds.CUSTOMER_SERVED;
        }

        private static string[] ExtractFeatureRoles(FeatureDescriptor descriptor)
        {
            FeatureObjectRoleDescriptor[] roles = descriptor != null ? descriptor.objectRoles ?? Array.Empty<FeatureObjectRoleDescriptor>() : Array.Empty<FeatureObjectRoleDescriptor>();
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
            TDescriptor[] safeValues = values ?? Array.Empty<TDescriptor>();
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
                case PromptIntentScenarioOptionDescriptor value:
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
                        supportsScenarioOptions = value.supportsScenarioOptions,
                        supportsRailSinkTarget = value.supportsRailSinkTarget,
                    };
                case PromptIntentScenarioOptionDescriptor value:
                    return (TDescriptor)(object)new PromptIntentScenarioOptionDescriptor
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
            string[] safeSource = source ?? Array.Empty<string>();
            for (int i = 0; i < safeSource.Length; i++)
            {
                string normalized = Normalize(safeSource[i]);
                if (!string.IsNullOrEmpty(normalized) && !values.Contains(normalized))
                    values.Add(normalized);
            }
        }

        private static string[] CloneStrings(string[] values)
        {
            string[] safeValues = values ?? Array.Empty<string>();
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
