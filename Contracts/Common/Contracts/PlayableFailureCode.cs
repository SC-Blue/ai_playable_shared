namespace Supercent.PlayableAI.Common.Contracts
{
    public enum PlayableFailureCode
    {
        None = 0,
        MissingPrompt,
        MissingRuntimeRoot,
        InvalidJson,
        UnsupportedInputContract,
        UnknownKey,
        MissingRequiredField,
        InvalidValue,
        DuplicateIdentifier,
        IntentValidationFailed,
        ModelBuildFailed,
        ModelValidationFailed,
        IntentAuditFailed,
        LoweringFailed,
        SemanticPreflightFailed,
        BakeFailed,
        CompiledPlanValidationFailed,
    }
}
