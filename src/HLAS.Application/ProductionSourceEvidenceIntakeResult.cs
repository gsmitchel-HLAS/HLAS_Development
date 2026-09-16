using HLAS.Domain;

namespace HLAS.Application
{
    public sealed record ProductionSourceEvidenceIntakeResult
    {
        public ProjectId ProjectId { get; }
        public EvidenceCustodyRecord EvidenceRecord { get; }
        public string Message { get; }

        public ProductionSourceEvidenceIntakeResult(
            ProjectId projectId,
            EvidenceCustodyRecord evidenceRecord,
            string message)
        {
            ArgumentNullException.ThrowIfNull(evidenceRecord);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            ProjectId = projectId;
            EvidenceRecord = evidenceRecord;
            Message = message;
        }
    }
}