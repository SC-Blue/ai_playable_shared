using System;
using Supercent.PlayableAI.Common.Contracts;
using UnityEngine;

namespace Supercent.PlayableAI.Common.Format
{
    [Serializable]
    public sealed class ItemRef
    {
        public string familyId;
        public string variantId;
    }

    public static class ItemRefUtility
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool IsEmpty(ItemRef item)
        {
            return item == null ||
                   (string.IsNullOrWhiteSpace(item.familyId) && string.IsNullOrWhiteSpace(item.variantId));
        }

        public static bool IsValid(ItemRef item)
        {
            return item != null &&
                   !string.IsNullOrWhiteSpace(item.familyId) &&
                   !string.IsNullOrWhiteSpace(item.variantId);
        }

        public static ItemRef Clone(ItemRef item)
        {
            if (item == null)
                return null;

            return new ItemRef
            {
                familyId = Normalize(item.familyId),
                variantId = Normalize(item.variantId),
            };
        }

        public static bool Equals(ItemRef left, ItemRef right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return string.Equals(Normalize(left.familyId), Normalize(right.familyId), StringComparison.Ordinal) &&
                   string.Equals(Normalize(left.variantId), Normalize(right.variantId), StringComparison.Ordinal);
        }

        public static string ToStableKey(ItemRef item)
        {
            if (!IsValid(item))
                return string.Empty;

            return Normalize(item.familyId) + "/" + Normalize(item.variantId);
        }

        public static string ToDisplayString(ItemRef item)
        {
            return ToStableKey(item);
        }

        public static bool TryParseStableKey(string stableKey, out ItemRef item)
        {
            item = null;
            string normalized = Normalize(stableKey);
            if (string.IsNullOrEmpty(normalized))
                return false;

            int delimiterIndex = normalized.IndexOf('/');
            if (delimiterIndex <= 0 || delimiterIndex >= normalized.Length - 1)
                return false;

            string familyId = Normalize(normalized.Substring(0, delimiterIndex));
            string variantId = Normalize(normalized.Substring(delimiterIndex + 1));
            if (string.IsNullOrEmpty(familyId) || string.IsNullOrEmpty(variantId))
                return false;

            item = new ItemRef
            {
                familyId = familyId,
                variantId = variantId,
            };
            return true;
        }

