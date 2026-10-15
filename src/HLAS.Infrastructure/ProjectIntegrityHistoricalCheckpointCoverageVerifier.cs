using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityHistoricalCheckpointCoverageVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            ProjectManifest project =
                ProjectPackageReader.Open(projectRoot);

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

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    checkpoint.FreezeId,
                    checkpoint.CheckpointType,
                    checkpoint.ReleaseId,
                    checkpoint.RelativeCheckpointPath,
                    checkpoint.ManifestRelativePath,
                    checkpoint.ManifestSha256Hex,
                    checkpoint.FrozenUtc,
                    operation.ProjectId
                FROM HLAS_Historical_Checkpoints AS checkpoint
                LEFT JOIN HLAS_Governed_Operations AS operation
                    ON operation.OperationId = checkpoint.OperationId;
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            int total = 0;
            int unsupported = 0;

            while (reader.Read())
            {
                total++;

                if (reader.IsDBNull(0) ||
                    !Guid.TryParse(reader.GetString(0), out Guid freezeId) ||
                    freezeId == Guid.Empty ||
                    reader.IsDBNull(1) ||
                    string.IsNullOrWhiteSpace(reader.GetString(1)) ||
                    reader.IsDBNull(3) ||
                    string.IsNullOrWhiteSpace(reader.GetString(3)) ||
                    reader.IsDBNull(4) ||
                    string.IsNullOrWhiteSpace(reader.GetString(4)) ||
                    reader.IsDBNull(5) ||
                    !ValidHash(reader.GetString(5)) ||
                    reader.IsDBNull(6) ||
                    !DateTimeOffset.TryParse(reader.GetString(6), out _) ||
                    reader.IsDBNull(7) ||
                    !Guid.TryParse(reader.GetString(7), out Guid projectId) ||
                    projectId != project.ProjectId.Value)
                {
                    return Failure(
                        "A Historical Checkpoint has invalid identity, manifest metadata, governed time, or project-operation linkage.");
                }

                if (!reader.IsDBNull(2) &&
                    (!Guid.TryParse(reader.GetString(2), out Guid releaseId) ||
                     releaseId == Guid.Empty))
                {
                    return Failure(
                        "A Historical Checkpoint has an invalid ReleaseId.");
                }

                if (reader.GetString(1) ==
                    HistoricalCheckpointRecord.ReleaseFreezeType)
                {
                    if (reader.IsDBNull(2))
                    {
                        return Failure(
                            "A RELEASE_FREEZE Historical Checkpoint has no ReleaseId.");
                    }
                }
                else
                {
                    unsupported++;
                }
            }

            if (total == 0)
            {
                return new ProjectIntegrityCheckItem(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "No generalized Historical Checkpoints exist to inspect.");
            }

            if (unsupported > 0)
            {
                return new ProjectIntegrityCheckItem(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    ProjectIntegrityCheckStatus.NotChecked,
                    $"{unsupported} Historical Checkpoint(s) use types without an approved content verifier. Their integrity is not claimed.");
            }

            return new ProjectIntegrityCheckItem(
                "HISTORICAL_CHECKPOINT_COVERAGE",
                ProjectIntegrityCheckStatus.Verified,
                $"{total} RELEASE_FREEZE Historical Checkpoint record(s) passed structural inventory checks. Their files and released basis require the separate Release Freeze integrity verifier.");
        }

        private static bool ValidHash(string value)
        {
            if (value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!Uri.IsHexDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "HISTORICAL_CHECKPOINT_COVERAGE",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }
    }
}