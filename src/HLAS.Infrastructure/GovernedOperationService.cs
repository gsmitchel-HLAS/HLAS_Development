using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class GovernedOperationService
    {
        public static GovernedOperationRecord Begin(
            string projectRoot,
            ProjectId projectId,
            UserId userId,
            SeriesId seriesId,
            ProjectRole projectRole)
        {
            ProjectManifest manifest =
                ProjectPackageReader.Open(projectRoot);

            if (manifest.ProjectId != projectId)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Operation ProjectId does not match the HLAS project package.");
            }

            GovernedOperationRecord operation =
                GovernedOperationRecord.Begin(
                    projectId,
                    userId,
                    seriesId,
                    projectRole);

            string databasePath =
                GetDatabasePath(projectRoot);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO HLAS_Governed_Operations
                    (
                        OperationId,
                        ProjectId,
                        UserId,
                        SeriesId,
                        ProjectRole,
                        StartedUtc,
                        CompletedUtc,
                        Outcome,
                        Decision,
                        Reason
                    )
                VALUES
                    (
                        $operationId,
                        $projectId,
                        $userId,
                        $seriesId,
                        $projectRole,
                        $startedUtc,
                        NULL,
                        NULL,
                        NULL,
                        NULL
                    );
                """;

            command.Parameters.AddWithValue(
                "$operationId",
                operation.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$projectId",
                operation.ProjectId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$userId",
                operation.UserId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$seriesId",
                operation.SeriesId.Value);

            command.Parameters.AddWithValue(
                "$projectRole",
                operation.ProjectRole.Value);

            command.Parameters.AddWithValue(
                "$startedUtc",
                operation.StartedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();

            transaction.Commit();

            return operation;
        }

        public static void FinalizeSuccess(
            string projectRoot,
            OperationId operationId,
            DecisionRecord? decisionRecord = null)
        {
            _ = ProjectPackageReader.Open(projectRoot);

            string databasePath =
                GetDatabasePath(projectRoot);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            FinalizeSuccessInTransaction(
                connection,
                transaction,
                operationId,
                decisionRecord);

            transaction.Commit();
        }

        public static void FinalizeSuccessInTransaction(
            SqliteConnection connection,
            SqliteTransaction transaction,
            OperationId operationId,
            DecisionRecord? decisionRecord = null)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            FinalizeOperation(
                connection,
                transaction,
                operationId,
                OperationOutcome.Success,
                decisionRecord);
        }

        public static void FinalizeSafeStop(
            string projectRoot,
            OperationId operationId,
            DecisionRecord decisionRecord)
        {
            _ = ProjectPackageReader.Open(projectRoot);

            string databasePath =
                GetDatabasePath(projectRoot);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            FinalizeOperation(
                connection,
                transaction,
                operationId,
                OperationOutcome.SafeStop,
                decisionRecord);

            transaction.Commit();
        }

        public static void FinalizeTechnicalFailure(
            string projectRoot,
            OperationId operationId,
            DecisionRecord decisionRecord)
        {
            _ = ProjectPackageReader.Open(projectRoot);

            string databasePath =
                GetDatabasePath(projectRoot);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            FinalizeOperation(
                connection,
                transaction,
                operationId,
                OperationOutcome.TechnicalFailure,
                decisionRecord);

            transaction.Commit();
        }

        private static void FinalizeOperation(
            SqliteConnection connection,
            SqliteTransaction transaction,
            OperationId operationId,
            OperationOutcome outcome,
            DecisionRecord? decisionRecord)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if ((outcome == OperationOutcome.SafeStop ||
                 outcome == OperationOutcome.TechnicalFailure) &&
                !decisionRecord.HasValue)
            {
                throw new ArgumentException(
                    "SAFE-STOP and TECHNICAL FAILURE require a DecisionRecord.",
                    nameof(decisionRecord));
            }

            GovernedTimestamp completedUtc =
                GovernedTimestamp.CreateNow();

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                UPDATE HLAS_Governed_Operations
                SET
                    CompletedUtc = $completedUtc,
                    Outcome = $outcome,
                    Decision = $decision,
                    Reason = $reason
                WHERE
                    OperationId = $operationId
                    AND CompletedUtc IS NULL
                    AND Outcome IS NULL;
                """;

            command.Parameters.AddWithValue(
                "$completedUtc",
                completedUtc.Value.ToString("O"));

            command.Parameters.AddWithValue(
                "$outcome",
                outcome.Value);

            command.Parameters.AddWithValue(
                "$decision",
                decisionRecord.HasValue
                    ? decisionRecord.Value.Decision
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "$reason",
                decisionRecord.HasValue
                    ? decisionRecord.Value.Reason
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "$operationId",
                operationId.Value.ToString("D"));

            int changedRows =
                command.ExecuteNonQuery();

            if (changedRows != 1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Governed operation does not exist or has already been finalized.");
            }
        }

        private static string GetDatabasePath(
            string projectRoot)
        {
            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            return Path.Combine(
                fullProjectRoot,
                ProjectPackageCreator.DatabaseFileName);
        }

        private static SqliteConnection OpenDatabase(
            string databasePath)
        {
            SqliteConnectionStringBuilder builder = new()
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