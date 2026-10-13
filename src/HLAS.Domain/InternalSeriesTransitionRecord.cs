using System;

namespace HLAS.Domain
{
    public sealed record InternalSeriesTransitionRecord
    {
        public InternalSeriesTransitionId TransitionId { get; }
        public ProjectId ProjectId { get; }
        public OperationId OperationId { get; }
        public SeriesId SourceSeriesId { get; }
        public SeriesId ReceivingSeriesId { get; }
        public ReleaseId ReleaseId { get; }
        public FreezeId ReleaseFreezeId { get; }
        public ReadinessCheckId ReadinessCheckId { get; }
        public GovernedTimestamp ReceivedUtc { get; }

        public InternalSeriesTransitionRecord(
            InternalSeriesTransitionId transitionId,
            ProjectId projectId,
            OperationId operationId,
            SeriesId receivingSeriesId,
            ReleaseId releaseId,
            FreezeId releaseFreezeId,
            ReadinessCheckId readinessCheckId,
            GovernedTimestamp receivedUtc)
        {
            if (transitionId.Value == Guid.Empty)
                throw new ArgumentException(
                    "TransitionId may not be empty.",
                    nameof(transitionId));

            if (projectId.Value == Guid.Empty)
                throw new ArgumentException(
                    "ProjectId may not be empty.",
                    nameof(projectId));

            if (operationId.Value == Guid.Empty)
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));

            if (releaseId.Value == Guid.Empty)
                throw new ArgumentException(
                    "ReleaseId may not be empty.",
                    nameof(releaseId));

            if (releaseFreezeId.Value == Guid.Empty)
                throw new ArgumentException(
                    "ReleaseFreezeId may not be empty.",
                    nameof(releaseFreezeId));

            if (readinessCheckId.Value == Guid.Empty)
                throw new ArgumentException(
                    "ReadinessCheckId may not be empty.",
                    nameof(readinessCheckId));

            if (string.IsNullOrWhiteSpace(receivingSeriesId.Value) ||
                receivingSeriesId == SeriesId.V)
                throw new ArgumentException(
                    "ReceivingSeriesId must identify a non-V series.",
                    nameof(receivingSeriesId));

            if (receivedUtc.Value == default)
                throw new ArgumentException(
                    "ReceivedUtc must be populated.",
                    nameof(receivedUtc));

            TransitionId = transitionId;
            ProjectId = projectId;
            OperationId = operationId;
            SourceSeriesId = SeriesId.V;
            ReceivingSeriesId = receivingSeriesId;
            ReleaseId = releaseId;
            ReleaseFreezeId = releaseFreezeId;
            ReadinessCheckId = readinessCheckId;
            ReceivedUtc = receivedUtc;
        }
    }
}