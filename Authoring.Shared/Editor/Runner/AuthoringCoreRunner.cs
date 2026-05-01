using System.Collections.Generic;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;
using Supercent.PlayableAI.AuthoringCore;
using Supercent.PlayableAI.Generation.Editor.Compile;
using Supercent.PlayableAI.Generation.Editor.Pipeline;
using Supercent.PlayableAI.Generation.Editor.Validation;

namespace PlayableAI.AuthoringCore
{
    public enum AuthoringCoreExecutionProfile
    {
        Validate = 0,
        GeneratePlayable = 1,
    }

    public sealed class AuthoringCoreRunResult
    {
        public bool IsSuccess;
        public string Stage = GenerationStageNames.ENVIRONMENT;
        public PlayableFailureCode FailureCode = PlayableFailureCode.InvalidValue;
        public string Message = string.Empty;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public PlayablePromptIntent Intent = null;
        public PlayableScenarioModel Model = null;
        public CompiledPlayablePlan Plan = null;
    }

    public static class AuthoringCoreRunner
    {
        public static AuthoringCoreRunResult Run(
            string intentJson,
            PlayableObjectCatalog catalog,
            string mode,
            LayoutSpecDocument layoutSpec = null)
        {
            return Run(intentJson, catalog, ResolveProfile(mode), layoutSpec);
        }

        public static AuthoringCoreRunResult Run(
            string intentJson,
            PlayableObjectCatalog catalog,
            AuthoringCoreExecutionProfile profile,
            LayoutSpecDocument layoutSpec = null)
        {
            PromptIntentContractRegistry.SetActiveFeatureDescriptors(catalog != null ? catalog.FeatureDescriptors : null);
            PromptIntentCapabilityRegistry.SetActiveFeatureDescriptors(catalog != null ? catalog.FeatureDescriptors : null);
            try
            {
                var result = new AuthoringCoreRunResult();

                AuthoringInputContractDetectionResult detection = AuthoringInputContractDetector.Detect(intentJson);
                if (!detection.IsPromptIntent)
                    return Fail(result, GenerationStageNames.INTENT_VALIDATION, detection.FailureCode, detection.Message, detection.Message);

                PromptIntentJsonValidationResult intentValidation = PromptIntentJsonValidator.Validate(intentJson, catalog);
                if (!intentValidation.IsValid)
                    return Fail(result, GenerationStageNames.INTENT_VALIDATION, intentValidation.FailureCode, intentValidation.Message, intentValidation.Errors);

                result.Intent = intentValidation.Contract;
                return Run(result, intentValidation.Contract, catalog, profile, layoutSpec);
            }
            finally
            {
                PromptIntentCapabilityRegistry.ClearActiveFeatureDescriptors();
                PromptIntentContractRegistry.ClearActiveFeatureDescriptors();
            }
        }

        public static AuthoringCoreRunResult Run(
            PlayablePromptIntent intent,
            PlayableObjectCatalog catalog,
            AuthoringCoreExecutionProfile profile,
            LayoutSpecDocument layoutSpec = null)
        {
            PromptIntentContractRegistry.SetActiveFeatureDescriptors(catalog != null ? catalog.FeatureDescriptors : null);
            PromptIntentCapabilityRegistry.SetActiveFeatureDescriptors(catalog != null ? catalog.FeatureDescriptors : null);
            try
            {
                var result = new AuthoringCoreRunResult();
                if (intent == null)
                    return Fail(result, GenerationStageNames.INTENT_VALIDATION, PlayableFailureCode.InvalidValue, "PlayablePromptIntent가 null입니다.", "PlayablePromptIntent가 null입니다.");

                result.Intent = intent;
                return Run(result, intent, catalog, profile, layoutSpec);
            }
            finally
            {
                PromptIntentCapabilityRegistry.ClearActiveFeatureDescriptors();
                PromptIntentContractRegistry.ClearActiveFeatureDescriptors();
            }
        }

