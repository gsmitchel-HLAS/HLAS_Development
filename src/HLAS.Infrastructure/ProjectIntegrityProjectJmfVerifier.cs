using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityProjectJmfVerifier
    {
        private static readonly HashSet<string> RequiredFields =
            new(StringComparer.Ordinal)
            {
                "PJT-001", "PJT-002", "PJT-003",
                "PJT-004", "PJT-005", "PJT-006",
                "PJT-007",
                "JMF-001", "JMF-002", "JMF-003",
                "JMF-004", "JMF-005", "JMF-006",
                "JMF-007", "JMF-008", "JMF-009",
                "JMF-010", "JMF-011", "JMF-012",
                "JMF-013"
            };

        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            _ = ProjectPackageReader.Open(projectRoot);

            string databasePath = Path.Combine(
                Path.GetFullPath(projectRoot),
                ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            string revisionId;

            using (SqliteCommand command =
                connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT
                        revision.ProjectJmfRevisionId,
                        revision.ApprovedUtc,
                        operation.CompletedUtc,
                        operation.Outcome,
                        revision.SourceEvidenceId,
                        source.EvidenceId
                    FROM HLAS_Project_JMF_Revisions AS revision
                    LEFT JOIN HLAS_Governed_Operations AS operation
                        ON operation.OperationId = revision.OperationId
                    LEFT JOIN HLAS_Source_Evidence_Catalog AS source
                        ON source.EvidenceId = revision.SourceEvidenceId
                    ORDER BY revision.RevisionNumber DESC
                    LIMIT 1;
                    """;

                using SqliteDataReader reader =
                    command.ExecuteReader();

                if (!reader.Read())
                {
                    return new ProjectIntegrityCheckItem(
                        "PROJECT_JMF_TRUTH",
                        ProjectIntegrityCheckStatus.Pending,
                        "No governed Project/JMF revision has been established.");
                }

                if (reader.IsDBNull(0) ||
                    !Guid.TryParse(reader.GetString(0), out Guid parsedRevision) ||
                    parsedRevision == Guid.Empty ||
                    reader.IsDBNull(1) ||
                    !DateTimeOffset.TryParse(reader.GetString(1), out _) ||
                    reader.IsDBNull(2) ||
                    !DateTimeOffset.TryParse(reader.GetString(2), out _) ||
                    reader.IsDBNull(3) ||
                    reader.GetString(3) != "SUCCESS" ||
                    reader.IsDBNull(4) ||
                    !Guid.TryParse(reader.GetString(4), out Guid sourceId) ||
                    sourceId == Guid.Empty ||
                    reader.IsDBNull(5))
                {
                    return Failure(
                        "The current Project/JMF revision has invalid approval, operation, or source lineage.");
                }

                revisionId = reader.GetString(0);
            }

            HashSet<string> remaining =
                new(RequiredFields, StringComparer.Ordinal);

            using SqliteCommand fieldCommand =
                connection.CreateCommand();

            fieldCommand.CommandText =
                """
                SELECT
                    field.FieldId,
                    field.FieldValue,
                    field.SourceEvidenceId,
                    source.EvidenceId
                FROM HLAS_Project_JMF_Revision_Fields AS field
                LEFT JOIN HLAS_Source_Evidence_Catalog AS source
                    ON source.EvidenceId = field.SourceEvidenceId
                WHERE field.ProjectJmfRevisionId = $revisionId;
                """;

            fieldCommand.Parameters.AddWithValue(
                "$revisionId",
                revisionId);

            using SqliteDataReader fieldReader =
                fieldCommand.ExecuteReader();

            while (fieldReader.Read())
            {
                if (fieldReader.IsDBNull(0) ||
                    !remaining.Remove(fieldReader.GetString(0)) ||
                    fieldReader.IsDBNull(1) ||
                    fieldReader.IsDBNull(2) ||
                    !Guid.TryParse(
                        fieldReader.GetString(2),
                        out Guid fieldSourceId) ||
                    fieldSourceId == Guid.Empty ||
                    fieldReader.IsDBNull(3))
                {
                    return Failure(
                        "The current Project/JMF revision contains invalid fields or incomplete source lineage.");
                }
            }

            if (remaining.Count != 0)
            {
                return Failure(
                    "The current Project/JMF revision does not contain the complete governed 20-field set.");
            }

            return new ProjectIntegrityCheckItem(
                "PROJECT_JMF_TRUTH",
                ProjectIntegrityCheckStatus.Verified,
                "The current approved Project/JMF revision has 20 governed fields, successful operation linkage, and registered source references. Historical revisions and unresolved reviews are checked separately.");
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "PROJECT_JMF_TRUTH",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }
    }
}