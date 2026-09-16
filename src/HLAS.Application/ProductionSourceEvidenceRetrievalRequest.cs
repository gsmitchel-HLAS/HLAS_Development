using HLAS.Domain;

namespace HLAS.Application
{
    public sealed record ProductionSourceEvidenceRetrievalRequest
    {
        public ProjectId ProjectId { get; }
        public UserId UserId { get; }
        public ProjectRole ProjectRole { get; }
        public SeriesId SeriesId { get; }
        public EvidenceId EvidenceId { get; }

        public ProductionSourceEvidenceRetrievalRequest(
            ProjectId projectId,
            UserId userId,
            ProjectRole projectRole,
            SeriesId seriesId,
            EvidenceId evidenceId)
        {
            ProjectId = projectId;
            UserId = userId;
            ProjectRole = projectRole;
            SeriesId = seriesId;
            EvidenceId = evidenceId;
        }
    }
}