using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.Generation.Editor.Compile;
using Supercent.PlayableAI.Generation.Editor.Pipeline;
using Supercent.PlayableAI.Generation.Editor.Validation;

namespace PlayableAI.AuthoringCore
{
    public sealed class DraftLayoutPreflightDiagnostic
    {
        public string severity = "blocker";
        public string stage = string.Empty;
        public string ruleCode = string.Empty;
        public string message = string.Empty;
        public string[] objectIds = new string[0];
        public string[] suggestedFixes = new string[0];
        public string fixability = "manual";
        public bool autoFixEligible;
    }

    public sealed class DraftLayoutPreflightValidationResult
    {
        public bool IsValid;
        public string Stage = string.Empty;
        public string Message = string.Empty;
        public string[] Blockers = new string[0];
        public string[] Warnings = new string[0];
        public string validationPath = string.Empty;
        public string[] completedStages = new string[0];
        public string terminalStageKind = string.Empty;
        public string recommendedAuthority = string.Empty;
        public DraftLayoutPreflightDiagnostic[] diagnostics = new DraftLayoutPreflightDiagnostic[0];
    }

    public static class DraftLayoutPreflightValidator
    {
        private static readonly Regex QUOTED_ID_REGEX = new("'([^']+)'", RegexOptions.Compiled);

        public static DraftLayoutPreflightValidationResult Validate(
            PlayablePromptIntent intent,
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            bool includeBasicContract = true)
        {
            var diagnostics = new List<DraftLayoutPreflightDiagnostic>();

            LayoutSpecDocument effectiveLayoutSpec = BuildEffectiveLayoutSpec(layoutSpec);

            if (includeBasicContract)
                ValidateBasicContract(intent, effectiveLayoutSpec, diagnostics);

            var result = new DraftLayoutPreflightValidationResult
            {
                Stage = "DraftLayout",
            };

            if (!HasBlocker(diagnostics))
                RunObjectPipeline(intent, effectiveLayoutSpec, catalog, result, diagnostics);

            FinalizeResult(result, diagnostics);
            return result;
        }

