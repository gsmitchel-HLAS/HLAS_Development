using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class GovernedOperationServiceTests
    {
        [TestMethod]
        public void Begin_ValidOperation_PersistsOpenAttemptAndAuthorityContext()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        SeriesId.V,
                        ProjectRole.Admin);

                OperationDatabaseRecord persisted =
                    ReadOperation(
                        projectRoot,
                        operation.OperationId);

                Assert.AreEqual(
                    operation.OperationId.Value.ToString("D"),
                    persisted.OperationId);

                Assert.AreEqual(
                    manifest.ProjectId.Value.ToString("D"),
                    persisted.ProjectId);

                Assert.AreEqual(
                    userId.Value.ToString("D"),
                    persisted.UserId);

                Assert.AreEqual(
                    "V",
                    persisted.SeriesId);

                Assert.AreEqual(
                    "Admin",
                    persisted.ProjectRole);

                Assert.AreEqual(
                    operation.StartedUtc.Value.ToString("O"),
                    persisted.StartedUtc);

                Assert.IsNull(
                    persisted.CompletedUtc);

                Assert.IsNull(
                    persisted.Outcome);

                Assert.IsNull(
                    persisted.Decision);

                Assert.IsNull(
                    persisted.Reason);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void Begin_ProjectIdMismatch_SafeStopsWithoutAttempt()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => GovernedOperationService.Begin(
                            projectRoot,
                            ProjectId.CreateNew(),
                            UserId.CreateNew(),
                            SeriesId.V,
                            ProjectRole.Admin));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    0L,
                    ReadOperationCount(projectRoot));
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void FinalizeSuccess_OpenAttempt_PersistsSuccess()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Senior);

                GovernedOperationService.FinalizeSuccess(
                    projectRoot,
                    operation.OperationId);

                OperationDatabaseRecord persisted =
                    ReadOperation(
                        projectRoot,
                        operation.OperationId);

                Assert.IsNotNull(
                    persisted.CompletedUtc);

                Assert.AreEqual(
                    "SUCCESS",
                    persisted.Outcome);

                Assert.IsNull(
                    persisted.Decision);

                Assert.IsNull(
                    persisted.Reason);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void FinalizeSafeStop_PersistsExactDecisionAndReason()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Technician);

                DecisionRecord decision = new(
                    "  SAFE-STOP  ",
                    "  Exact governed reason.  ");

                GovernedOperationService.FinalizeSafeStop(
                    projectRoot,
                    operation.OperationId,
                    decision);

                OperationDatabaseRecord persisted =
                    ReadOperation(
                        projectRoot,
                        operation.OperationId);

                Assert.AreEqual(
                    "SAFE-STOP",
                    persisted.Outcome);

                Assert.AreEqual(
                    decision.Decision,
                    persisted.Decision);

                Assert.AreEqual(
                    decision.Reason,
                    persisted.Reason);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void FinalizeTechnicalFailure_PersistsExactDecisionAndReason()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Senior);

                DecisionRecord decision = new(
                    "TECHNICAL FAILURE",
                    "Bounded technical failure prevented completion.");

                GovernedOperationService.FinalizeTechnicalFailure(
                    projectRoot,
                    operation.OperationId,
                    decision);

                OperationDatabaseRecord persisted =
                    ReadOperation(
                        projectRoot,
                        operation.OperationId);

                Assert.AreEqual(
                    "TECHNICAL FAILURE",
                    persisted.Outcome);

                Assert.AreEqual(
                    decision.Decision,
                    persisted.Decision);

                Assert.AreEqual(
                    decision.Reason,
                    persisted.Reason);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void Finalize_AlreadyFinalizedAttempt_SafeStopsAndPreservesFirstOutcome()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin);

                GovernedOperationService.FinalizeSuccess(
                    projectRoot,
                    operation.OperationId);

                DecisionRecord secondDecision = new(
                    "SAFE-STOP",
                    "This second finalization must not replace history.");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => GovernedOperationService.FinalizeSafeStop(
                            projectRoot,
                            operation.OperationId,
                            secondDecision));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                OperationDatabaseRecord persisted =
                    ReadOperation(
                        projectRoot,
                        operation.OperationId);

                Assert.AreEqual(
                    "SUCCESS",
                    persisted.Outcome);

                Assert.IsNull(
                    persisted.Decision);

                Assert.IsNull(
                    persisted.Reason);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void FinalizeSuccessInTransaction_Rollback_LeavesAttemptOpen()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                using SqliteConnection connection =
                    OpenDatabase(databasePath);

                connection.Open();

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    GovernedOperationService
                        .FinalizeSuccessInTransaction(
                            connection,
                            transaction,
                            operation.OperationId);

                    transaction.Rollback();
                }

                OperationDatabaseRecord persisted =
                    ReadOperation(
                        projectRoot,
                        operation.OperationId);

                Assert.IsNull(
                    persisted.CompletedUtc);

                Assert.IsNull(
                    persisted.Outcome);

                Assert.IsNull(
                    persisted.Decision);

                Assert.IsNull(
                    persisted.Reason);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        private static OperationDatabaseRecord ReadOperation(
            string projectRoot,
            OperationId operationId)
        {
            string databasePath = Path.Combine(
                projectRoot,
                ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
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
                FROM HLAS_Governed_Operations
                WHERE OperationId = $operationId;
                """;

            command.Parameters.AddWithValue(
                "$operationId",
                operationId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "Governed operation was not found.");
            }

            return new OperationDatabaseRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6)
                    ? null
                    : reader.GetString(6),
                reader.IsDBNull(7)
                    ? null
                    : reader.GetString(7),
                reader.IsDBNull(8)
                    ? null
                    : reader.GetString(8),
                reader.IsDBNull(9)
                    ? null
                    : reader.GetString(9));
        }

        private static long ReadOperationCount(
            string projectRoot)
        {
            string databasePath = Path.Combine(
                projectRoot,
                ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT COUNT(*)
                FROM HLAS_Governed_Operations;
                """;

            return Convert.ToInt64(
                command.ExecuteScalar());
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

        private static string CreateTemporaryProjectRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
        }

        private static void DeleteTemporaryProjectRoot(
            string projectRoot)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(
                    projectRoot,
                    recursive: true);
            }
        }

        private sealed record OperationDatabaseRecord(
            string OperationId,
            string ProjectId,
            string UserId,
            string SeriesId,
            string ProjectRole,
            string StartedUtc,
            string? CompletedUtc,
            string? Outcome,
            string? Decision,
            string? Reason);
    }
}