        private static AuthoringCoreRunResult Run(
            AuthoringCoreRunResult result,
            PlayablePromptIntent intent,
            PlayableObjectCatalog catalog,
            AuthoringCoreExecutionProfile profile,
            LayoutSpecDocument layoutSpec)
        {
            PromptIntentSemanticValidationResult semanticValidation = PromptIntentSemanticValidator.Validate(intent, catalog);
            if (!semanticValidation.IsValid)
                return Fail(result, GenerationStageNames.INTENT_VALIDATION, semanticValidation.FailureCode, semanticValidation.Message, semanticValidation.Errors);

            ScenarioModelBuildResult modelBuild = ScenarioModelBuilder.Build(intent);
            if (!modelBuild.IsValid)
                return Fail(result, GenerationStageNames.MODEL_BUILD, modelBuild.FailureCode, modelBuild.Message, modelBuild.Errors);

            result.Model = modelBuild.Model;

            ScenarioModelValidationResult modelValidation = ScenarioModelValidator.Validate(modelBuild.Model);
            if (!modelValidation.IsValid)
                return Fail(result, GenerationStageNames.MODEL_VALIDATION, modelValidation.FailureCode, modelValidation.Message, modelValidation.Errors);

            bool hasLayoutSpec = layoutSpec != null;
            if (!hasLayoutSpec)
            {
                if (profile == AuthoringCoreExecutionProfile.Validate)
                    return Success(result, GenerationStageNames.MODEL_VALIDATION, "intent 검증이 완료되었습니다.");

                return Fail(
                    result,
                    GenerationStageNames.LOWERING,
                    PlayableFailureCode.MissingRequiredField,
                    "layoutSpec이 필요합니다. Step3 geometry 없이 compile/generate를 진행할 수 없습니다.",
                    "layoutSpec이 필요합니다. Step3 geometry 없이 compile/generate를 진행할 수 없습니다.");
            }

            ScenarioModelLoweringResult lowering = ScenarioModelLoweringCompiler.Compile(modelBuild.Model, catalog, layoutSpec);
            if (!lowering.IsValid)
                return Fail(result, GenerationStageNames.LOWERING, lowering.FailureCode, lowering.Message, lowering.Errors);
            AddWarnings(result.Warnings, lowering.Warnings);

            result.Plan = lowering.Plan;

            IntentAuditValidationResult audit = IntentAuditValidator.Validate(intent, modelBuild.Model, lowering.Plan, catalog, layoutSpec);
            if (!audit.IsValid)
                return Fail(result, GenerationStageNames.INTENT_AUDIT, audit.FailureCode, audit.Message, audit.Errors);

            if (profile == AuthoringCoreExecutionProfile.Validate)
                return Success(result, GenerationStageNames.INTENT_AUDIT, "검증이 완료되었습니다.");

            CompiledPlayablePlanValidationResult compiledValidation = CompiledPlayablePlanValidator.Validate(lowering.Plan, catalog, layoutSpec);
            if (!compiledValidation.IsValid)
                return Fail(result, GenerationStageNames.COMPILED_PLAN_VALIDATION, compiledValidation.FailureCode, compiledValidation.Message, compiledValidation.Errors);

            AddWarnings(result.Warnings, compiledValidation.Warnings);
            return Success(result, GenerationStageNames.COMPILED_PLAN_VALIDATION, "검증 및 컴파일이 완료되었습니다.");
        }

        private static void AddWarnings(List<string> target, IReadOnlyList<string> warnings)
        {
            if (target == null || warnings == null)
                return;

            for (int i = 0; i < warnings.Count; i++)
            {
                string warning = warnings[i] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(warning) && !target.Contains(warning))
                    target.Add(warning);
            }
        }

        private static AuthoringCoreExecutionProfile ResolveProfile(string mode)
        {
            return string.Equals(mode, "validate", System.StringComparison.Ordinal)
                ? AuthoringCoreExecutionProfile.Validate
                : AuthoringCoreExecutionProfile.GeneratePlayable;
        }

        private static AuthoringCoreRunResult Success(AuthoringCoreRunResult result, string stage, string message)
        {
            result.IsSuccess = true;
            result.Stage = stage ?? string.Empty;
            result.FailureCode = PlayableFailureCode.None;
            result.Message = message ?? string.Empty;
            result.Errors.Clear();
            return result;
        }

        private static AuthoringCoreRunResult Fail(AuthoringCoreRunResult result, string stage, PlayableFailureCode failureCode, string message, IReadOnlyList<string> errors)
        {
            result.IsSuccess = false;
            result.Stage = stage ?? string.Empty;
            result.FailureCode = failureCode;
            result.Message = message ?? string.Empty;
            result.Errors = new List<string>();
            if (errors != null)
            {
                for (int i = 0; i < errors.Count; i++)
                    result.Errors.Add(errors[i] ?? string.Empty);
            }

            if (result.Errors.Count == 0 && !string.IsNullOrWhiteSpace(message))
                result.Errors.Add(message);
            return result;
        }

        private static AuthoringCoreRunResult Fail(AuthoringCoreRunResult result, string stage, PlayableFailureCode failureCode, string message, string error)
        {
            return Fail(result, stage, failureCode, message, string.IsNullOrWhiteSpace(error) ? new string[0] : new[] { error });
        }
    }
}