        public static LayoutSpecDocument BuildEffectiveLayoutSpec(
            LayoutSpecDocument layoutSpec)
        {
            LayoutSpecDocument safeLayoutSpec = layoutSpec ?? new LayoutSpecDocument();
            LayoutSpecEnvironmentEntry[] environmentEntries = safeLayoutSpec.environment ?? new LayoutSpecEnvironmentEntry[0];
            environmentEntries = environmentEntries
                .Where(static entry => entry != null && !string.Equals(entry.objectId, "floor", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return new LayoutSpecDocument
            {
                floorBounds = safeLayoutSpec.floorBounds ?? new LayoutSpecFloorBounds(),
                placements = safeLayoutSpec.placements ?? new LayoutSpecPlacementEntry[0],
                playerStart = safeLayoutSpec.playerStart ?? new LayoutSpecPlayerStartEntry(),
                environment = environmentEntries,
                customerPaths = safeLayoutSpec.customerPaths ?? new LayoutSpecCustomerPathEntry[0],
                sourceImages = safeLayoutSpec.sourceImages ?? new LayoutSpecSourceImageEntry[0],
            };
        }

        private static void FinalizeResult(
            DraftLayoutPreflightValidationResult result,
            List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            DraftLayoutPreflightDiagnostic[] filteredDiagnostics = FilterOverlapDiagnostics(diagnostics);
            result.diagnostics = filteredDiagnostics;
            result.Blockers = BuildDisplayMessages(result.diagnostics, "blocker");
            result.Warnings = BuildDisplayMessages(result.diagnostics, "warning");
            result.IsValid = result.Blockers.Length == 0;
            if (string.IsNullOrWhiteSpace(result.Message))
            {
                result.Message = result.IsValid
                    ? "draft preflight 검사를 완료했습니다."
                    : "draft preflight blocker가 있습니다.";
            }
        }

        private static string[] BuildDisplayMessages(
            DraftLayoutPreflightDiagnostic[] diagnostics,
            string severity)
        {
            var messages = new List<string>();
            DraftLayoutPreflightDiagnostic[] safeDiagnostics = diagnostics ?? new DraftLayoutPreflightDiagnostic[0];
            for (int i = 0; i < safeDiagnostics.Length; i++)
            {
                DraftLayoutPreflightDiagnostic diagnostic = safeDiagnostics[i];
                if (!string.Equals(Normalize(diagnostic.severity), Normalize(severity), StringComparison.Ordinal))
                    continue;

                AddUnique(messages, FormatStageMessage(diagnostic.stage, diagnostic.message));
            }

            return messages.ToArray();
        }

        private static bool HasBlocker(List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return false;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(Normalize(diagnostics[i].severity), "blocker", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void RunObjectPipeline(
            PlayablePromptIntent intent,
            LayoutSpecDocument layoutSpec,
            PlayableObjectCatalog catalog,
            DraftLayoutPreflightValidationResult result,
            List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            AuthoringCoreRunResult runResult = AuthoringCoreRunner.Run(
                intent,
                catalog,
                AuthoringCoreExecutionProfile.GeneratePlayable,
                layoutSpec);
            if (!runResult.IsSuccess)
            {
                ApplyFailure(result, runResult.Stage, runResult.Message, runResult.Errors, diagnostics);
                return;
            }

            if (runResult.Warnings != null)
            {
                for (int i = 0; i < runResult.Warnings.Count; i++)
                    AddDiagnostic(diagnostics, "warning", GenerationStageNames.COMPILED_PLAN_VALIDATION, runResult.Warnings[i]);
            }

            result.Stage = GenerationStageNames.COMPILED_PLAN_VALIDATION;
            result.Message = "draft preflight 검사를 완료했습니다.";
        }

        private static void ApplyFailure(
            DraftLayoutPreflightValidationResult result,
            string stage,
            string message,
            IReadOnlyList<string> errors,
            List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            result.Stage = stage ?? string.Empty;
            result.Message = message ?? string.Empty;

            if (errors != null)
            {
                for (int i = 0; i < errors.Count; i++)
                    AddDiagnostic(diagnostics, "blocker", stage, errors[i]);
            }

            if ((errors == null || errors.Count == 0) && !string.IsNullOrWhiteSpace(message))
                AddDiagnostic(diagnostics, "blocker", stage, message);
        }

        private static void ValidateBasicContract(
            PlayablePromptIntent intent,
            LayoutSpecDocument layoutSpec,
            List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return;

            if (intent == null)
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "intent가 null입니다.");
                return;
            }

            if (layoutSpec == null)
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec이 null입니다.");
                return;
            }

            Dictionary<string, PromptIntentObjectDefinition> objects = BuildObjectLookup(intent);
            var placementByObjectId = new Dictionary<string, LayoutSpecPlacementEntry>(StringComparer.Ordinal);
            LayoutSpecPlacementEntry[] placements = layoutSpec.placements ?? new LayoutSpecPlacementEntry[0];
            for (int i = 0; i < placements.Length; i++)
            {
                LayoutSpecPlacementEntry entry = placements[i];
                string objectId = Normalize(entry != null ? entry.objectId : string.Empty);
                if (string.IsNullOrEmpty(objectId))
                {
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.placements[" + i + "].objectId가 비어 있습니다.");
                    continue;
                }

                if (placementByObjectId.ContainsKey(objectId))
                {
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.placements에 중복 objectId가 있습니다: '" + objectId + "'.");
                    continue;
                }

                if (!objects.TryGetValue(objectId, out PromptIntentObjectDefinition objectDefinition))
                {
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.placements[" + i + "].objectId '" + objectId + "'에 대응하는 intent object가 없습니다.");
                    continue;
                }

                if (string.Equals(Normalize(objectDefinition.role), PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "player object '" + objectId + "'는 placements가 아니라 playerStart에 작성해야 합니다.");
                    continue;
                }

                placementByObjectId.Add(objectId, entry);
            }

            foreach (KeyValuePair<string, PromptIntentObjectDefinition> pair in objects)
            {
                if (string.Equals(Normalize(pair.Value != null ? pair.Value.role : string.Empty), PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
                    continue;

                if (!placementByObjectId.ContainsKey(pair.Key))
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec에 intent object '" + pair.Key + "'의 placement가 없습니다.");
            }

            LayoutSpecPlayerStartEntry playerStart = layoutSpec.playerStart ?? new LayoutSpecPlayerStartEntry();
            string playerObjectId = Normalize(playerStart.objectId);
            if (string.IsNullOrEmpty(playerObjectId))
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.playerStart.objectId가 필요합니다.");
            }
            else if (!objects.TryGetValue(playerObjectId, out PromptIntentObjectDefinition playerDefinition))
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.playerStart.objectId '" + playerObjectId + "'에 대응하는 intent object가 없습니다.");
            }
            else if (!string.Equals(Normalize(playerDefinition.role), PromptIntentObjectRoles.PLAYER, StringComparison.Ordinal))
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.playerStart.objectId '" + playerObjectId + "'는 player role object여야 합니다.");
            }

            LayoutSpecCustomerPathEntry[] customerPaths = layoutSpec.customerPaths ?? new LayoutSpecCustomerPathEntry[0];
            for (int i = 0; i < customerPaths.Length; i++)
            {
                LayoutSpecCustomerPathEntry entry = customerPaths[i];
                string targetId = Normalize(entry != null ? entry.targetId : string.Empty);
                if (string.IsNullOrEmpty(targetId))
                {
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.customerPaths[" + i + "].targetId가 비어 있습니다.");
                    continue;
                }

                string objectId = ResolveCustomerPathObjectId(targetId);
                if (!objects.ContainsKey(objectId))
                    AddDiagnostic(diagnostics, "blocker", "DraftLayout", "layoutSpec.customerPaths[" + i + "].targetId '" + targetId + "'에 대응하는 intent object가 없습니다.");
            }

            for (int i = 0; i < placements.Length; i++)
            {
                LayoutSpecPlacementEntry placement = placements[i];
                string objectId = Normalize(placement != null ? placement.objectId : string.Empty);
                if (!objects.TryGetValue(objectId, out PromptIntentObjectDefinition objectDefinition))
                    continue;

                ValidateFeatureLayoutPayload(objectId, objectDefinition, placement, diagnostics);
            }
        }

