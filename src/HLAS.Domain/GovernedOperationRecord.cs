using System;

namespace HLAS.Domain
{
    public sealed record GovernedOperationRecord
    {
        public OperationId OperationId { get; }
        public ProjectId ProjectId { get; }
        public UserId UserId { get; }
        public SeriesId SeriesId { get; }
        public ProjectRole ProjectRole { get; }
        public GovernedTimestamp StartedUtc { get; }

        public GovernedTimestamp? CompletedUtc { get; }
        public OperationOutcome? Outcome { get; }
        public DecisionRecord? DecisionRecord { get; }

        public bool IsFinalized =>
            CompletedUtc.HasValue &&
            Outcome.HasValue;

        private GovernedOperationRecord(
            OperationId operationId,
            ProjectId projectId,
            UserId userId,
            SeriesId seriesId,
            ProjectRole projectRole,
            GovernedTimestamp startedUtc,
            GovernedTimestamp? completedUtc,
            OperationOutcome? outcome,
            DecisionRecord? decisionRecord)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (projectId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ProjectId may not be empty.",
                    nameof(projectId));
            }

            if (userId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "UserId may not be empty.",
                    nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(seriesId.Value))
            {
                throw new ArgumentException(
                    "SeriesId may not be blank.",
                    nameof(seriesId));
            }

            if (string.IsNullOrWhiteSpace(projectRole.Value))
            {
                throw new ArgumentException(
                    "ProjectRole may not be blank.",
                    nameof(projectRole));
            }

            if (startedUtc.Value == default)
            {
                throw new ArgumentException(
                    "StartedUtc must be populated.",
                    nameof(startedUtc));
            }

            OperationId = operationId;
            ProjectId = projectId;
            UserId = userId;
            SeriesId = seriesId;
            ProjectRole = projectRole;
            StartedUtc = startedUtc;
            CompletedUtc = completedUtc;
            Outcome = outcome;
            DecisionRecord = decisionRecord;
        }

        public static GovernedOperationRecord Begin(
            ProjectId projectId,
            UserId userId,
            SeriesId seriesId,
            ProjectRole projectRole)
        {
            return new GovernedOperationRecord(
                OperationId.CreateNew(),
                projectId,
                userId,
                seriesId,
                projectRole,
                GovernedTimestamp.CreateNow(),
                completedUtc: null,
                outcome: null,
                decisionRecord: null);
        }

        public GovernedOperationRecord FinalizeSuccess(
            DecisionRecord? decisionRecord = null)
        {
            EnsureOpen();

            return CreateFinalized(
                OperationOutcome.Success,
                decisionRecord);
        }

        public GovernedOperationRecord FinalizeSafeStop(
            DecisionRecord decisionRecord)
        {
            EnsureOpen();

            return CreateFinalized(
                OperationOutcome.SafeStop,
                decisionRecord);
        }

        public GovernedOperationRecord FinalizeTechnicalFailure(
            DecisionRecord decisionRecord)
        {
            EnsureOpen();

            return CreateFinalized(
                OperationOutcome.TechnicalFailure,
                decisionRecord);
        }

        private GovernedOperationRecord CreateFinalized(
            OperationOutcome outcome,
            DecisionRecord? decisionRecord)
        {
            return new GovernedOperationRecord(
                OperationId,
                ProjectId,
                UserId,
                SeriesId,
                ProjectRole,
                StartedUtc,
                GovernedTimestamp.CreateNow(),
                outcome,
                decisionRecord);
        }

        private void EnsureOpen()
        {
            if (IsFinalized)
            {
                throw new InvalidOperationException(
                    "A governed operation may only be finalized once.");
            }
        }
    }
}