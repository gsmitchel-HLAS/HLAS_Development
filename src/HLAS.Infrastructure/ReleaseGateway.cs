using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ReleaseGateway
    {
        public static ReleaseRecord RequestRelease(
            string projectRoot,
            UserId userId,
            ProjectRole projectRole,
            ReadinessCheckId readinessCheckId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            if (projectRole != ProjectRole.Senior &&
    projectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release requires Senior or Admin authority.");
            }
            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            ProjectManifest manifest =
                ProjectPackageReader.Open(fullProjectRoot);

            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    fullProjectRoot,
                    manifest.ProjectId,
                    userId,
                    SeriesId.V,
                    projectRole,
                    "V_SERIES_RELEASE_REQUEST");

            try
            {
                VerifyReadyBasis(
     fullProjectRoot,
     manifest.ProjectId,
     readinessCheckId);

                ReleaseRecord releaseRecord =
                    new(
                        ReleaseId.CreateNew(),
                        readinessCheckId,
                        operation.OperationId,
                        GovernedTimestamp.CreateNow());

                using SqliteConnection connection =
                    OpenDatabase(fullProjectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertReleaseRecord(
                    connection,
                    transaction,
                    releaseRecord);

                GovernedOperationService.FinalizeSuccessInTransaction(
                    connection,
                    transaction,
                    operation.OperationId);

                transaction.Commit();

                return releaseRecord;
            }
            catch (Exception exception)
            {
                DecisionRecord decisionRecord =
                    new(
                        exception.Message.StartsWith(
                            "SAFE-STOP:",
                            StringComparison.Ordinal)
                            ? "RELEASE REQUEST SAFE-STOP"
                            : "RELEASE REQUEST TECHNICAL FAILURE",
                        exception.Message);

                if (exception.Message.StartsWith(
                        "SAFE-STOP:",
                        StringComparison.Ordinal))
                {
                    GovernedOperationService.FinalizeSafeStop(
                        fullProjectRoot,
                        operation.OperationId,
                        decisionRecord);
                }
                else
                {
                    GovernedOperationService.FinalizeTechnicalFailure(
                        fullProjectRoot,
                        operation.OperationId,
                        decisionRecord);
                }

                throw;
            }
        }

        private static void VerifyReadyBasis(
    string fullProjectRoot,
    ProjectId projectId,
    ReadinessCheckId readinessCheckId)
        {
            using SqliteConnection connection =
                OpenDatabase(fullProjectRoot);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
     """
    SELECT COUNT(*)
    FROM HLAS_Readiness_Checks AS readiness
    INNER JOIN HLAS_Governed_Operations AS operation
        ON operation.OperationId = readiness.OperationId
    WHERE
        readiness.ReadinessCheckId = $readinessCheckId
        AND readiness.OverallStatus = 'READY'
        AND operation.ProjectId = $projectId
        AND operation.SeriesId = 'V'
        AND operation.CompletedUtc IS NOT NULL
        AND operation.Outcome = 'SUCCESS'
        AND
        (
            SELECT COUNT(*)
            FROM HLAS_Readiness_Check_Items AS item
            WHERE item.ReadinessCheckId = readiness.ReadinessCheckId
        ) = 7
        AND NOT EXISTS
        (
            SELECT 1
            FROM HLAS_Readiness_Check_Items AS item
            WHERE
                item.ReadinessCheckId = readiness.ReadinessCheckId
                AND item.GateStatus <> 'PASS'
        );
    """;

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                readinessCheckId.Value.ToString("D"));
            command.Parameters.AddWithValue(
    "$projectId",
    projectId.Value.ToString("D"));
            long count =
                Convert.ToInt64(
                    command.ExecuteScalar());

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release requires a completed successful READY result.");
            }
            using SqliteCommand existingReleaseCommand =
    connection.CreateCommand();

            existingReleaseCommand.CommandText =
                """
    SELECT COUNT(*)
    FROM HLAS_Release_Records
    WHERE ReadinessCheckId = $readinessCheckId;
    """;

            existingReleaseCommand.Parameters.AddWithValue(
                "$readinessCheckId",
                readinessCheckId.Value.ToString("D"));
            command.Parameters.AddWithValue(
    "$projectId",
    projectId.Value.ToString("D"));
            long existingReleaseCount =
                Convert.ToInt64(
                    existingReleaseCommand.ExecuteScalar());

            if (existingReleaseCount > 0)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: This READY result has already been used for a Release request.");
            }
        }

        private static void InsertReleaseRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ReleaseRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO HLAS_Release_Records
                (
                    ReleaseId,
                    ReadinessCheckId,
                    OperationId,
                    ReleaseRequestedUtc
                )
                VALUES
                (
                    $releaseId,
                    $readinessCheckId,
                    $operationId,
                    $releaseRequestedUtc
                );
                """;

            command.Parameters.AddWithValue(
                "$releaseId",
                record.ReleaseId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                record.ReadinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$releaseRequestedUtc",
                record.ReleaseRequestedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }

        private static SqliteConnection OpenDatabase(
            string fullProjectRoot)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

            return new SqliteConnection(
                builder.ToString());
        }
    }
}