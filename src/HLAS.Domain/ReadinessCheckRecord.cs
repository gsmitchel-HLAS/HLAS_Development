using System;

namespace HLAS.Domain
{
    public sealed record ReadinessCheckRecord
    {
        public ReadinessCheckId ReadinessCheckId { get; }
        public OperationId OperationId { get; }
        public ReadinessStatus OverallStatus { get; }
        public GovernedTimestamp EvaluatedUtc { get; }

        public ReadinessCheckRecord(
            ReadinessCheckId readinessCheckId,
            OperationId operationId,
            ReadinessStatus overallStatus,
            GovernedTimestamp evaluatedUtc)
        {
            if (readinessCheckId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ReadinessCheckId may not be empty.",
                    nameof(readinessCheckId));
            }

            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (string.IsNullOrWhiteSpace(overallStatus.Value))
            {
                throw new ArgumentException(
                    "OverallStatus may not be blank.",
                    nameof(overallStatus));
            }

            if (evaluatedUtc.Value == default)
            {
                throw new ArgumentException(
                    "EvaluatedUtc must be populated.",
                    nameof(evaluatedUtc));
            }

            ReadinessCheckId = readinessCheckId;
            OperationId = operationId;
            OverallStatus = overallStatus;
            EvaluatedUtc = evaluatedUtc;
        }
    }
}