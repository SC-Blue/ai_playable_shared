using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public sealed class PromptIntentValueDescriptor
    {
        public string value;
        public string summary;
    }

    public sealed class PromptIntentObjectRoleDescriptor
    {
        public string value;
        public string summary;
        public bool catalogBacked;
        public bool supportsDesignId;
        public bool supportsFeatureOptions;
    }

    public sealed class PromptIntentFeatureOptionDescriptor
    {
        public string value;
        public string summary;
        public string[] supportedRoles = new string[0];
    }

    public sealed class PromptIntentConditionKindDescriptor
    {
        public string value;
        public string summary;
        public bool supportsStageId;
        public bool requiresStageId;
        public bool supportsTargetObjectId;
        public bool requiresTargetObjectId;
        public bool supportsItem;
        public bool requiresItem;
        public bool supportsCurrencyId;
        public bool requiresCurrencyId;
        public bool supportsAmountValue;
        public bool requiresPositiveAmountValue;
    }

    public sealed class PromptIntentObjectiveKindDescriptor
    {
        public string value;
        public string summary;
        public bool requiresTargetObjectId;
        public bool requiresItem;
        public bool requiresInputItem;
        public bool requiresCurrencyId;
        public bool supportsAmountValue;
        public bool requiresAmountValue;
        public bool requiresSeconds;
        public bool canAbsorbArrow;
    }

    public sealed class PromptIntentEffectKindDescriptor
    {
        public string value;
        public string summary;
        public bool requiresTargetObjectId;
        public bool supportsTiming;
        public bool requiresEventKey;
        public bool supportsEventKey;
        public bool isNonBlockingSystemAction;
    }

    public sealed class PromptIntentCompiledGameplayRoleDescriptor
    {
        public string gameplayObjectId;
        public string role;
        public string summary;
    }

    public sealed class PromptIntentContractRegistrySnapshot
    {
        public int schemaVersion;
        public PromptIntentObjectRoleDescriptor[] objectRoles = new PromptIntentObjectRoleDescriptor[0];
        public PromptIntentFeatureOptionDescriptor[] featureOptions = new PromptIntentFeatureOptionDescriptor[0];
        public PromptIntentConditionKindDescriptor[] conditionKinds = new PromptIntentConditionKindDescriptor[0];
        public PromptIntentObjectiveKindDescriptor[] objectiveKinds = new PromptIntentObjectiveKindDescriptor[0];
        public PromptIntentEffectKindDescriptor[] effectKinds = new PromptIntentEffectKindDescriptor[0];
        public PromptIntentValueDescriptor[] effectTimingKinds = new PromptIntentValueDescriptor[0];
        public PromptIntentValueDescriptor[] flowTargetEventKeys = new PromptIntentValueDescriptor[0];
        public PromptIntentValueDescriptor[] currencyStartVisualModes = new PromptIntentValueDescriptor[0];
        public PromptIntentCompiledGameplayRoleDescriptor[] compiledGameplayRoles = new PromptIntentCompiledGameplayRoleDescriptor[0];
    }

    public static class PromptIntentContractRegistry
    {
        // <generated-contract-registry-data>
        public const int SCHEMA_VERSION = 4;


        private static readonly PromptIntentObjectRoleDescriptor[] OBJECT_ROLES =
        {
            new PromptIntentObjectRoleDescriptor { value = PromptIntentObjectRoles.PLAYER, summary = "Player model", catalogBacked = true, supportsDesignId = true, supportsFeatureOptions = false },
            new PromptIntentObjectRoleDescriptor { value = PromptIntentObjectRoles.UNLOCK_PAD, summary = "Unlock pad", catalogBacked = true, supportsDesignId = true, supportsFeatureOptions = false },
        };

        private static readonly PromptIntentFeatureOptionDescriptor[] FEATURE_OPTIONS =
        {
        };

        private static readonly PromptIntentConditionKindDescriptor[] CONDITION_KINDS =
        {
            new PromptIntentConditionKindDescriptor { value = PromptIntentConditionKinds.START, summary = "Enter at start", supportsStageId = false, requiresStageId = false, supportsTargetObjectId = false, requiresTargetObjectId = false, supportsItem = false, requiresItem = false, supportsCurrencyId = false, requiresCurrencyId = false, supportsAmountValue = false, requiresPositiveAmountValue = false },
            new PromptIntentConditionKindDescriptor { value = PromptIntentConditionKinds.STAGE_COMPLETED, summary = "Enter after previous stage completion", supportsStageId = true, requiresStageId = true, supportsTargetObjectId = false, requiresTargetObjectId = false, supportsItem = false, requiresItem = false, supportsCurrencyId = false, requiresCurrencyId = false, supportsAmountValue = false, requiresPositiveAmountValue = false },
            new PromptIntentConditionKindDescriptor { value = PromptIntentConditionKinds.BALANCE_AT_LEAST, summary = "Enter when balance reaches threshold", supportsStageId = false, requiresStageId = false, supportsTargetObjectId = false, requiresTargetObjectId = false, supportsItem = false, requiresItem = false, supportsCurrencyId = true, requiresCurrencyId = true, supportsAmountValue = true, requiresPositiveAmountValue = true },
            new PromptIntentConditionKindDescriptor { value = PromptIntentConditionKinds.UNLOCK_COMPLETED, summary = "Unlock pad completed", supportsStageId = false, requiresStageId = false, supportsTargetObjectId = true, requiresTargetObjectId = true, supportsItem = false, requiresItem = false, supportsCurrencyId = false, requiresCurrencyId = false, supportsAmountValue = false, requiresPositiveAmountValue = false },
            new PromptIntentConditionKindDescriptor { value = PromptIntentConditionKinds.CAPABILITY_LEVEL_AT_LEAST, summary = "Enter when a generic runtime capability reaches a level", supportsStageId = false, requiresStageId = false, supportsTargetObjectId = true, requiresTargetObjectId = true, supportsItem = false, requiresItem = false, supportsCurrencyId = false, requiresCurrencyId = false, supportsAmountValue = true, requiresPositiveAmountValue = false },
        };

        private static readonly PromptIntentObjectiveKindDescriptor[] OBJECTIVE_KINDS =
        {
            new PromptIntentObjectiveKindDescriptor { value = PromptIntentObjectiveKinds.UNLOCK_OBJECT, summary = "Interact with unlock pad", requiresTargetObjectId = true, requiresItem = false, requiresInputItem = false, requiresCurrencyId = true, supportsAmountValue = true, requiresAmountValue = true, requiresSeconds = false, canAbsorbArrow = true },
            new PromptIntentObjectiveKindDescriptor { value = PromptIntentObjectiveKinds.WAIT_SECONDS, summary = "Wait for specified duration", requiresTargetObjectId = false, requiresItem = false, requiresInputItem = false, requiresCurrencyId = false, supportsAmountValue = false, requiresAmountValue = false, requiresSeconds = true, canAbsorbArrow = false },
        };

        private static readonly PromptIntentEffectKindDescriptor[] EFFECT_KINDS =
        {
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.REVEAL_OBJECT, summary = "Reveal object", requiresTargetObjectId = true, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.ACTIVATE_OBJECT, summary = "Activate object", requiresTargetObjectId = true, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.FOCUS_CAMERA, summary = "Focus camera", requiresTargetObjectId = true, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.SHOW_ARROW, summary = "Show objective-bound arrow guidance", requiresTargetObjectId = true, supportsTiming = true, requiresEventKey = true, supportsEventKey = true, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.SHOW_GUIDE_ARROW, summary = "Show presentation-only arrow guidance", requiresTargetObjectId = true, supportsTiming = true, requiresEventKey = true, supportsEventKey = true, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.REVEAL_ENDCARD, summary = "Reveal endcard", requiresTargetObjectId = false, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.END_GAME, summary = "End game immediately and trigger CTA without endcard", requiresTargetObjectId = false, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = false },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.HIDE_GUIDE, summary = "Hide guide", requiresTargetObjectId = false, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = true },
            new PromptIntentEffectKindDescriptor { value = PromptIntentEffectKinds.SET_CAPABILITY_LEVEL, summary = "Set a generic runtime capability level", requiresTargetObjectId = true, supportsTiming = false, requiresEventKey = false, supportsEventKey = false, isNonBlockingSystemAction = false },
        };

        private static readonly PromptIntentValueDescriptor[] EFFECT_TIMING_KINDS =
        {
            new PromptIntentValueDescriptor { value = PromptIntentEffectTimingKinds.ARRIVAL, summary = "Immediately after target arrival/focus" },
            new PromptIntentValueDescriptor { value = PromptIntentEffectTimingKinds.COMPLETED, summary = "After presentation/return completes" },
        };

        private static readonly PromptIntentValueDescriptor[] FLOW_TARGET_EVENT_KEYS =
        {
            new PromptIntentValueDescriptor { value = FlowTargetEventKeys.ROOT, summary = "Object root interaction" },
        };

        private static readonly PromptIntentValueDescriptor[] CURRENCY_START_VISUAL_MODES =
        {
            new PromptIntentValueDescriptor { value = CurrencyStartVisualRules.STACKED, summary = "Money stack visible at start" },
            new PromptIntentValueDescriptor { value = CurrencyStartVisualRules.NONE, summary = "No money visual at start" },
        };

        private static readonly PromptIntentCompiledGameplayRoleDescriptor[] COMPILED_GAMEPLAY_ROLES =
        {
            new PromptIntentCompiledGameplayRoleDescriptor { gameplayObjectId = "unlocker", role = PromptIntentObjectRoles.UNLOCK_PAD, summary = "Compiled unlocker spawn maps to unlock_pad role" },
        };
        // </generated-contract-registry-data>

        public static void SetActiveFeatureDescriptors(FeatureDescriptor[] descriptors)
        {
            PromptIntentFeatureDescriptorBridge.SetActiveFeatureDescriptors(descriptors);
        }

        public static void ClearActiveFeatureDescriptors()
        {
            PromptIntentFeatureDescriptorBridge.ClearActiveFeatureDescriptors();
        }

        public static PromptIntentContractRegistrySnapshot CreateSnapshot()
        {
            return new PromptIntentContractRegistrySnapshot
            {
                schemaVersion = SCHEMA_VERSION,
                objectRoles = CloneObjectRoleDescriptors(GetObjectRolesInternal()),
                featureOptions = CloneFeatureOptionDescriptors(GetFeatureOptionsInternal()),
                conditionKinds = CloneConditionKindDescriptors(GetConditionKindsInternal()),
                objectiveKinds = CloneObjectiveKindDescriptors(GetObjectiveKindsInternal()),
                effectKinds = CloneEffectKindDescriptors(GetEffectKindsInternal()),
                effectTimingKinds = CloneValueDescriptors(EFFECT_TIMING_KINDS),
                flowTargetEventKeys = CloneValueDescriptors(GetFlowTargetEventKeysInternal()),
                currencyStartVisualModes = CloneValueDescriptors(CURRENCY_START_VISUAL_MODES),
                compiledGameplayRoles = CloneCompiledGameplayRoleDescriptors(GetCompiledGameplayRolesInternal()),
            };
        }

        public static string[] GetObjectRoleValues()
        {
            return ExtractValues(GetObjectRolesInternal());
        }

        public static bool IsSupportedObjectRole(string role)
        {
            return FindObjectRole(role) != null;
        }

        public static bool IsCatalogBackedObjectRole(string role)
        {
            PromptIntentObjectRoleDescriptor descriptor = FindObjectRole(role);
            return descriptor != null && descriptor.catalogBacked;
        }

        public static bool ObjectRoleSupportsDesignId(string role)
        {
            PromptIntentObjectRoleDescriptor descriptor = FindObjectRole(role);
            return descriptor != null && descriptor.supportsDesignId;
        }

        public static bool ObjectRoleSupportsFeatureOptions(string role)
        {
            PromptIntentObjectRoleDescriptor descriptor = FindObjectRole(role);
            return descriptor != null && descriptor.supportsFeatureOptions;
        }

        public static string ResolveFeatureTypeForRole(string role)
        {
            return PromptIntentFeatureDescriptorBridge.ResolveFeatureTypeForRole(role);
        }

        public static bool ObjectiveDefinesFeatureOutputItem(string featureType, string objectiveKind)
        {
            return PromptIntentFeatureDescriptorBridge.ObjectiveDefinesFeatureOutputItem(featureType, objectiveKind);
        }

        public static PromptIntentFeatureOptionDescriptor[] GetFeatureOptions()
        {
            return CloneFeatureOptionDescriptors(GetFeatureOptionsInternal());
        }

        public static string[] GetSupportedFeatureOptionNames(string role)
        {
            string normalizedRole = Normalize(role);
            if (string.IsNullOrEmpty(normalizedRole))
                return new string[0];

            var values = new List<string>();
            PromptIntentFeatureOptionDescriptor[] options = GetFeatureOptionsInternal();
            for (int i = 0; i < options.Length; i++)
            {
                PromptIntentFeatureOptionDescriptor descriptor = options[i];
                if (descriptor != null && ContainsValue(descriptor.supportedRoles, normalizedRole))
                    values.Add(descriptor.value);
            }

            return values.ToArray();
        }

        public static bool SupportsFeatureOption(string role, string optionName)
        {
            string normalizedRole = Normalize(role);
            string normalizedOptionName = Normalize(optionName);
            if (string.IsNullOrEmpty(normalizedRole) || string.IsNullOrEmpty(normalizedOptionName))
                return false;

            PromptIntentFeatureOptionDescriptor descriptor = FindFeatureOption(normalizedOptionName);
            return descriptor != null && ContainsValue(descriptor.supportedRoles, normalizedRole);
        }

        public static string DescribeSupportedFeatureOptions(string role)
        {
            string[] names = GetSupportedFeatureOptionNames(role);
            if (names.Length == 0)
                return "No scenario options are supported.";
            if (names.Length == 1)
                return "Only " + names[0] + " is supported.";

            return "Only " + JoinNames(names) + " are supported.";
        }

        public static PlayableScenarioFeatureOptions CreateRoleDefaultFeatureOptions(string role)
        {
            return PlayableScenarioFeatureDefaults.CreateRoleOptions(role);
        }

        public static void ApplyRoleDefaultFeatureOptions(string role, ref PlayableScenarioFeatureOptions options)
        {
            PlayableScenarioFeatureDefaults.ApplyRoleDefaults(role, ref options);
        }

        public static string[] GetConditionKindValues()
        {
            return ExtractValues(GetConditionKindsInternal());
        }

        public static bool IsSupportedConditionKind(string kind)
        {
            return FindConditionKind(kind) != null;
        }

        public static bool ConditionSupportsStageId(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.supportsStageId;
        }

        public static bool ConditionRequiresStageId(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.requiresStageId;
        }

        public static bool ConditionSupportsTargetObjectId(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.supportsTargetObjectId;
        }

        public static bool ConditionRequiresTargetObjectId(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.requiresTargetObjectId;
        }

        public static bool ConditionSupportsItem(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.supportsItem;
        }

        public static bool ConditionRequiresItem(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.requiresItem;
        }

        public static bool ConditionSupportsCurrencyId(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.supportsCurrencyId;
        }

        public static bool ConditionRequiresCurrencyId(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.requiresCurrencyId;
        }

        public static bool ConditionSupportsAmountValue(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.supportsAmountValue;
        }

        public static bool ConditionRequiresPositiveAmountValue(string kind)
        {
            PromptIntentConditionKindDescriptor descriptor = FindConditionKind(kind);
            return descriptor != null && descriptor.requiresPositiveAmountValue;
        }

        public static string[] GetObjectiveKindValues()
        {
            return ExtractValues(GetObjectiveKindsInternal());
        }

        public static bool IsSupportedObjectiveKind(string kind)
        {
            return FindObjectiveKind(kind) != null;
        }

        public static bool ObjectiveSupportsTargetObjectId(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresTargetObjectId;
        }

        public static bool ObjectiveRequiresTargetObjectId(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresTargetObjectId;
        }

        public static bool ObjectiveSupportsItem(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return (descriptor != null && descriptor.requiresItem) ||
                   PromptIntentFeatureDescriptorBridge.ObjectiveKindDefinesFeatureOutputItem(kind);
        }

        public static bool ObjectiveRequiresItem(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresItem;
        }

        public static bool ObjectiveCanAbsorbArrow(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.canAbsorbArrow;
        }

        public static bool ObjectiveSupportsInputItem(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresInputItem;
        }

        public static bool ObjectiveRequiresInputItem(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresInputItem;
        }

        public static bool ObjectiveSupportsCurrencyId(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresCurrencyId;
        }

        public static bool ObjectiveRequiresCurrencyId(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresCurrencyId;
        }

        public static bool ObjectiveSupportsAmountValue(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && (descriptor.supportsAmountValue || descriptor.requiresAmountValue);
        }

        public static bool ObjectiveRequiresPositiveAmountValue(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresAmountValue;
        }

        public static bool ObjectiveSupportsSeconds(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresSeconds;
        }

        public static bool ObjectiveRequiresPositiveSeconds(string kind)
        {
            PromptIntentObjectiveKindDescriptor descriptor = FindObjectiveKind(kind);
            return descriptor != null && descriptor.requiresSeconds;
        }

        public static bool IsUnlockObjectiveKind(string kind)
        {
            return string.Equals(Normalize(kind), PromptIntentObjectiveKinds.UNLOCK_OBJECT, StringComparison.Ordinal);
        }

        public static string[] GetEffectKindValues()
        {
            return ExtractValues(GetEffectKindsInternal());
        }

        public static bool IsSupportedEffectKind(string kind)
        {
            return FindEffectKind(kind) != null;
        }

        public static bool EffectRequiresTargetObjectId(string kind)
        {
            PromptIntentEffectKindDescriptor descriptor = FindEffectKind(kind);
            return descriptor != null && descriptor.requiresTargetObjectId;
        }

        public static bool EffectIsNonBlockingSystemAction(string kind)
        {
            PromptIntentEffectKindDescriptor descriptor = FindEffectKind(kind);
            return descriptor != null && descriptor.isNonBlockingSystemAction;
        }

        public static bool EffectSupportsExplicitTiming(string kind)
        {
            PromptIntentEffectKindDescriptor descriptor = FindEffectKind(kind);
            return descriptor != null && descriptor.supportsTiming;
        }

        public static bool EffectRequiresEventKey(string kind)
        {
            PromptIntentEffectKindDescriptor descriptor = FindEffectKind(kind);
            return descriptor != null && descriptor.requiresEventKey;
        }

        public static bool EffectSupportsEventKey(string kind)
        {
            PromptIntentEffectKindDescriptor descriptor = FindEffectKind(kind);
            return descriptor != null && descriptor.supportsEventKey;
        }

        public static string[] GetEffectTimingKindValues()
        {
            return ExtractValues(EFFECT_TIMING_KINDS);
        }

        public static bool IsSupportedEffectTimingKind(string kind)
        {
            return FindValueDescriptor(EFFECT_TIMING_KINDS, kind) != null;
        }

        public static string[] GetFlowTargetEventKeys()
        {
            return ExtractValues(GetFlowTargetEventKeysInternal());
        }

        public static bool IsSupportedFlowTargetEventKey(string eventKey)
        {
            return FindValueDescriptor(GetFlowTargetEventKeysInternal(), eventKey) != null;
        }

        public static string[] GetCurrencyStartVisualModes()
        {
            return ExtractValues(CURRENCY_START_VISUAL_MODES);
        }

        public static bool IsSupportedCurrencyStartVisualMode(string mode)
        {
            return FindValueDescriptor(CURRENCY_START_VISUAL_MODES, mode) != null;
        }

        public static PromptIntentCompiledGameplayRoleDescriptor[] GetCompiledGameplayRoles()
        {
            return CloneCompiledGameplayRoleDescriptors(GetCompiledGameplayRolesInternal());
        }

        public static string ResolveCompiledGameplayRole(string gameplayObjectId)
        {
            PromptIntentCompiledGameplayRoleDescriptor descriptor = FindCompiledGameplayRole(gameplayObjectId);
            return descriptor != null ? descriptor.role : string.Empty;
        }

        public static string ResolveCatalogGameplayObjectIdForRole(string role)
        {
            string normalizedRole = Normalize(role);
            if (string.IsNullOrEmpty(normalizedRole))
                return string.Empty;

            PromptIntentCompiledGameplayRoleDescriptor[] descriptors = GetCompiledGameplayRolesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].role == normalizedRole)
                    return descriptors[i].gameplayObjectId;
            }

            return string.Empty;
        }

        private static PromptIntentObjectRoleDescriptor FindObjectRole(string role)
        {
            string normalized = Normalize(role);
            PromptIntentObjectRoleDescriptor[] descriptors = GetObjectRolesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].value == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentFeatureOptionDescriptor FindFeatureOption(string optionName)
        {
            string normalized = Normalize(optionName);
            PromptIntentFeatureOptionDescriptor[] descriptors = GetFeatureOptionsInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].value == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentConditionKindDescriptor FindConditionKind(string kind)
        {
            string normalized = Normalize(kind);
            PromptIntentConditionKindDescriptor[] descriptors = GetConditionKindsInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].value == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentObjectiveKindDescriptor FindObjectiveKind(string kind)
        {
            string normalized = Normalize(kind);
            PromptIntentObjectiveKindDescriptor[] descriptors = GetObjectiveKindsInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].value == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentEffectKindDescriptor FindEffectKind(string kind)
        {
            string normalized = Normalize(kind);
            PromptIntentEffectKindDescriptor[] descriptors = GetEffectKindsInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].value == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentCompiledGameplayRoleDescriptor FindCompiledGameplayRole(string gameplayObjectId)
        {
            string normalized = Normalize(gameplayObjectId);
            PromptIntentCompiledGameplayRoleDescriptor[] descriptors = GetCompiledGameplayRolesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].gameplayObjectId == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentObjectRoleDescriptor[] GetObjectRolesInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeObjectRoles(OBJECT_ROLES);
        }

        private static PromptIntentFeatureOptionDescriptor[] GetFeatureOptionsInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeFeatureOptions(FEATURE_OPTIONS);
        }

        private static PromptIntentConditionKindDescriptor[] GetConditionKindsInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeConditionKinds(CONDITION_KINDS);
        }

        private static PromptIntentObjectiveKindDescriptor[] GetObjectiveKindsInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeObjectiveKinds(OBJECTIVE_KINDS);
        }

        private static PromptIntentEffectKindDescriptor[] GetEffectKindsInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeEffectKinds(EFFECT_KINDS);
        }

        private static PromptIntentValueDescriptor[] GetFlowTargetEventKeysInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeFlowTargetEventKeys(FLOW_TARGET_EVENT_KEYS);
        }

        private static PromptIntentCompiledGameplayRoleDescriptor[] GetCompiledGameplayRolesInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeCompiledGameplayRoles(COMPILED_GAMEPLAY_ROLES);
        }

        private static PromptIntentValueDescriptor FindValueDescriptor(PromptIntentValueDescriptor[] values, string input)
        {
            string normalized = Normalize(input);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].value == normalized)
                    return values[i];
            }

            return null;
        }

        private static string[] ExtractValues(PromptIntentObjectRoleDescriptor[] descriptors)
        {
            var values = new string[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
                values[i] = descriptors[i].value;
            return values;
        }

        private static string[] ExtractValues(PromptIntentConditionKindDescriptor[] descriptors)
        {
            var values = new string[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
                values[i] = descriptors[i].value;
            return values;
        }

        private static string[] ExtractValues(PromptIntentObjectiveKindDescriptor[] descriptors)
        {
            var values = new string[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
                values[i] = descriptors[i].value;
            return values;
        }

        private static string[] ExtractValues(PromptIntentEffectKindDescriptor[] descriptors)
        {
            var values = new string[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
                values[i] = descriptors[i].value;
            return values;
        }

        private static string[] ExtractValues(PromptIntentValueDescriptor[] descriptors)
        {
            var values = new string[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
                values[i] = descriptors[i].value;
            return values;
        }

        private static PromptIntentObjectRoleDescriptor[] CloneObjectRoleDescriptors(PromptIntentObjectRoleDescriptor[] values)
        {
            var copies = new PromptIntentObjectRoleDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentObjectRoleDescriptor
                {
                    value = values[i].value,
                    summary = values[i].summary,
                    catalogBacked = values[i].catalogBacked,
                    supportsDesignId = values[i].supportsDesignId,
                    supportsFeatureOptions = values[i].supportsFeatureOptions,
                };
            }

            return copies;
        }

        private static PromptIntentFeatureOptionDescriptor[] CloneFeatureOptionDescriptors(PromptIntentFeatureOptionDescriptor[] values)
        {
            var copies = new PromptIntentFeatureOptionDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentFeatureOptionDescriptor
                {
                    value = values[i].value,
                    summary = values[i].summary,
                    supportedRoles = CloneStrings(values[i].supportedRoles),
                };
            }

            return copies;
        }

        private static PromptIntentConditionKindDescriptor[] CloneConditionKindDescriptors(PromptIntentConditionKindDescriptor[] values)
        {
            var copies = new PromptIntentConditionKindDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentConditionKindDescriptor
                {
                    value = values[i].value,
                    summary = values[i].summary,
                    supportsStageId = values[i].supportsStageId,
                    requiresStageId = values[i].requiresStageId,
                    supportsTargetObjectId = values[i].supportsTargetObjectId,
                    requiresTargetObjectId = values[i].requiresTargetObjectId,
                    supportsItem = values[i].supportsItem,
                    requiresItem = values[i].requiresItem,
                    supportsCurrencyId = values[i].supportsCurrencyId,
                    requiresCurrencyId = values[i].requiresCurrencyId,
                    supportsAmountValue = values[i].supportsAmountValue,
                    requiresPositiveAmountValue = values[i].requiresPositiveAmountValue,
                };
            }

            return copies;
        }

        private static PromptIntentObjectiveKindDescriptor[] CloneObjectiveKindDescriptors(PromptIntentObjectiveKindDescriptor[] values)
        {
            var copies = new PromptIntentObjectiveKindDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentObjectiveKindDescriptor
                {
                    value = values[i].value,
                    summary = values[i].summary,
                    requiresTargetObjectId = values[i].requiresTargetObjectId,
                    requiresItem = values[i].requiresItem,
                    requiresInputItem = values[i].requiresInputItem,
                    requiresCurrencyId = values[i].requiresCurrencyId,
                    supportsAmountValue = values[i].supportsAmountValue,
                    requiresAmountValue = values[i].requiresAmountValue,
                    requiresSeconds = values[i].requiresSeconds,
                    canAbsorbArrow = values[i].canAbsorbArrow,
                };
            }

            return copies;
        }

        private static PromptIntentEffectKindDescriptor[] CloneEffectKindDescriptors(PromptIntentEffectKindDescriptor[] values)
        {
            var copies = new PromptIntentEffectKindDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentEffectKindDescriptor
                {
                    value = values[i].value,
                    summary = values[i].summary,
                    requiresTargetObjectId = values[i].requiresTargetObjectId,
                    supportsTiming = values[i].supportsTiming,
                    requiresEventKey = values[i].requiresEventKey,
                    supportsEventKey = values[i].supportsEventKey,
                    isNonBlockingSystemAction = values[i].isNonBlockingSystemAction,
                };
            }

            return copies;
        }

        private static PromptIntentCompiledGameplayRoleDescriptor[] CloneCompiledGameplayRoleDescriptors(PromptIntentCompiledGameplayRoleDescriptor[] values)
        {
            var copies = new PromptIntentCompiledGameplayRoleDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentCompiledGameplayRoleDescriptor
                {
                    gameplayObjectId = values[i].gameplayObjectId,
                    role = values[i].role,
                    summary = values[i].summary,
                };
            }

            return copies;
        }

        private static PromptIntentValueDescriptor[] CloneValueDescriptors(PromptIntentValueDescriptor[] values)
        {
            var copies = new PromptIntentValueDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentValueDescriptor
                {
                    value = values[i].value,
                    summary = values[i].summary,
                };
            }

            return copies;
        }

        private static string[] CloneStrings(string[] values)
        {
            if (values == null || values.Length == 0)
                return new string[0];

            var copies = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                copies[i] = values[i];
            return copies;
        }

        private static bool ContainsValue(string[] values, string target)
        {
            string normalizedTarget = Normalize(target);
            if (string.IsNullOrEmpty(normalizedTarget) || values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (Normalize(values[i]) == normalizedTarget)
                    return true;
            }

            return false;
        }

        private static string JoinNames(string[] names)
        {
            if (names == null || names.Length == 0)
                return string.Empty;

            string text = names[0];
            for (int i = 1; i < names.Length; i++)
                text += ", " + names[i];

            return text;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
