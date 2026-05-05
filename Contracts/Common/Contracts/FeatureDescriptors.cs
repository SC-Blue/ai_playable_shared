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
        public const string VALUE_TYPE_STRING = "string";
        public const string VALUE_TYPE_BOOL = "bool";
        public const string VALUE_TYPE_ITEM_REF = "item_ref";
        public const string VALUE_TYPE_TARGET_OBJECT_ID = "target_object_id";
        public const string VALUE_TYPE_AUDIO_CLIP_REF = "audio_clip_ref";
        public const string AUTHORING_STAGE_GENERATE_BAKE = "generate_bake";
        public const string AUTHORING_STAGE_POST_BAKE = "post_bake";
        public const string AUTHORING_STAGE_BOTH = "both";
        public const string POST_BAKE_EDIT_MODE_EDITABLE = "editable";
        public const string POST_BAKE_EDIT_MODE_READ_ONLY = "read_only";
        public const string POST_BAKE_EDIT_MODE_HIDDEN = "hidden";
        public const string EDITOR_CONTROL_ITEM_PICKER = "item_picker";
        public const string EDITOR_CONTROL_TARGET_OBJECT_PICKER = "target_object_picker";
        public const string EDITOR_CONTROL_INT = "int";
        public const string EDITOR_CONTROL_FLOAT = "float";
        public const string EDITOR_CONTROL_STRING = "string";
        public const string EDITOR_CONTROL_TOGGLE = "toggle";
        public const string EDITOR_CONTROL_AUDIO_CLIP_PICKER = "audio_clip_picker";
        public const string EDITOR_PREVIEW_RENDERER_PATH = "path";
        public const string EDITOR_PREVIEW_RENDERER_BOUNDS = "bounds";
        public const string EDITOR_PREVIEW_VISUAL_OPTION_ITEM_REF = "option_item_ref";

        public static bool IsSupportedValueType(string value)
        {
            return Contains(Normalize(value), new[]
            {
                VALUE_TYPE_INT,
                VALUE_TYPE_INT_RANGE,
                VALUE_TYPE_FLOAT,
                VALUE_TYPE_STRING,
                VALUE_TYPE_BOOL,
                VALUE_TYPE_ITEM_REF,
                VALUE_TYPE_TARGET_OBJECT_ID,
                VALUE_TYPE_AUDIO_CLIP_REF,
            });
        }

        public static bool IsSupportedAuthoringStage(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, new[]
            {
                AUTHORING_STAGE_GENERATE_BAKE,
                AUTHORING_STAGE_POST_BAKE,
                AUTHORING_STAGE_BOTH,
            });
        }

        public static bool IsSupportedPostBakeEditMode(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, new[]
            {
                POST_BAKE_EDIT_MODE_EDITABLE,
                POST_BAKE_EDIT_MODE_READ_ONLY,
                POST_BAKE_EDIT_MODE_HIDDEN,
            });
        }

        public static bool IsSupportedEditorControl(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, new[]
            {
                EDITOR_CONTROL_ITEM_PICKER,
                EDITOR_CONTROL_TARGET_OBJECT_PICKER,
                EDITOR_CONTROL_INT,
                EDITOR_CONTROL_FLOAT,
                EDITOR_CONTROL_STRING,
                EDITOR_CONTROL_TOGGLE,
                EDITOR_CONTROL_AUDIO_CLIP_PICKER,
            });
        }

        public static bool IsSupportedEditorControlForValueType(string editorControl, string valueType)
        {
            string control = Normalize(editorControl);
            if (string.IsNullOrEmpty(control))
                return true;

            string type = Normalize(valueType);
            switch (type)
            {
                case VALUE_TYPE_ITEM_REF:
                    return string.Equals(control, EDITOR_CONTROL_ITEM_PICKER, StringComparison.Ordinal);
                case VALUE_TYPE_TARGET_OBJECT_ID:
                    return string.Equals(control, EDITOR_CONTROL_TARGET_OBJECT_PICKER, StringComparison.Ordinal);
                case VALUE_TYPE_INT:
                case VALUE_TYPE_INT_RANGE:
                    return string.Equals(control, EDITOR_CONTROL_INT, StringComparison.Ordinal);
                case VALUE_TYPE_FLOAT:
                    return string.Equals(control, EDITOR_CONTROL_FLOAT, StringComparison.Ordinal);
                case VALUE_TYPE_STRING:
                    return string.Equals(control, EDITOR_CONTROL_STRING, StringComparison.Ordinal);
                case VALUE_TYPE_BOOL:
                    return string.Equals(control, EDITOR_CONTROL_TOGGLE, StringComparison.Ordinal);
                case VALUE_TYPE_AUDIO_CLIP_REF:
                    return string.Equals(control, EDITOR_CONTROL_AUDIO_CLIP_PICKER, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public static bool IsSupportedEditorPreviewRenderer(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, new[]
            {
                EDITOR_PREVIEW_RENDERER_PATH,
                EDITOR_PREVIEW_RENDERER_BOUNDS,
            });
        }

        public static bool IsSupportedEditorPreviewVisualSourceKind(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) ||
                   string.Equals(normalized, EDITOR_PREVIEW_VISUAL_OPTION_ITEM_REF, StringComparison.Ordinal);
        }

        public static bool IsSupportedDesignMode(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, new[]
            {
                GeneratedContentCatalogContracts.DESIGN_MODE_SINGLE_PREFAB,
                GeneratedContentCatalogContracts.DESIGN_MODE_ASSEMBLED_PATH,
                GeneratedContentCatalogContracts.DESIGN_MODE_ENVIRONMENT,
            });
        }

        public static bool IsSupportedPlacementMode(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, new[]
            {
                PlacementModeIds.FILL,
                PlacementModeIds.PERIMETER,
                PlacementModeIds.PATH,
                "free",
                "guide",
            });
        }

        public static bool IsSupportedStepConditionType(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, StepConditionRules.GetSupportedTypes());
        }

        public static bool IsSupportedReactiveConditionType(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || Contains(normalized, ReactiveConditionRules.GetSupportedTypes());
        }

        public static bool IsSupportedSystemActionId(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || SystemActionIds.IsSupportedAuthoring(normalized);
        }

        public static bool IsSupportedRuntimeEventKey(string value)
        {
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) || FlowTargetEventKeys.IsSupported(normalized);
        }

        public static bool IsLowerSnakeCaseToken(string value)
        {
            string normalized = Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (!IsAsciiLower(normalized[0]))
                return false;
            if (normalized[normalized.Length - 1] == '_')
                return false;

            bool previousUnderscore = false;
            for (int i = 0; i < normalized.Length; i++)
            {
                char current = normalized[i];
                if (current == '_')
                {
                    if (previousUnderscore)
                        return false;
                    previousUnderscore = true;
                    continue;
                }

                previousUnderscore = false;
                if (!IsAsciiLower(current) && !IsAsciiDigit(current))
                    return false;
            }

            return true;
        }

        public static bool IsStableIdentifierToken(string value)
        {
            string normalized = Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (!IsAsciiLower(normalized[0]))
                return false;

            for (int i = 0; i < normalized.Length; i++)
            {
                char current = normalized[i];
                if (!IsAsciiLower(current) &&
                    !IsAsciiUpper(current) &&
                    !IsAsciiDigit(current) &&
                    current != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(string value, string[] supported)
        {
            string normalized = Normalize(value);
            string[] safeSupported = supported ?? new string[0];
            for (int i = 0; i < safeSupported.Length; i++)
            {
                if (string.Equals(normalized, Normalize(safeSupported[i]), StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool IsAsciiLower(char value)
        {
            return value >= 'a' && value <= 'z';
        }

        private static bool IsAsciiUpper(char value)
        {
            return value >= 'A' && value <= 'Z';
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }
    }

    [Serializable]
    public sealed class FeatureDescriptorDocument
    {
        public int schemaVersion = FeatureDescriptorContracts.SCHEMA_VERSION;
        public FeatureDescriptor[] featureDescriptors = new FeatureDescriptor[0];
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
        public FeatureObjectRoleDescriptor[] objectRoles = new FeatureObjectRoleDescriptor[0];
        public FeatureOptionSchema optionSchema = new FeatureOptionSchema();
        public FeatureTargetSurfaceDescriptor[] targetSurfaces = new FeatureTargetSurfaceDescriptor[0];
        public FeatureGameplaySignalDescriptor[] gameplaySignals = new FeatureGameplaySignalDescriptor[0];
        public FeatureConditionDescriptor[] conditionKinds = new FeatureConditionDescriptor[0];
        public FeatureObjectiveDescriptor[] objectiveKinds = new FeatureObjectiveDescriptor[0];
        public FeatureEffectDescriptor[] effectKinds = new FeatureEffectDescriptor[0];
        public FeatureCompiledGameplayRoleDescriptor[] compiledGameplayRoleMappings = new FeatureCompiledGameplayRoleDescriptor[0];
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
        public FeatureOptionFieldDescriptor[] fields = new FeatureOptionFieldDescriptor[0];
    }

    [Serializable]
    public sealed class FeatureOptionFieldDescriptor
    {
        public string fieldId = string.Empty;
        public string summary = string.Empty;
        public string valueType = string.Empty;
        public bool required;
        public string[] requiredItemDesignCapabilities = new string[0];
        public int minIntValue;
        public string authoringStage = string.Empty;
        public string postBakeEditMode = string.Empty;
        public string editorControl = string.Empty;
        public string sourceCategory = string.Empty;
    }

    [Serializable]
    public sealed class FeatureTargetSurfaceDescriptor
    {
        public string role = string.Empty;
        public string summary = string.Empty;
        public string[] supportedEventKeys = new string[0];
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
        public string[] supportedTargetRoles = new string[0];
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
        public bool supportsAmountValue;
        public bool requiresAmountValue;
        public bool requiresSeconds;
        public bool canAbsorbArrow;
        public string[] supportedTargetRoles = new string[0];
        public bool allowAnyTargetRole;
        public string completionStepConditionType = string.Empty;
        public string completionGameplaySignalId = string.Empty;
        public string targetEventKey = string.Empty;
        public bool supportsProjectedCurrencyGuide;
        public bool requiresAbsorbedArrow;
        public string requiredArrowEventKey = string.Empty;
    }

    [Serializable]
    public sealed class FeatureEffectDescriptor
    {
        public string kind = string.Empty;
        public string summary = string.Empty;
        public string[] semanticTags = new string[0];
        public bool requiresTargetObjectId;
        public bool supportsTiming;
        public bool requiresEventKey;
        public bool supportsEventKey;
        public bool isNonBlockingSystemAction;
        public string[] supportedTargetRoles = new string[0];
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
        public string[] generatedItems = new string[0];
        public string[] outputItems = new string[0];
        public string[] acceptedTargetRoles = new string[0];
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
        public string[] pathShape = new string[0];
        public string[] requiredDesignCapabilities = new string[0];
    }

    [Serializable]
    public sealed class FeatureEditorPreviewDescriptor
    {
        public string renderer = string.Empty;
        public FeatureEditorPreviewPathDescriptor path = new FeatureEditorPreviewPathDescriptor();
        public FeatureEditorPreviewBoundsDescriptor[] bounds = new FeatureEditorPreviewBoundsDescriptor[0];
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
            FeatureDescriptor[] safeValues = values ?? new FeatureDescriptor[0];
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
            FeatureObjectRoleDescriptor[] safeValues = values ?? new FeatureObjectRoleDescriptor[0];
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
            FeatureOptionFieldDescriptor[] safeValues = values ?? new FeatureOptionFieldDescriptor[0];
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
                    authoringStage = Normalize(value.authoringStage),
                    postBakeEditMode = Normalize(value.postBakeEditMode),
                    editorControl = Normalize(value.editorControl),
                    sourceCategory = Normalize(value.sourceCategory),
                };
            }

            return clones;
        }

        public static FeatureTargetSurfaceDescriptor[] Clone(FeatureTargetSurfaceDescriptor[] values)
        {
            FeatureTargetSurfaceDescriptor[] safeValues = values ?? new FeatureTargetSurfaceDescriptor[0];
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
            FeatureGameplaySignalDescriptor[] safeValues = values ?? new FeatureGameplaySignalDescriptor[0];
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
            FeatureConditionDescriptor[] safeValues = values ?? new FeatureConditionDescriptor[0];
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
            FeatureObjectiveDescriptor[] safeValues = values ?? new FeatureObjectiveDescriptor[0];
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
                    supportsAmountValue = value.supportsAmountValue,
                    requiresAmountValue = value.requiresAmountValue,
                    requiresSeconds = value.requiresSeconds,
                    canAbsorbArrow = value.canAbsorbArrow,
                    supportedTargetRoles = CloneStrings(value.supportedTargetRoles),
                    allowAnyTargetRole = value.allowAnyTargetRole,
                    completionStepConditionType = Normalize(value.completionStepConditionType),
                    completionGameplaySignalId = Normalize(value.completionGameplaySignalId),
                    targetEventKey = Normalize(value.targetEventKey),
                    supportsProjectedCurrencyGuide = value.supportsProjectedCurrencyGuide,
                    requiresAbsorbedArrow = value.requiresAbsorbedArrow,
                    requiredArrowEventKey = Normalize(value.requiredArrowEventKey),
                };
            }

            return clones;
        }

        public static FeatureEffectDescriptor[] Clone(FeatureEffectDescriptor[] values)
        {
            FeatureEffectDescriptor[] safeValues = values ?? new FeatureEffectDescriptor[0];
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
            FeatureCompiledGameplayRoleDescriptor[] safeValues = values ?? new FeatureCompiledGameplayRoleDescriptor[0];
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
            FeatureEditorPreviewBoundsDescriptor[] safeValues = values ?? new FeatureEditorPreviewBoundsDescriptor[0];
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
            string[] safeValues = values ?? new string[0];
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
            FeatureDescriptor[] safeValues = values ?? new FeatureDescriptor[0];
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
            FeatureDescriptor[] safeDescriptors = descriptors ?? new FeatureDescriptor[0];
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
            ValidateLowerSnakeToken("featureType", featureType, featureType, errors);

            var roles = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("role", featureType, ExtractRoles(descriptor), roles, errors);
            ValidateLowerSnakeTokens("role", featureType, ExtractRoles(descriptor), errors);

            var signals = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("gameplaySignalId", featureType, ExtractSignals(descriptor), signals, errors);
            ValidateLowerSnakeTokens("gameplaySignalId", featureType, ExtractSignals(descriptor), errors);

            var conditions = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("conditionKind", featureType, ExtractConditionKinds(descriptor), conditions, errors);
            ValidateLowerSnakeTokens("conditionKind", featureType, ExtractConditionKinds(descriptor), errors);

            var objectives = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("objectiveKind", featureType, ExtractObjectiveKinds(descriptor), objectives, errors);
            ValidateLowerSnakeTokens("objectiveKind", featureType, ExtractObjectiveKinds(descriptor), errors);

            var effects = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("effectKind", featureType, ExtractEffectKinds(descriptor), effects, errors);
            ValidateLowerSnakeTokens("effectKind", featureType, ExtractEffectKinds(descriptor), errors);

            var eventKeys = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueValues("eventKey", featureType, ExtractEventKeys(descriptor), eventKeys, errors);
            ValidateLowerSnakeTokens("eventKey", featureType, ExtractEventKeys(descriptor), errors);

            FeatureCatalogExposure exposure = descriptor.catalogExposure ?? new FeatureCatalogExposure();
            if (exposure.exposeToAuthoring)
            {
                if ((descriptor.objectRoles ?? new FeatureObjectRoleDescriptor[0]).Length == 0)
                    errors.Add("authoring 노출 feature에는 objectRoles가 필요합니다: " + featureType);

                if ((descriptor.compiledGameplayRoleMappings ?? new FeatureCompiledGameplayRoleDescriptor[0]).Length == 0)
                    errors.Add("authoring 노출 feature에는 compiledGameplayRoleMappings가 필요합니다: " + featureType);
            }

            ValidateSignalReferences(featureType, descriptor.conditionKinds, signals, errors);
            ValidateSignalReferences(featureType, descriptor.objectiveKinds, signals, errors);
            ValidateRoleReferences(featureType, descriptor.compiledGameplayRoleMappings, roles, errors);
            ValidateInputOutputReferences(featureType, descriptor.inputOutputSemantics, objectives, errors);
            ValidateOptionSchema(featureType, descriptor.optionSchema, errors);
            ValidateTargetSurfaces(featureType, descriptor.targetSurfaces, roles, errors);
            ValidateGameplaySignals(featureType, descriptor.gameplaySignals, eventKeys, errors);
            ValidateConditions(featureType, descriptor.conditionKinds, roles, errors);
            ValidateObjectives(featureType, descriptor.objectiveKinds, roles, eventKeys, errors);
            ValidateEffects(featureType, descriptor.effectKinds, roles, errors);
            ValidateCompiledGameplayRoleMappings(featureType, descriptor.compiledGameplayRoleMappings, roles, errors);
            ValidateInputOutputRoles(featureType, descriptor.inputOutputSemantics, errors);
            ValidateLayoutRequirements(featureType, descriptor.layoutRequirements, errors);
            ValidateEditorPreview(featureType, descriptor.editorPreview, descriptor.optionSchema, errors);
        }

        private static void ValidateLowerSnakeTokens(string label, string featureType, string[] values, List<string> errors)
        {
            string[] safeValues = values ?? new string[0];
            for (int i = 0; i < safeValues.Length; i++)
                ValidateLowerSnakeToken(label, featureType, safeValues[i], errors);
        }

        private static void ValidateLowerSnakeToken(string label, string featureType, string value, List<string> errors)
        {
            string normalized = FeatureDescriptorUtility.Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return;
            if (!FeatureDescriptorContracts.IsLowerSnakeCaseToken(normalized))
                errors.Add(label + "는 lower_snake_case stable token이어야 합니다: " + featureType + "/" + normalized);
        }

        private static void ValidateStableIdentifierToken(string label, string featureType, string value, List<string> errors)
        {
            string normalized = FeatureDescriptorUtility.Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return;
            if (!FeatureDescriptorContracts.IsStableIdentifierToken(normalized))
                errors.Add(label + "는 공백/구두점이 없는 stable identifier여야 합니다: " + featureType + "/" + normalized);
        }

        private static void ValidateOptionSchema(string featureType, FeatureOptionSchema optionSchema, List<string> errors)
        {
            FeatureOptionFieldDescriptor[] fields = optionSchema != null
                ? optionSchema.fields ?? new FeatureOptionFieldDescriptor[0]
                : new FeatureOptionFieldDescriptor[0];
            var fieldIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Length; i++)
            {
                FeatureOptionFieldDescriptor field = fields[i];
                if (field == null)
                    continue;

                string fieldId = FeatureDescriptorUtility.Normalize(field.fieldId);
                if (string.IsNullOrEmpty(fieldId))
                {
                    errors.Add("optionSchema.fields[" + i + "].fieldId가 필요합니다: " + featureType);
                    continue;
                }

                if (!fieldIds.Add(fieldId))
                    errors.Add("fieldId가 중복되었습니다: " + featureType + "/" + fieldId);
                ValidateStableIdentifierToken("fieldId", featureType, fieldId, errors);

                string valueType = FeatureDescriptorUtility.Normalize(field.valueType);
                if (string.IsNullOrEmpty(valueType))
                    errors.Add("option field valueType이 필요합니다: " + featureType + "/" + fieldId);
                else if (!FeatureDescriptorContracts.IsSupportedValueType(valueType))
                    errors.Add("option field valueType이 지원되지 않습니다: " + featureType + "/" + fieldId + "=" + valueType);

                string authoringStage = FeatureDescriptorUtility.Normalize(field.authoringStage);
                if (!FeatureDescriptorContracts.IsSupportedAuthoringStage(authoringStage))
                    errors.Add("option field authoringStage가 지원되지 않습니다: " + featureType + "/" + fieldId + "=" + authoringStage);

                string postBakeEditMode = FeatureDescriptorUtility.Normalize(field.postBakeEditMode);
                if (!FeatureDescriptorContracts.IsSupportedPostBakeEditMode(postBakeEditMode))
                    errors.Add("option field postBakeEditMode가 지원되지 않습니다: " + featureType + "/" + fieldId + "=" + postBakeEditMode);

                string editorControl = FeatureDescriptorUtility.Normalize(field.editorControl);
                if (!FeatureDescriptorContracts.IsSupportedEditorControl(editorControl))
                    errors.Add("option field editorControl이 지원되지 않습니다: " + featureType + "/" + fieldId + "=" + editorControl);
                else if (!FeatureDescriptorContracts.IsSupportedEditorControlForValueType(editorControl, valueType))
                    errors.Add("option field editorControl이 valueType과 맞지 않습니다: " + featureType + "/" + fieldId + " editorControl=" + editorControl + " valueType=" + valueType);
            }
        }

        private static void ValidateTargetSurfaces(
            string featureType,
            FeatureTargetSurfaceDescriptor[] targetSurfaces,
            HashSet<string> declaredRoles,
            List<string> errors)
        {
            FeatureTargetSurfaceDescriptor[] safeSurfaces = targetSurfaces ?? new FeatureTargetSurfaceDescriptor[0];
            for (int i = 0; i < safeSurfaces.Length; i++)
            {
                FeatureTargetSurfaceDescriptor surface = safeSurfaces[i];
                string role = FeatureDescriptorUtility.Normalize(surface != null ? surface.role : string.Empty);
                if (string.IsNullOrEmpty(role))
                {
                    errors.Add("targetSurfaces[" + i + "].role이 필요합니다: " + featureType);
                    continue;
                }

                if (!IsDeclaredOrCoreRole(role, declaredRoles))
                    errors.Add("targetSurface role이 선언된 feature role 또는 core role이 아닙니다: " + featureType + "/" + role);
            }
        }

        private static void ValidateGameplaySignals(
            string featureType,
            FeatureGameplaySignalDescriptor[] gameplaySignals,
            HashSet<string> declaredEventKeys,
            List<string> errors)
        {
            FeatureGameplaySignalDescriptor[] safeSignals = gameplaySignals ?? new FeatureGameplaySignalDescriptor[0];
            for (int i = 0; i < safeSignals.Length; i++)
            {
                string eventKey = FeatureDescriptorUtility.Normalize(safeSignals[i] != null ? safeSignals[i].requiredTargetEventKey : string.Empty);
                if (!string.IsNullOrEmpty(eventKey) && !declaredEventKeys.Contains(eventKey))
                    errors.Add("gameplaySignal.requiredTargetEventKey가 선언되지 않은 eventKey를 참조합니다: " + featureType + "/" + eventKey);
            }
        }

        private static void ValidateConditions(
            string featureType,
            FeatureConditionDescriptor[] conditionKinds,
            HashSet<string> declaredRoles,
            List<string> errors)
        {
            FeatureConditionDescriptor[] safeConditions = conditionKinds ?? new FeatureConditionDescriptor[0];
            for (int i = 0; i < safeConditions.Length; i++)
            {
                FeatureConditionDescriptor condition = safeConditions[i];
                string kind = FeatureDescriptorUtility.Normalize(condition != null ? condition.kind : string.Empty);
                string stepType = FeatureDescriptorUtility.Normalize(condition != null ? condition.stepConditionType : string.Empty);
                if (!FeatureDescriptorContracts.IsSupportedStepConditionType(stepType))
                    errors.Add("condition stepConditionType이 지원되지 않습니다: " + featureType + "/" + kind + "=" + stepType);

                string reactiveType = FeatureDescriptorUtility.Normalize(condition != null ? condition.reactiveConditionType : string.Empty);
                if (!FeatureDescriptorContracts.IsSupportedReactiveConditionType(reactiveType))
                    errors.Add("condition reactiveConditionType이 지원되지 않습니다: " + featureType + "/" + kind + "=" + reactiveType);

                ValidateSupportedTargetRoles(featureType, "condition.supportedTargetRoles", kind, condition != null ? condition.supportedTargetRoles : null, declaredRoles, errors);
            }
        }

        private static void ValidateObjectives(
            string featureType,
            FeatureObjectiveDescriptor[] objectiveKinds,
            HashSet<string> declaredRoles,
            HashSet<string> declaredEventKeys,
            List<string> errors)
        {
            FeatureObjectiveDescriptor[] safeObjectives = objectiveKinds ?? new FeatureObjectiveDescriptor[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                FeatureObjectiveDescriptor objective = safeObjectives[i];
                string kind = FeatureDescriptorUtility.Normalize(objective != null ? objective.kind : string.Empty);
                string completionType = FeatureDescriptorUtility.Normalize(objective != null ? objective.completionStepConditionType : string.Empty);
                if (!FeatureDescriptorContracts.IsSupportedStepConditionType(completionType))
                    errors.Add("objective completionStepConditionType이 지원되지 않습니다: " + featureType + "/" + kind + "=" + completionType);
                if (objective != null && objective.requiresAmountValue && !objective.supportsAmountValue)
                    errors.Add("objective requiresAmountValue는 supportsAmountValue 없이 사용할 수 없습니다: " + featureType + "/" + kind);

                ValidateEventKeyReference(featureType, "objective.targetEventKey", kind, objective != null ? objective.targetEventKey : string.Empty, declaredEventKeys, errors);
                ValidateEventKeyReference(featureType, "objective.requiredArrowEventKey", kind, objective != null ? objective.requiredArrowEventKey : string.Empty, declaredEventKeys, errors);
                ValidateSupportedTargetRoles(featureType, "objective.supportedTargetRoles", kind, objective != null ? objective.supportedTargetRoles : null, declaredRoles, errors);
            }
        }

        private static void ValidateEffects(
            string featureType,
            FeatureEffectDescriptor[] effectKinds,
            HashSet<string> declaredRoles,
            List<string> errors)
        {
            FeatureEffectDescriptor[] safeEffects = effectKinds ?? new FeatureEffectDescriptor[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                FeatureEffectDescriptor effect = safeEffects[i];
                string kind = FeatureDescriptorUtility.Normalize(effect != null ? effect.kind : string.Empty);
                string systemActionId = FeatureDescriptorUtility.Normalize(effect != null ? effect.systemActionId : string.Empty);
                if (!FeatureDescriptorContracts.IsSupportedSystemActionId(systemActionId))
                    errors.Add("effect systemActionId가 지원되지 않습니다: " + featureType + "/" + kind + "=" + systemActionId);

                string runtimeEventKey = FeatureDescriptorUtility.Normalize(effect != null ? effect.runtimeEventKey : string.Empty);
                if (!FeatureDescriptorContracts.IsSupportedRuntimeEventKey(runtimeEventKey))
                    errors.Add("effect runtimeEventKey가 shared flow target event key가 아닙니다: " + featureType + "/" + kind + "=" + runtimeEventKey);

                ValidateSupportedTargetRoles(featureType, "effect.supportedTargetRoles", kind, effect != null ? effect.supportedTargetRoles : null, declaredRoles, errors);
            }
        }

        private static void ValidateCompiledGameplayRoleMappings(
            string featureType,
            FeatureCompiledGameplayRoleDescriptor[] mappings,
            HashSet<string> declaredRoles,
            List<string> errors)
        {
            FeatureCompiledGameplayRoleDescriptor[] safeMappings = mappings ?? new FeatureCompiledGameplayRoleDescriptor[0];
            for (int i = 0; i < safeMappings.Length; i++)
            {
                FeatureCompiledGameplayRoleDescriptor mapping = safeMappings[i];
                ValidateLowerSnakeToken("compiledGameplayRoleMappings.gameplayObjectId", featureType, mapping != null ? mapping.gameplayObjectId : string.Empty, errors);
                string role = FeatureDescriptorUtility.Normalize(mapping != null ? mapping.role : string.Empty);
                if (!string.IsNullOrEmpty(role) && !IsDeclaredOrCoreRole(role, declaredRoles))
                    errors.Add("compiledGameplayRoleMappings.role이 선언된 feature role 또는 core role이 아닙니다: " + featureType + "/" + role);
            }
        }

        private static void ValidateInputOutputRoles(
            string featureType,
            FeatureInputOutputSemantics semantics,
            List<string> errors)
        {
            string[] acceptedTargetRoles = semantics != null ? semantics.acceptedTargetRoles ?? new string[0] : new string[0];
            for (int i = 0; i < acceptedTargetRoles.Length; i++)
            {
                string role = FeatureDescriptorUtility.Normalize(acceptedTargetRoles[i]);
                if (string.IsNullOrEmpty(role))
                    continue;
                if (!FeatureDescriptorContracts.IsLowerSnakeCaseToken(role))
                    errors.Add("inputOutputSemantics.acceptedTargetRoles는 lower_snake_case stable token이어야 합니다: " + featureType + "/" + role);
            }
        }

        private static void ValidateLayoutRequirements(string featureType, FeatureLayoutRequirementDescriptor layout, List<string> errors)
        {
            if (layout == null)
                return;

            string designMode = FeatureDescriptorUtility.Normalize(layout.designMode);
            if (!FeatureDescriptorContracts.IsSupportedDesignMode(designMode))
                errors.Add("layoutRequirements.designMode가 지원되지 않습니다: " + featureType + "/" + designMode);

            string placementMode = FeatureDescriptorUtility.Normalize(layout.placementMode);
            if (!FeatureDescriptorContracts.IsSupportedPlacementMode(placementMode))
                errors.Add("layoutRequirements.placementMode가 지원되지 않습니다: " + featureType + "/" + placementMode);

            ValidateLowerSnakeTokens("layoutRequirements.pathShape", featureType, layout.pathShape, errors);
        }

        private static void ValidateEditorPreview(
            string featureType,
            FeatureEditorPreviewDescriptor preview,
            FeatureOptionSchema optionSchema,
            List<string> errors)
        {
            if (preview == null)
                return;

            string renderer = FeatureDescriptorUtility.Normalize(preview.renderer);
            if (!FeatureDescriptorContracts.IsSupportedEditorPreviewRenderer(renderer))
                errors.Add("editorPreview.renderer가 지원되지 않습니다: " + featureType + "/" + renderer);

            if (preview.path != null)
            {
                ValidateStableIdentifierToken("editorPreview.path.cellsField", featureType, preview.path.cellsField, errors);
                ValidateStableIdentifierToken("editorPreview.path.sinkTargetField", featureType, preview.path.sinkTargetField, errors);
                ValidateLowerSnakeToken("editorPreview.path.straightDesignSlot", featureType, preview.path.straightDesignSlot, errors);
                ValidateLowerSnakeToken("editorPreview.path.cornerDesignSlot", featureType, preview.path.cornerDesignSlot, errors);
            }

            FeatureEditorPreviewBoundsDescriptor[] bounds = preview.bounds ?? new FeatureEditorPreviewBoundsDescriptor[0];
            for (int i = 0; i < bounds.Length; i++)
            {
                ValidateStableIdentifierToken("editorPreview.bounds.field", featureType, bounds[i] != null ? bounds[i].field : string.Empty, errors);
                ValidateLowerSnakeToken("editorPreview.bounds.zoneKind", featureType, bounds[i] != null ? bounds[i].zoneKind : string.Empty, errors);
            }

            FeatureEditorPreviewVisualSourceDescriptor visualSource = preview.visualSource;
            string visualKind = FeatureDescriptorUtility.Normalize(visualSource != null ? visualSource.kind : string.Empty);
            if (!FeatureDescriptorContracts.IsSupportedEditorPreviewVisualSourceKind(visualKind))
                errors.Add("editorPreview.visualSource.kind가 지원되지 않습니다: " + featureType + "/" + visualKind);
            if (string.Equals(visualKind, FeatureDescriptorContracts.EDITOR_PREVIEW_VISUAL_OPTION_ITEM_REF, StringComparison.Ordinal))
            {
                string optionFieldId = FeatureDescriptorUtility.Normalize(visualSource != null ? visualSource.optionFieldId : string.Empty);
                if (!OptionFieldExists(optionSchema, optionFieldId))
                    errors.Add("editorPreview.visualSource.optionFieldId가 optionSchema.fields에 없습니다: " + featureType + "/" + optionFieldId);
            }
        }

        private static void ValidateSupportedTargetRoles(
            string featureType,
            string label,
            string owner,
            string[] roles,
            HashSet<string> declaredRoles,
            List<string> errors)
        {
            string[] safeRoles = roles ?? new string[0];
            for (int i = 0; i < safeRoles.Length; i++)
            {
                string role = FeatureDescriptorUtility.Normalize(safeRoles[i]);
                if (string.IsNullOrEmpty(role))
                    continue;
                if (!IsDeclaredOrCoreRole(role, declaredRoles))
                    errors.Add(label + "이 선언된 feature role 또는 core role이 아닙니다: " + featureType + "/" + owner + "/" + role);
            }
        }

        private static void ValidateEventKeyReference(
            string featureType,
            string label,
            string owner,
            string eventKey,
            HashSet<string> declaredEventKeys,
            List<string> errors)
        {
            string normalized = FeatureDescriptorUtility.Normalize(eventKey);
            if (string.IsNullOrEmpty(normalized))
                return;
            if (!declaredEventKeys.Contains(normalized))
                errors.Add(label + "가 선언되지 않은 eventKey를 참조합니다: " + featureType + "/" + owner + "/" + normalized);
        }

        private static bool IsDeclaredOrCoreRole(string role, HashSet<string> declaredRoles)
        {
            string normalized = FeatureDescriptorUtility.Normalize(role);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (declaredRoles != null && declaredRoles.Contains(normalized))
                return true;
            return string.Equals(normalized, PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal) ||
                   string.Equals(normalized, PromptIntentObjectRoles.UNLOCK_PAD, StringComparison.Ordinal);
        }

        private static bool OptionFieldExists(FeatureOptionSchema optionSchema, string fieldId)
        {
            string normalized = FeatureDescriptorUtility.Normalize(fieldId);
            if (string.IsNullOrEmpty(normalized))
                return false;

            FeatureOptionFieldDescriptor[] fields = optionSchema != null
                ? optionSchema.fields ?? new FeatureOptionFieldDescriptor[0]
                : new FeatureOptionFieldDescriptor[0];
            for (int i = 0; i < fields.Length; i++)
            {
                if (string.Equals(FeatureDescriptorUtility.Normalize(fields[i] != null ? fields[i].fieldId : string.Empty), normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AddUniqueValues(
            string label,
            string featureType,
            string[] values,
            HashSet<string> seen,
            List<string> errors)
        {
            string[] safeValues = values ?? new string[0];
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
            FeatureConditionDescriptor[] safeConditions = conditions ?? new FeatureConditionDescriptor[0];
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
            FeatureObjectiveDescriptor[] safeObjectives = objectives ?? new FeatureObjectiveDescriptor[0];
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
            FeatureCompiledGameplayRoleDescriptor[] safeMappings = mappings ?? new FeatureCompiledGameplayRoleDescriptor[0];
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
            string[] safeValues = values ?? new string[0];
            for (int i = 0; i < safeValues.Length; i++)
            {
                string objectiveKind = FeatureDescriptorUtility.Normalize(safeValues[i]);
                if (!string.IsNullOrWhiteSpace(objectiveKind) && !objectives.Contains(objectiveKind))
                    errors.Add(label + "가 선언되지 않은 objectiveKind를 참조합니다: " + featureType + "/" + objectiveKind);
            }
        }

        private static string[] ExtractRoles(FeatureDescriptor descriptor)
        {
            FeatureObjectRoleDescriptor[] values = descriptor.objectRoles ?? new FeatureObjectRoleDescriptor[0];
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].role : string.Empty;
            return result;
        }

        private static string[] ExtractSignals(FeatureDescriptor descriptor)
        {
            FeatureGameplaySignalDescriptor[] values = descriptor.gameplaySignals ?? new FeatureGameplaySignalDescriptor[0];
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].signalId : string.Empty;
            return result;
        }

        private static string[] ExtractConditionKinds(FeatureDescriptor descriptor)
        {
            FeatureConditionDescriptor[] values = descriptor.conditionKinds ?? new FeatureConditionDescriptor[0];
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].kind : string.Empty;
            return result;
        }

        private static string[] ExtractObjectiveKinds(FeatureDescriptor descriptor)
        {
            FeatureObjectiveDescriptor[] values = descriptor.objectiveKinds ?? new FeatureObjectiveDescriptor[0];
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].kind : string.Empty;
            return result;
        }

        private static string[] ExtractEffectKinds(FeatureDescriptor descriptor)
        {
            FeatureEffectDescriptor[] values = descriptor.effectKinds ?? new FeatureEffectDescriptor[0];
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i] != null ? values[i].kind : string.Empty;
            return result;
        }

        private static string[] ExtractEventKeys(FeatureDescriptor descriptor)
        {
            var result = new List<string>();
            FeatureTargetSurfaceDescriptor[] surfaces = descriptor.targetSurfaces ?? new FeatureTargetSurfaceDescriptor[0];
            for (int i = 0; i < surfaces.Length; i++)
                result.AddRange(surfaces[i] != null ? surfaces[i].supportedEventKeys ?? new string[0] : new string[0]);
            return result.ToArray();
        }
    }

}

