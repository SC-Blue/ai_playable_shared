using System;
using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public sealed class ScenarioModelValidationResult
    {
        public bool IsValid;
        public PlayableFailureCode FailureCode;
        public string Message;
        public List<string> Errors = new List<string>();
        public List<ValidationIssueRecord> Issues = new List<ValidationIssueRecord>();
    }

    public static class ScenarioModelValidator
    {
        public static ScenarioModelValidationResult Validate(PlayableScenarioModel model)
        {
            var result = new ScenarioModelValidationResult
            {
                FailureCode = PlayableFailureCode.None,
                Message = string.Empty,
            };

            if (model == null)
                return Fail(result, "PlayableScenarioModel이 null입니다.");

            if (model.objects == null)
            {
                Fail(result, "objects[]가 필요합니다.");
                return FinalizeResult(result);
            }

            if (model.objects.Length == 0)
            {
                Fail(result, "objects[]에는 최소 1개의 entry가 필요합니다.");
                return FinalizeResult(result);
            }

            if (model.stages == null)
            {
                Fail(result, "stages[]가 필요합니다.");
                return FinalizeResult(result);
            }

            if (model.stages.Length == 0)
            {
                Fail(result, "stages[]에는 최소 1개의 entry가 필요합니다.");
                return FinalizeResult(result);
            }

            ValidatePlayerOptions(model.playerOptions, result);
            ValidateContentSelections(model.contentSelections, result);
            ValidateObjects(model.objects, result);
            ValidateStages(model, result);
            return FinalizeResult(result);
        }

        private static void ValidateContentSelections(ContentSelectionDefinition[] selections, ScenarioModelValidationResult result)
        {
            var seenObjectIds = new HashSet<string>(StringComparer.Ordinal);
            ContentSelectionDefinition[] safeSelections = selections ?? new ContentSelectionDefinition[0];
            for (int i = 0; i < safeSelections.Length; i++)
            {
                ContentSelectionDefinition selection = safeSelections[i];
                string label = "contentSelections[" + i + "]";
                if (selection == null)
                {
                    Fail(result, label + "가 null입니다.");
                    continue;
                }

                string objectId = selection.objectId != null ? selection.objectId.Trim() : string.Empty;
                string designId = selection.designId != null ? selection.designId.Trim() : string.Empty;
                if (string.IsNullOrEmpty(objectId))
                    Fail(result, label + ".objectId는 비어 있을 수 없습니다.");
                else if (!seenObjectIds.Add(objectId))
                    Fail(result, "중복된 contentSelections objectId '" + objectId + "'입니다.");

                if (string.IsNullOrEmpty(designId))
                    Fail(result, label + ".designId는 비어 있을 수 없습니다.");
                else if (ContentSelectionRules.IsUnsetDesignId(designId))
                    Fail(result, label + ".designId는 '" + ContentSelectionRules.DESIGN_ID_NOT_SET + "'일 수 없습니다. 실제 카탈로그 design을 선택해야 합니다.");
            }

            for (int i = 0; i < ContentSelectionRules.REQUIRED_OBJECT_IDS.Length; i++)
            {
                string requiredObjectId = ContentSelectionRules.REQUIRED_OBJECT_IDS[i];
                if (!seenObjectIds.Contains(requiredObjectId))
                    Fail(result, "필수 UI content '" + requiredObjectId + "'에 대한 contentSelections entry가 필요합니다.");
            }
        }

        private static void ValidatePlayerOptions(PlayableScenarioPlayerOptions options, ScenarioModelValidationResult result)
        {
            if (options.itemStacker.maxCount < 0)
                Fail(result, "playerOptions.itemStacker.maxCount는 0 이상이어야 합니다.");
        }

        private static void ValidateFeatureOptions(
            string role,
            string objectId,
            PlayableScenarioFeatureOptions options,
            string label,
            ScenarioModelValidationResult result)
        {
            string normalizedRole = Normalize(role);
            string normalizedObjectId = Normalize(objectId);
            bool isCoreObject = IsCoreObjectRole(normalizedRole);

            string featureType = Normalize(options.featureType);
            string targetId = Normalize(options.targetId);
            string optionsJson = options.optionsJson != null ? options.optionsJson.Trim() : string.Empty;

            if (isCoreObject)
            {
                if (!string.IsNullOrEmpty(featureType) ||
                    !string.IsNullOrEmpty(targetId) ||
                    !string.IsNullOrEmpty(optionsJson))
                {
                    Fail(result, label + "는 core object role '" + normalizedRole + "'에서 사용할 수 없습니다.");
                }

                return;
            }

            if (string.IsNullOrEmpty(featureType))
                Fail(result, label + ".featureType이 필요합니다.");
            else if (!string.Equals(featureType, normalizedRole, StringComparison.Ordinal))
                Fail(result, label + ".featureType '" + featureType + "'는 object role '" + normalizedRole + "'와 같아야 합니다.");

            if (string.IsNullOrEmpty(targetId))
                Fail(result, label + ".targetId가 필요합니다.");
            else if (!string.Equals(targetId, normalizedObjectId, StringComparison.Ordinal))
                Fail(result, label + ".targetId '" + targetId + "'는 object id '" + normalizedObjectId + "'와 같아야 합니다.");

            if (string.IsNullOrEmpty(optionsJson))
                Fail(result, label + ".optionsJson이 필요합니다.");
            else if (!LooksLikeJsonObject(optionsJson))
                Fail(result, label + ".optionsJson은 JSON object 문자열이어야 합니다.");
        }

        private static void ValidateObjects(ScenarioModelObjectDefinition[] objects, ScenarioModelValidationResult result)
        {
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                if (value == null)
                    continue;

                string firstPresentingStageId = value.firstPresentingStageId != null ? value.firstPresentingStageId.Trim() : string.Empty;
                string firstActivatingStageId = value.firstActivatingStageId != null ? value.firstActivatingStageId.Trim() : string.Empty;
                if (!string.Equals(firstPresentingStageId, firstActivatingStageId, StringComparison.Ordinal))
                {
                    Fail(result, "objects[" + i + "]는 reveal_object와 activate_object를 서로 다른 stage에 나누어 visible but non-interactive lifecycle을 요구합니다.");
                }

                if (value.startsPresent && !value.startsActive)
                    Fail(result, "objects[" + i + "]는 visible but non-interactive 상태를 요구합니다. 이번 runtime contract에서는 지원하지 않습니다.");

                if (!value.startsPresent && value.startsActive)
                    Fail(result, "objects[" + i + "]는 present later인데 active at start로 표시되었습니다.");

                string label = "objects[" + i + "].featureOptions";
                ValidateFeatureOptions(value.role, value.id, value.featureOptions, label, result);
            }
        }

        private static bool LooksLikeJsonObject(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
        }

        private static bool IsCoreObjectRole(string role)
        {
            string normalized = Normalize(role);
            return string.Equals(normalized, PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal) ||
                   string.Equals(normalized, PromptIntentObjectRoles.UNLOCK_PAD, StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void ValidateWorldBounds(
            WorldBoundsDefinition bounds,
            string label,
            ScenarioModelValidationResult result)
        {
            if (bounds == null || !bounds.hasWorldBounds)
                return;

            if (bounds.worldWidth <= 0f)
                Fail(result, label + ".worldWidth는 0보다 커야 합니다.");
            if (bounds.worldDepth <= 0f)
                Fail(result, label + ".worldDepth는 0보다 커야 합니다.");
        }

        private static void ValidateStages(PlayableScenarioModel model, ScenarioModelValidationResult result)
        {
            ScenarioModelStageDefinition[] stages = model.stages ?? new ScenarioModelStageDefinition[0];
            Dictionary<string, ScenarioModelObjectDefinition> objectById = BuildObjectLookup(model.objects);
            for (int i = 0; i < stages.Length; i++)
            {
                ScenarioModelStageDefinition stage = stages[i];
                if (stage == null)
                    continue;

                ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
                ScenarioModelEffectDefinition[] entryEffects = stage.entryEffects ?? new ScenarioModelEffectDefinition[0];
                ScenarioModelEffectDefinition[] completionEffects = stage.completionEffects ?? new ScenarioModelEffectDefinition[0];

                int absorbableCount = 0;
                bool hasAbsorbedArrow = false;
                for (int j = 0; j < objectives.Length; j++)
                {
                    ScenarioModelObjectiveDefinition objective = objectives[j];
                    if (objective == null)
                        continue;

                    if (PromptIntentObjectiveKinds.CanAbsorbArrow(objective.kind))
                        absorbableCount++;
                    if (objective.absorbsArrow)
                        hasAbsorbedArrow = true;

                    ValidateObjectiveContract(objective, objectById, "stages[" + i + "].objectives[" + j + "]", result);
                }

                ValidateEffectContract(entryEffects, objectById, "stages[" + i + "].entryEffects", result);
                ValidateEffectContract(completionEffects, objectById, "stages[" + i + "].completionEffects", result);

                bool hasResidualArrowEffect = HasEffect(entryEffects, PromptIntentEffectKinds.SHOW_ARROW);
                if (hasResidualArrowEffect)
                    Fail(result, "stages[" + i + "]는 arrow를 흡수할 수 있는 beat가 없습니다.");

                if (hasAbsorbedArrow && absorbableCount == 0)
                    Fail(result, "stages[" + i + "]는 absorbable objective 없이 arrow 흡수를 요청했습니다.");

                bool hasBlockingBeat = HasFocusCamera(entryEffects) || objectives.Length > 0;
                bool hasEntryArtifact = HasLowerableReactiveEntryEffect(entryEffects);
                bool hasCompletionArtifact = completionEffects.Length > 0;
                if (!hasBlockingBeat && !hasEntryArtifact && !hasCompletionArtifact)
                    Fail(result, "stages[" + i + "]는 executable artifact를 하나도 생성하지 못하는 empty stage입니다.");

                if (!hasBlockingBeat && completionEffects.Length > 0)
                    Fail(result, "stages[" + i + "]는 completion trigger를 만들 beat가 없는데 onComplete effect를 가지고 있습니다.");

                if (HasFocusCamera(entryEffects) &&
                    objectives.Length == 0 &&
                    completionEffects.Length == 0 &&
                    !HasEffect(entryEffects, PromptIntentEffectKinds.SHOW_GUIDE_ARROW))
                {
                    Fail(result, "stages[" + i + "]는 focus_camera만 있고 이후 progression을 만들 objective/effect가 없습니다.");
                }

                int unlockObjectiveCount = CountObjectives(objectives, PromptIntentObjectiveKinds.UNLOCK_OBJECT);
                if (unlockObjectiveCount > 1)
                    Fail(result, "stages[" + i + "]는 unlock_object objective를 1개까지만 지원합니다.");

                ValidateShowArrowTiming(stage, i, result);
            }
        }

        private static void ValidateFeatureEndpointTargetRole(
            string objectId,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            Func<string, bool> isSupportedRole,
            string label,
            string expectedRoles,
            ScenarioModelValidationResult result)
        {
            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
                return;

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            string role = target.role != null ? target.role.Trim() : string.Empty;
            if (isSupportedRole == null || !isSupportedRole(role))
                Fail(result, label + " '" + normalizedObjectId + "'는 " + expectedRoles + "여야 합니다.");
        }

        private static Dictionary<string, ScenarioModelObjectDefinition> BuildObjectLookup(ScenarioModelObjectDefinition[] objects)
        {
            var objectById = new Dictionary<string, ScenarioModelObjectDefinition>(StringComparer.Ordinal);
            ScenarioModelObjectDefinition[] safeObjects = objects ?? new ScenarioModelObjectDefinition[0];
            for (int i = 0; i < safeObjects.Length; i++)
            {
                ScenarioModelObjectDefinition value = safeObjects[i];
                string objectId = value != null && value.id != null ? value.id.Trim() : string.Empty;
                if (string.IsNullOrEmpty(objectId) || objectById.ContainsKey(objectId))
                    continue;

                objectById.Add(objectId, value);
            }

            return objectById;
        }

        private static void ValidateScenarioObjectReference(
            string objectId,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string expectedRole,
            string label,
            ScenarioModelValidationResult result,
            bool allowAnyRole = false)
        {
            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            if (allowAnyRole)
                return;

            string normalizedExpectedRole = expectedRole != null ? expectedRole.Trim() : string.Empty;
            string actualRole = target.role != null ? target.role.Trim() : string.Empty;
            if (!string.Equals(actualRole, normalizedExpectedRole, StringComparison.Ordinal))
                Fail(result, label + " '" + normalizedObjectId + "'는 role '" + normalizedExpectedRole + "'이어야 합니다.");
        }

        private static void ValidateScenarioObjectReferenceByRolePolicy(
            string objectId,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string[] supportedRoles,
            bool allowAnyRole,
            string label,
            ScenarioModelValidationResult result)
        {
            string[] safeSupportedRoles = supportedRoles ?? new string[0];
            if (allowAnyRole || safeSupportedRoles.Length == 0)
            {
                ValidateScenarioObjectReference(objectId, objectById, string.Empty, label, result, allowAnyRole: true);
                return;
            }

            if (safeSupportedRoles.Length == 1)
            {
                ValidateScenarioObjectReference(objectId, objectById, safeSupportedRoles[0], label, result);
                return;
            }

            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId))
            {
                Fail(result, label + "가 필요합니다.");
                return;
            }

            if (objectById == null || !objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition target) || target == null)
            {
                Fail(result, label + " '" + normalizedObjectId + "'가 objects[]에 존재하지 않습니다.");
                return;
            }

            string actualRole = target.role != null ? target.role.Trim() : string.Empty;
            for (int i = 0; i < safeSupportedRoles.Length; i++)
            {
                if (string.Equals(actualRole, safeSupportedRoles[i] != null ? safeSupportedRoles[i].Trim() : string.Empty, StringComparison.Ordinal))
                    return;
            }

            Fail(result, label + " '" + normalizedObjectId + "'는 role '" + JoinRoles(safeSupportedRoles) + "' 중 하나여야 합니다.");
        }

        private static void ValidateObjectiveContract(
            ScenarioModelObjectiveDefinition objective,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string label,
            ScenarioModelValidationResult result)
        {
            if (objective == null)
                return;

            string kind = objective.kind != null ? objective.kind.Trim() : string.Empty;
            string targetEventKey = PromptIntentCapabilityRegistry.GetObjectiveTargetEventKey(kind);
            if (!string.IsNullOrWhiteSpace(targetEventKey) && !string.IsNullOrWhiteSpace(objective.targetObjectId))
            {
                ValidateTargetEventKeyCapability(objective.targetObjectId, targetEventKey, objectById, label + ".targetObjectId", kind, result);
            }

            if (PromptIntentContractRegistry.ObjectiveRequiresItem(kind) && !ItemRefUtility.IsValid(objective.item))
                Fail(result, label + ".item이 필요합니다.");

            if (PromptIntentContractRegistry.ObjectiveRequiresInputItem(kind) && !ItemRefUtility.IsValid(objective.inputItem))
                Fail(result, label + ".inputItem이 필요합니다.");

            if (PromptIntentCapabilityRegistry.ObjectiveRequiresAbsorbedArrow(kind) && !objective.absorbsArrow)
            {
                string missingArrowEventKey = PromptIntentCapabilityRegistry.GetObjectiveRequiredArrowEventKey(kind);
                Fail(result, label + "에는 explicit show_arrow(eventKey='" + missingArrowEventKey + "')가 필요합니다.");
                return;
            }

            if (!objective.absorbsArrow)
                return;

            string arrowTargetObjectId = !string.IsNullOrWhiteSpace(objective.arrowTargetObjectId)
                ? objective.arrowTargetObjectId.Trim()
                : (objective.targetObjectId != null ? objective.targetObjectId.Trim() : string.Empty);
            string arrowEventKey = objective.arrowEventKey != null ? objective.arrowEventKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(arrowEventKey))
            {
                Fail(result, label + ".arrowEventKey가 필요합니다.");
                return;
            }

            string requiredArrowEventKey = PromptIntentCapabilityRegistry.GetObjectiveRequiredArrowEventKey(kind);
            if (!string.IsNullOrEmpty(requiredArrowEventKey) &&
                !string.Equals(arrowEventKey, requiredArrowEventKey, StringComparison.Ordinal))
            {
                Fail(result, label + "의 absorbed arrow는 eventKey '" + requiredArrowEventKey + "'이어야 합니다.");
            }

            ValidateTargetEventKeyCapability(arrowTargetObjectId, arrowEventKey, objectById, label, "show_arrow", result);
        }

        private static void ValidateEffectContract(
            ScenarioModelEffectDefinition[] effects,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string label,
            ScenarioModelValidationResult result)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                ScenarioModelEffectDefinition effect = safeEffects[i];
                if (effect == null ||
                    !string.Equals(effect.kind != null ? effect.kind.Trim() : string.Empty, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal))
                {
                    continue;
                }

                string eventKey = effect.eventKey != null ? effect.eventKey.Trim() : string.Empty;
                if (string.IsNullOrEmpty(eventKey))
                {
                    Fail(result, label + "[" + i + "].eventKey가 필요합니다.");
                    continue;
                }

                ValidateTargetEventKeyCapability(effect.targetObjectId, eventKey, objectById, label + "[" + i + "]", "show_arrow", result);
            }
        }

        private static void ValidateTargetEventKeyCapability(
            string objectId,
            string eventKey,
            Dictionary<string, ScenarioModelObjectDefinition> objectById,
            string label,
            string usageLabel,
            ScenarioModelValidationResult result)
        {
            string normalizedObjectId = objectId != null ? objectId.Trim() : string.Empty;
            string normalizedEventKey = eventKey != null ? eventKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedObjectId) || string.IsNullOrEmpty(normalizedEventKey))
                return;

            if (!objectById.TryGetValue(normalizedObjectId, out ScenarioModelObjectDefinition value) || value == null)
                return;

            string role = value.role != null ? value.role.Trim() : string.Empty;
            if (PromptIntentCapabilityRegistry.RoleSupportsTargetEventKey(role, normalizedEventKey))
                return;

            Fail(result, label + "는 object '" + normalizedObjectId + "'(role='" + role + "')에서 " + usageLabel + " eventKey '" + normalizedEventKey + "'를 지원하지 않습니다." + BuildTargetEventKeyGuidance(normalizedObjectId, role, usageLabel));
        }

        private static bool HasFocusCamera(ScenarioModelEffectDefinition[] effects)
        {
            return HasEffect(effects, PromptIntentEffectKinds.FOCUS_CAMERA);
        }

        private static bool HasLowerableReactiveEntryEffect(ScenarioModelEffectDefinition[] effects)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                string kind = safeEffects[i] != null ? safeEffects[i].kind : string.Empty;
                if (string.Equals(kind, PromptIntentEffectKinds.FOCUS_CAMERA, StringComparison.Ordinal))
                    continue;
                if (string.Equals(kind, PromptIntentEffectKinds.SHOW_ARROW, StringComparison.Ordinal))
                    continue;
                return true;
            }

            return false;
        }

        private static void ValidateShowArrowTiming(
            ScenarioModelStageDefinition stage,
            int stageIndex,
            ScenarioModelValidationResult result)
        {
            if (stage == null)
                return;

            ScenarioModelObjectiveDefinition[] objectives = stage.objectives ?? new ScenarioModelObjectiveDefinition[0];
            bool hasArrivalArrow = false;
            for (int i = 0; i < objectives.Length; i++)
            {
                ScenarioModelObjectiveDefinition objective = objectives[i];
                if (objective == null)
                    continue;

                string arrowTiming = PromptIntentCapabilityRegistry.ResolveArrowTiming(objective.arrowTiming, objective.arrowOnFocusArrival);
                if (!string.IsNullOrEmpty(arrowTiming) && !PromptIntentEffectTimingKinds.IsSupported(arrowTiming))
                {
                    Fail(result, "stages[" + stageIndex + "].objectives[" + i + "].arrowTiming '" + arrowTiming + "'은(는) 지원되지 않습니다.");
                    continue;
                }

                if (!string.IsNullOrEmpty(arrowTiming) && !objective.absorbsArrow)
                {
                    Fail(result, "stages[" + stageIndex + "].objectives[" + i + "]는 absorbed arrow 없이 arrow timing을 가질 수 없습니다.");
                    continue;
                }

                if (objective.absorbsArrow &&
                    (string.Equals(arrowTiming, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal) ||
                     string.Equals(arrowTiming, PromptIntentEffectTimingKinds.COMPLETED, StringComparison.Ordinal)))
                {
                    if (!HasFocusCamera(stage.entryEffects))
                    {
                        Fail(result, "stages[" + stageIndex + "]의 explicit show_arrow timing은 same-stage entry focus_camera가 있을 때만 사용할 수 있습니다.");
                        continue;
                    }
                }

                if (objective.absorbsArrow &&
                    string.Equals(arrowTiming, PromptIntentEffectTimingKinds.ARRIVAL, StringComparison.Ordinal))
                    hasArrivalArrow = true;
            }

            if (hasArrivalArrow && !HasFocusCamera(stage.entryEffects))
                Fail(result, "stages[" + stageIndex + "]의 show_arrow arrival timing은 same-stage entry focus_camera가 있을 때만 사용할 수 있습니다.");
        }

        private static int CountObjectives(ScenarioModelObjectiveDefinition[] objectives, string kind)
        {
            int count = 0;
            ScenarioModelObjectiveDefinition[] safeObjectives = objectives ?? new ScenarioModelObjectiveDefinition[0];
            for (int i = 0; i < safeObjectives.Length; i++)
            {
                if (string.Equals(safeObjectives[i] != null ? safeObjectives[i].kind : string.Empty, kind, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static bool HasEffect(ScenarioModelEffectDefinition[] effects, string kind)
        {
            ScenarioModelEffectDefinition[] safeEffects = effects ?? new ScenarioModelEffectDefinition[0];
            for (int i = 0; i < safeEffects.Length; i++)
            {
                if (string.Equals(safeEffects[i] != null ? safeEffects[i].kind : string.Empty, kind, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static ScenarioModelValidationResult FinalizeResult(ScenarioModelValidationResult result)
        {
            result.IsValid = result.Errors.Count == 0;
            if (result.IsValid)
            {
                result.FailureCode = PlayableFailureCode.None;
                result.Message = "유효합니다.";
            }
            else
            {
                if (result.FailureCode == PlayableFailureCode.None)
                    result.FailureCode = PlayableFailureCode.ModelValidationFailed;
                result.Message = result.Errors[0];
            }

            return result;
        }

        private static ScenarioModelValidationResult Fail(ScenarioModelValidationResult result, string message)
        {
            return Fail(result, new ValidationIssueRecord(
                ValidationRuleId.SCENARIO_MODEL_GENERIC,
                ValidationSeverity.Blocker,
                message,
                "ScenarioModel"));
        }

        private static ScenarioModelValidationResult Fail(ScenarioModelValidationResult result, ValidationIssueRecord issue)
        {
            if (result == null || issue == null)
                return result;

            if (result.FailureCode == PlayableFailureCode.None)
                result.FailureCode = PlayableFailureCode.ModelValidationFailed;
            result.Errors.Add(issue.message ?? string.Empty);
            result.Issues ??= new List<ValidationIssueRecord>();
            result.Issues.Add(issue);
            return result;
        }

        private static string JoinRoles(string[] roles)
        {
            string[] safeRoles = roles ?? new string[0];
            if (safeRoles.Length == 0)
                return string.Empty;

            string text = safeRoles[0] != null ? safeRoles[0].Trim() : string.Empty;
            for (int i = 1; i < safeRoles.Length; i++)
                text += ", " + (safeRoles[i] != null ? safeRoles[i].Trim() : string.Empty);
            return text;
        }

        private static string BuildTargetEventKeyGuidance(string objectId, string role, string usageLabel)
        {
            string[] supportedEventKeys = PromptIntentCapabilityRegistry.GetSupportedFlowTargetEventKeys(role);
            if (supportedEventKeys == null || supportedEventKeys.Length == 0)
                return " -> 수정 가이드: 현재 계약에서 role '" + role + "'의 허용 eventKey를 확인하세요.";

            var segments = new List<string>
            {
                "현재 model objects[] 기준 object '" + objectId + "'의 role은 '" + role + "'입니다",
                "제작 계약상 role '" + role + "'의 허용 eventKey: [" + string.Join(", ", supportedEventKeys) + "]"
            };

            if (supportedEventKeys.Length == 1)
                segments.Add("추천 수정: " + usageLabel + ".eventKey를 '" + supportedEventKeys[0] + "'로 바꾸세요");

            return " -> 수정 가이드: " + string.Join("; ", segments) + ".";
        }
    }
}
