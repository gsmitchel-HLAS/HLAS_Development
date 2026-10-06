using System;

namespace HLAS.Domain
{
    public sealed record ReleaseRecord
    {
        public ReleaseId ReleaseId { get; }
        public ReadinessCheckId ReadinessCheckId { get; }
        public OperationId OperationId { get; }
        public GovernedTimestamp ReleaseRequestedUtc { get; }

        public ReleaseRecord(
            ReleaseId releaseId,
            ReadinessCheckId readinessCheckId,
            OperationId operationId,
            GovernedTimestamp releaseRequestedUtc)
        {
            if (releaseId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ReleaseId may not be empty.",
                    nameof(releaseId));
            }

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

            if (releaseRequestedUtc.Value == default)
            {
                throw new ArgumentException(
                    "ReleaseRequestedUtc must be populated.",
                    nameof(releaseRequestedUtc));
            }

            ReleaseId = releaseId;
            ReadinessCheckId = readinessCheckId;
            OperationId = operationId;
            ReleaseRequestedUtc = releaseRequestedUtc;
        }
    }
}