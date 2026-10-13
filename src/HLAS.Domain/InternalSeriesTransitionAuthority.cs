using System;

namespace HLAS.Domain
{
    public sealed class InternalSeriesTransitionAuthority
    {
        public string ProjectRoot { get; }
        public UserId UserId { get; }
        public ProjectRole ProjectRole { get; }

        private InternalSeriesTransitionAuthority(
            string projectRoot,
            UserId userId,
            ProjectRole projectRole)
        {
            ProjectRoot = projectRoot;
            UserId = userId;
            ProjectRole = projectRole;
        }

        internal static InternalSeriesTransitionAuthority Create(
            string projectRoot,
            UserId userId,
            ProjectRole projectRole)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            if (userId.Value == Guid.Empty ||
                (projectRole != ProjectRole.Senior &&
                 projectRole != ProjectRole.Admin))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Internal transition requires verified Senior/Admin authority.");
            }

            return new InternalSeriesTransitionAuthority(
                projectRoot,
                userId,
                projectRole);
        }
    }
}