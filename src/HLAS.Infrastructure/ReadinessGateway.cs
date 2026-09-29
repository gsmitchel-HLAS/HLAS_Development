using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ReadinessGateway
    {
        public static ReadinessCheckResult Evaluate(
            string projectRoot,
            UserId userId,
            ProjectRole projectRole)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

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
                    projectRole);

            try
            {
                ReadinessCheckId readinessCheckId =
                    ReadinessCheckId.CreateNew();

                GovernedTimestamp evaluatedUtc =
                    GovernedTimestamp.CreateNow();

                List<ReadinessCheckItemRecord> items =
                [
                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        1,
                        ReadinessGateCode.ProjectIdentity,
                        ReadinessGateStatus.Pass,
                        "Governed HLAS project identity opened successfully."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        "Required Source Evidence completeness is not yet implemented in the C# Readiness evaluator."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        3,
                        ReadinessGateCode.ProjectJmfTruth,
                        ReadinessGateStatus.Blocked,
                        "Project/JMF governed truth is not yet represented completely in the C# project database."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        4,
                        ReadinessGateCode.Maintenance,
                        ReadinessGateStatus.Blocked,
                        "Unresolved governed maintenance evaluation is not yet implemented."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        5,
                        ReadinessGateCode.ProjectJmfReview,
                        ReadinessGateStatus.Blocked,
                        "Project/JMF review state is not yet represented completely in the C# project database."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        6,
                        ReadinessGateCode.Lineage,
                        ReadinessGateStatus.Blocked,
                        "Current-lineage consistency evaluation is not yet implemented."),

                  EvaluateFreezeCapability(
    fullProjectRoot,
    readinessCheckId)
                ];

                ReadinessCheckRecord check =
                    new(
                        readinessCheckId,
                        operation.OperationId,
                      items.Exists(
    item => item.GateStatus == ReadinessGateStatus.Blocked)
        ? ReadinessStatus.NotReady
        : ReadinessStatus.Ready,
                        evaluatedUtc);

                ReadinessCheckResult result =
                    new(
                        check,
                        items);

                using SqliteConnection connection =
                    OpenDatabase(fullProjectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertReadinessCheck(
                    connection,
                    transaction,
                    result.Check);

                foreach (ReadinessCheckItemRecord item in result.Items)
                {
                    InsertReadinessCheckItem(
                        connection,
                        transaction,
                        item);
                }

                GovernedOperationService.FinalizeSuccessInTransaction(
                    connection,
                    transaction,
                    operation.OperationId);

                transaction.Commit();

                return result;
            }
            catch (Exception exception)
            {
                DecisionRecord decisionRecord =
                    new(
                        IsSafeStopException(exception)
                            ? "READINESS SAFE-STOP"
                            : "READINESS TECHNICAL FAILURE",
                        exception.Message);

                if (IsSafeStopException(exception))
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
        private static ReadinessCheckItemRecord EvaluateFreezeCapability(
    string fullProjectRoot,
    ReadinessCheckId readinessCheckId)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };

            using (SqliteConnection connection =
                new(builder.ToString()))
            {
                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE
                type = 'table'
                AND name = 'HLAS_Frozen_States';
            """;

                long tableCount =
                    Convert.ToInt64(
                        command.ExecuteScalar());

                if (tableCount != 1)
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        7,
                        ReadinessGateCode.FreezeCapability,
                        ReadinessGateStatus.Blocked,
                        "Required HLAS_Frozen_States schema is not available.");
                }
            }

            string frozenStatesRoot =
                Path.Combine(
                    fullProjectRoot,
                    PreChangeFreezeService.FrozenStatesDirectoryName);

            string probeParent =
                Directory.Exists(frozenStatesRoot)
                    ? frozenStatesRoot
                    : fullProjectRoot;

            string probeDirectory =
                Path.Combine(
                    probeParent,
                    $".HLAS_Readiness_FreezeProbe_{Guid.NewGuid():N}");

            bool probeDirectoryCreated = false;

            try
            {
                Directory.CreateDirectory(
                    probeDirectory);

                probeDirectoryCreated = true;

                string probeFilePath =
                    Path.Combine(
                        probeDirectory,
                        "probe.tmp");

                File.WriteAllText(
                    probeFilePath,
                    "HLAS readiness freeze-capability probe.");

                File.Delete(
                    probeFilePath);

                Directory.Delete(
                    probeDirectory);

                probeDirectoryCreated = false;

                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    7,
                    ReadinessGateCode.FreezeCapability,
                    ReadinessGateStatus.Pass,
                    "Freeze schema exists and the controlled-store location passed a non-destructive write/delete capability probe.");
            }
            catch (UnauthorizedAccessException)
            {
                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    7,
                    ReadinessGateCode.FreezeCapability,
                    ReadinessGateStatus.Blocked,
                    "The controlled freeze-store location is not writable.");
            }
            catch (IOException)
            {
                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    7,
                    ReadinessGateCode.FreezeCapability,
                    ReadinessGateStatus.Blocked,
                    "The controlled freeze-store location could not complete the required write/delete capability probe.");
            }
            finally
            {
                if (probeDirectoryCreated &&
                    Directory.Exists(probeDirectory))
                {
                    try
                    {
                        Directory.Delete(
                            probeDirectory,
                            recursive: true);
                    }
                    catch
                    {
                        // Best-effort cleanup only.
                        // A failed probe never becomes a governed freeze event.
                    }
                }
            }
        }
        private static void InsertReadinessCheck(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ReadinessCheckRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO HLAS_Readiness_Checks
                (
                    ReadinessCheckId,
                    OperationId,
                    OverallStatus,
                    EvaluatedUtc
                )
                VALUES
                (
                    $readinessCheckId,
                    $operationId,
                    $overallStatus,
                    $evaluatedUtc
                );
                """;

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                record.ReadinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$overallStatus",
                record.OverallStatus.Value);

            command.Parameters.AddWithValue(
                "$evaluatedUtc",
                record.EvaluatedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }

        private static void InsertReadinessCheckItem(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ReadinessCheckItemRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO HLAS_Readiness_Check_Items
                (
                    ReadinessCheckId,
                    GateSequence,
                    GateCode,
                    GateStatus,
                    Detail
                )
                VALUES
                (
                    $readinessCheckId,
                    $gateSequence,
                    $gateCode,
                    $gateStatus,
                    $detail
                );
                """;

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                record.ReadinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$gateSequence",
                record.GateSequence);

            command.Parameters.AddWithValue(
                "$gateCode",
                record.GateCode.Value);

            command.Parameters.AddWithValue(
                "$gateStatus",
                record.GateStatus.Value);

            command.Parameters.AddWithValue(
                "$detail",
                (object?)record.Detail ?? DBNull.Value);

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

        private static bool IsSafeStopException(
            Exception exception)
        {
            return exception.Message.StartsWith(
                "SAFE-STOP:",
                StringComparison.Ordinal);
        }
    }
}