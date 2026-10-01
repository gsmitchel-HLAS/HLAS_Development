using System;

namespace HLAS.Domain
{
    public sealed record ProjectJmfRevisionRecord
    {
        public ProjectJmfRevisionId ProjectJmfRevisionId { get; }
        public int RevisionNumber { get; }
        public ProjectJmfRevisionId? PriorProjectJmfRevisionId { get; }
        public EvidenceId SourceEvidenceId { get; }
        public OperationId OperationId { get; }
        public GovernedTimestamp ApprovedUtc { get; }

        public ProjectJmfRevisionRecord(
            ProjectJmfRevisionId projectJmfRevisionId,
            int revisionNumber,
            ProjectJmfRevisionId? priorProjectJmfRevisionId,
            EvidenceId sourceEvidenceId,
            OperationId operationId,
            GovernedTimestamp approvedUtc)
        {
            if (projectJmfRevisionId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ProjectJmfRevisionId may not be empty.",
                    nameof(projectJmfRevisionId));
            }

            if (revisionNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(revisionNumber),
                    "RevisionNumber must be greater than zero.");
            }

            if (revisionNumber == 1 &&
                priorProjectJmfRevisionId.HasValue)
            {
                throw new ArgumentException(
                    "Revision 1 may not have a prior Project/JMF revision.",
                    nameof(priorProjectJmfRevisionId));
            }

            if (revisionNumber > 1 &&
                !priorProjectJmfRevisionId.HasValue)
            {
                throw new ArgumentException(
                    "Later Project/JMF revisions require a prior revision.",
                    nameof(priorProjectJmfRevisionId));
            }

            if (priorProjectJmfRevisionId.HasValue &&
                priorProjectJmfRevisionId.Value.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "PriorProjectJmfRevisionId may not be empty.",
                    nameof(priorProjectJmfRevisionId));
            }

            if (sourceEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "SourceEvidenceId may not be empty.",
                    nameof(sourceEvidenceId));
            }

            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (approvedUtc.Value == default)
            {
                throw new ArgumentException(
                    "ApprovedUtc must be populated.",
                    nameof(approvedUtc));
            }

            ProjectJmfRevisionId = projectJmfRevisionId;
            RevisionNumber = revisionNumber;
            PriorProjectJmfRevisionId = priorProjectJmfRevisionId;
            SourceEvidenceId = sourceEvidenceId;
            OperationId = operationId;
            ApprovedUtc = approvedUtc;
        }
    }
}