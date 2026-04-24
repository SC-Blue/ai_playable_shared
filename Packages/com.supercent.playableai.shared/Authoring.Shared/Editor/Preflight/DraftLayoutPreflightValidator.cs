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
        public string[] objectIds = Array.Empty<string>();
        public string[] suggestedFixes = Array.Empty<string>();
        public string fixability = "manual";
        public bool autoFixEligible;
    }

    public sealed class DraftLayoutPreflightValidationResult
    {
        public bool IsValid;
        public string Stage = string.Empty;
        public string Message = string.Empty;
        public string[] Blockers = Array.Empty<string>();
        public string[] Warnings = Array.Empty<string>();
        public string validationPath = string.Empty;
        public string[] completedStages = Array.Empty<string>();
        public string terminalStageKind = string.Empty;
        public string recommendedAuthority = string.Empty;
        public DraftLayoutPreflightDiagnostic[] diagnostics = Array.Empty<DraftLayoutPreflightDiagnostic>();
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
            LayoutSpecEnvironmentEntry[] environmentEntries = safeLayoutSpec.environment ?? Array.Empty<LayoutSpecEnvironmentEntry>();
            environmentEntries = environmentEntries
                .Where(static entry => entry != null && !string.Equals(entry.objectId, "floor", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return new LayoutSpecDocument
            {
                floorBounds = safeLayoutSpec.floorBounds ?? new LayoutSpecFloorBounds(),
                placements = safeLayoutSpec.placements ?? Array.Empty<LayoutSpecPlacementEntry>(),
                playerStart = safeLayoutSpec.playerStart ?? new LayoutSpecPlayerStartEntry(),
                environment = environmentEntries,
                customerPaths = safeLayoutSpec.customerPaths ?? Array.Empty<LayoutSpecCustomerPathEntry>(),
                sourceImages = safeLayoutSpec.sourceImages ?? Array.Empty<LayoutSpecSourceImageEntry>(),
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
            DraftLayoutPreflightDiagnostic[] safeDiagnostics = diagnostics ?? Array.Empty<DraftLayoutPreflightDiagnostic>();
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
            LayoutSpecPlacementEntry[] placements = layoutSpec.placements ?? Array.Empty<LayoutSpecPlacementEntry>();
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

            LayoutSpecCustomerPathEntry[] customerPaths = layoutSpec.customerPaths ?? Array.Empty<LayoutSpecCustomerPathEntry>();
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

                string role = Normalize(objectDefinition.role);
                if (string.Equals(role, PromptIntentObjectRoles.RAIL, StringComparison.Ordinal))
                {
                    RailOptionsDefinition railOptions = objectDefinition.railOptions;
                    if (railOptions == null ||
                        string.IsNullOrWhiteSpace(railOptions.sinkEndpointTargetObjectId))
                    {
                        AddDiagnostic(diagnostics, "blocker", "DraftLayout", "rail object '" + objectId + "'는 intent.railOptions.sinkEndpointTargetObjectId를 가져야 합니다.");
                    }

                    if (placement == null ||
                        placement.railLayout == null ||
                        placement.railLayout.pathCells == null ||
                        placement.railLayout.pathCells.Length == 0)
                    {
                        AddDiagnostic(diagnostics, "blocker", "DraftLayout", "rail object '" + objectId + "'는 layoutSpec.railLayout.pathCells가 필요합니다.");
                    }
                    else if (!TryResolveRailSinkBounds(railOptions, placements, out WorldBoundsDefinition sinkBounds))
                    {
                        AddDiagnostic(diagnostics, "blocker", "DraftLayout", "rail object '" + objectId + "'의 sink endpoint target placement를 찾지 못했습니다.");
                    }
                    else if (!RailPathAuthoringUtility.TryBuildResolvedPath(placement.railLayout.pathCells, sinkBounds, out _, out string railPathError))
                    {
                        AddDiagnostic(diagnostics, "blocker", "DraftLayout", "rail object '" + objectId + "'의 pathCells가 유효하지 않습니다: " + railPathError);
                    }
                }

                if (string.Equals(role, PromptIntentObjectRoles.PHYSICS_AREA, StringComparison.Ordinal))
                {
                    LayoutSpecPhysicsAreaLayoutEntry physicsAreaLayout = placement != null ? placement.physicsAreaLayout : null;
                    bool hasRealBounds =
                        physicsAreaLayout != null &&
                        physicsAreaLayout.realPhysicsZoneBounds != null &&
                        physicsAreaLayout.realPhysicsZoneBounds.hasWorldBounds;
                    bool hasFakeBounds =
                        physicsAreaLayout != null &&
                        physicsAreaLayout.fakeSpriteZoneBounds != null &&
                        physicsAreaLayout.fakeSpriteZoneBounds.hasWorldBounds;
                    if (!hasRealBounds || !hasFakeBounds)
                    {
                        AddDiagnostic(diagnostics, "blocker", "DraftLayout", "physics_area object '" + objectId + "'는 realPhysicsZoneBounds와 fakeSpriteZoneBounds를 함께 가져야 합니다.");
                    }

                }
            }
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
            if (message.IndexOf("pathCells", StringComparison.Ordinal) >= 0 && message.IndexOf("필요합니다", StringComparison.Ordinal) >= 0)
                return "MissingRailPathCells";
            if (message.IndexOf("sink endpoint target placement", StringComparison.Ordinal) >= 0)
                return "MissingRailSinkPlacement";
            if (message.IndexOf("pathCells가 유효하지 않습니다", StringComparison.Ordinal) >= 0)
                return "InvalidRailPathTopology";
            if (message.IndexOf("realPhysicsZoneBounds", StringComparison.Ordinal) >= 0 || message.IndexOf("fakeSpriteZoneBounds", StringComparison.Ordinal) >= 0)
                return "MissingPhysicsAreaBounds";
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
            string[] safeObjectIds = objectIds ?? Array.Empty<string>();
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
            if (string.Equals(ruleCode, "MissingRailPathCells", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.placements[*].railLayout.pathCells를 작성해주세요. 연결된 경로 자체가 권위 데이터입니다."
                    : "draft_layout.placements[objectId=" + firstObjectId + "].railLayout.pathCells를 작성해주세요. 연결된 경로 자체가 권위 데이터입니다.");
            if (string.Equals(ruleCode, "MissingRailSinkPlacement", StringComparison.Ordinal))
                AddUnique(fixes, "intent.railOptions.sinkEndpointTargetObjectId가 가리키는 object의 placement를 draft_layout.placements에 함께 작성해주세요.");
            if (string.Equals(ruleCode, "InvalidRailPathTopology", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.placements[*].railLayout.pathCells를 하나의 connected non-branching path로 다시 작성해주세요. terminal은 정확히 2개여야 합니다."
                    : "draft_layout.placements[objectId=" + firstObjectId + "].railLayout.pathCells를 하나의 connected non-branching path로 다시 작성해주세요. terminal은 정확히 2개여야 합니다.");
            if (string.Equals(ruleCode, "MissingPhysicsAreaBounds", StringComparison.Ordinal))
                AddUnique(fixes, string.IsNullOrEmpty(firstObjectId)
                    ? "draft_layout.placements[*].physicsAreaLayout.realPhysicsZoneBounds와 fakeSpriteZoneBounds를 함께 작성해주세요."
                    : "draft_layout.placements[objectId=" + firstObjectId + "].physicsAreaLayout.realPhysicsZoneBounds와 fakeSpriteZoneBounds를 함께 작성해주세요.");
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
            if (fixes.Count == 0 && !string.IsNullOrWhiteSpace(message))
                AddUnique(fixes, "현재 blocker message를 기준으로 가장 직접적인 원인부터 제거해주세요.");

            return fixes.ToArray();
        }

        private static DraftLayoutPreflightDiagnostic[] FilterOverlapDiagnostics(List<DraftLayoutPreflightDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return Array.Empty<DraftLayoutPreflightDiagnostic>();

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

        private static bool TryResolveRailSinkBounds(
            RailOptionsDefinition railOptions,
            LayoutSpecPlacementEntry[] placements,
            out WorldBoundsDefinition sinkBounds)
        {
            sinkBounds = new WorldBoundsDefinition();
            string sinkObjectId = Normalize(railOptions != null ? railOptions.sinkEndpointTargetObjectId : string.Empty);
            if (string.IsNullOrEmpty(sinkObjectId))
                return false;

            LayoutSpecPlacementEntry[] safePlacements = placements ?? Array.Empty<LayoutSpecPlacementEntry>();
            for (int i = 0; i < safePlacements.Length; i++)
            {
                LayoutSpecPlacementEntry placement = safePlacements[i];
                if (placement == null || !string.Equals(Normalize(placement.objectId), sinkObjectId, StringComparison.Ordinal))
                    continue;

                sinkBounds = new WorldBoundsDefinition
                {
                    hasWorldBounds = true,
                    worldX = placement.worldX,
                    worldZ = placement.worldZ,
                    worldWidth = 1f,
                    worldDepth = 1f,
                };
                return true;
            }

            return false;
        }

        private static Dictionary<string, PromptIntentObjectDefinition> BuildObjectLookup(PlayablePromptIntent intent)
        {
            var lookup = new Dictionary<string, PromptIntentObjectDefinition>(StringComparer.Ordinal);
            PromptIntentObjectDefinition[] objects = intent != null ? intent.objects ?? Array.Empty<PromptIntentObjectDefinition>() : Array.Empty<PromptIntentObjectDefinition>();
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
