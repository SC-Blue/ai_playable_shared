using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Format;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Contracts
{
    public interface IFeatureDescriptorRegistry
    {
        FeatureDescriptor[] GetAll();
        bool TryGetFeature(string featureType, out FeatureDescriptor descriptor);
    }

    public static class FeatureDescriptorContracts
    {
        public const int SCHEMA_VERSION = 1;
        public const string VALUE_TYPE_INT = "int";
        public const string VALUE_TYPE_INT_RANGE = "int_range";
        public const string VALUE_TYPE_FLOAT = "float";
        public const string VALUE_TYPE_ITEM_REF = "item_ref";
        public const string VALUE_TYPE_TARGET_OBJECT_ID = "target_object_id";
        public const string EDITOR_PREVIEW_RENDERER_PATH = "path";
        public const string EDITOR_PREVIEW_RENDERER_BOUNDS = "bounds";
        public const string EDITOR_PREVIEW_VISUAL_OPTION_ITEM_REF = "option_item_ref";
    }

    [Serializable]
    public sealed class FeatureDescriptorDocument
    {
        public int schemaVersion = FeatureDescriptorContracts.SCHEMA_VERSION;
        public FeatureDescriptor[] featureDescriptors = Array.Empty<FeatureDescriptor>();
    }

    [Serializable]
    public sealed class FeatureDescriptor
    {
        public string featureId = string.Empty;
        public string featureType = string.Empty;
        public bool isRuntimePackage = true;
        public string displayName = string.Empty;
        public string summary = string.Empty;
        public FeatureCatalogExposure catalogExposure = new FeatureCatalogExposure();
        public FeatureObjectRoleDescriptor[] objectRoles = Array.Empty<FeatureObjectRoleDescriptor>();
        public FeatureOptionSchema optionSchema = new FeatureOptionSchema();
        public FeatureTargetSurfaceDescriptor[] targetSurfaces = Array.Empty<FeatureTargetSurfaceDescriptor>();
        public FeatureGameplaySignalDescriptor[] gameplaySignals = Array.Empty<FeatureGameplaySignalDescriptor>();
        public FeatureConditionDescriptor[] conditionKinds = Array.Empty<FeatureConditionDescriptor>();
        public FeatureObjectiveDescriptor[] objectiveKinds = Array.Empty<FeatureObjectiveDescriptor>();
        public FeatureEffectDescriptor[] effectKinds = Array.Empty<FeatureEffectDescriptor>();
        public FeatureCompiledGameplayRoleDescriptor[] compiledGameplayRoleMappings = Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
        public FeatureInputOutputSemantics inputOutputSemantics = new FeatureInputOutputSemantics();
        public FeatureLayoutRequirementDescriptor layoutRequirements = new FeatureLayoutRequirementDescriptor();
        public FeatureEditorPreviewDescriptor editorPreview = new FeatureEditorPreviewDescriptor();
    }

    [Serializable]
    public sealed class FeatureCatalogExposure
    {
        public bool exposeInCatalog = true;
        public bool exposeToServer = true;
        public bool exposeToAuthoring = true;
    }

    [Serializable]
    public sealed class FeatureObjectRoleDescriptor
    {
        public string role = string.Empty;
        public string summary = string.Empty;
        public bool catalogBacked;
        public bool supportsDesignId;
        public bool supportsFeatureOptions;
    }

    [Serializable]
    public sealed class FeatureOptionSchema
    {
        public FeatureOptionFieldDescriptor[] fields = Array.Empty<FeatureOptionFieldDescriptor>();
    }

    [Serializable]
    public sealed class FeatureOptionFieldDescriptor
    {
        public string fieldId = string.Empty;
        public string summary = string.Empty;
        public string valueType = string.Empty;
        public bool required;
        public string[] requiredItemDesignCapabilities = Array.Empty<string>();
        public int minIntValue;
    }

    [Serializable]
    public sealed class FeatureTargetSurfaceDescriptor
    {
        public string role = string.Empty;
        public string summary = string.Empty;
        public string[] supportedEventKeys = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FeatureGameplaySignalDescriptor
    {
        public string signalId = string.Empty;
        public string summary = string.Empty;
        public bool supportsTargetId;
        public bool requiresTargetId;
        public bool supportsItem;
        public bool requiresItem;
        public bool supportsCurrencyId;
        public bool requiresCurrencyId;
        public string requiredTargetEventKey = string.Empty;
    }

    [Serializable]
    public sealed class FeatureConditionDescriptor
    {
        public string kind = string.Empty;
        public string summary = string.Empty;
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
        public string[] supportedTargetRoles = Array.Empty<string>();
        public bool allowAnyTargetRole;
        public string gameplaySignalId = string.Empty;
        public string stepConditionType = string.Empty;
        public string reactiveConditionType = string.Empty;
    }

    [Serializable]
    public sealed class FeatureObjectiveDescriptor
    {
        public string kind = string.Empty;
        public string summary = string.Empty;
        public bool requiresTargetObjectId;
        public bool requiresItem;
        public bool requiresInputItem;
        public bool requiresCurrencyId;
        public bool requiresAmountValue;
        public bool requiresSeconds;
        public bool canAbsorbArrow;
        public string[] supportedTargetRoles = Array.Empty<string>();
        public bool allowAnyTargetRole;
        public string completionStepConditionType = string.Empty;
        public string completionGameplaySignalId = string.Empty;
        public string targetEventKey = string.Empty;
        public bool requiresAbsorbedArrow;
        public string requiredArrowEventKey = string.Empty;
    }

    [Serializable]
    public sealed class FeatureEffectDescriptor
    {
        public string kind = string.Empty;
        public string summary = string.Empty;
        public string[] semanticTags = Array.Empty<string>();
        public bool requiresTargetObjectId;
        public bool supportsTiming;
        public bool requiresEventKey;
        public bool supportsEventKey;
        public bool isNonBlockingSystemAction;
        public string[] supportedTargetRoles = Array.Empty<string>();
        public bool allowAnyTargetRole;
        public string systemActionId = string.Empty;
        public string runtimeEventKey = string.Empty;
        public bool buildsSceneActivationTarget;
        public bool buildsSystemActionTarget;
    }

    [Serializable]
    public sealed class FeatureCompiledGameplayRoleDescriptor
    {
        public string gameplayObjectId = string.Empty;
        public string role = string.Empty;
        public string summary = string.Empty;
    }

    [Serializable]
    public sealed class FeatureInputOutputSemantics
    {
        public string[] generatedItems = Array.Empty<string>();
        public string[] outputItems = Array.Empty<string>();
        public string[] acceptedTargetRoles = Array.Empty<string>();
        public bool supportsCustomerFeature;
        public bool containsCustomerSingleLine;
        public bool containsMoneyHandler;
    }

    [Serializable]
    public sealed class FeatureLayoutRequirementDescriptor
    {
        public bool catalogBacked;
        public bool supportsDesignId;
        public string designMode = string.Empty;
        public string placementMode = string.Empty;
        public string[] pathShape = Array.Empty<string>();
        public string[] requiredDesignCapabilities = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FeatureEditorPreviewDescriptor
    {
        public string renderer = string.Empty;
        public FeatureEditorPreviewPathDescriptor path = new FeatureEditorPreviewPathDescriptor();
        public FeatureEditorPreviewBoundsDescriptor[] bounds = Array.Empty<FeatureEditorPreviewBoundsDescriptor>();
        public FeatureEditorPreviewVisualSourceDescriptor visualSource = new FeatureEditorPreviewVisualSourceDescriptor();
    }

    [Serializable]
    public sealed class FeatureEditorPreviewPathDescriptor
    {
        public string cellsField = string.Empty;
        public string sinkTargetField = string.Empty;
        public string straightDesignSlot = string.Empty;
        public string cornerDesignSlot = string.Empty;
        public int tileWidthCells = 2;
        public int tileDepthCells = 2;
    }

    [Serializable]
    public sealed class FeatureEditorPreviewBoundsDescriptor
    {
        public string field = string.Empty;
        public string zoneKind = string.Empty;
        public string label = string.Empty;
        public string color = string.Empty;
    }

    [Serializable]
    public sealed class FeatureEditorPreviewVisualSourceDescriptor
    {
        public string kind = string.Empty;
        public string optionFieldId = string.Empty;
        public string contentCategory = string.Empty;
        public string mediaSlot = string.Empty;
    }

    public sealed class FeatureDescriptorRegistry : IFeatureDescriptorRegistry
    {
        private readonly Dictionary<string, FeatureDescriptor> _byType;
        private readonly FeatureDescriptor[] _descriptors;

        public FeatureDescriptorRegistry(FeatureDescriptor[] descriptors)
        {
            _descriptors = FeatureDescriptorUtility.CloneArray(descriptors);
            _byType = new Dictionary<string, FeatureDescriptor>(StringComparer.Ordinal);
            for (int i = 0; i < _descriptors.Length; i++)
            {
                FeatureDescriptor descriptor = _descriptors[i];
                string featureType = FeatureDescriptorUtility.Normalize(descriptor != null ? descriptor.featureType : string.Empty);
                if (string.IsNullOrEmpty(featureType))
                    continue;

                _byType[featureType] = FeatureDescriptorUtility.Clone(descriptor);
            }
        }

        public FeatureDescriptor[] GetAll()
        {
            return FeatureDescriptorUtility.CloneArray(_descriptors);
        }

        public bool TryGetFeature(string featureType, out FeatureDescriptor descriptor)
        {
            if (_byType.TryGetValue(FeatureDescriptorUtility.Normalize(featureType), out FeatureDescriptor value))
            {
                descriptor = FeatureDescriptorUtility.Clone(value);
                return true;
            }

            descriptor = new FeatureDescriptor();
            return false;
        }
    }

    public static class FeatureDescriptorUtility
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool IsValid(FeatureDescriptor descriptor)
        {
            return descriptor != null &&
                   !string.IsNullOrWhiteSpace(descriptor.featureId) &&
                   !string.IsNullOrWhiteSpace(descriptor.featureType);
        }

        public static FeatureDescriptor[] CloneArray(FeatureDescriptor[] values)
        {
            FeatureDescriptor[] safeValues = values ?? Array.Empty<FeatureDescriptor>();
            var clones = new FeatureDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
                clones[i] = Clone(safeValues[i]);
            return clones;
        }

        public static FeatureDescriptor Clone(FeatureDescriptor value)
        {
            if (value == null)
                return new FeatureDescriptor();

            return new FeatureDescriptor
            {
                featureId = Normalize(value.featureId),
                featureType = Normalize(value.featureType),
                isRuntimePackage = value.isRuntimePackage,
                displayName = value.displayName ?? string.Empty,
                summary = value.summary ?? string.Empty,
                catalogExposure = Clone(value.catalogExposure),
                objectRoles = Clone(value.objectRoles),
                optionSchema = Clone(value.optionSchema),
                targetSurfaces = Clone(value.targetSurfaces),
                gameplaySignals = Clone(value.gameplaySignals),
                conditionKinds = Clone(value.conditionKinds),
                objectiveKinds = Clone(value.objectiveKinds),
                effectKinds = Clone(value.effectKinds),
                compiledGameplayRoleMappings = Clone(value.compiledGameplayRoleMappings),
                inputOutputSemantics = Clone(value.inputOutputSemantics),
                layoutRequirements = Clone(value.layoutRequirements),
                editorPreview = Clone(value.editorPreview),
            };
        }

        public static FeatureCatalogExposure Clone(FeatureCatalogExposure value)
        {
            return value == null
                ? new FeatureCatalogExposure()
                : new FeatureCatalogExposure
                {
                    exposeInCatalog = value.exposeInCatalog,
                    exposeToServer = value.exposeToServer,
                    exposeToAuthoring = value.exposeToAuthoring,
                };
        }

        public static FeatureObjectRoleDescriptor[] Clone(FeatureObjectRoleDescriptor[] values)
        {
            FeatureObjectRoleDescriptor[] safeValues = values ?? Array.Empty<FeatureObjectRoleDescriptor>();
            var clones = new FeatureObjectRoleDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureObjectRoleDescriptor value = safeValues[i] ?? new FeatureObjectRoleDescriptor();
                clones[i] = new FeatureObjectRoleDescriptor
                {
                    role = Normalize(value.role),
                    summary = value.summary ?? string.Empty,
                    catalogBacked = value.catalogBacked,
                    supportsDesignId = value.supportsDesignId,
                    supportsFeatureOptions = value.supportsFeatureOptions,
                };
            }

            return clones;
        }

        public static FeatureOptionSchema Clone(FeatureOptionSchema value)
        {
            return value == null
                ? new FeatureOptionSchema()
                : new FeatureOptionSchema
                {
                    fields = Clone(value.fields),
                };
        }

        public static FeatureOptionFieldDescriptor[] Clone(FeatureOptionFieldDescriptor[] values)
        {
            FeatureOptionFieldDescriptor[] safeValues = values ?? Array.Empty<FeatureOptionFieldDescriptor>();
            var clones = new FeatureOptionFieldDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureOptionFieldDescriptor value = safeValues[i] ?? new FeatureOptionFieldDescriptor();
                clones[i] = new FeatureOptionFieldDescriptor
                {
                    fieldId = Normalize(value.fieldId),
                    summary = value.summary ?? string.Empty,
                    valueType = Normalize(value.valueType),
                    required = value.required,
                    requiredItemDesignCapabilities = CloneStrings(value.requiredItemDesignCapabilities),
                    minIntValue = value.minIntValue,
                };
            }

            return clones;
        }

        public static FeatureTargetSurfaceDescriptor[] Clone(FeatureTargetSurfaceDescriptor[] values)
        {
            FeatureTargetSurfaceDescriptor[] safeValues = values ?? Array.Empty<FeatureTargetSurfaceDescriptor>();
            var clones = new FeatureTargetSurfaceDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureTargetSurfaceDescriptor value = safeValues[i] ?? new FeatureTargetSurfaceDescriptor();
                clones[i] = new FeatureTargetSurfaceDescriptor
                {
                    role = Normalize(value.role),
                    summary = value.summary ?? string.Empty,
                    supportedEventKeys = CloneStrings(value.supportedEventKeys),
                };
            }

            return clones;
        }

        public static FeatureGameplaySignalDescriptor[] Clone(FeatureGameplaySignalDescriptor[] values)
        {
            FeatureGameplaySignalDescriptor[] safeValues = values ?? Array.Empty<FeatureGameplaySignalDescriptor>();
            var clones = new FeatureGameplaySignalDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureGameplaySignalDescriptor value = safeValues[i] ?? new FeatureGameplaySignalDescriptor();
                clones[i] = new FeatureGameplaySignalDescriptor
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
            }

            return clones;
        }

        public static FeatureConditionDescriptor[] Clone(FeatureConditionDescriptor[] values)
        {
            FeatureConditionDescriptor[] safeValues = values ?? Array.Empty<FeatureConditionDescriptor>();
            var clones = new FeatureConditionDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureConditionDescriptor value = safeValues[i] ?? new FeatureConditionDescriptor();
                clones[i] = new FeatureConditionDescriptor
                {
                    kind = Normalize(value.kind),
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
                    supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                    allowAnyTargetRole = value.allowAnyTargetRole,
                    gameplaySignalId = Normalize(value.gameplaySignalId),
                    stepConditionType = Normalize(value.stepConditionType),
                    reactiveConditionType = Normalize(value.reactiveConditionType),
                };
            }

            return clones;
        }

        public static FeatureObjectiveDescriptor[] Clone(FeatureObjectiveDescriptor[] values)
        {
            FeatureObjectiveDescriptor[] safeValues = values ?? Array.Empty<FeatureObjectiveDescriptor>();
            var clones = new FeatureObjectiveDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureObjectiveDescriptor value = safeValues[i] ?? new FeatureObjectiveDescriptor();
                clones[i] = new FeatureObjectiveDescriptor
                {
                    kind = Normalize(value.kind),
                    summary = value.summary ?? string.Empty,
                    requiresTargetObjectId = value.requiresTargetObjectId,
                    requiresItem = value.requiresItem,
                    requiresInputItem = value.requiresInputItem,
                    requiresCurrencyId = value.requiresCurrencyId,
                    requiresAmountValue = value.requiresAmountValue,
                    requiresSeconds = value.requiresSeconds,
                    canAbsorbArrow = value.canAbsorbArrow,
                    supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                    allowAnyTargetRole = value.allowAnyTargetRole,
                    completionStepConditionType = Normalize(value.completionStepConditionType),
                    completionGameplaySignalId = Normalize(value.completionGameplaySignalId),
                    targetEventKey = Normalize(value.targetEventKey),
                    requiresAbsorbedArrow = value.requiresAbsorbedArrow,
                    requiredArrowEventKey = Normalize(value.requiredArrowEventKey),
                };
            }

            return clones;
        }

        public static FeatureEffectDescriptor[] Clone(FeatureEffectDescriptor[] values)
        {
            FeatureEffectDescriptor[] safeValues = values ?? Array.Empty<FeatureEffectDescriptor>();
            var clones = new FeatureEffectDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureEffectDescriptor value = safeValues[i] ?? new FeatureEffectDescriptor();
                clones[i] = new FeatureEffectDescriptor
                {
                    kind = Normalize(value.kind),
                    summary = value.summary ?? string.Empty,
                    semanticTags = CloneStrings(value.semanticTags),
                    requiresTargetObjectId = value.requiresTargetObjectId,
                    supportsTiming = value.supportsTiming,
                    requiresEventKey = value.requiresEventKey,
                    supportsEventKey = value.supportsEventKey,
                    isNonBlockingSystemAction = value.isNonBlockingSystemAction,
                    supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                    allowAnyTargetRole = value.allowAnyTargetRole,
                    systemActionId = Normalize(value.systemActionId),
                    runtimeEventKey = Normalize(value.runtimeEventKey),
                    buildsSceneActivationTarget = value.buildsSceneActivationTarget,
                    buildsSystemActionTarget = value.buildsSystemActionTarget,
                };
            }

            return clones;
        }

        public static FeatureCompiledGameplayRoleDescriptor[] Clone(FeatureCompiledGameplayRoleDescriptor[] values)
        {
            FeatureCompiledGameplayRoleDescriptor[] safeValues = values ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
            var clones = new FeatureCompiledGameplayRoleDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureCompiledGameplayRoleDescriptor value = safeValues[i] ?? new FeatureCompiledGameplayRoleDescriptor();
                clones[i] = new FeatureCompiledGameplayRoleDescriptor
                {
                    gameplayObjectId = Normalize(value.gameplayObjectId),
                    role = Normalize(value.role),
                    summary = value.summary ?? string.Empty,
                };
            }

            return clones;
        }

        public static FeatureInputOutputSemantics Clone(FeatureInputOutputSemantics value)
        {
            return value == null
                ? new FeatureInputOutputSemantics()
                : new FeatureInputOutputSemantics
                {
                    generatedItems = CloneStrings(value.generatedItems),
                    outputItems = CloneStrings(value.outputItems),
                    acceptedTargetRoles = CloneStrings(value.acceptedTargetRoles),
                    supportsCustomerFeature = value.supportsCustomerFeature,
                    containsCustomerSingleLine = value.containsCustomerSingleLine,
                    containsMoneyHandler = value.containsMoneyHandler,
                };
        }

        public static FeatureLayoutRequirementDescriptor Clone(FeatureLayoutRequirementDescriptor value)
        {
            return value == null
                ? new FeatureLayoutRequirementDescriptor()
                : new FeatureLayoutRequirementDescriptor
                {
                    catalogBacked = value.catalogBacked,
                    supportsDesignId = value.supportsDesignId,
                    designMode = Normalize(value.designMode),
                    placementMode = Normalize(value.placementMode),
                    pathShape = CloneStrings(value.pathShape),
                    requiredDesignCapabilities = CloneStrings(value.requiredDesignCapabilities),
                };
        }

        public static FeatureEditorPreviewDescriptor Clone(FeatureEditorPreviewDescriptor value)
        {
            return value == null
                ? new FeatureEditorPreviewDescriptor()
                : new FeatureEditorPreviewDescriptor
                {
                    renderer = Normalize(value.renderer),
                    path = Clone(value.path),
                    bounds = Clone(value.bounds),
                    visualSource = Clone(value.visualSource),
                };
        }

        public static FeatureEditorPreviewPathDescriptor Clone(FeatureEditorPreviewPathDescriptor value)
        {
            return value == null
                ? new FeatureEditorPreviewPathDescriptor()
                : new FeatureEditorPreviewPathDescriptor
                {
                    cellsField = Normalize(value.cellsField),
                    sinkTargetField = Normalize(value.sinkTargetField),
                    straightDesignSlot = Normalize(value.straightDesignSlot),
                    cornerDesignSlot = Normalize(value.cornerDesignSlot),
                    tileWidthCells = Math.Max(1, value.tileWidthCells),
                    tileDepthCells = Math.Max(1, value.tileDepthCells),
                };
        }

        public static FeatureEditorPreviewBoundsDescriptor[] Clone(FeatureEditorPreviewBoundsDescriptor[] values)
        {
            FeatureEditorPreviewBoundsDescriptor[] safeValues = values ?? Array.Empty<FeatureEditorPreviewBoundsDescriptor>();
            var clones = new FeatureEditorPreviewBoundsDescriptor[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureEditorPreviewBoundsDescriptor value = safeValues[i] ?? new FeatureEditorPreviewBoundsDescriptor();
                clones[i] = new FeatureEditorPreviewBoundsDescriptor
                {
                    field = Normalize(value.field),
                    zoneKind = Normalize(value.zoneKind),
                    label = Normalize(value.label),
                    color = Normalize(value.color),
                };
            }

            return clones;
        }

        public static FeatureEditorPreviewVisualSourceDescriptor Clone(FeatureEditorPreviewVisualSourceDescriptor value)
        {
            return value == null
                ? new FeatureEditorPreviewVisualSourceDescriptor()
                : new FeatureEditorPreviewVisualSourceDescriptor
                {
                    kind = Normalize(value.kind),
                    optionFieldId = Normalize(value.optionFieldId),
                    contentCategory = Normalize(value.contentCategory),
                    mediaSlot = Normalize(value.mediaSlot),
                };
        }

        public static string[] CloneStrings(string[] values)
        {
            string[] safeValues = values ?? Array.Empty<string>();
            var clones = new string[safeValues.Length];
            for (int i = 0; i < safeValues.Length; i++)
                clones[i] = Normalize(safeValues[i]);
            return clones;
        }

        public static FeatureDescriptor[] Merge(FeatureDescriptor[] baseDescriptors, FeatureDescriptor[] runtimeDescriptors)
        {
            var byType = new Dictionary<string, FeatureDescriptor>(StringComparer.Ordinal);
            AddRange(byType, baseDescriptors, allowDuplicateOverride: true);
            AddRange(byType, runtimeDescriptors, allowDuplicateOverride: false);

            var merged = new FeatureDescriptor[byType.Count];
            int index = 0;
            foreach (FeatureDescriptor descriptor in byType.Values)
                merged[index++] = Clone(descriptor);
            return merged;
        }

        private static void AddRange(IDictionary<string, FeatureDescriptor> target, FeatureDescriptor[] values, bool allowDuplicateOverride)
        {
            FeatureDescriptor[] safeValues = values ?? Array.Empty<FeatureDescriptor>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                FeatureDescriptor descriptor = safeValues[i];
                if (!IsValid(descriptor))
                    continue;

                string featureType = Normalize(descriptor.featureType);
                if (!allowDuplicateOverride &&
                    target.TryGetValue(featureType, out FeatureDescriptor existing))
                {
                    throw new InvalidOperationException("feature runtime descriptor '" + featureType + "'가 이미 정의된 featureType과 중복됩니다.");
                }

                target[featureType] = Clone(descriptor);
            }
        }
    }

    public static class FeatureDescriptorValidator
    {
        public static bool TryValidate(FeatureDescriptor[] descriptors, out string error)
        {
            var errors = new List<string>();
            var featureTypes = new HashSet<string>(StringComparer.Ordinal);
            FeatureDescriptor[] safeDescriptors = descriptors ?? Array.Empty<FeatureDescriptor>();
            for (int i = 0; i < safeDescriptors.Length; i++)
                ValidateDescriptor(safeDescriptors[i], featureTypes, errors);

            error = errors.Count == 0 ? string.Empty : string.Join(" | ", errors.ToArray());
            return errors.Count == 0;
        }

        private static void ValidateDescriptor(
            FeatureDescriptor descriptor,
            HashSet<string> featureTypes,
            List<string> errors)
        {
            if (!FeatureDescriptorUtility.IsValid(descriptor))
            {
                errors.Add("feature descriptor에 featureId/featureType이 필요합니다.");
                return;
            }

            string featureType = FeatureDescriptorUtility.Normalize(descriptor.featureType);
            if (!featureTypes.Add(featureType))
                errors.Add("featureType이 중복되었습니다: " + featureType);

            var roles = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("role", featureType, ExtractRoles(descriptor), roles, errors);

            var signals = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("gameplaySignalId", featureType, ExtractSignals(descriptor), signals, errors);

            var conditions = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("conditionKind", featureType, ExtractConditionKinds(descriptor), conditions, errors);

            var objectives = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("objectiveKind", featureType, ExtractObjectiveKinds(descriptor), objectives, errors);

            var effects = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("effectKind", featureType, ExtractEffectKinds(descriptor), effects, errors);

            var eventKeys = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("eventKey", featureType, ExtractEventKeys(descriptor), eventKeys, errors);

            FeatureCatalogExposure exposure = descriptor.catalogExposure ?? new FeatureCatalogExposure();
            if (exposure.exposeToAuthoring)
            {
                if ((descriptor.objectRoles ?? Array.Empty<FeatureObjectRoleDescriptor>()).Length == 0)
                    errors.Add("authoring 노출 feature에는 objectRoles가 필요합니다: " + featureType);

                if ((descriptor.compiledGameplayRoleMappings ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>()).Length == 0)
                    errors.Add("authoring 노출 feature에는 compiledGameplayRoleMappings가 필요합니다: " + featureType);
            }

            ValidateSignalReferences(featureType, descriptor.conditionKinds, signals, errors);
            ValidateSignalReferences(featureType, descriptor.objectiveKinds, signals, errors);
            ValidateRoleReferences(featureType, descriptor.compiledGameplayRoleMappings, roles, errors);
            ValidateInputOutputReferences(featureType, descriptor.inputOutputSemantics, objectives, errors);
        }

        private static void AddUniqueValues(
            string label,
            string featureType,
            string[] values,
            HashSet<string> seen,
            List<string> errors)
        {
            string[] safeValues = values ?? Array.Empty<string>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string value = FeatureDescriptorUtility.Normalize(safeValues[i]);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (!seen.Add(value))
                    errors.Add(label + "이 중복되었습니다: " + featureType + "/" + value);
            }
        }

        private static void ValidateSignalReferences(
            string featureType,
            FeatureConditionDescriptor[] conditions,
            HashSet<string> signals,
            List<string> errors)
        {
            FeatureConditionDescriptor[] safeConditions = conditions ?? Array.Empty<FeatureConditionDescriptor>();
            for (int i = 0; i < safeConditions.Length; i++)
            {
                string signal = FeatureDescriptorUtility.Normalize(safeConditions[i] != null ? safeConditions[i].gameplaySignalId : string.Empty);
                if (!string.IsNullOrWhiteSpace(signal) && !signals.Contains(signal))
                    errors.Add("condition이 선언되지 않은 gameplaySignalId를 참조합니다: " + featureType + "/" + signal);
            }
        }

        private static void ValidateSignalReferences(
            string featureType,
            FeatureObjectiveDescriptor[] objectives,
            HashSet<string> signals,
            List<string> errors)
        {
            FeatureObjectiveDescriptor[] safeObjectives = objectives ?? Array.Empty<FeatureObjectiveDescriptor>();
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                string signal = FeatureDescriptorUtility.Normalize(safeObjectives[i] != null ? safeObjectives[i].completionGameplaySignalId : string.Empty);
                if (!string.IsNullOrWhiteSpace(signal) && !signals.Contains(signal))
                    errors.Add("objective가 선언되지 않은 gameplaySignalId를 참조합니다: " + featureType + "/" + signal);
            }
        }

        private static void ValidateRoleReferences(
            string featureType,
            FeatureCompiledGameplayRoleDescriptor[] mappings,
            HashSet<string> roles,
            List<string> errors)
        {
            FeatureCompiledGameplayRoleDescriptor[] safeMappings = mappings ?? Array.Empty<FeatureCompiledGameplayRoleDescriptor>();
            for (int i = 0; i < safeMappings.Length; i++)
            {
                string role = FeatureDescriptorUtility.Normalize(safeMappings[i] != null ? safeMappings[i].role : string.Empty);
                if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role))
                    errors.Add("compiledGameplayRoleMappings가 선언되지 않은 role을 참조합니다: " + featureType + "/" + role);
            }
        }

        private static void ValidateInputOutputReferences(
            string featureType,
            FeatureInputOutputSemantics semantics,
            HashSet<string> objectives,
            List<string> errors)
        {
            ValidateObjectiveKindReferences(featureType, "inputOutputSemantics.generatedItems", semantics != null ? semantics.generatedItems : null, objectives, errors);
            ValidateObjectiveKindReferences(featureType, "inputOutputSemantics.outputItems", semantics != null ? semantics.outputItems : null, objectives, errors);
        }

        private static void ValidateObjectiveKindReferences(
            string featureType,
            string label,
            string[] values,
            HashSet<string> objectives,
            List<string> errors)
        {
            string[] safeValues = values ?? Array.Empty<string>();
            for (int i = 0; i < safeValues.Length; i++)
            {
                string objectiveKind = FeatureDescriptorUtility.Normalize(safeValues[i]);
                if (!string.IsNullOrWhiteSpace(objectiveKind) && !objectives.Contains(objectiveKind))
                    errors.Add(label + "가 선언되지 않은 objectiveKind를 참조합니다: " + featureType + "/" + objectiveKind);
            }
        }

        private static string[] ExtractRoles(FeatureDescriptor descriptor)
        {
            FeatureObjectRoleDescriptor[] values = descriptor.objectRoles ?? Array.Empty<FeatureObjectRoleDescriptor>();
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].role : string.Empty;
            return result;
        }

        private static string[] ExtractSignals(FeatureDescriptor descriptor)
        {
            FeatureGameplaySignalDescriptor[] values = descriptor.gameplaySignals ?? Array.Empty<FeatureGameplaySignalDescriptor>();
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].signalId : string.Empty;
            return result;
        }

        private static string[] ExtractConditionKinds(FeatureDescriptor descriptor)
        {
            FeatureConditionDescriptor[] values = descriptor.conditionKinds ?? Array.Empty<FeatureConditionDescriptor>();
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].kind : string.Empty;
            return result;
        }

        private static string[] ExtractObjectiveKinds(FeatureDescriptor descriptor)
        {
            FeatureObjectiveDescriptor[] values = descriptor.objectiveKinds ?? Array.Empty<FeatureObjectiveDescriptor>();
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].kind : string.Empty;
            return result;
        }

        private static string[] ExtractEffectKinds(FeatureDescriptor descriptor)
        {
            FeatureEffectDescriptor[] values = descriptor.effectKinds ?? Array.Empty<FeatureEffectDescriptor>();
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].kind : string.Empty;
            return result;
        }

        private static string[] ExtractEventKeys(FeatureDescriptor descriptor)
        {
            var result = new List<string>();
            FeatureTargetSurfaceDescriptor[] surfaces = descriptor.targetSurfaces ?? Array.Empty<FeatureTargetSurfaceDescriptor>();
            for (int i = 0; i < surfaces.Length; i++)
                result.AddRange(surfaces[i] != null ? surfaces[i].supportedEventKeys ?? Array.Empty<string>() : Array.Empty<string>());
            return result.ToArray();
        }
    }

}
