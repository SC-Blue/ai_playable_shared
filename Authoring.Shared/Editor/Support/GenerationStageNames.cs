namespace Supercent.PlayableAI.Generation.Editor.Pipeline
{
    public static class GenerationStageNames
    {
        public const string INIT = "Init";
        public const string STATIC_GUARD = "StaticGuard";
        public const string INTENT_VALIDATION = "IntentValidation";
        public const string MODEL_BUILD = "ModelBuild";
        public const string MODEL_VALIDATION = "ModelValidation";
        public const string INTENT_AUDIT = "IntentAudit";
        public const string LOWERING = "Lowering";
        public const string COMPILED_PLAN_VALIDATION = "CompiledPlanValidation";
        public const string PLACEMENT = "Placement";
        public const string BAKE = "Bake";
        public const string ENVIRONMENT = "Environment";
        public const string SKIPPED = "Skipped";
    }
}
