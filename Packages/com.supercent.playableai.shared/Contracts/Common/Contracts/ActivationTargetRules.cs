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
        private const float EPSILON = 0.0001f;

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

        public sealed class Participant
        {
            public string ReferenceId = string.Empty;
            public string SceneObjectId = string.Empty;
            public string Role = string.Empty;
            public string SourceEndpointTargetObjectId = string.Empty;
            public string SourceEndpointSide = string.Empty;
            public string SinkEndpointTargetObjectId = string.Empty;
            public string SinkEndpointSide = string.Empty;
            public HashSet<string> UnlockTargetReferenceIds = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> ImageOverlapReferenceIds = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> SharedSlotReferenceIds = new HashSet<string>(StringComparer.Ordinal);
            public OverlapAllowanceRect[] OverlapAllowanceRects = new OverlapAllowanceRect[0];
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        public sealed class OverlapAllowanceDescriptor
        {
            public string CounterpartRole = string.Empty;
            public float CenterOffsetX;
            public float CenterOffsetZ;
            public float WidthWorld = 1f;
            public float DepthWorld = 1f;
        }

        public sealed class OverlapAllowanceRect
        {
            public string CounterpartRole = string.Empty;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }

        private struct RectBounds
        {
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
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

        public static OverlapAllowanceDescriptor[] BuildOverlapAllowanceDescriptors(
            PlacementOverlapAllowanceDefinition[] allowances,
            float gridWorldStep)
        {
            PlacementOverlapAllowanceDefinition[] safeAllowances = allowances ?? new PlacementOverlapAllowanceDefinition[0];
            if (safeAllowances.Length == 0)
                return new OverlapAllowanceDescriptor[0];

            float safeGridWorldStep = gridWorldStep > EPSILON ? gridWorldStep : 1f;
            var descriptors = new List<OverlapAllowanceDescriptor>(safeAllowances.Length);
            for (int i = 0; i < safeAllowances.Length; i++)
            {
                PlacementOverlapAllowanceDefinition allowance = safeAllowances[i];
                string counterpartRole = Normalize(allowance != null ? allowance.counterpartRole : string.Empty);
                int widthCells = allowance != null && allowance.widthCells > 0 ? allowance.widthCells : 0;
                int depthCells = allowance != null && allowance.depthCells > 0 ? allowance.depthCells : 0;
                if (string.IsNullOrEmpty(counterpartRole) || widthCells <= 0 || depthCells <= 0)
                    continue;

                descriptors.Add(new OverlapAllowanceDescriptor
                {
                    CounterpartRole = counterpartRole,
                    CenterOffsetX = allowance.centerOffsetX * safeGridWorldStep,
                    CenterOffsetZ = allowance.centerOffsetZ * safeGridWorldStep,
                    WidthWorld = widthCells * safeGridWorldStep,
                    DepthWorld = depthCells * safeGridWorldStep,
                });
            }

            return descriptors.ToArray();
        }

        public static OverlapAllowanceRect[] BuildWorldOverlapAllowanceRects(
            OverlapAllowanceDescriptor[] descriptors,
            float footprintCenterX,
            float footprintCenterZ,
            float yawDegrees)
        {
            OverlapAllowanceDescriptor[] safeDescriptors = descriptors ?? new OverlapAllowanceDescriptor[0];
            if (safeDescriptors.Length == 0)
                return new OverlapAllowanceRect[0];

            var results = new List<OverlapAllowanceRect>(safeDescriptors.Length);
            double radians = yawDegrees * Math.PI / 180d;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            for (int i = 0; i < safeDescriptors.Length; i++)
            {
                OverlapAllowanceDescriptor descriptor = safeDescriptors[i];
                string counterpartRole = Normalize(descriptor != null ? descriptor.CounterpartRole : string.Empty);
                float widthWorld = descriptor != null ? Math.Max(descriptor.WidthWorld, 0f) : 0f;
                float depthWorld = descriptor != null ? Math.Max(descriptor.DepthWorld, 0f) : 0f;
                if (string.IsNullOrEmpty(counterpartRole) || widthWorld <= EPSILON || depthWorld <= EPSILON)
                    continue;

                float halfWidth = widthWorld * 0.5f;
                float halfDepth = depthWorld * 0.5f;
                float localCenterX = descriptor.CenterOffsetX;
                float localCenterZ = descriptor.CenterOffsetZ;
                float minX = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                float minZ = float.PositiveInfinity;
                float maxZ = float.NegativeInfinity;

                for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
                {
                    float localX = localCenterX + (((cornerIndex & 1) == 0) ? -halfWidth : halfWidth);
                    float localZ = localCenterZ + ((cornerIndex < 2) ? -halfDepth : halfDepth);
                    float rotatedX = (localX * cos) - (localZ * sin);
                    float rotatedZ = (localX * sin) + (localZ * cos);
                    float worldX = footprintCenterX + rotatedX;
                    float worldZ = footprintCenterZ + rotatedZ;
                    minX = Math.Min(minX, worldX);
                    maxX = Math.Max(maxX, worldX);
                    minZ = Math.Min(minZ, worldZ);
                    maxZ = Math.Max(maxZ, worldZ);
                }

                results.Add(new OverlapAllowanceRect
                {
                    CounterpartRole = counterpartRole,
                    MinX = minX,
                    MaxX = maxX,
                    MinZ = minZ,
                    MaxZ = maxZ,
                });
            }

            return results.ToArray();
        }

        public static bool HasDeclaredAllowanceForCounterpartRole(Participant participant, string counterpartRole)
        {
            string normalizedCounterpartRole = Normalize(counterpartRole);
            if (participant == null || string.IsNullOrEmpty(normalizedCounterpartRole))
                return false;

            OverlapAllowanceRect[] safeRects = participant.OverlapAllowanceRects ?? new OverlapAllowanceRect[0];
            for (int i = 0; i < safeRects.Length; i++)
            {
                OverlapAllowanceRect rect = safeRects[i];
                if (rect == null)
                    continue;

                if (string.Equals(Normalize(rect.CounterpartRole), normalizedCounterpartRole, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool HasDeclaredAllowancePair(Participant left, Participant right)
        {
            string leftRole = Normalize(left != null ? left.Role : string.Empty);
            string rightRole = Normalize(right != null ? right.Role : string.Empty);
            return
                HasDeclaredAllowanceForCounterpartRole(left, rightRole) ||
                HasDeclaredAllowanceForCounterpartRole(right, leftRole);
        }

        public static bool IsAllowedOverlap(Participant left, Participant right, out string error)
        {
            error = string.Empty;
            if (left == null || right == null)
                return false;

            if (IsUnlockPadOverlapAllowed(left, right) || IsUnlockPadOverlapAllowed(right, left))
                return true;

            if (IsSharedSlotOverlapAllowed(left, right) || IsSharedSlotOverlapAllowed(right, left))
                return true;

            if (IsImageOverlapAllowed(left, right) || IsImageOverlapAllowed(right, left))
                return true;

            if (!TryResolveOverlapRect(left, right, out RectBounds overlapRect))
                return false;

            if (!TryValidateDeclaredCounterpartAllowances(left, right, overlapRect, out bool hasDeclaredAllowance, out error))
                return false;

            if (hasDeclaredAllowance)
                return true;

            error = string.Empty;
            return false;
        }

        private static bool IsUnlockPadOverlapAllowed(Participant unlockPad, Participant other)
        {
            if (!string.Equals(Normalize(unlockPad != null ? unlockPad.Role : string.Empty), PromptIntentObjectRoles.UNLOCK_PAD, StringComparison.Ordinal))
                return false;

            return HasMatchingParticipantIdentifier(unlockPad != null ? unlockPad.UnlockTargetReferenceIds : null, other);
        }

        private static bool IsImageOverlapAllowed(Participant owner, Participant other)
        {
            return HasMatchingParticipantIdentifier(owner != null ? owner.ImageOverlapReferenceIds : null, other);
        }

        private static bool IsSharedSlotOverlapAllowed(Participant owner, Participant other)
        {
            return HasMatchingParticipantIdentifier(owner != null ? owner.SharedSlotReferenceIds : null, other);
        }

        private static bool HasMatchingParticipantIdentifier(HashSet<string> identifiers, Participant other)
        {
            if (identifiers == null)
                return false;

            return
                HasMatchingIdentifier(identifiers, other != null ? other.ReferenceId : string.Empty) ||
                HasMatchingIdentifier(identifiers, other != null ? other.SceneObjectId : string.Empty);
        }

        private static bool HasMatchingIdentifier(HashSet<string> identifiers, string identifier)
        {
            string normalizedIdentifier = Normalize(identifier);
            if (identifiers == null || string.IsNullOrEmpty(normalizedIdentifier))
                return false;

            return identifiers.Contains(normalizedIdentifier);
        }

        private static bool TryValidateDeclaredCounterpartAllowances(
            Participant left,
            Participant right,
            RectBounds overlapRect,
            out bool hasDeclaredAllowance,
            out string error)
        {
            hasDeclaredAllowance = false;
            error = string.Empty;
            if (!TryValidateDeclaredCounterpartAllowance(left, right, overlapRect, ref hasDeclaredAllowance, out error))
                return false;

            return TryValidateDeclaredCounterpartAllowance(right, left, overlapRect, ref hasDeclaredAllowance, out error);
        }

        private static bool TryValidateDeclaredCounterpartAllowance(
            Participant owner,
            Participant counterpart,
            RectBounds overlapRect,
            ref bool hasDeclaredAllowance,
            out string error)
        {
            error = string.Empty;
            string counterpartRole = Normalize(counterpart != null ? counterpart.Role : string.Empty);
            if (!HasDeclaredAllowanceForCounterpartRole(owner, counterpartRole))
                return true;

            hasDeclaredAllowance = true;
            if (IsOverlapRectContainedWithinAllowance(owner, counterpartRole, overlapRect))
                return true;

            string ownerRole = Normalize(owner != null ? owner.Role : string.Empty);
            if (string.IsNullOrEmpty(ownerRole))
                ownerRole = "participant";

            error =
                ownerRole + " '" + ResolveParticipantLabel(owner) +
                "'의 '" + counterpartRole + "' overlap가 선언된 allowance rect 밖으로 확장됩니다.";
            return false;
        }

        private static bool IsOverlapRectContainedWithinAllowance(
            Participant participant,
            string counterpartRole,
            RectBounds overlapRect)
        {
            string normalizedCounterpartRole = Normalize(counterpartRole);
            OverlapAllowanceRect[] safeRects = participant != null
                ? participant.OverlapAllowanceRects ?? new OverlapAllowanceRect[0]
                : new OverlapAllowanceRect[0];
            for (int i = 0; i < safeRects.Length; i++)
            {
                OverlapAllowanceRect rect = safeRects[i];
                if (rect == null ||
                    !string.Equals(Normalize(rect.CounterpartRole), normalizedCounterpartRole, StringComparison.Ordinal))
                {
                    continue;
                }

                if (rect.MinX <= overlapRect.MinX + EPSILON &&
                    rect.MaxX >= overlapRect.MaxX - EPSILON &&
                    rect.MinZ <= overlapRect.MinZ + EPSILON &&
                    rect.MaxZ >= overlapRect.MaxZ - EPSILON)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveOverlapRect(Participant left, Participant right, out RectBounds overlapRect)
        {
            overlapRect = default;
            float overlapMinX = Math.Max(left.MinX, right.MinX);
            float overlapMaxX = Math.Min(left.MaxX, right.MaxX);
            float overlapMinZ = Math.Max(left.MinZ, right.MinZ);
            float overlapMaxZ = Math.Min(left.MaxZ, right.MaxZ);
            if (overlapMinX >= overlapMaxX - EPSILON || overlapMinZ >= overlapMaxZ - EPSILON)
                return false;

            overlapRect = new RectBounds
            {
                MinX = overlapMinX,
                MaxX = overlapMaxX,
                MinZ = overlapMinZ,
                MaxZ = overlapMaxZ,
            };
            return true;
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

        private static string ResolveParticipantLabel(Participant participant)
        {
            string referenceId = Normalize(participant != null ? participant.ReferenceId : string.Empty);
            if (!string.IsNullOrEmpty(referenceId))
                return referenceId;

            string sceneObjectId = Normalize(participant != null ? participant.SceneObjectId : string.Empty);
            if (!string.IsNullOrEmpty(sceneObjectId))
                return sceneObjectId;

            return Normalize(participant != null ? participant.Role : string.Empty);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
