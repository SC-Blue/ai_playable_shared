using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Common.Contracts
{
    public sealed class PromptIntentTargetSurfaceRuleDescriptor
    {
        public string role;
        public string summary;
        public string[] supportedEventKeys = new string[0];
    }

    public sealed class PromptIntentGameplaySignalRuleDescriptor
    {
        public string signalId;
        public string summary;
        public bool supportsTargetId;
        public bool requiresTargetId;
        public bool supportsItem;
        public bool requiresItem;
        public bool supportsCurrencyId;
        public bool requiresCurrencyId;
        public string requiredTargetEventKey;
    }

    public sealed class PromptIntentSystemActionRuleDescriptor
    {
        public string authoringId;
        public string summary;
        public bool requiresTargetObjectId;
        public string defaultEventKey;
    }

    public sealed class PromptIntentConditionCapabilityDescriptor
    {
        public string kind;
        public string summary;
        public string[] supportedTargetRoles = new string[0];
        public bool allowAnyTargetRole;
        public string gameplaySignalId;
        public string stepConditionType;
        public string reactiveConditionType;
    }

    public sealed class PromptIntentObjectiveCapabilityDescriptor
    {
        public string kind;
        public string summary;
        public string[] supportedTargetRoles = new string[0];
        public bool allowAnyTargetRole;
        public string completionStepConditionType;
        public string completionGameplaySignalId;
        public string targetEventKey;
        public bool requiresAbsorbedArrow;
        public string requiredArrowEventKey;
    }

    public sealed class PromptIntentEffectCapabilityDescriptor
    {
        public string kind;
        public string summary;
        public string[] semanticTags = new string[0];
        public string[] supportedTargetRoles = new string[0];
        public bool allowAnyTargetRole;
        public string systemActionId;
        public string runtimeEventKey;
        public bool buildsSceneActivationTarget;
        public bool buildsSystemActionTarget;
    }

    public static class PromptIntentCapabilityRegistry
    {
        // <generated-capability-registry-data>
        private static readonly PromptIntentTargetSurfaceRuleDescriptor[] TARGET_SURFACES =
        {
            new PromptIntentTargetSurfaceRuleDescriptor { role = PromptIntentObjectRoles.UNLOCK_PAD, summary = "해금 발판 surface", supportedEventKeys = new[] { FlowTargetEventKeys.ROOT } },
            new PromptIntentTargetSurfaceRuleDescriptor { role = PromptIntentObjectRoles.GENERATOR, summary = "generator surface", supportedEventKeys = new[] { FlowTargetEventKeys.ROOT, FlowTargetEventKeys.GET_ITEM } },
            new PromptIntentTargetSurfaceRuleDescriptor { role = PromptIntentObjectRoles.PROCESSOR, summary = "processor surface", supportedEventKeys = new[] { FlowTargetEventKeys.ROOT, FlowTargetEventKeys.DROP_ITEM, FlowTargetEventKeys.GET_ITEM } },
            new PromptIntentTargetSurfaceRuleDescriptor { role = PromptIntentObjectRoles.SELLER, summary = "seller surface", supportedEventKeys = new[] { FlowTargetEventKeys.ROOT, FlowTargetEventKeys.DROP_ITEM, FlowTargetEventKeys.SELL_ITEM, FlowTargetEventKeys.COLLECT_MONEY } },
            new PromptIntentTargetSurfaceRuleDescriptor { role = PromptIntentObjectRoles.PHYSICS_AREA, summary = "physics_area surface", supportedEventKeys = new[] { FlowTargetEventKeys.ROOT, FlowTargetEventKeys.GET_ITEM } },
        };

        private static readonly PromptIntentGameplaySignalRuleDescriptor[] GAMEPLAY_SIGNALS =
        {
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.ITEM_GENERATED, summary = "generator 생성 완료 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = true, requiresItem = true, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.ITEM_COLLECTED, summary = "아이템 수집 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = true, requiresItem = true, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.RAIL_ITEM_ARRIVED, summary = "rail 도착 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = true, requiresItem = true, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.ITEM_DELIVERED, summary = "아이템 전달 신호", supportsTargetId = false, requiresTargetId = false, supportsItem = true, requiresItem = true, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.ITEM_CONVERTED, summary = "가공 완료 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = true, requiresItem = true, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.SALE_COMPLETED, summary = "판매 완료 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = true, requiresItem = false, supportsCurrencyId = true, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.MONEY_COLLECTED, summary = "돈 수집 완료 신호", supportsTargetId = false, requiresTargetId = false, supportsItem = false, requiresItem = false, supportsCurrencyId = true, requiresCurrencyId = true, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.CUSTOMER_SPAWN_STARTED, summary = "손님 등장 시작 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = false, requiresItem = false, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
            new PromptIntentGameplaySignalRuleDescriptor { signalId = GameplaySignalIds.CUSTOMER_SERVED, summary = "손님 응대 완료 신호", supportsTargetId = true, requiresTargetId = true, supportsItem = false, requiresItem = false, supportsCurrencyId = false, requiresCurrencyId = false, requiredTargetEventKey = string.Empty },
        };

        private static readonly PromptIntentSystemActionRuleDescriptor[] SYSTEM_ACTIONS =
        {
            new PromptIntentSystemActionRuleDescriptor { authoringId = SystemActionIds.REVEAL_ENDCARD_UI, summary = "엔드카드 UI 노출", requiresTargetObjectId = false, defaultEventKey = string.Empty },
            new PromptIntentSystemActionRuleDescriptor { authoringId = SystemActionIds.END_GAME, summary = "엔드카드 없이 즉시 게임 종료 및 CTA 활성화", requiresTargetObjectId = false, defaultEventKey = string.Empty },
            new PromptIntentSystemActionRuleDescriptor { authoringId = SystemActionIds.FOCUS_CAMERA_ON_TARGET, summary = "타깃 카메라 포커스", requiresTargetObjectId = true, defaultEventKey = FlowTargetEventKeys.ROOT },
            new PromptIntentSystemActionRuleDescriptor { authoringId = SystemActionIds.SHOW_ARROW_ON_TARGET, summary = "타깃 화살표 표시", requiresTargetObjectId = true, defaultEventKey = FlowTargetEventKeys.ROOT },
            new PromptIntentSystemActionRuleDescriptor { authoringId = SystemActionIds.HIDE_GUIDE, summary = "가이드 숨김", requiresTargetObjectId = false, defaultEventKey = string.Empty },
        };

        private static readonly PromptIntentConditionCapabilityDescriptor[] CONDITION_CAPABILITIES =
        {
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.START, summary = "stage 즉시 시작", supportedTargetRoles = new string[0], allowAnyTargetRole = false, gameplaySignalId = string.Empty, stepConditionType = StepConditionRules.ALWAYS, reactiveConditionType = StepConditionRules.ALWAYS },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.STAGE_COMPLETED, summary = "이전 stage 마지막 beat 완료 대기", supportedTargetRoles = new string[0], allowAnyTargetRole = false, gameplaySignalId = string.Empty, stepConditionType = StepConditionRules.BEAT_COMPLETED, reactiveConditionType = ReactiveConditionRules.BEAT_COMPLETED },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.BALANCE_AT_LEAST, summary = "잔액 threshold 진입", supportedTargetRoles = new string[0], allowAnyTargetRole = false, gameplaySignalId = string.Empty, stepConditionType = StepConditionRules.CURRENCY_AT_LEAST, reactiveConditionType = StepConditionRules.CURRENCY_AT_LEAST },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.UNLOCK_COMPLETED, summary = "unlock pad 완료", supportedTargetRoles = new[] { PromptIntentObjectRoles.UNLOCK_PAD }, allowAnyTargetRole = false, gameplaySignalId = string.Empty, stepConditionType = StepConditionRules.UNLOCKER_UNLOCKED, reactiveConditionType = StepConditionRules.UNLOCKER_UNLOCKED },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.ITEM_GENERATED, summary = "generator 생성 신호", supportedTargetRoles = new[] { PromptIntentObjectRoles.GENERATOR }, allowAnyTargetRole = false, gameplaySignalId = GameplaySignalIds.ITEM_GENERATED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.ITEM_COLLECTED, summary = "수집 신호", supportedTargetRoles = new string[0], allowAnyTargetRole = true, gameplaySignalId = GameplaySignalIds.ITEM_COLLECTED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.ITEM_CONVERTED, summary = "가공 완료 신호", supportedTargetRoles = new[] { PromptIntentObjectRoles.PROCESSOR }, allowAnyTargetRole = false, gameplaySignalId = GameplaySignalIds.ITEM_CONVERTED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.RAIL_ITEM_ARRIVED, summary = "rail 도착 신호", supportedTargetRoles = new string[0], allowAnyTargetRole = true, gameplaySignalId = GameplaySignalIds.RAIL_ITEM_ARRIVED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.SALE_COMPLETED, summary = "판매 완료 신호", supportedTargetRoles = new[] { PromptIntentObjectRoles.SELLER }, allowAnyTargetRole = false, gameplaySignalId = GameplaySignalIds.SALE_COMPLETED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.MONEY_COLLECTED, summary = "돈 수집 완료 신호", supportedTargetRoles = new string[0], allowAnyTargetRole = false, gameplaySignalId = GameplaySignalIds.MONEY_COLLECTED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
            new PromptIntentConditionCapabilityDescriptor { kind = PromptIntentConditionKinds.CUSTOMER_SERVED, summary = "seller 응대 완료 신호", supportedTargetRoles = new[] { PromptIntentObjectRoles.SELLER }, allowAnyTargetRole = false, gameplaySignalId = GameplaySignalIds.CUSTOMER_SERVED, stepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, reactiveConditionType = StepConditionRules.GAMEPLAY_SIGNAL },
        };

        private static readonly PromptIntentObjectiveCapabilityDescriptor[] OBJECTIVE_CAPABILITIES =
        {
            new PromptIntentObjectiveCapabilityDescriptor { kind = PromptIntentObjectiveKinds.UNLOCK_OBJECT, summary = "unlock pad 완료 beat", supportedTargetRoles = new[] { PromptIntentObjectRoles.UNLOCK_PAD }, allowAnyTargetRole = false, completionStepConditionType = StepConditionRules.UNLOCKER_UNLOCKED, completionGameplaySignalId = string.Empty, targetEventKey = string.Empty, requiresAbsorbedArrow = false, requiredArrowEventKey = string.Empty },
            new PromptIntentObjectiveCapabilityDescriptor { kind = PromptIntentObjectiveKinds.COLLECT_ITEM, summary = "item collected signal beat", supportedTargetRoles = new string[0], allowAnyTargetRole = true, completionStepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, completionGameplaySignalId = GameplaySignalIds.ITEM_COLLECTED, targetEventKey = string.Empty, requiresAbsorbedArrow = false, requiredArrowEventKey = string.Empty },
            new PromptIntentObjectiveCapabilityDescriptor { kind = PromptIntentObjectiveKinds.CONVERT_ITEM, summary = "item converted signal beat", supportedTargetRoles = new[] { PromptIntentObjectRoles.PROCESSOR }, allowAnyTargetRole = false, completionStepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, completionGameplaySignalId = GameplaySignalIds.ITEM_CONVERTED, targetEventKey = string.Empty, requiresAbsorbedArrow = true, requiredArrowEventKey = FlowTargetEventKeys.DROP_ITEM },
            new PromptIntentObjectiveCapabilityDescriptor { kind = PromptIntentObjectiveKinds.SELL_ITEM, summary = "sale completed signal beat", supportedTargetRoles = new[] { PromptIntentObjectRoles.SELLER }, allowAnyTargetRole = false, completionStepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, completionGameplaySignalId = GameplaySignalIds.SALE_COMPLETED, targetEventKey = string.Empty, requiresAbsorbedArrow = false, requiredArrowEventKey = string.Empty },
            new PromptIntentObjectiveCapabilityDescriptor { kind = PromptIntentObjectiveKinds.COLLECT_CURRENCY, summary = "money collected signal beat", supportedTargetRoles = new string[0], allowAnyTargetRole = true, completionStepConditionType = StepConditionRules.GAMEPLAY_SIGNAL, completionGameplaySignalId = GameplaySignalIds.MONEY_COLLECTED, targetEventKey = string.Empty, requiresAbsorbedArrow = false, requiredArrowEventKey = string.Empty },
            new PromptIntentObjectiveCapabilityDescriptor { kind = PromptIntentObjectiveKinds.WAIT_SECONDS, summary = "timeout beat", supportedTargetRoles = new string[0], allowAnyTargetRole = false, completionStepConditionType = StepConditionRules.TIMEOUT, completionGameplaySignalId = string.Empty, targetEventKey = string.Empty, requiresAbsorbedArrow = false, requiredArrowEventKey = string.Empty },
        };

        private static readonly PromptIntentEffectCapabilityDescriptor[] EFFECT_CAPABILITIES =
        {
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.REVEAL_OBJECT, summary = "scene ref reveal target", supportedTargetRoles = new string[0], allowAnyTargetRole = true, systemActionId = string.Empty, runtimeEventKey = string.Empty, buildsSceneActivationTarget = true, buildsSystemActionTarget = false },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.ACTIVATE_OBJECT, summary = "scene ref activate target", supportedTargetRoles = new string[0], allowAnyTargetRole = true, systemActionId = string.Empty, runtimeEventKey = string.Empty, buildsSceneActivationTarget = true, buildsSystemActionTarget = false },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.FOCUS_CAMERA, summary = "focus camera system action mapping", supportedTargetRoles = new string[0], allowAnyTargetRole = true, systemActionId = SystemActionIds.FOCUS_CAMERA_ON_TARGET, runtimeEventKey = FlowTargetEventKeys.ROOT, buildsSceneActivationTarget = false, buildsSystemActionTarget = false },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.SHOW_ARROW, summary = "objective-bound show arrow system action mapping", supportedTargetRoles = new string[0], allowAnyTargetRole = true, systemActionId = SystemActionIds.SHOW_ARROW_ON_TARGET, runtimeEventKey = FlowTargetEventKeys.ROOT, buildsSceneActivationTarget = false, buildsSystemActionTarget = false },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.SHOW_GUIDE_ARROW, summary = "presentation-only show arrow system action mapping", supportedTargetRoles = new string[0], allowAnyTargetRole = true, systemActionId = SystemActionIds.SHOW_ARROW_ON_TARGET, runtimeEventKey = FlowTargetEventKeys.ROOT, buildsSceneActivationTarget = false, buildsSystemActionTarget = false },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.SPAWN_CUSTOMER, summary = "customer spawn effect", supportedTargetRoles = new[] { PromptIntentObjectRoles.SELLER }, allowAnyTargetRole = false, systemActionId = string.Empty, runtimeEventKey = string.Empty, buildsSceneActivationTarget = false, buildsSystemActionTarget = false },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.REVEAL_ENDCARD, summary = "reveal endcard system action target", supportedTargetRoles = new string[0], allowAnyTargetRole = false, systemActionId = SystemActionIds.REVEAL_ENDCARD_UI, runtimeEventKey = string.Empty, buildsSceneActivationTarget = false, buildsSystemActionTarget = true },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.END_GAME, summary = "end game system action target", supportedTargetRoles = new string[0], allowAnyTargetRole = false, systemActionId = SystemActionIds.END_GAME, runtimeEventKey = string.Empty, buildsSceneActivationTarget = false, buildsSystemActionTarget = true },
            new PromptIntentEffectCapabilityDescriptor { kind = PromptIntentEffectKinds.HIDE_GUIDE, summary = "hide guide system action target", supportedTargetRoles = new string[0], allowAnyTargetRole = false, systemActionId = SystemActionIds.HIDE_GUIDE, runtimeEventKey = string.Empty, buildsSceneActivationTarget = false, buildsSystemActionTarget = true },
        };
        // </generated-capability-registry-data>

        public static void SetActiveFeatureDescriptors(FeatureDescriptor[] descriptors)
        {
            PromptIntentFeatureDescriptorBridge.SetActiveFeatureDescriptors(descriptors);
        }

        public static void ClearActiveFeatureDescriptors()
        {
            PromptIntentFeatureDescriptorBridge.ClearActiveFeatureDescriptors();
        }

        public static PromptIntentTargetSurfaceRuleDescriptor[] GetTargetSurfaceRules()
        {
            return CloneTargetSurfaceRules(GetTargetSurfacesInternal());
        }

        public static string[] GetSupportedFlowTargetEventKeys(string role)
        {
            PromptIntentTargetSurfaceRuleDescriptor descriptor = FindTargetSurface(role);
            return descriptor != null ? CloneStrings(descriptor.supportedEventKeys) : new string[0];
        }

        public static bool RoleSupportsTargetEventKey(string role, string eventKey)
        {
            PromptIntentTargetSurfaceRuleDescriptor descriptor = FindTargetSurface(role);
            return descriptor != null && ContainsValue(descriptor.supportedEventKeys, eventKey);
        }

        public static PromptIntentGameplaySignalRuleDescriptor[] GetGameplaySignalRules()
        {
            return CloneGameplaySignalRules(GetGameplaySignalsInternal());
        }

        public static bool IsSupportedGameplaySignalId(string signalId)
        {
            return FindGameplaySignal(signalId) != null;
        }

        public static bool GameplaySignalRequiresTargetId(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null && descriptor.requiresTargetId;
        }

        public static bool GameplaySignalSupportsTargetId(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null && descriptor.supportsTargetId;
        }

        public static bool GameplaySignalRequiresItem(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null && descriptor.requiresItem;
        }

        public static bool GameplaySignalSupportsItem(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null && descriptor.supportsItem;
        }

        public static bool GameplaySignalRequiresCurrencyId(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null && descriptor.requiresCurrencyId;
        }

        public static bool GameplaySignalSupportsCurrencyId(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null && descriptor.supportsCurrencyId;
        }

        public static string GetGameplaySignalRequiredTargetEventKey(string signalId)
        {
            PromptIntentGameplaySignalRuleDescriptor descriptor = FindGameplaySignal(signalId);
            return descriptor != null ? descriptor.requiredTargetEventKey : string.Empty;
        }

        public static PromptIntentSystemActionRuleDescriptor[] GetSystemActionRules()
        {
            return CloneSystemActionRules(SYSTEM_ACTIONS);
        }

        public static bool IsSupportedSystemActionAuthoringId(string id)
        {
            return FindSystemAction(id) != null;
        }

        public static bool SystemActionRequiresTargetObjectId(string id)
        {
            PromptIntentSystemActionRuleDescriptor descriptor = FindSystemAction(id);
            return descriptor != null && descriptor.requiresTargetObjectId;
        }

        public static string GetSystemActionDefaultEventKey(string id)
        {
            PromptIntentSystemActionRuleDescriptor descriptor = FindSystemAction(id);
            return descriptor != null ? descriptor.defaultEventKey : string.Empty;
        }

        public static PromptIntentConditionCapabilityDescriptor[] GetConditionCapabilities()
        {
            return CloneConditionCapabilities(GetConditionCapabilitiesInternal());
        }

        public static string[] GetConditionSupportedTargetRoles(string kind)
        {
            PromptIntentConditionCapabilityDescriptor descriptor = FindConditionCapability(kind);
            return descriptor != null ? CloneStrings(descriptor.supportedTargetRoles) : new string[0];
        }

        public static bool ConditionAllowsAnyTargetRole(string kind)
        {
            PromptIntentConditionCapabilityDescriptor descriptor = FindConditionCapability(kind);
            return descriptor != null && descriptor.allowAnyTargetRole;
        }

        public static string GetConditionGameplaySignalId(string kind)
        {
            PromptIntentConditionCapabilityDescriptor descriptor = FindConditionCapability(kind);
            return descriptor != null ? descriptor.gameplaySignalId : string.Empty;
        }

        public static string GetConditionStepConditionType(string kind)
        {
            PromptIntentConditionCapabilityDescriptor descriptor = FindConditionCapability(kind);
            return descriptor != null ? descriptor.stepConditionType : string.Empty;
        }

        public static string GetConditionReactiveConditionType(string kind)
        {
            PromptIntentConditionCapabilityDescriptor descriptor = FindConditionCapability(kind);
            return descriptor != null ? descriptor.reactiveConditionType : string.Empty;
        }

        public static PromptIntentObjectiveCapabilityDescriptor[] GetObjectiveCapabilities()
        {
            return CloneObjectiveCapabilities(GetObjectiveCapabilitiesInternal());
        }

        public static string[] GetObjectiveSupportedTargetRoles(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null ? CloneStrings(descriptor.supportedTargetRoles) : new string[0];
        }

        public static bool ObjectiveAllowsAnyTargetRole(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null && descriptor.allowAnyTargetRole;
        }

        public static string GetObjectiveCompletionStepConditionType(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null ? descriptor.completionStepConditionType : string.Empty;
        }

        public static string GetObjectiveCompletionGameplaySignalId(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null ? descriptor.completionGameplaySignalId : string.Empty;
        }

        public static string GetObjectiveTargetEventKey(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null ? descriptor.targetEventKey : string.Empty;
        }

        public static bool ObjectiveRequiresAbsorbedArrow(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null && descriptor.requiresAbsorbedArrow;
        }

        public static string GetObjectiveRequiredArrowEventKey(string kind)
        {
            PromptIntentObjectiveCapabilityDescriptor descriptor = FindObjectiveCapability(kind);
            return descriptor != null ? descriptor.requiredArrowEventKey : string.Empty;
        }

        public static PromptIntentEffectCapabilityDescriptor[] GetEffectCapabilities()
        {
            return CloneEffectCapabilities(GetEffectCapabilitiesInternal());
        }

        public static string[] GetEffectSupportedTargetRoles(string kind)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            return descriptor != null ? CloneStrings(descriptor.supportedTargetRoles) : new string[0];
        }

        public static bool EffectAllowsAnyTargetRole(string kind)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            return descriptor != null && descriptor.allowAnyTargetRole;
        }

        public static bool TryGetEffectSystemActionAuthoringId(string kind, out string systemActionId)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            if (descriptor == null || string.IsNullOrEmpty(descriptor.systemActionId))
            {
                systemActionId = string.Empty;
                return false;
            }

            systemActionId = descriptor.systemActionId;
            return true;
        }

        public static bool EffectHasSemanticTag(string kind, string tag)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            return descriptor != null && ContainsValue(descriptor.semanticTags, tag);
        }

        public static bool IsCustomerSpawnEffectKind(string kind)
        {
            return EffectHasSemanticTag(kind, "customer_spawn");
        }

        public static bool IsCameraFocusEffectKind(string kind)
        {
            return TryGetEffectSystemActionAuthoringId(kind, out string systemActionId) &&
                   string.Equals(systemActionId, SystemActionIds.FOCUS_CAMERA_ON_TARGET, System.StringComparison.Ordinal);
        }

        public static string GetEffectRuntimeEventKey(string kind)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            return descriptor != null ? descriptor.runtimeEventKey : string.Empty;
        }

        public static bool EffectBuildsSceneActivationTarget(string kind)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            return descriptor != null && descriptor.buildsSceneActivationTarget;
        }

        public static bool EffectBuildsSystemActionTarget(string kind)
        {
            PromptIntentEffectCapabilityDescriptor descriptor = FindEffectCapability(kind);
            return descriptor != null && descriptor.buildsSystemActionTarget;
        }

        public static bool EffectBuildsActivationTarget(string kind)
        {
            return EffectBuildsSceneActivationTarget(kind) || EffectBuildsSystemActionTarget(kind);
        }

        public static string ResolveArrowTiming(string explicitTiming, bool arrowOnFocusArrival)
        {
            string normalizedTiming = Normalize(explicitTiming);
            if (!string.IsNullOrEmpty(normalizedTiming))
                return normalizedTiming;

            return arrowOnFocusArrival ? PromptIntentEffectTimingKinds.ARRIVAL : string.Empty;
        }

        private static PromptIntentTargetSurfaceRuleDescriptor FindTargetSurface(string role)
        {
            string normalized = Normalize(role);
            PromptIntentTargetSurfaceRuleDescriptor[] descriptors = GetTargetSurfacesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].role == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentGameplaySignalRuleDescriptor FindGameplaySignal(string signalId)
        {
            string normalized = Normalize(signalId);
            PromptIntentGameplaySignalRuleDescriptor[] descriptors = GetGameplaySignalsInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].signalId == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentSystemActionRuleDescriptor FindSystemAction(string id)
        {
            string normalized = Normalize(id);
            for (int i = 0; i < SYSTEM_ACTIONS.Length; i++)
            {
                if (SYSTEM_ACTIONS[i].authoringId == normalized)
                    return SYSTEM_ACTIONS[i];
            }

            return null;
        }

        private static PromptIntentConditionCapabilityDescriptor FindConditionCapability(string kind)
        {
            string normalized = Normalize(kind);
            PromptIntentConditionCapabilityDescriptor[] descriptors = GetConditionCapabilitiesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].kind == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentObjectiveCapabilityDescriptor FindObjectiveCapability(string kind)
        {
            string normalized = Normalize(kind);
            PromptIntentObjectiveCapabilityDescriptor[] descriptors = GetObjectiveCapabilitiesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].kind == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentEffectCapabilityDescriptor FindEffectCapability(string kind)
        {
            string normalized = Normalize(kind);
            PromptIntentEffectCapabilityDescriptor[] descriptors = GetEffectCapabilitiesInternal();
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].kind == normalized)
                    return descriptors[i];
            }

            return null;
        }

        private static PromptIntentTargetSurfaceRuleDescriptor[] GetTargetSurfacesInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeTargetSurfaces(TARGET_SURFACES);
        }

        private static PromptIntentGameplaySignalRuleDescriptor[] GetGameplaySignalsInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeGameplaySignals(GAMEPLAY_SIGNALS);
        }

        private static PromptIntentConditionCapabilityDescriptor[] GetConditionCapabilitiesInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeConditionCapabilities(CONDITION_CAPABILITIES);
        }

        private static PromptIntentObjectiveCapabilityDescriptor[] GetObjectiveCapabilitiesInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeObjectiveCapabilities(OBJECTIVE_CAPABILITIES);
        }

        private static PromptIntentEffectCapabilityDescriptor[] GetEffectCapabilitiesInternal()
        {
            return PromptIntentFeatureDescriptorBridge.MergeEffectCapabilities(EFFECT_CAPABILITIES);
        }

        private static PromptIntentTargetSurfaceRuleDescriptor[] CloneTargetSurfaceRules(PromptIntentTargetSurfaceRuleDescriptor[] values)
        {
            var copies = new PromptIntentTargetSurfaceRuleDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentTargetSurfaceRuleDescriptor
                {
                    role = values[i].role,
                    summary = values[i].summary,
                    supportedEventKeys = CloneStrings(values[i].supportedEventKeys),
                };
            }

            return copies;
        }

        private static PromptIntentGameplaySignalRuleDescriptor[] CloneGameplaySignalRules(PromptIntentGameplaySignalRuleDescriptor[] values)
        {
            var copies = new PromptIntentGameplaySignalRuleDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentGameplaySignalRuleDescriptor
                {
                    signalId = values[i].signalId,
                    summary = values[i].summary,
                    supportsTargetId = values[i].supportsTargetId,
                    requiresTargetId = values[i].requiresTargetId,
                    supportsItem = values[i].supportsItem,
                    requiresItem = values[i].requiresItem,
                    supportsCurrencyId = values[i].supportsCurrencyId,
                    requiresCurrencyId = values[i].requiresCurrencyId,
                    requiredTargetEventKey = values[i].requiredTargetEventKey,
                };
            }

            return copies;
        }

        private static PromptIntentSystemActionRuleDescriptor[] CloneSystemActionRules(PromptIntentSystemActionRuleDescriptor[] values)
        {
            var copies = new PromptIntentSystemActionRuleDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentSystemActionRuleDescriptor
                {
                    authoringId = values[i].authoringId,
                    summary = values[i].summary,
                    requiresTargetObjectId = values[i].requiresTargetObjectId,
                    defaultEventKey = values[i].defaultEventKey,
                };
            }

            return copies;
        }

        private static PromptIntentConditionCapabilityDescriptor[] CloneConditionCapabilities(PromptIntentConditionCapabilityDescriptor[] values)
        {
            var copies = new PromptIntentConditionCapabilityDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentConditionCapabilityDescriptor
                {
                    kind = values[i].kind,
                    summary = values[i].summary,
                    supportedTargetRoles = CloneStrings(values[i].supportedTargetRoles),
                    allowAnyTargetRole = values[i].allowAnyTargetRole,
                    gameplaySignalId = values[i].gameplaySignalId,
                    stepConditionType = values[i].stepConditionType,
                    reactiveConditionType = values[i].reactiveConditionType,
                };
            }

            return copies;
        }

        private static PromptIntentObjectiveCapabilityDescriptor[] CloneObjectiveCapabilities(PromptIntentObjectiveCapabilityDescriptor[] values)
        {
            var copies = new PromptIntentObjectiveCapabilityDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentObjectiveCapabilityDescriptor
                {
                    kind = values[i].kind,
                    summary = values[i].summary,
                    supportedTargetRoles = CloneStrings(values[i].supportedTargetRoles),
                    allowAnyTargetRole = values[i].allowAnyTargetRole,
                    completionStepConditionType = values[i].completionStepConditionType,
                    completionGameplaySignalId = values[i].completionGameplaySignalId,
                    targetEventKey = values[i].targetEventKey,
                    requiresAbsorbedArrow = values[i].requiresAbsorbedArrow,
                    requiredArrowEventKey = values[i].requiredArrowEventKey,
                };
            }

            return copies;
        }

        private static PromptIntentEffectCapabilityDescriptor[] CloneEffectCapabilities(PromptIntentEffectCapabilityDescriptor[] values)
        {
            var copies = new PromptIntentEffectCapabilityDescriptor[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copies[i] = new PromptIntentEffectCapabilityDescriptor
                {
                    kind = values[i].kind,
                    summary = values[i].summary,
                    semanticTags = CloneStrings(values[i].semanticTags),
                    supportedTargetRoles = CloneStrings(values[i].supportedTargetRoles),
                    allowAnyTargetRole = values[i].allowAnyTargetRole,
                    systemActionId = values[i].systemActionId,
                    runtimeEventKey = values[i].runtimeEventKey,
                    buildsSceneActivationTarget = values[i].buildsSceneActivationTarget,
                    buildsSystemActionTarget = values[i].buildsSystemActionTarget,
                };
            }

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

        private static string[] CloneStrings(string[] values)
        {
            if (values == null || values.Length == 0)
                return new string[0];

            var copies = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                copies[i] = values[i];
            return copies;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