        private static void ValidateFeatureLayoutPayload(
            string objectId,
            PromptIntentObjectDefinition objectDefinition,
            LayoutSpecPlacementEntry placement,
            List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            if (objectDefinition == null || string.IsNullOrEmpty(Normalize(objectDefinition.featureOptions.featureType)))
                return;

            FeatureJsonPayload featureLayout = placement != null ? placement.featureLayout : null;
            if (featureLayout == null)
                return;

            string expectedFeatureType = Normalize(objectDefinition.featureOptions.featureType);
            string layoutFeatureType = Normalize(featureLayout.featureType);
            if (!string.IsNullOrEmpty(layoutFeatureType) &&
                !string.Equals(layoutFeatureType, expectedFeatureType, StringComparison.Ordinal))
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "placement '" + objectId + "' featureLayout.featureType은 '" + expectedFeatureType + "'여야 합니다.");
            }

            string layoutTargetId = Normalize(featureLayout.targetId);
            if (!string.IsNullOrEmpty(layoutTargetId) &&
                !string.Equals(layoutTargetId, objectId, StringComparison.Ordinal))
            {
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "placement '" + objectId + "' featureLayout.targetId는 placement objectId와 같아야 합니다.");
            }

            if (!string.IsNullOrWhiteSpace(featureLayout.json) && !LooksLikeJsonObject(featureLayout.json))
                AddDiagnostic(diagnostics, "blocker", "DraftLayout", "placement '" + objectId + "' featureLayout.json은 JSON object 문자열이어야 합니다.");
        }

        private static bool LooksLikeJsonObject(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
        }

        private static void AddDiagnostic(
            List<DraftLayoutPreflightDiagnostic> diagnostics,
            string severity,
            string stage,
            string message)
        {
            if (diagnostics == null)
                return;

            DraftLayoutPreflightDiagnostic diagnostic = CreateDiagnostic(severity, stage, message);
            string key = Normalize(diagnostic.severity) + "|" + Normalize(diagnostic.stage) + "|" + Normalize(diagnostic.message);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                DraftLayoutPreflightDiagnostic existing = diagnostics[i];
                string existingKey = Normalize(existing.severity) + "|" + Normalize(existing.stage) + "|" + Normalize(existing.message);
                if (string.Equals(existingKey, key, StringComparison.Ordinal))
                    return;
            }

            diagnostics.Add(diagnostic);
        }

        private static DraftLayoutPreflightDiagnostic CreateDiagnostic(
            string severity,
            string stage,
            string message)
        {
            string normalizedMessage = Normalize(message);
            string ruleCode = InferRuleCode(normalizedMessage);
            string[] objectIds = ExtractObjectIds(normalizedMessage);
            string[] suggestedFixes = BuildSuggestedFixes(ruleCode, normalizedMessage, objectIds);
            string fixability = ResolveFixability(ruleCode, normalizedMessage);
            return new DraftLayoutPreflightDiagnostic
            {
                severity = string.IsNullOrWhiteSpace(severity) ? "blocker" : severity.Trim(),
                stage = Normalize(stage),
                ruleCode = ruleCode,
                message = normalizedMessage,
                objectIds = objectIds,
                suggestedFixes = suggestedFixes,
                fixability = fixability,
                autoFixEligible = string.Equals(fixability, "automatic_candidate", StringComparison.Ordinal),
            };
        }

        private static string InferRuleCode(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "Unknown";

            int colonIndex = message.IndexOf(':');
            if (colonIndex > 0)
            {
                string prefixedCode = message.Substring(0, colonIndex).Trim();
                if (prefixedCode.All(static value => char.IsLetter(value)))
                    return prefixedCode;
            }

            if (message.IndexOf("exitWaypoints", StringComparison.Ordinal) >= 0)
                return "MissingExitWaypoints";
            if (message.IndexOf("entryWaypoints", StringComparison.Ordinal) >= 0)
                return "MissingEntryWaypoints";
            if (message.IndexOf("queuePoints", StringComparison.Ordinal) >= 0)
                return "MissingQueuePoints";
            if (message.IndexOf("spawnPoint", StringComparison.Ordinal) >= 0)
                return "MissingSpawnPoint";
            if (message.IndexOf("leavePoint", StringComparison.Ordinal) >= 0)
                return "MissingLeavePoint";
            if (message.IndexOf("targetId", StringComparison.Ordinal) >= 0 && message.IndexOf("customerPaths", StringComparison.Ordinal) >= 0)
                return "CustomerPathTargetCoverage";
            if (message.IndexOf("featureLayout", StringComparison.Ordinal) >= 0 &&
                message.IndexOf("필요", StringComparison.Ordinal) >= 0)
                return "MissingFeatureLayoutPayload";
            if (message.IndexOf("featureLayout", StringComparison.Ordinal) >= 0 &&
                message.IndexOf("유효하지", StringComparison.Ordinal) >= 0)
                return "InvalidFeatureLayoutPayload";
            if (message.IndexOf("environment 점유 셀과 충돌", StringComparison.Ordinal) >= 0)
                return "EnvironmentOccupiedCellConflict";
            if (message.IndexOf("layout 경계를 벗어났습니다", StringComparison.Ordinal) >= 0)
                return "LayoutBoundsContainment";
            if (message.IndexOf("playerStart.objectId", StringComparison.Ordinal) >= 0)
                return "MissingPlayerStart";
            if (message.IndexOf("placements에 중복 objectId", StringComparison.Ordinal) >= 0)
                return "DuplicatePlacementObjectId";
            if (message.IndexOf("placement가 없습니다", StringComparison.Ordinal) >= 0)
                return "MissingPlacementCoverage";

            return "DraftPreflight";
        }

        private static string[] ExtractObjectIds(string message)
        {
            var objectIds = new List<string>();
            if (string.IsNullOrEmpty(message))
                return objectIds.ToArray();

            MatchCollection matches = QUOTED_ID_REGEX.Matches(message);
            for (int i = 0; i < matches.Count; i++)
            {
                string value = Normalize(matches[i].Groups[1].Value);
                if (string.IsNullOrEmpty(value))
                    continue;
                AddUnique(objectIds, value);
            }

            return objectIds.ToArray();
        }

        private static string[] BuildSuggestedFixes(string ruleCode, string message, string[] objectIds)
        {
            var fixes = new List<string>();
            string[] safeObjectIds = objectIds ?? new string[0];
            string firstObjectId = safeObjectIds.Length > 0 ? Normalize(safeObjectIds[0]) : string.Empty;

            if (string.Equals(ruleCode, "MissingExitWaypoints", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.customerPaths[*].exitWaypoints에 최소 1개의 waypoint를 추가해주세요."
                    : "draft_layout.customerPaths[targetId=" + firstObjectId + "].exitWaypoints에 최소 1개의 waypoint를 추가해주세요.");
            if (string.Equals(ruleCode, "MissingEntryWaypoints", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.customerPaths[*].entryWaypoints에 최소 1개의 waypoint를 추가해주세요."
                    : "draft_layout.customerPaths[targetId=" + firstObjectId + "].entryWaypoints에 최소 1개의 waypoint를 추가해주세요.");
            if (string.Equals(ruleCode, "MissingQueuePoints", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.customerPaths[*].queuePoints를 최소 1개 이상 추가해주세요."
                    : "draft_layout.customerPaths[targetId=" + firstObjectId + "].queuePoints를 최소 1개 이상 추가해주세요.");
            if (string.Equals(ruleCode, "MissingSpawnPoint", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.customerPaths[*].spawnPoint를 명시해주세요."
                    : "draft_layout.customerPaths[targetId=" + firstObjectId + "].spawnPoint를 명시해주세요.");
            if (string.Equals(ruleCode, "MissingLeavePoint", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.customerPaths[*].leavePoint를 명시해주세요."
                    : "draft_layout.customerPaths[targetId=" + firstObjectId + "].leavePoint를 명시해주세요.");
            if (string.Equals(ruleCode, "CustomerPathTargetCoverage", StringComparison.Ordinal))
                AddUnique(fixes, safeObjectIds.Length == 0
                    ? "customer-facing feature마다 draft_layout.customerPaths[targetId=<targetId>]를 정확히 1개씩 작성해주세요."
                    : "customer-facing feature마다 customer path를 정확히 1개씩 작성해주세요. 현재 누락/중복 확인 대상: " + string.Join(", ", safeObjectIds) + ".");
            if (string.Equals(ruleCode, "MissingFeatureLayoutPayload", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.placements[*].featureLayout.json에 descriptor가 요구하는 layout payload를 작성해주세요."
                    : "draft_layout.placements[objectId=" + firstObjectId + "].featureLayout.json에 descriptor가 요구하는 layout payload를 작성해주세요.");
            if (string.Equals(ruleCode, "InvalidFeatureLayoutPayload", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.placements[*].featureLayout.json을 descriptor layout schema에 맞게 다시 작성해주세요."
                    : "draft_layout.placements[objectId=" + firstObjectId + "].featureLayout.json을 descriptor layout schema에 맞게 다시 작성해주세요.");
            if (string.Equals(ruleCode, "EnvironmentOccupiedCellConflict", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.environment[*] bounds를 줄이거나 이동해 gameplay footprint와 겹치지 않게 해주세요."
                    : "draft_layout.environment[objectId=" + firstObjectId + "] bounds를 줄이거나 이동해 gameplay footprint와 겹치지 않게 해주세요.");
            if (string.Equals(ruleCode, "LayoutBoundsContainment", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "해당 placement footprint가 draft_layout.floorBounds 안으로 들어오도록 다시 배치해주세요."
                    : "draft_layout.placements[objectId=" + firstObjectId + "] footprint가 floorBounds 안으로 들어오도록 다시 배치해주세요.");
            if (string.Equals(ruleCode, "MissingPlayerStart", StringComparison.Ordinal))
                AddUnique(fixes, "draft_layout.playerStart.objectId를 player role objectId로 채우고, worldX/worldZ를 함께 작성해주세요.");
            if (string.Equals(ruleCode, "DuplicatePlacementObjectId", StringComparison.Ordinal))
                AddUnique(fixes, "draft_layout.placements에서 중복 objectId를 제거해 각 objectId가 정확히 1번만 나오게 해주세요.");
            if (string.Equals(ruleCode, "MissingPlacementCoverage", StringComparison.Ordinal))
                AddUnique(fixes, "draft_layout.placements, draft_layout.playerStart, draft_layout.environment, draft_layout.customerPaths 중 최소 하나 이상을 채워주세요.");
            if (string.Equals(ruleCode, "EnvironmentPerimeterThickness", StringComparison.Ordinal))
                AddEnvironmentPerimeterThicknessFixes(fixes, message);
            if (fixes.Count == 0 && !string.IsNullOrWhiteSpace(message))
                AddUnique(fixes, "현재 blocker message를 기준으로 가장 직접적인 원인부터 제거해주세요.");

            return fixes.ToArray();
        }

        private static void AddEnvironmentPerimeterThicknessFixes(List<string> fixes, string message)
        {
            if (fixes == null)
                return;

            string normalizedMessage = Normalize(message);
            Match match = Regex.Match(
                normalizedMessage,
                @"layoutSpec\.environment\[(?<index>[0-9]+)\]\((?<objectId>[^/)]+)/(?<designId>[^)]+)\).*thickness=(?<thickness>[0-9]+(?:\.[0-9]+)?), max=(?<max>[0-9]+(?:\.[0-9]+)?)",
                RegexOptions.CultureInvariant);

            if (match.Success)
            {
                string index = match.Groups["index"].Value;
                string objectId = Normalize(match.Groups["objectId"].Value);
                string designId = Normalize(match.Groups["designId"].Value);
                string max = Normalize(match.Groups["max"].Value);
                string target = "draft_layout.environment[" + index + "]";
                string label = string.IsNullOrEmpty(objectId) ? target : target + "(" + objectId + "/" + designId + ")";

                AddUnique(fixes, label + "의 perimeter 짧은 축 두께를 " + max + " 이하로 줄여주세요. 가로 띠면 worldDepth를, 세로 띠면 worldWidth를 줄입니다.");
                AddUnique(fixes, label + "는 긴 축으로만 연장하고 짧은 축은 catalog footprint 두께를 유지해야 합니다.");
                return;
            }

            AddUnique(fixes, "문제가 된 draft_layout.environment 항목의 짧은 축 두께를 catalog footprint 이하로 줄여주세요. 가로 띠는 worldDepth, 세로 띠는 worldWidth가 두께입니다.");
        }

        private static DraftLayoutPreflightDiagnostic[] FilterOverlapDiagnostics(List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return new DraftLayoutPreflightDiagnostic[0];

            var filtered = new List<DraftLayoutPreflightDiagnostic>(diagnostics.Count);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                DraftLayoutPreflightDiagnostic diagnostic = diagnostics[i];
                if (IsOverlapDiagnostic(diagnostic))
                    continue;

                filtered.Add(diagnostic);
            }

            return filtered.ToArray();
        }

        private static bool IsOverlapDiagnostic(DraftLayoutPreflightDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return false;

            string ruleCode = Normalize(diagnostic.ruleCode);
            string message = Normalize(diagnostic.message);
            return ruleCode.Contains("overlap", StringComparison.Ordinal) ||
                message.Contains("overlap", StringComparison.Ordinal) ||
                message.Contains("겹칩", StringComparison.Ordinal) ||
                message.Contains("겹칩니다", StringComparison.Ordinal);
        }

        private static string ResolveFixability(string ruleCode, string message)
        {
            if (string.Equals(ruleCode, "MissingExitWaypoints", StringComparison.Ordinal) ||
                string.Equals(ruleCode, "MissingEntryWaypoints", StringComparison.Ordinal) ||
                string.Equals(ruleCode, "MissingQueuePoints", StringComparison.Ordinal) ||
                string.Equals(ruleCode, "MissingSpawnPoint", StringComparison.Ordinal) ||
                string.Equals(ruleCode, "MissingLeavePoint", StringComparison.Ordinal))
            {
                return "automatic_candidate";
            }

            return "manual";
        }

        private static Dictionary<string, PromptIntentObjectDefinition> BuildObjectLookup(PlayablePromptIntent intent)
        {
            var lookup = new Dictionary<string, PromptIntentObjectDefinition>(StringComparer.Ordinal);
            PromptIntentObjectDefinition[] objects = intent != null ? intent.objects ?? new PromptIntentObjectDefinition[0] : new PromptIntentObjectDefinition[0];
            for (int i = 0; i < objects.Length; i++)
            {
                PromptIntentObjectDefinition entry = objects[i];
                string objectId = Normalize(entry != null ? entry.id : string.Empty);
                if (string.IsNullOrEmpty(objectId) || lookup.ContainsKey(objectId))
                    continue;

                lookup.Add(objectId, entry);
            }

            return lookup;
        }

        private static string ResolveCustomerPathObjectId(string targetId)
        {
            string normalized = Normalize(targetId);
            if (normalized.StartsWith("spawn_", StringComparison.Ordinal))
                return normalized.Substring("spawn_".Length);

            return normalized;
        }

        private static string FormatStageMessage(string stage, string message)
        {
            string normalizedMessage = message != null ? message.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedMessage))
                return string.Empty;

            string normalizedStage = stage != null ? stage.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedStage))
                return normalizedMessage;

            return "[" + normalizedStage + "] " + normalizedMessage;
        }

        private static void AddUnique(List<string> messages, string message)
        {
            if (messages == null)
                return;

            string normalized = message != null ? message.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalized))
                return;

            for (int i = 0; i < messages.Count; i++)
            {
                if (string.Equals(messages[i], normalized, StringComparison.Ordinal))
                    return;
            }

            messages.Add(normalized);
        }

        private static string Normalize(string value)
        {
            return value != null ? value.Trim() : string.Empty;
        }
    }
}

