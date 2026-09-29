using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReadinessGatewayTests
    {
        [TestMethod]
        public void Evaluate_CurrentDevelopmentalProject_PersistsTruthfulNotReadyResult()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessStatus.NotReady,
                    result.Check.OverallStatus);

                Assert.HasCount(
                    7,
                    result.Items);

                Assert.AreEqual(
                    ReadinessGateCode.ProjectIdentity,
                    result.Items[0].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[0].GateStatus);

                for (int index = 1; index < 6; index++)
                {
                    Assert.AreEqual(
                        ReadinessGateStatus.Blocked,
                        result.Items[index].GateStatus);
                }

                Assert.AreEqual(
                    ReadinessGateCode.FreezeCapability,
                    result.Items[6].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[6].GateStatus);
                Assert.AreEqual(
    0L,
    ReadRecordCount(
        projectRoot,
        "HLAS_Frozen_States"));

                Assert.IsFalse(
                    Directory.Exists(
                        Path.Combine(
                            projectRoot,
                            PreChangeFreezeService.FrozenStatesDirectoryName)));
                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Readiness_Checks"));

                Assert.AreEqual(
                    7L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Readiness_Check_Items"));

                VerifyStoredReadinessResult(
                    projectRoot,
                    result);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_ReadinessItemWriteFailure_RollsBackAndRecordsTechnicalFailure()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                string databasePath =
                    Path.Combine(
                        projectRoot,
                        ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder =
                    new()
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
                CREATE TRIGGER HLAS_Test_ForceReadinessItemFailure
                BEFORE INSERT ON HLAS_Readiness_Check_Items
                BEGIN
                    SELECT RAISE(
                        ABORT,
                        'forced developmental Readiness item failure');
                END;
                """;

                    command.ExecuteNonQuery();
                }

                Assert.ThrowsExactly<SqliteException>(
                    () => ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Readiness_Checks"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Readiness_Check_Items"));

                using SqliteConnection verificationConnection =
                    new(builder.ToString());

                verificationConnection.Open();

                using SqliteCommand verificationCommand =
                    verificationConnection.CreateCommand();

                verificationCommand.CommandText =
                    """
            SELECT
                Outcome,
                Decision
            FROM HLAS_Governed_Operations;
            """;

                using SqliteDataReader reader =
                    verificationCommand.ExecuteReader();

                Assert.IsTrue(
                    reader.Read());

                Assert.AreEqual(
                    "TECHNICAL FAILURE",
                    reader.GetString(0));

                Assert.AreEqual(
                    "READINESS TECHNICAL FAILURE",
                    reader.GetString(1));

                Assert.IsFalse(
                    reader.Read());
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_IncompleteProjectPackage_SafeStopsBeforeReadinessHistory()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                string manifestPath =
                    Path.Combine(
                        projectRoot,
                        ProjectPackageCreator.ManifestFileName);

                File.Delete(
                    manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReadinessGateway.Evaluate(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Technician));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Readiness_Checks"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Readiness_Check_Items"));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        private static void VerifyStoredReadinessResult(
            string projectRoot,
            ReadinessCheckResult result)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using (SqliteCommand readinessCommand =
                connection.CreateCommand())
            {
                readinessCommand.CommandText =
                    """
                    SELECT
                        OperationId,
                        OverallStatus
                    FROM HLAS_Readiness_Checks
                    WHERE ReadinessCheckId = $readinessCheckId;
                    """;

                readinessCommand.Parameters.AddWithValue(
                    "$readinessCheckId",
                    result.Check.ReadinessCheckId.Value.ToString("D"));

                using SqliteDataReader reader =
                    readinessCommand.ExecuteReader();

                Assert.IsTrue(
                    reader.Read());

                Assert.AreEqual(
                    result.Check.OperationId.Value.ToString("D"),
                    reader.GetString(0));

                Assert.AreEqual(
                    "NOT READY",
                    reader.GetString(1));

                Assert.IsFalse(
                    reader.Read());
            }

            using SqliteCommand operationCommand =
                connection.CreateCommand();

            operationCommand.CommandText =
                """
                SELECT Outcome
                FROM HLAS_Governed_Operations
                WHERE OperationId = $operationId;
                """;

            operationCommand.Parameters.AddWithValue(
                "$operationId",
                result.Check.OperationId.Value.ToString("D"));

            Assert.AreEqual(
                "SUCCESS",
                Convert.ToString(
                    operationCommand.ExecuteScalar()));
        }

        private static long ReadRecordCount(
            string projectRoot,
            string tableName)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
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
                $"SELECT COUNT(*) FROM {tableName};";

            return Convert.ToInt64(
                command.ExecuteScalar());
        }

        private static string CreateTemporaryTestRoot()
        {
            string testRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "HLAS_Tests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                testRoot);

            return testRoot;
        }

        private static void DeleteTemporaryTestRoot(
            string testRoot)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(testRoot))
            {
                Directory.Delete(
                    testRoot,
                    recursive: true);
            }
        }
    }
}