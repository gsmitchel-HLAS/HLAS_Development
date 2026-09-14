using System;

namespace HLAS.Domain
{
    public sealed record ProjectManifest
    {
        public const int CurrentProjectFormatVersion = 1;

        public ProjectId ProjectId { get; }
        public int ProjectFormatVersion { get; }
        public GovernedTimestamp CreatedUtc { get; }

        public ProjectManifest(
            ProjectId projectId,
            int projectFormatVersion,
            GovernedTimestamp createdUtc)
        {
            if (projectId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ProjectId may not be empty.",
                    nameof(projectId));
            }

            if (projectFormatVersion != CurrentProjectFormatVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectFormatVersion),
                    "Unsupported project format version.");
            }

            if (createdUtc.Value == default)
            {
                throw new ArgumentException(
                    "CreatedUtc must be populated.",
                    nameof(createdUtc));
            }

            ProjectId = projectId;
            ProjectFormatVersion = projectFormatVersion;
            CreatedUtc = createdUtc;
        }

        public static ProjectManifest CreateNew()
        {
            return new ProjectManifest(
                ProjectId.CreateNew(),
                CurrentProjectFormatVersion,
                GovernedTimestamp.CreateNow());
        }
    }
}