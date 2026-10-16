using System;

namespace HLAS.Infrastructure
{
    internal enum ProjectPackageIssue
    {
        MissingProjectRoot,
        MissingRequiredFiles,
        ProjectIdMismatch,
        InvalidManifestProjectId,
        InvalidManifestFormatMetadata,
        InvalidManifestCreatedUtc,
        MalformedManifest,
        MissingDatabaseMetadata,
        InvalidDatabaseProjectId,
        InvalidDatabaseSchemaMetadata,
        UnsupportedProjectFormat,
        UnsupportedDatabaseSchema
    }

    internal static class ProjectPackageFailureMarker
    {
        private const string IssueKey =
            "HLAS.ProjectPackageReader.Issue";

        internal static InvalidOperationException Create(
            string message,
            ProjectPackageIssue issue,
            Exception? innerException = null)
        {
            InvalidOperationException exception =
                innerException is null
                    ? new InvalidOperationException(message)
                    : new InvalidOperationException(
                        message, innerException);

            exception.Data[IssueKey] = issue;
            return exception;
        }

        internal static bool TryRead(
            InvalidOperationException exception,
            out ProjectPackageIssue issue)
        {
            if (exception.Data[IssueKey] is ProjectPackageIssue recorded)
            {
                issue = recorded;
                return true;
            }

            issue = default;
            return false;
        }
    }
}