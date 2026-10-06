using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReleaseGatewayTests
    {
        [TestMethod]
        public void RequestRelease_ReadyBasis_PersistsReleaseRecord()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                UserId userId =
                    UserId.CreateNew();

                ReadinessCheckId readinessCheckId =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        "READY");

                ReleaseRecord result =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessCheckId);

                Assert.AreNotEqual(
                    Guid.Empty,
                    result.ReleaseId.Value);

                Assert.AreEqual(
                    readinessCheckId,
                    result.ReadinessCheckId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records
                    WHERE
                        ReleaseId = $releaseId
                        AND ReadinessCheckId = $readinessCheckId
                        AND OperationId = $operationId;
                    """;

                command.Parameters.AddWithValue(
                    "$releaseId",
                    result.ReleaseId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$readinessCheckId",
                    readinessCheckId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$operationId",
                    result.OperationId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
                using SqliteCommand detailCommand =
    connection.CreateCommand();

                detailCommand.CommandText =
                    """
    SELECT
        release.ReleaseRequestedUtc,
        operation.ProjectId,
        operation.UserId,
        operation.SeriesId,
        operation.ProjectRole,
        operation.OperationKind,
        operation.Outcome
    FROM HLAS_Release_Records AS release
    INNER JOIN HLAS_Governed_Operations AS operation
        ON operation.OperationId = release.OperationId
    WHERE release.ReleaseId = $releaseId;
    """;

                detailCommand.Parameters.AddWithValue(
                    "$releaseId",
                    result.ReleaseId.Value.ToString("D"));

                using SqliteDataReader reader =
                    detailCommand.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.IsTrue(
                    DateTimeOffset.TryParse(
                        reader.GetString(0),
                        out _));

                Assert.AreEqual(
                    manifest.ProjectId.Value.ToString("D"),
                    reader.GetString(1));

                Assert.AreEqual(
                    userId.Value.ToString("D"),
                    reader.GetString(2));

                Assert.AreEqual(
                    "V",
                    reader.GetString(3));

                Assert.AreEqual(
                    "Admin",
                    reader.GetString(4));

                Assert.AreEqual(
                    "V_SERIES_RELEASE_REQUEST",
                    reader.GetString(5));

                Assert.AreEqual(
                    "SUCCESS",
                    reader.GetString(6));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }

        [TestMethod]
        public void RequestRelease_NotReadyBasis_SafeStopsWithoutReleaseRecord()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                UserId userId =
                    UserId.CreateNew();

                ReadinessCheckId readinessCheckId =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        "NOT READY");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseGateway.RequestRelease(
                            projectRoot,
                            userId,
                            ProjectRole.Admin,
                            readinessCheckId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records;
                    """;

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
                using SqliteCommand outcomeCommand =
    connection.CreateCommand();

                outcomeCommand.CommandText =
                    """
    SELECT COUNT(*)
    FROM HLAS_Governed_Operations
    WHERE
        OperationKind = 'V_SERIES_RELEASE_REQUEST'
        AND Outcome = 'SAFE-STOP';
    """;

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        outcomeCommand.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void RequestRelease_SameReadyBasisTwice_SecondRequestSafeStops()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                UserId userId =
                    UserId.CreateNew();

                ReadinessCheckId readinessCheckId =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        "READY");

                _ = ReleaseGateway.RequestRelease(
                    projectRoot,
                    userId,
                    ProjectRole.Admin,
                    readinessCheckId);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseGateway.RequestRelease(
                            projectRoot,
                            userId,
                            ProjectRole.Admin,
                            readinessCheckId));
                using SqliteConnection connection =
    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
    SELECT COUNT(*)
    FROM HLAS_Release_Records
    WHERE ReadinessCheckId = $readinessCheckId;
    """;

                command.Parameters.AddWithValue(
                    "$readinessCheckId",
                    readinessCheckId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void RequestRelease_Technician_SafeStopsWithoutReleaseRecord()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                UserId userId =
                    UserId.CreateNew();

                ReadinessCheckId readinessCheckId =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        "READY");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseGateway.RequestRelease(
                            projectRoot,
                            userId,
                            ProjectRole.Technician,
                            readinessCheckId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void RequestRelease_TwoFreshReadyBases_PreservesBothReleaseRecords()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                UserId userId =
                    UserId.CreateNew();

                ReadinessCheckId firstReadinessCheckId =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        "READY");

                ReleaseRecord firstRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstReadinessCheckId);

                ReadinessCheckId secondReadinessCheckId =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        "READY");

                ReleaseRecord secondRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        secondReadinessCheckId);

                Assert.AreNotEqual(
                    firstRelease.ReleaseId,
                    secondRelease.ReleaseId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
            SELECT COUNT(*)
            FROM HLAS_Release_Records;
            """;

                Assert.AreEqual(
                    2L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        private static ReadinessCheckId CreateReadinessCheck(
            string projectRoot,
            ProjectId projectId,
            UserId userId,
            string overallStatus)
        {
            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    projectRoot,
                    projectId,
                    userId,
                    SeriesId.V,
                    ProjectRole.Admin);

            ReadinessCheckId readinessCheckId =
                ReadinessCheckId.CreateNew();

            using SqliteConnection connection =
                OpenDatabase(projectRoot);

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

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
                readinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$operationId",
                operation.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$overallStatus",
                overallStatus);

            command.Parameters.AddWithValue(
                "$evaluatedUtc",
                DateTimeOffset.UtcNow.ToString("O"));

            command.ExecuteNonQuery();
            string firstGateStatus =
    overallStatus == "READY"
        ? "PASS"
        : "BLOCKED";

            using SqliteCommand itemCommand =
                connection.CreateCommand();

            itemCommand.Transaction = transaction;

            itemCommand.CommandText =
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
        ($readinessCheckId, 1, 'PROJECT_IDENTITY', $firstGateStatus, NULL),
        ($readinessCheckId, 2, 'SOURCE_EVIDENCE', 'PASS', NULL),
        ($readinessCheckId, 3, 'PROJECT_JMF_TRUTH', 'PASS', NULL),
        ($readinessCheckId, 4, 'MAINTENANCE', 'PASS', NULL),
        ($readinessCheckId, 5, 'PROJECT_JMF_REVIEW', 'PASS', NULL),
        ($readinessCheckId, 6, 'LINEAGE', 'PASS', NULL),
        ($readinessCheckId, 7, 'FREEZE_CAPABILITY', 'PASS', NULL);
    """;

            itemCommand.Parameters.AddWithValue(
                "$readinessCheckId",
                readinessCheckId.Value.ToString("D"));

            itemCommand.Parameters.AddWithValue(
                "$firstGateStatus",
                firstGateStatus);

            itemCommand.ExecuteNonQuery();
            GovernedOperationService.FinalizeSuccessInTransaction(
                connection,
                transaction,
                operation.OperationId);

            transaction.Commit();

            return readinessCheckId;
        }

        private static SqliteConnection OpenDatabase(
            string projectRoot)
        {
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
    }
}