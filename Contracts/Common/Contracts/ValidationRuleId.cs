namespace Supercent.PlayableAI.Common.Contracts
{
    /// <summary>
    /// Stable wire identifiers for validator emit sites.
    /// 새 ruleId 추가 시 contracts/VALIDATION_RULES.source.json에도 함께 등록한다.
    /// </summary>
    public static class ValidationRuleId
    {
        // Catalog contract validator
        public const string CATALOG_SECTION_CATEGORY_MISMATCH = "CATALOG.SECTION_CATEGORY_MISMATCH";
        public const string CATALOG_OBJECT_ID_MISSING = "CATALOG.OBJECT_ID_MISSING";
        public const string CATALOG_OBJECT_ID_DUPLICATE = "CATALOG.OBJECT_ID_DUPLICATE";
        public const string CATALOG_DESIGN_ID_MISSING = "CATALOG.DESIGN_ID_MISSING";
        public const string CATALOG_DESIGN_LIST_EMPTY = "CATALOG.DESIGN_LIST_EMPTY";
        public const string CATALOG_DESIGN_FOOTPRINT_INVALID = "CATALOG.DESIGN_FOOTPRINT_INVALID";
        public const string CATALOG_GENERIC = "CATALOG.GENERIC";

        // PromptIntent JSON validator
        public const string INTENT_JSON_PARSE_FAILED = "INTENT_JSON.PARSE_FAILED";
        public const string INTENT_JSON_UNKNOWN_KEY = "INTENT_JSON.UNKNOWN_KEY";
        public const string INTENT_JSON_MISSING_REQUIRED_FIELD = "INTENT_JSON.MISSING_REQUIRED_FIELD";
        public const string INTENT_JSON_INVALID_VALUE = "INTENT_JSON.INVALID_VALUE";
        public const string INTENT_JSON_DUPLICATE_IDENTIFIER = "INTENT_JSON.DUPLICATE_IDENTIFIER";
        public const string INTENT_JSON_GENERIC = "INTENT_JSON.GENERIC";

        // PromptIntent semantic validator
        public const string INTENT_SEMANTIC_GENERIC = "INTENT_SEMANTIC.GENERIC";

        // Intent audit validator
        public const string INTENT_AUDIT_GENERIC = "INTENT_AUDIT.GENERIC";

        // Scenario model validator
        public const string SCENARIO_MODEL_GENERIC = "SCENARIO_MODEL.GENERIC";

        // Compiled playable plan validator
        public const string COMPILED_PLAN_GENERIC = "COMPILED_PLAN.GENERIC";

        // Layout / draft validator (server side via DraftLayoutPreflightValidator)
        public const string LAYOUT_GENERIC = "LAYOUT.GENERIC";

        public static bool IsRegistered(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
                return false;

            switch (ruleId)
            {
                case CATALOG_SECTION_CATEGORY_MISMATCH:
                case CATALOG_OBJECT_ID_MISSING:
                case CATALOG_OBJECT_ID_DUPLICATE:
                case CATALOG_DESIGN_ID_MISSING:
                case CATALOG_DESIGN_LIST_EMPTY:
                case CATALOG_DESIGN_FOOTPRINT_INVALID:
                case CATALOG_GENERIC:
                case INTENT_JSON_PARSE_FAILED:
                case INTENT_JSON_UNKNOWN_KEY:
                case INTENT_JSON_MISSING_REQUIRED_FIELD:
                case INTENT_JSON_INVALID_VALUE:
                case INTENT_JSON_DUPLICATE_IDENTIFIER:
                case INTENT_JSON_GENERIC:
                case INTENT_SEMANTIC_GENERIC:
                case INTENT_AUDIT_GENERIC:
                case SCENARIO_MODEL_GENERIC:
                case COMPILED_PLAN_GENERIC:
                case LAYOUT_GENERIC:
                    return true;
                default:
                    return false;
            }
        }
    }
}
