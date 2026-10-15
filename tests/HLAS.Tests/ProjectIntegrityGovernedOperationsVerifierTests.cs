using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityGovernedOperationsVerifierTests
    {
        [TestMethod]
        public void Check_EmptyLedger_ReturnsNotChecked()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    "GOVERNED_OPERATIONS",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);

                Assert.AreEqual(0L, CountOperations(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_OpenOperation_ReturnsPendingWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);
                BeginOperation(root);

                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(root));

                Assert.AreEqual(1L, CountOperations(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_SuccessfulOperation_ReturnsVerified()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                GovernedOperationRecord operation =
                    BeginOperation(root);

                GovernedOperationService.FinalizeSuccess(
                    root,
                    operation.OperationId);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                Assert.AreEqual(1L, CountOperations(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_LegitimateFailures_AreNotCorruption()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                GovernedOperationRecord stopped =
                    BeginOperation(root);

                GovernedOperationService.FinalizeSafeStop(
                    root,
                    stopped.OperationId,
                    new DecisionRecord(
                        "SAFE-STOP",
                        "Developmental prerequisite was not met."));

                GovernedOperationRecord failed =
                    BeginOperation(root);

                GovernedOperationService.FinalizeTechnicalFailure(
                    root,
                    failed.OperationId,
                    new DecisionRecord(
                        "TECHNICAL FAILURE",
                        "Developmental technical failure recorded."));

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                Assert.AreEqual(2L, CountOperations(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_TamperedOperationProject_ReturnsIntegrityFailure()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                GovernedOperationRecord operation =
                    BeginOperation(root);

                GovernedOperationService.FinalizeSuccess(
                    root,
                    operation.OperationId);

                string databasePath = DatabasePath(root);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
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
                        UPDATE HLAS_Governed_Operations
                        SET ProjectId = $wrongProject
                        WHERE OperationId = $operationId;
                        """;

                    command.Parameters.AddWithValue(
                        "$wrongProject",
                        ProjectId.CreateNew().Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operation.OperationId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static GovernedOperationRecord BeginOperation(
            string root)
        {
            ProjectManifest manifest =
                ProjectPackageReader.Open(root);

            return GovernedOperationService.Begin(
                root,
                manifest.ProjectId,
                UserId.CreateNew(),
                SeriesId.V,
                ProjectRole.Admin,
                "DEVELOPMENTAL_TEST");
        }

        private static long CountOperations(string root)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = DatabasePath(root),
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
                SELECT COUNT(*)
                FROM HLAS_Governed_Operations;
                """;

            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static byte[] ReadDatabaseBytes(string root)
        {
            SqliteConnection.ClearAllPools();
            return File.ReadAllBytes(DatabasePath(root));
        }

        private static string DatabasePath(string root)
        {
            return Path.Combine(
                root,
                ProjectPackageCreator.DatabaseFileName);
        }

        private static string NewRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
        }

        private static void DeleteRoot(string root)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}