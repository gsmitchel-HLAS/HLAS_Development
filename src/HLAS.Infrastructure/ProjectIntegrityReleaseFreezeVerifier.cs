using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityReleaseFreezeVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string fullRoot = Path.GetFullPath(projectRoot);

            ProjectManifest project =
                ProjectPackageReader.Open(fullRoot);

            string databasePath = Path.Combine(
                fullRoot,
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

            using SqliteTransaction transaction =
                connection.BeginTransaction(deferred: true);

            List<ReleaseId> releaseIds = new();

            using (SqliteCommand command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText =
                    """
                    SELECT ReleaseId
                    FROM HLAS_Historical_Checkpoints
                    WHERE CheckpointType = 'RELEASE_FREEZE'
                    ORDER BY FrozenUtc, FreezeId;
                    """;

                using SqliteDataReader reader =
                    command.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.IsDBNull(0) ||
                        !Guid.TryParse(
                            reader.GetString(0),
                            out Guid parsedId) ||
                        parsedId == Guid.Empty)
                    {
                        return Failure(
                            "A Release Freeze has an invalid or missing ReleaseId.");
                    }

                    releaseIds.Add(new ReleaseId(parsedId));
                }
            }

            long unlinkedSuccesses = Count(
                connection,
                transaction,
                """
                SELECT COUNT(*)
                FROM HLAS_Governed_Operations AS operation
                WHERE operation.OperationKind =
                    'V_SERIES_RELEASE_FREEZE'
                  AND operation.Outcome = 'SUCCESS'
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM HLAS_Historical_Checkpoints AS checkpoint
                      WHERE checkpoint.OperationId =
                          operation.OperationId
                        AND checkpoint.CheckpointType =
                          'RELEASE_FREEZE'
                  );
                """);

            if (unlinkedSuccesses > 0)
            {
                return Failure(
                    "A successful Release Freeze operation has no linked RELEASE_FREEZE Historical Checkpoint.");
            }

            long pendingReleases = Count(
                connection,
                transaction,
                """
                SELECT COUNT(*)
                FROM HLAS_Release_Records AS release
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM HLAS_Historical_Checkpoints AS checkpoint
                    WHERE checkpoint.ReleaseId = release.ReleaseId
                      AND checkpoint.CheckpointType =
                        'RELEASE_FREEZE'
                );
                """);

            foreach (ReleaseId releaseId in releaseIds)
            {
                try
                {
                    _ = InternalSeriesTransitionBasisVerifier.Verify(
                        fullRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        releaseId);
                }
                catch (InvalidOperationException exception)
                    when (exception.InnerException is
                        FileNotFoundException or
                        DirectoryNotFoundException)
                {
                    return Failure(
                        $"Release Freeze {releaseId.Value:D} has missing historical files.");
                }
                catch (InvalidOperationException exception)
                    when (exception.InnerException is
                        IOException or UnauthorizedAccessException)
                {
                    return new ProjectIntegrityCheckItem(
                        "RELEASE_FREEZE",
                        ProjectIntegrityCheckStatus.NotChecked,
                        $"Release Freeze {releaseId.Value:D} could not be fully read.");
                }
                catch (InvalidOperationException exception)
                {
                    return Failure(
                        $"Release Freeze {releaseId.Value:D} failed integrity verification: {exception.Message}");
                }
            }

            if (pendingReleases > 0)
            {
                return new ProjectIntegrityCheckItem(
                    "RELEASE_FREEZE",
                    ProjectIntegrityCheckStatus.Pending,
                    $"{releaseIds.Count} completed Release Freeze checkpoint(s) verified; {pendingReleases} release request(s) have no Release Freeze. A release request alone is not RELEASED.");
            }

            if (releaseIds.Count == 0)
            {
                return new ProjectIntegrityCheckItem(
                    "RELEASE_FREEZE",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "No Release Freeze checkpoints exist to verify.");
            }

            return new ProjectIntegrityCheckItem(
                "RELEASE_FREEZE",
                ProjectIntegrityCheckStatus.Verified,
                $"{releaseIds.Count} Release Freeze checkpoint(s) passed released-basis, manifest, snapshot, Source Evidence, and frozen-custody verification. Other Historical Checkpoint types and receiving receipts are outside this check.");
        }

        private static long Count(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = sql;

            return Convert.ToInt64(
                command.ExecuteScalar());
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "RELEASE_FREEZE",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }
    }
}