using System;

namespace HLAS.Domain
{
    public sealed record SourceEvidenceMaintenanceRecord
    {
        public const string CorrectMaintenanceType =
            "CORRECT";

        public const string ReplaceMaintenanceType =
            "REPLACE";

        public OperationId OperationId { get; }
        public FreezeId FreezeId { get; }
        public EvidenceId PriorEvidenceId { get; }
        public EvidenceId ResultingEvidenceId { get; }
        public string MaintenanceType { get; }
        public GovernedTimestamp MaintainedUtc { get; }
        public string? ReplacementReason { get; }

        public SourceEvidenceMaintenanceRecord(
            OperationId operationId,
            FreezeId freezeId,
            EvidenceId priorEvidenceId,
            EvidenceId resultingEvidenceId,
            string maintenanceType,
            GovernedTimestamp maintainedUtc,
            string? replacementReason = null)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (freezeId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "FreezeId may not be empty.",
                    nameof(freezeId));
            }

            if (priorEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "PriorEvidenceId may not be empty.",
                    nameof(priorEvidenceId));
            }

            if (resultingEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ResultingEvidenceId may not be empty.",
                    nameof(resultingEvidenceId));
            }

            if (!string.Equals(
                    maintenanceType,
                    CorrectMaintenanceType,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    maintenanceType,
                    ReplaceMaintenanceType,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "MaintenanceType must be CORRECT or REPLACE.",
                    nameof(maintenanceType));
            }
            if (string.Equals(
        maintenanceType,
        CorrectMaintenanceType,
        StringComparison.Ordinal) &&
    priorEvidenceId != resultingEvidenceId)
            {
                throw new ArgumentException(
                    "CORRECT must preserve the existing EvidenceId.",
                    nameof(resultingEvidenceId));
            }

            if (string.Equals(
                    maintenanceType,
                    ReplaceMaintenanceType,
                    StringComparison.Ordinal) &&
                priorEvidenceId == resultingEvidenceId)
            {
                throw new ArgumentException(
                    "REPLACE must create a new EvidenceId.",
                    nameof(resultingEvidenceId));
            }
            if (string.Equals(
        maintenanceType,
        CorrectMaintenanceType,
        StringComparison.Ordinal) &&
    replacementReason is not null)
            {
                throw new ArgumentException(
                    "CORRECT may not contain a ReplacementReason.",
                    nameof(replacementReason));
            }

            if (string.Equals(
                    maintenanceType,
                    ReplaceMaintenanceType,
                    StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(replacementReason))
            {
                throw new ArgumentException(
                    "REPLACE requires a nonblank ReplacementReason.",
                    nameof(replacementReason));
            }
            if (maintainedUtc.Value == default)
            {
                throw new ArgumentException(
                    "MaintainedUtc must be populated.",
                    nameof(maintainedUtc));
            }

            OperationId = operationId;
            FreezeId = freezeId;
            PriorEvidenceId = priorEvidenceId;
            ResultingEvidenceId = resultingEvidenceId;
            MaintenanceType = maintenanceType;
            MaintainedUtc = maintainedUtc;
            ReplacementReason = replacementReason;
        }
    }
}