using System.Collections.Generic;

namespace Supercent.PlayableAI.Common.Contracts
{
    public sealed class ValidationIssueRecord
    {
        public string ruleId = string.Empty;
        public ValidationSeverity severity = ValidationSeverity.Blocker;
        public string message = string.Empty;
        public string targetPath = string.Empty;
        public Dictionary<string, string> relatedIds = new Dictionary<string, string>();
        public string fixHint = string.Empty;
        public string sourceValidator = string.Empty;

        public ValidationIssueRecord()
        {
        }

        public ValidationIssueRecord(
            string ruleId,
            ValidationSeverity severity,
            string message,
            string sourceValidator)
        {
            this.ruleId = ruleId ?? string.Empty;
            this.severity = severity;
            this.message = message ?? string.Empty;
            this.sourceValidator = sourceValidator ?? string.Empty;
        }

        public ValidationIssueRecord WithTargetPath(string targetPath)
        {
            this.targetPath = targetPath ?? string.Empty;
            return this;
        }

        public ValidationIssueRecord WithFixHint(string fixHint)
        {
            this.fixHint = fixHint ?? string.Empty;
            return this;
        }

        public ValidationIssueRecord WithRelated(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return this;
            relatedIds ??= new Dictionary<string, string>();
            relatedIds[key] = value ?? string.Empty;
            return this;
        }
    }
}
