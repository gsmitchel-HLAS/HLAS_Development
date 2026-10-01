using System;

namespace HLAS.Domain
{
    public sealed record ProjectJmfReviewCaseRecord
    {
        public ProjectJmfReviewCaseId ReviewCaseId { get; }
        public EvidenceId SourceEvidenceId { get; }
        public ProjectJmfRevisionId BaselineProjectJmfRevisionId { get; }
        public OperationId OpenedOperationId { get; }
        public GovernedTimestamp OpenedUtc { get; }
        public OperationId? ResolvedOperationId { get; }
        public GovernedTimestamp? ResolvedUtc { get; }

        public ProjectJmfReviewCaseRecord(
            ProjectJmfReviewCaseId reviewCaseId,
            EvidenceId sourceEvidenceId,
            ProjectJmfRevisionId baselineProjectJmfRevisionId,
            OperationId openedOperationId,
            GovernedTimestamp openedUtc,
            OperationId? resolvedOperationId,
            GovernedTimestamp? resolvedUtc)
        {
            if (reviewCaseId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ReviewCaseId may not be empty.",
                    nameof(reviewCaseId));
            }

            if (sourceEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "SourceEvidenceId may not be empty.",
                    nameof(sourceEvidenceId));
            }

            if (baselineProjectJmfRevisionId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "BaselineProjectJmfRevisionId may not be empty.",
                    nameof(baselineProjectJmfRevisionId));
            }

            if (openedOperationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OpenedOperationId may not be empty.",
                    nameof(openedOperationId));
            }

            if (openedUtc.Value == default)
            {
                throw new ArgumentException(
                    "OpenedUtc must be populated.",
                    nameof(openedUtc));
            }

            if (resolvedOperationId.HasValue != resolvedUtc.HasValue)
            {
                throw new ArgumentException(
                    "ResolvedOperationId and ResolvedUtc must either both be populated or both be null.");
            }

            if (resolvedOperationId.HasValue &&
                resolvedOperationId.Value.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ResolvedOperationId may not be empty.",
                    nameof(resolvedOperationId));
            }

            if (resolvedUtc.HasValue &&
                resolvedUtc.Value.Value == default)
            {
                throw new ArgumentException(
                    "ResolvedUtc must be populated when the review case is resolved.",
                    nameof(resolvedUtc));
            }

            ReviewCaseId = reviewCaseId;
            SourceEvidenceId = sourceEvidenceId;
            BaselineProjectJmfRevisionId = baselineProjectJmfRevisionId;
            OpenedOperationId = openedOperationId;
            OpenedUtc = openedUtc;
            ResolvedOperationId = resolvedOperationId;
            ResolvedUtc = resolvedUtc;
        }
    }
}