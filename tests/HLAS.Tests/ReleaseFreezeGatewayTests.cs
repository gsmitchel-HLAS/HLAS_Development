using System;
using System.IO;
using System.Text.Json;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReleaseFreezeGatewayTests
    {
        [TestMethod]
        public void CreateReleaseFreeze_ValidRelease_CreatesHistoricalCheckpoint()
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
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessCheckId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                Assert.AreEqual(
                    release.ReleaseId,
                    checkpoint.ReleaseId);

                Assert.AreEqual(
                    HistoricalCheckpointRecord.ReleaseFreezeType,
                    checkpoint.CheckpointType);

                Assert.IsTrue(
                    Directory.Exists(
                        Path.Combine(
                            projectRoot,
                            checkpoint.RelativeCheckpointPath)));

                Assert.IsTrue(
                    File.Exists(
                        Path.Combine(
                            projectRoot,
                            checkpoint.ManifestRelativePath)));
                string manifestPath =
    Path.Combine(
        projectRoot,
        checkpoint.ManifestRelativePath);

                using JsonDocument manifestDocument =
                    JsonDocument.Parse(
                        File.ReadAllText(
                            manifestPath));

                JsonElement manifestRoot =
                    manifestDocument.RootElement;

                Assert.AreEqual(
                    release.ReleaseId.Value.ToString("D"),
                    manifestRoot.GetProperty("ReleaseId").GetString());

                Assert.AreEqual(
                    readinessCheckId.Value.ToString("D"),
                    manifestRoot.GetProperty("ReadinessCheckId").GetString());

                Assert.AreEqual(
                    checkpoint.FreezeId.Value.ToString("D"),
                    manifestRoot.GetProperty("FreezeId").GetString());
                string frozenDatabasePath =
     Path.Combine(
         projectRoot,
         checkpoint.RelativeCheckpointPath,
         ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder frozenBuilder =
                    new()
                    {
                        DataSource = frozenDatabasePath,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    };

                using SqliteConnection frozenConnection =
                    new(frozenBuilder.ToString());

                frozenConnection.Open();

                using SqliteCommand frozenReleaseCommand =
                    frozenConnection.CreateCommand();

                frozenReleaseCommand.CommandText =
                    """
    SELECT COUNT(*)
    FROM HLAS_Release_Records
    WHERE ReleaseId = $releaseId;
    """;

                frozenReleaseCommand.Parameters.AddWithValue(
                    "$releaseId",
                    release.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        frozenReleaseCommand.ExecuteScalar()));

                using SqliteCommand frozenCheckpointCommand =
                    frozenConnection.CreateCommand();

                frozenCheckpointCommand.CommandText =
                    """
    SELECT COUNT(*)
    FROM HLAS_Historical_Checkpoints;
    """;

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        frozenCheckpointCommand.ExecuteScalar()));

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Historical_Checkpoints
                    WHERE
                        FreezeId = $freezeId
                        AND ReleaseId = $releaseId
                        AND CheckpointType = 'RELEASE_FREEZE';
                    """;

                command.Parameters.AddWithValue(
                    "$freezeId",
                    checkpoint.FreezeId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$releaseId",
                    release.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_SameReleaseTwice_SafeStopsWithoutSecondCheckpoint()
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
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessCheckId);

                HistoricalCheckpointRecord firstCheckpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseFreezeGateway.CreateReleaseFreeze(
                            projectRoot,
                            userId,
                            ProjectRole.Admin,
                            release.ReleaseId));

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
            FROM HLAS_Historical_Checkpoints
            WHERE ReleaseId = $releaseId;
            """;

                command.Parameters.AddWithValue(
                    "$releaseId",
                    release.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));

                Assert.IsTrue(
                    Directory.Exists(
                        Path.Combine(
                            projectRoot,
                            firstCheckpoint.RelativeCheckpointPath)));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_GovernedStateChangedAfterRelease_SafeStopsWithoutCheckpoint()
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
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessCheckId);

                GovernedOperationRecord interveningOperation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        SeriesId.V,
                        ProjectRole.Admin);

                using (SqliteConnection connection =
                    OpenDatabase(projectRoot))
                {
                    connection.Open();

                    using SqliteTransaction transaction =
                        connection.BeginTransaction();

                    GovernedOperationService.FinalizeSuccessInTransaction(
                        connection,
                        transaction,
                        interveningOperation.OperationId);

                    transaction.Commit();
                }

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseFreezeGateway.CreateReleaseFreeze(
                            projectRoot,
                            userId,
                            ProjectRole.Admin,
                            release.ReleaseId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                using SqliteConnection verificationConnection =
                    OpenDatabase(projectRoot);

                verificationConnection.Open();

                using SqliteCommand command =
                    verificationConnection.CreateCommand();

                command.CommandText =
                    """
            SELECT COUNT(*)
            FROM HLAS_Historical_Checkpoints
            WHERE ReleaseId = $releaseId;
            """;

                command.Parameters.AddWithValue(
                    "$releaseId",
                    release.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_Technician_SafeStopsWithoutCheckpoint()
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
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessCheckId);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseFreezeGateway.CreateReleaseFreeze(
                            projectRoot,
                            userId,
                            ProjectRole.Technician,
                            release.ReleaseId));

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
            FROM HLAS_Historical_Checkpoints
            WHERE ReleaseId = $releaseId;
            """;

                command.Parameters.AddWithValue(
                    "$releaseId",
                    release.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_FailedInterveningOperation_DoesNotBlockReleaseFreeze()
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
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessCheckId);

                GovernedOperationRecord failedOperation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        SeriesId.V,
                        ProjectRole.Admin);

                GovernedOperationService.FinalizeSafeStop(
                    projectRoot,
                    failedOperation.OperationId,
                    new DecisionRecord(
                        "DEVELOPMENTAL SAFE-STOP",
                        "Intentional test SAFE-STOP."));

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                Assert.AreEqual(
                    release.ReleaseId,
                    checkpoint.ReleaseId);
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_SeniorAuthority_Succeeds()
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
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Senior,
                        readinessCheckId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Senior,
                        release.ReleaseId);

                Assert.AreEqual(
                    release.ReleaseId,
                    checkpoint.ReleaseId);
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_InvalidReleaseId_SafeStopsWithoutCheckpoint()
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

                _ =
                    CreateReadinessCheck(
                        projectRoot,
                        manifest.ProjectId,
                        userId);

                ReleaseId invalidReleaseId =
                    ReleaseId.CreateNew();

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ReleaseFreezeGateway.CreateReleaseFreeze(
                            projectRoot,
                            userId,
                            ProjectRole.Admin,
                            invalidReleaseId));

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
            FROM HLAS_Historical_Checkpoints;
            """;

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_LaterGovernedReadiness_PreservesPriorReleasedCheckpoint()
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
                        userId);

                ReleaseRecord firstRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstReadinessCheckId);

                HistoricalCheckpointRecord firstCheckpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstRelease.ReleaseId);

                string firstManifestPath =
      Path.Combine(
          projectRoot,
          firstCheckpoint.ManifestRelativePath);

                byte[] firstManifestBytesBeforeLaterWork =
                    File.ReadAllBytes(
                        firstManifestPath);

                string firstManifestSha256BeforeLaterWork =
                    firstCheckpoint.ManifestSha256Hex;

                ReadinessCheckResult laterReadiness =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        userId,
                        ProjectRole.Admin);

                Assert.AreNotEqual(
                    firstReadinessCheckId,
                    laterReadiness.Check.ReadinessCheckId);
                byte[] firstManifestBytesAfterLaterWork =
                    File.ReadAllBytes(
                        firstManifestPath);

                CollectionAssert.AreEqual(
                    firstManifestBytesBeforeLaterWork,
                    firstManifestBytesAfterLaterWork);

                string firstManifestSha256AfterLaterWork =
                    Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            firstManifestBytesAfterLaterWork))
                    .ToLowerInvariant();

                Assert.AreEqual(
                    firstManifestSha256BeforeLaterWork,
                    firstManifestSha256AfterLaterWork);
                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand checkpointCommand =
                    connection.CreateCommand();

                checkpointCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Historical_Checkpoints AS checkpoint
                    INNER JOIN HLAS_Governed_Operations AS operation
                        ON operation.OperationId = checkpoint.OperationId
                    WHERE
                        checkpoint.FreezeId = $freezeId
                        AND checkpoint.ReleaseId = $releaseId
                        AND checkpoint.CheckpointType = 'RELEASE_FREEZE'
                        AND operation.CompletedUtc IS NOT NULL
                        AND operation.Outcome = 'SUCCESS';
                    """;

                checkpointCommand.Parameters.AddWithValue(
                    "$freezeId",
                    firstCheckpoint.FreezeId.Value.ToString("D"));

                checkpointCommand.Parameters.AddWithValue(
                    "$releaseId",
                    firstRelease.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        checkpointCommand.ExecuteScalar()));

                using SqliteCommand releaseCommand =
                    connection.CreateCommand();

                releaseCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records
                    WHERE
                        ReleaseId = $releaseId
                        AND ReadinessCheckId = $readinessCheckId;
                    """;

                releaseCommand.Parameters.AddWithValue(
                    "$releaseId",
                    firstRelease.ReleaseId.Value.ToString("D"));

                releaseCommand.Parameters.AddWithValue(
                    "$readinessCheckId",
                    firstReadinessCheckId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        releaseCommand.ExecuteScalar()));

                Assert.IsTrue(
                    Directory.Exists(
                        Path.Combine(
                            projectRoot,
                            firstCheckpoint.RelativeCheckpointPath)));

                Assert.IsTrue(
                    File.Exists(
                        Path.Combine(
                            projectRoot,
                            firstCheckpoint.ManifestRelativePath)));
            }
            finally
            {
                DeleteTemporaryProjectRoot(
                    projectRoot);
            }
        }
        [TestMethod]
        public void CreateReleaseFreeze_FreshRereleaseAfterPriorFreeze_CreatesDistinctCheckpoint()
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
                        userId);

                ReleaseRecord firstRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstReadinessCheckId);

                HistoricalCheckpointRecord firstCheckpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstRelease.ReleaseId);

                ReadinessCheckId secondReadinessCheckId =
     CreateReadinessCheck(
         projectRoot,
         manifest.ProjectId,
         userId);

                ReleaseRecord secondRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        secondReadinessCheckId);

                HistoricalCheckpointRecord secondCheckpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        secondRelease.ReleaseId);

                Assert.AreNotEqual(
     firstReadinessCheckId,
     secondReadinessCheckId);

                Assert.AreNotEqual(
                    firstRelease.ReleaseId,
                    secondRelease.ReleaseId);

                Assert.AreNotEqual(
                    firstCheckpoint.FreezeId,
                    secondCheckpoint.FreezeId);

                Assert.AreNotEqual(
                    firstCheckpoint.RelativeCheckpointPath,
                    secondCheckpoint.RelativeCheckpointPath);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand releaseCommand =
                    connection.CreateCommand();

                releaseCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records;
                    """;

                Assert.AreEqual(
                    2L,
                    Convert.ToInt64(
                        releaseCommand.ExecuteScalar()));

                using SqliteCommand checkpointCommand =
                    connection.CreateCommand();

                checkpointCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Historical_Checkpoints AS checkpoint
                    INNER JOIN HLAS_Governed_Operations AS operation
                        ON operation.OperationId = checkpoint.OperationId
                    WHERE
                        checkpoint.CheckpointType = 'RELEASE_FREEZE'
                        AND operation.CompletedUtc IS NOT NULL
                        AND operation.Outcome = 'SUCCESS';
                    """;

                Assert.AreEqual(
                    2L,
                    Convert.ToInt64(
                        checkpointCommand.ExecuteScalar()));

                Assert.IsTrue(
                    Directory.Exists(
                        Path.Combine(
                            projectRoot,
                            firstCheckpoint.RelativeCheckpointPath)));
                string firstFrozenDatabasePath =
                   Path.Combine(
                       projectRoot,
                       firstCheckpoint.RelativeCheckpointPath,
                       ProjectPackageCreator.DatabaseFileName);

                string secondFrozenDatabasePath =
                    Path.Combine(
                        projectRoot,
                        secondCheckpoint.RelativeCheckpointPath,
                        ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder firstFrozenBuilder =
                    new()
                    {
                        DataSource = firstFrozenDatabasePath,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    };

                using SqliteConnection firstFrozenConnection =
                    new(firstFrozenBuilder.ToString());

                firstFrozenConnection.Open();

                using SqliteCommand firstFrozenReleaseCommand =
                    firstFrozenConnection.CreateCommand();

                firstFrozenReleaseCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records
                    WHERE ReleaseId = $releaseId;
                    """;

                firstFrozenReleaseCommand.Parameters.AddWithValue(
                    "$releaseId",
                    firstRelease.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        firstFrozenReleaseCommand.ExecuteScalar()));

                using SqliteCommand firstFrozenLaterReleaseCommand =
                    firstFrozenConnection.CreateCommand();

                firstFrozenLaterReleaseCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records
                    WHERE ReleaseId = $releaseId;
                    """;

                firstFrozenLaterReleaseCommand.Parameters.AddWithValue(
                    "$releaseId",
                    secondRelease.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        firstFrozenLaterReleaseCommand.ExecuteScalar()));

                using SqliteCommand firstFrozenCheckpointCommand =
                    firstFrozenConnection.CreateCommand();

                firstFrozenCheckpointCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Historical_Checkpoints;
                    """;

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        firstFrozenCheckpointCommand.ExecuteScalar()));

                SqliteConnectionStringBuilder secondFrozenBuilder =
                    new()
                    {
                        DataSource = secondFrozenDatabasePath,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    };

                using SqliteConnection secondFrozenConnection =
                    new(secondFrozenBuilder.ToString());

                secondFrozenConnection.Open();

                using SqliteCommand secondFrozenReleaseCommand =
                    secondFrozenConnection.CreateCommand();

                secondFrozenReleaseCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Release_Records
                    WHERE ReleaseId IN ($firstReleaseId, $secondReleaseId);
                    """;

                secondFrozenReleaseCommand.Parameters.AddWithValue(
                    "$firstReleaseId",
                    firstRelease.ReleaseId.Value.ToString("D"));

                secondFrozenReleaseCommand.Parameters.AddWithValue(
                    "$secondReleaseId",
                    secondRelease.ReleaseId.Value.ToString("D"));

                Assert.AreEqual(
                    2L,
                    Convert.ToInt64(
                        secondFrozenReleaseCommand.ExecuteScalar()));

                using SqliteCommand secondFrozenCheckpointCommand =
                    secondFrozenConnection.CreateCommand();

                secondFrozenCheckpointCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Historical_Checkpoints
                    WHERE FreezeId = $freezeId;
                    """;

                secondFrozenCheckpointCommand.Parameters.AddWithValue(
                    "$freezeId",
                    firstCheckpoint.FreezeId.Value.ToString("D"));

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        secondFrozenCheckpointCommand.ExecuteScalar()));

                using SqliteCommand secondFrozenOwnCheckpointCommand =
                    secondFrozenConnection.CreateCommand();

                secondFrozenOwnCheckpointCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Historical_Checkpoints
                    WHERE FreezeId = $freezeId;
                    """;

                secondFrozenOwnCheckpointCommand.Parameters.AddWithValue(
                    "$freezeId",
                    secondCheckpoint.FreezeId.Value.ToString("D"));

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(
                        secondFrozenOwnCheckpointCommand.ExecuteScalar()));
                Assert.IsTrue(
                    Directory.Exists(
                        Path.Combine(
                            projectRoot,
                            secondCheckpoint.RelativeCheckpointPath)));
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
            UserId userId)
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

            command.Transaction =
                transaction;

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
                    'READY',
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
                "$evaluatedUtc",
                DateTimeOffset.UtcNow.ToString("O"));

            command.ExecuteNonQuery();

            using SqliteCommand itemCommand =
                connection.CreateCommand();

            itemCommand.Transaction =
                transaction;

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
                    ($readinessCheckId, 1, 'PROJECT_IDENTITY', 'PASS', NULL),
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