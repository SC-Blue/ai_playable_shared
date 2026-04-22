using System.Text.RegularExpressions;
using Supercent.PlayableAI.Common.Contracts;

namespace Supercent.PlayableAI.Generation.Editor.Pipeline
{
    public enum AuthoringInputContractKind
    {
        Unknown = 0,
        PromptIntent,
        UnsupportedOrMixed,
    }

    public sealed class AuthoringInputContractDetectionResult
    {
        public AuthoringInputContractKind Kind;
        public PlayableFailureCode FailureCode;
        public string Message;

        public bool IsPromptIntent => Kind == AuthoringInputContractKind.PromptIntent;
    }

    public static class AuthoringInputContractDetector
    {
        private static readonly string[] REQUIRED_INTENT_KEYS =
        {
            "themeId",
            "currencies",
            "saleValues",
            "objects",
            "contentSelections",
            "stages",
        };

        private static readonly string[] REMOVED_ROOT_KEYS =
        {
            "startingBalances",
            "currencyValues",
            "salePrices",
            "unlocks",
            "tutorialSteps",
            "objectReveals",
            "customerSpawns",
            "systemActions",
            "runtimeObjectDesigns",
        };

        public static AuthoringInputContractDetectionResult Detect(string json)
        {
            var result = new AuthoringInputContractDetectionResult
            {
                Kind = AuthoringInputContractKind.Unknown,
                FailureCode = PlayableFailureCode.UnsupportedInputContract,
                Message = "입력 계약을 판정하지 못했습니다.",
            };

            string safeJson = json ?? string.Empty;
            bool hasAllIntentKeys = true;
            for (int i = 0; i < REQUIRED_INTENT_KEYS.Length; i++)
            {
                if (HasKey(safeJson, REQUIRED_INTENT_KEYS[i]))
                    continue;

                hasAllIntentKeys = false;
                break;
            }

            bool hasAnyRemovedRootKeys = false;
            for (int i = 0; i < REMOVED_ROOT_KEYS.Length; i++)
            {
                if (!HasKey(safeJson, REMOVED_ROOT_KEYS[i]))
                    continue;

                hasAnyRemovedRootKeys = true;
                break;
            }

            if (hasAllIntentKeys && !hasAnyRemovedRootKeys)
            {
                result.Kind = AuthoringInputContractKind.PromptIntent;
                result.FailureCode = PlayableFailureCode.None;
                result.Message = "PlayablePromptIntent 계약입니다.";
                return result;
            }

            if (hasAnyRemovedRootKeys)
            {
                result.Kind = AuthoringInputContractKind.UnsupportedOrMixed;
                result.Message = hasAllIntentKeys
                    ? "intent root와 제거된 실행 레이어 root key가 함께 존재합니다. 입력 계약이 혼합되었습니다."
                    : "현재 계약에서 지원되지 않는 제거된 실행 레이어 root key가 포함되어 있습니다.";
                return result;
            }

            result.Kind = AuthoringInputContractKind.Unknown;
            result.Message = "입력 JSON이 PlayablePromptIntent root shape를 만족하지 않습니다.";
            return result;
        }

        private static bool HasKey(string json, string key)
        {
            return !string.IsNullOrWhiteSpace(key) &&
                   Regex.IsMatch(json ?? string.Empty, "\"" + Regex.Escape(key) + "\"\\s*:", RegexOptions.CultureInvariant);
        }
    }
}
