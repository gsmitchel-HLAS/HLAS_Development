using HLAS.Domain;

namespace HLAS.Application
{
    public sealed record ProductionSourceEvidenceIntakeRequest
    {
        public ProjectId ProjectId { get; }
        public UserId UserId { get; }
        public ProjectRole ProjectRole { get; }
        public SeriesId SeriesId { get; }
        public string SelectedSourceFilePath { get; }

        public ProductionSourceEvidenceIntakeRequest(
            ProjectId projectId,
            UserId userId,
            ProjectRole projectRole,
            SeriesId seriesId,
            string selectedSourceFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                selectedSourceFilePath);

            ProjectId = projectId;
            UserId = userId;
            ProjectRole = projectRole;
            SeriesId = seriesId;
            SelectedSourceFilePath = selectedSourceFilePath;
        }
    }
}