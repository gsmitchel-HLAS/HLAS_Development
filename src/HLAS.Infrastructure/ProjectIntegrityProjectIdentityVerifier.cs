using System;
using HLAS.Domain;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityProjectIdentityVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot,
            ProjectId expectedProjectId)
        {
            if (expectedProjectId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Expected ProjectId may not be empty.",
                    nameof(expectedProjectId));
            }

            ProjectManifest manifest =
                ProjectPackageReader.Open(projectRoot);

            if (manifest.ProjectId != expectedProjectId)
            {
                return new ProjectIntegrityCheckItem(
                    "PROJECT_IDENTITY",
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    "Opened project identity does not match the selected ProjectId.");
            }

            return new ProjectIntegrityCheckItem(
                "PROJECT_IDENTITY",
                ProjectIntegrityCheckStatus.Verified,
                "Project manifest, database metadata, and selected ProjectId agree.");
        }
    }
}