        public static ItemRef FromStableKey(string stableKey)
        {
            return TryParseStableKey(stableKey, out ItemRef item) ? item : null;
        }
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float xValue, float yValue, float zValue)
        {
            x = xValue;
            y = yValue;
            z = zValue;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        public static SerializableVector3 FromVector3(Vector3 vector)
        {
            return new SerializableVector3(vector.x, vector.y, vector.z);
        }
    }

    [Serializable]
    public sealed class ObjectDesignSelectionDefinition
    {
        public string objectId;
        public string designId;
        public int designIndex = -1;
    }

    public static class ContentSelectionRules
    {
        public const string DESIGN_ID_NOT_SET = "NOT_SET";
        public const string ARROW_OBJECT_ID = "arrow";
        public const string CURRENCY_HUD_OBJECT_ID = "currency_hud";
        public const string DRAG_TO_MOVE_OBJECT_ID = "drag_to_move";
        public const string ENDCARD_CONTENT_OBJECT_ID = "endcard";
        public const string JOYSTICK_OBJECT_ID = "joystick";

        public static readonly string[] REQUIRED_OBJECT_IDS =
        {
            ARROW_OBJECT_ID,
            CURRENCY_HUD_OBJECT_ID,
            DRAG_TO_MOVE_OBJECT_ID,
            ENDCARD_CONTENT_OBJECT_ID,
            JOYSTICK_OBJECT_ID,
        };

        public static bool IsManagedObjectId(string objectId)
        {
            string normalized = Normalize(objectId);
            for (int i = 0; i < REQUIRED_OBJECT_IDS.Length; i++)
            {
                if (string.Equals(normalized, REQUIRED_OBJECT_IDS[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool IsUnsetDesignId(string designId)
        {
            return string.Equals(Normalize(designId), DESIGN_ID_NOT_SET, StringComparison.Ordinal);
        }

        public static string GetRuntimeObjectId(string objectId)
        {
            string normalized = Normalize(objectId);
            if (string.Equals(normalized, ENDCARD_CONTENT_OBJECT_ID, StringComparison.Ordinal))
                return SystemActionIds.ENDCARD_UI_OBJECT_ID;

            return normalized;
        }

        public static string GetRuntimeSpawnKey(string objectId)
        {
            return GetRuntimeObjectId(objectId);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class ContentSelectionDefinition
    {
        public string objectId;
        public string designId = ContentSelectionRules.DESIGN_ID_NOT_SET;
        public int designIndex = -1;
    }

    [Serializable]
    public sealed class ItemPriceDefinition
    {
        public ItemRef item = new ItemRef();
        public int price;
    }

    [Serializable]
    public sealed class FacilityAcceptedItemDefinition
    {
        public string facilityId;
        public ItemRef item = new ItemRef();
        public int laneIndex;
    }

    [Serializable]
    public sealed class FacilityOutputItemDefinition
    {
        public string facilityId;
        public ItemRef item = new ItemRef();
    }

    [Serializable]
    public sealed class CurrencyDefinition
    {
        public string currencyId;
        public int startBalance;
        public int unitValue = 1;
        public string startVisualMode;
    }

    [Serializable]
    public sealed class UnlockDefinition
    {
        public string unlockerId;
        public string currencyId;
        public int cost;
        public ActivationTargetDefinition[] targets = new ActivationTargetDefinition[0];
    }

    [Serializable]
    public sealed class StepConditionDefinition
    {
        public string type;
        public string targetId;
        public string unlockerId;
        public string currencyId;
        public string signalId;
        public ItemRef item = new ItemRef();
        public int amount;
        public float seconds;
    }

    [Serializable]
    public sealed class ReactiveConditionDefinition
    {
        public string targetId;
        public string unlockerId;
        public string currencyId;
        public string signalId;
        public ItemRef item = new ItemRef();
        public int amount;
        public float seconds;
        public string type;
    }

    [Serializable]
    public sealed class ReactiveConditionGroupDefinition
    {
        public string mode;
        public float delaySeconds;
        public ReactiveConditionDefinition[] conditions = new ReactiveConditionDefinition[0];
    }

    [Serializable]
    public sealed class CameraFocusActionPayload
    {
        public string targetId;
        public string eventKey;
        public float movingTime = 0.5f;
        public float startDelay;
        public float returnDelay;
    }

    [Serializable]
    public sealed class ArrowGuideActionPayload
    {
        public string targetId;
        public string eventKey;
        public bool autoHideOnBeatExit = true;
    }

    [Serializable]
    public sealed class ActivationTargetDefinition
    {
        public string kind;
        public string id;
    }

    [Serializable]
    public sealed class RevealActionPayload
    {
        public ActivationTargetDefinition[] targets = new ActivationTargetDefinition[0];
    }

    [Serializable]
    public sealed class CustomerSpawnActionPayload
    {
        public string targetId;
        public int customerDesignIndex = -1;
    }

    [Serializable]
    public sealed class SellerRequestActionPayload
    {
        public string targetId;
        public ItemRef item = new ItemRef();
    }

    [Serializable]
    public sealed class FlowActionPayloadDefinition
    {
        public CameraFocusActionPayload cameraFocus = new CameraFocusActionPayload();
        public ArrowGuideActionPayload arrowGuide = new ArrowGuideActionPayload();
        public RevealActionPayload reveal = new RevealActionPayload();
        public CustomerSpawnActionPayload customerSpawn = new CustomerSpawnActionPayload();
        public SellerRequestActionPayload sellerRequest = new SellerRequestActionPayload();
    }

    [Serializable]
    public sealed class FlowActionDefinition
    {
        public string id;
        public string ownerBeatId;
        public string kind;
        public string triggerMode;
        public ReactiveConditionGroupDefinition when = new ReactiveConditionGroupDefinition();
        public FlowActionPayloadDefinition payload = new FlowActionPayloadDefinition();
    }

    [Serializable]
    public sealed class FlowBeatDefinition
    {
        public string id;
        public StepConditionDefinition enterWhen = new StepConditionDefinition();
        public StepConditionDefinition completeWhen = new StepConditionDefinition();
    }

    [Serializable]
    public sealed class RevealRuleDefinition
    {
        public ActivationTargetDefinition[] targets = new ActivationTargetDefinition[0];
        public ReactiveConditionGroupDefinition when = new ReactiveConditionGroupDefinition();
    }

    [Serializable]
    public sealed class CustomerSpawnRuleDefinition
    {
        public string targetId;
        public int customerDesignIndex = -1;
        public ReactiveConditionGroupDefinition startWhen = new ReactiveConditionGroupDefinition();
    }

    [Serializable]
    public sealed class SellerRequestableItemRuleDefinition
    {
        public string targetId;
        public ItemRef item = new ItemRef();
        public ReactiveConditionGroupDefinition startWhen = new ReactiveConditionGroupDefinition();
    }
}
