using System;

namespace HLAS.Domain
{
    public sealed record SourceEvidenceMaintenanceChangeRecord
    {
        public OperationId OperationId { get; }
        public int ChangeSequence { get; }
        public string FieldName { get; }
        public string? PriorValue { get; }
        public string? ResultingValue { get; }

        public SourceEvidenceMaintenanceChangeRecord(
            OperationId operationId,
            int changeSequence,
            string fieldName,
            string? priorValue,
            string? resultingValue)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (changeSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(changeSequence),
                    "ChangeSequence must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException(
                    "FieldName may not be blank.",
                    nameof(fieldName));
            }

            if (string.Equals(
                    priorValue,
                    resultingValue,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "PriorValue and ResultingValue must differ.",
                    nameof(resultingValue));
            }

            OperationId = operationId;
            ChangeSequence = changeSequence;
            FieldName = fieldName;
            PriorValue = priorValue;
            ResultingValue = resultingValue;
        }
    }
}