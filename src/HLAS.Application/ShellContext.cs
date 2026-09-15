using HLAS.Domain;

namespace HLAS.Application
{
    public sealed record ShellContext
    {
        public const string ApplicationName = "HLAS";

        public ProjectId? ProjectId { get; }
        public UserId? UserId { get; }
        public ProjectRole? ProjectRole { get; }
        public SeriesId? SeriesId { get; }

        public ShellContext(
            ProjectId? projectId,
            UserId? userId,
            ProjectRole? projectRole,
            SeriesId? seriesId)
        {
            ProjectId = projectId;
            UserId = userId;
            ProjectRole = projectRole;
            SeriesId = seriesId;
        }
    }
}