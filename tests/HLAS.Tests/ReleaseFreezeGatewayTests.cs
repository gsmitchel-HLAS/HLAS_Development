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
        public void InternalSeriesTransition_ReleasedBasis_PassesIntegrityVerification()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InternalSeriesTransitionBasisReference basis =
                    InternalSeriesTransitionBasisVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        release.ReleaseId);

                Assert.AreEqual(
                    release.ReleaseId,
                    basis.ReleaseId);

                Assert.AreEqual(
                    checkpoint.FreezeId,
                    basis.ReleaseFreezeId);

                Assert.AreEqual(
                    readinessId,
                    basis.ReadinessCheckId);

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_TamperedReleaseManifest_SafeStops()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                string manifestPath =
                    Path.Combine(
                        projectRoot,
                        checkpoint.ManifestRelativePath);

                File.AppendAllText(
                    manifestPath,
                    Environment.NewLine);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionBasisVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            release.ReleaseId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_MissingFrozenDatabase_SafeStops()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                string frozenDatabasePath =
                    Path.Combine(
                        projectRoot,
                        checkpoint.RelativeCheckpointPath,
                        ProjectPackageCreator.DatabaseFileName);

                File.Delete(frozenDatabasePath);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionBasisVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            release.ReleaseId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_TamperedControlledEvidence_SafeStops()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                string sourcePath =
                    Path.Combine(
                        projectRoot,
                        "developmental-evidence.txt");

                File.WriteAllText(
                    sourcePath,
                    "Original developmental evidence.");

                EvidenceCustodyRecord evidence =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    projectRoot,
                    userId,
                    ProjectRole.Admin,
                    release.ReleaseId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                // First prove the original released basis is valid.
                _ = InternalSeriesTransitionBasisVerifier.Verify(
                    projectRoot,
                    connection,
                    transaction,
                    project.ProjectId,
                    release.ReleaseId);

                string controlledPath =
                    Path.Combine(
                        projectRoot,
                        evidence.RelativeCustodyPath);

                File.AppendAllText(
                    controlledPath,
                    "Tampered after release.");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionBasisVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            release.ReleaseId));

                StringAssert.Contains(
                    exception.Message,
                    "Released Source Evidence integrity failed");

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_ReadyWithoutReleaseFreeze_SafeStops()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                // A Release Record exists, but no Release Freeze.
                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionBasisVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            release.ReleaseId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_TamperedFrozenDatabase_SafeStops()
        {
            string projectRoot =
                CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                string frozenDatabasePath =
                    Path.Combine(
                        projectRoot,
                        checkpoint.RelativeCheckpointPath,
                        ProjectPackageCreator.DatabaseFileName);

                File.AppendAllText(
                    frozenDatabasePath,
                    "Tampered after release.");

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionBasisVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            release.ReleaseId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_WriterCommit_PreservesReleasedBasis()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        project.ProjectId,
                        userId,
                        SeriesId.V,
                        ProjectRole.Admin,
                        "V_SERIES_INTERNAL_TRANSITION");

                InternalSeriesTransitionRecord record = new(
                    InternalSeriesTransitionId.CreateNew(),
                    project.ProjectId,
                    operation.OperationId,
                    SeriesId.A,
                    release.ReleaseId,
                    checkpoint.FreezeId,
                    readinessId,
                    GovernedTimestamp.CreateNow());

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    InternalSeriesTransitionWriter.Insert(
                        connection,
                        transaction,
                        record);

                    GovernedOperationService.FinalizeSuccessInTransaction(
                        connection,
                        transaction,
                        operation.OperationId);

                    transaction.Commit();
                }

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
            SELECT
                ProjectId,
                OperationId,
                SourceSeriesId,
                ReceivingSeriesId,
                ReleaseId,
                ReleaseFreezeId,
                ReadinessCheckId,
                ReceivedUtc
            FROM HLAS_Internal_Series_Transitions
            WHERE TransitionId = $transitionId;
            """;

                command.Parameters.AddWithValue(
                    "$transitionId",
                    record.TransitionId.Value.ToString("D"));

                using SqliteDataReader reader =
                    command.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.AreEqual(
                    project.ProjectId.Value.ToString("D"),
                    reader.GetString(0));

                Assert.AreEqual(
                    operation.OperationId.Value.ToString("D"),
                    reader.GetString(1));

                Assert.AreEqual("V", reader.GetString(2));
                Assert.AreEqual("A", reader.GetString(3));

                Assert.AreEqual(
                    release.ReleaseId.Value.ToString("D"),
                    reader.GetString(4));

                Assert.AreEqual(
                    checkpoint.FreezeId.Value.ToString("D"),
                    reader.GetString(5));

                Assert.AreEqual(
                    readinessId.Value.ToString("D"),
                    reader.GetString(6));

                Assert.AreEqual(
                    record.ReceivedUtc.Value.ToString("O"),
                    reader.GetString(7));

                Assert.IsFalse(reader.Read());
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_WriterRollback_DoesNotPersistReceiptOrSuccess()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        project.ProjectId,
                        userId,
                        SeriesId.V,
                        ProjectRole.Admin,
                        "V_SERIES_INTERNAL_TRANSITION");

                InternalSeriesTransitionRecord record = new(
                    InternalSeriesTransitionId.CreateNew(),
                    project.ProjectId,
                    operation.OperationId,
                    SeriesId.A,
                    release.ReleaseId,
                    checkpoint.FreezeId,
                    readinessId,
                    GovernedTimestamp.CreateNow());

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    InternalSeriesTransitionWriter.Insert(
                        connection,
                        transaction,
                        record);

                    GovernedOperationService.FinalizeSuccessInTransaction(
                        connection,
                        transaction,
                        operation.OperationId);

                    transaction.Rollback();
                }

                using SqliteCommand receiptCommand =
                    connection.CreateCommand();

                receiptCommand.CommandText =
                    """
            SELECT COUNT(*)
            FROM HLAS_Internal_Series_Transitions
            WHERE TransitionId = $transitionId;
            """;

                receiptCommand.Parameters.AddWithValue(
                    "$transitionId",
                    record.TransitionId.Value.ToString("D"));

                Assert.AreEqual(
                    0L,
                    Convert.ToInt64(receiptCommand.ExecuteScalar()));

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
                    operation.OperationId.Value.ToString("D"));

                Assert.IsTrue(
                    operationCommand.ExecuteScalar() is DBNull);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_UnregisteredUser_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                _ = ProjectPackageCreator.CreateNew(projectRoot);

                InternalSeriesTransitionAuthority authority =
                    InternalSeriesTransitionAuthority.Create(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionGateway.CreateTransition(
                            projectRoot,
                            authority,
                            SeriesId.A,
                            ReleaseId.CreateNew()));

                StringAssert.Contains(
                    exception.Message,
                    "project-specific Senior/Admin authority");
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        private sealed class IntegratedTransitionTestBridge
           : HLAS.Application.IAuthenticationGateway,
             HLAS.Application.IProjectAuthorizationGateway,
             HLAS.Application.IInternalSeriesTransitionGateway
        {
            private readonly UserId _userId;

            public IntegratedTransitionTestBridge(UserId userId)
            {
                _userId = userId;
            }

            public HLAS.Application.AuthenticationGatewayResult Authenticate(
                string loginName,
                string secret)
            {
                return HLAS.Application.AuthenticationGatewayResult.Succeeded(
                    _userId);
            }

            public HLAS.Application.ProjectAuthorizationGatewayResult Resolve(
                string projectRoot,
                UserId userId)
            {
                ProjectRole? role =
                    ProjectAuthorizationStore.ResolveRole(
                        projectRoot,
                        userId);

                return role is null
                    ? HLAS.Application.ProjectAuthorizationGatewayResult.Rejected(
                        "User lacks project authorization.")
                    : HLAS.Application.ProjectAuthorizationGatewayResult.Authorized(
                        role.Value);
            }

            public InternalSeriesTransitionRecord CreateTransition(
                string projectRoot,
                InternalSeriesTransitionAuthority authority,
                SeriesId receivingSeriesId,
                ReleaseId releaseId)
            {
                return InternalSeriesTransitionGateway.CreateTransition(
                    projectRoot,
                    authority,
                    receivingSeriesId,
                    releaseId);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_AuthenticatedService_PersistsVerifiedReceipt()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                IntegratedTransitionTestBridge bridge = new(userId);

                HLAS.Application.AuthenticationService authentication =
                    new(bridge);

                HLAS.Application.AuthenticatedIdentity identity =
                    authentication.Authenticate(
                        "development-user",
                        "development-secret");

                HLAS.Application.ProjectAuthorizationService authorization =
                    new(bridge);

                HLAS.Application.InternalSeriesTransitionService service =
                    new(authorization, bridge);

                InternalSeriesTransitionRecord receipt =
                    service.CreateTransition(
                        projectRoot,
                        identity,
                        SeriesId.A,
                        release.ReleaseId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InternalSeriesTransitionRecord verified =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        receipt.TransitionId);

                Assert.AreEqual(receipt.TransitionId, verified.TransitionId);
                Assert.AreEqual(project.ProjectId, verified.ProjectId);
                Assert.AreEqual(SeriesId.A, verified.ReceivingSeriesId);
                Assert.AreEqual(release.ReleaseId, verified.ReleaseId);
                Assert.AreEqual(checkpoint.FreezeId, verified.ReleaseFreezeId);
                Assert.AreEqual(readinessId, verified.ReadinessCheckId);

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_GatewayValidRelease_PersistsSuccessfulReceipt()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                InternalSeriesTransitionRecord receipt =
                    CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        release.ReleaseId);

                Assert.AreEqual(project.ProjectId, receipt.ProjectId);
                Assert.AreEqual(SeriesId.V, receipt.SourceSeriesId);
                Assert.AreEqual(SeriesId.A, receipt.ReceivingSeriesId);
                Assert.AreEqual(release.ReleaseId, receipt.ReleaseId);
                Assert.AreEqual(checkpoint.FreezeId, receipt.ReleaseFreezeId);
                Assert.AreEqual(readinessId, receipt.ReadinessCheckId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
            SELECT
                receipt.ReleaseId,
                receipt.ReleaseFreezeId,
                receipt.ReadinessCheckId,
                operation.Outcome
            FROM HLAS_Internal_Series_Transitions AS receipt
            INNER JOIN HLAS_Governed_Operations AS operation
                ON operation.OperationId = receipt.OperationId
            WHERE receipt.TransitionId = $transitionId;
            """;

                command.Parameters.AddWithValue(
                    "$transitionId",
                    receipt.TransitionId.Value.ToString("D"));

                using SqliteDataReader reader =
                    command.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.AreEqual(
                    release.ReleaseId.Value.ToString("D"),
                    reader.GetString(0));

                Assert.AreEqual(
                    checkpoint.FreezeId.Value.ToString("D"),
                    reader.GetString(1));

                Assert.AreEqual(
                    readinessId.Value.ToString("D"),
                    reader.GetString(2));

                Assert.AreEqual("SUCCESS", reader.GetString(3));
                Assert.IsFalse(reader.Read());
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_ReceiptReadback_VerifiesReleasedBasis()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                InternalSeriesTransitionRecord receipt =
                    CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        release.ReleaseId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InternalSeriesTransitionRecord verified =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        receipt.TransitionId);

                Assert.AreEqual(receipt.TransitionId, verified.TransitionId);
                Assert.AreEqual(receipt.OperationId, verified.OperationId);
                Assert.AreEqual(project.ProjectId, verified.ProjectId);
                Assert.AreEqual(SeriesId.V, verified.SourceSeriesId);
                Assert.AreEqual(SeriesId.A, verified.ReceivingSeriesId);
                Assert.AreEqual(release.ReleaseId, verified.ReleaseId);
                Assert.AreEqual(checkpoint.FreezeId, verified.ReleaseFreezeId);
                Assert.AreEqual(readinessId, verified.ReadinessCheckId);

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_AlteredReceiptReadinessBasis_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId originalReadiness =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        originalReadiness);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    projectRoot,
                    userId,
                    ProjectRole.Admin,
                    release.ReleaseId);

                InternalSeriesTransitionRecord receipt =
                    CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        release.ReleaseId);

                ReadinessCheckId otherReadiness =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InternalSeriesTransitionRecord original =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        receipt.TransitionId);

                Assert.AreEqual(
                    originalReadiness,
                    original.ReadinessCheckId);

                using (SqliteCommand command =
                    connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText =
                        """
                UPDATE HLAS_Internal_Series_Transitions
                SET ReadinessCheckId = $readinessId
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$readinessId",
                        otherReadiness.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => InternalSeriesTransitionReceiptVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            receipt.TransitionId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_MultipleReceivingSeries_PreserveDistinctReceipts()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                InternalSeriesTransitionRecord first =
                    CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        release.ReleaseId);

                InternalSeriesTransitionRecord second =
                    CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.I,
                        release.ReleaseId);

                Assert.AreNotEqual(
                    first.TransitionId,
                    second.TransitionId);

                Assert.AreNotEqual(
                    first.OperationId,
                    second.OperationId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InternalSeriesTransitionRecord verifiedFirst =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        first.TransitionId);

                InternalSeriesTransitionRecord verifiedSecond =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        second.TransitionId);

                Assert.AreEqual(
                    SeriesId.A,
                    verifiedFirst.ReceivingSeriesId);

                Assert.AreEqual(
                    SeriesId.I,
                    verifiedSecond.ReceivingSeriesId);

                Assert.AreEqual(
                    release.ReleaseId,
                    verifiedFirst.ReleaseId);

                Assert.AreEqual(
                    release.ReleaseId,
                    verifiedSecond.ReleaseId);

                Assert.AreEqual(
                    checkpoint.FreezeId,
                    verifiedFirst.ReleaseFreezeId);

                Assert.AreEqual(
                    checkpoint.FreezeId,
                    verifiedSecond.ReleaseFreezeId);

                Assert.AreEqual(
                    readinessId,
                    verifiedFirst.ReadinessCheckId);

                Assert.AreEqual(
                    readinessId,
                    verifiedSecond.ReadinessCheckId);

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_LaterVWork_PreservesEarlierReceipt()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId originalReadiness =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        originalReadiness);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        release.ReleaseId);

                InternalSeriesTransitionRecord receipt =
                   CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        release.ReleaseId);

                InternalSeriesTransitionRecord ReadReceipt()
                {
                    using SqliteConnection connection =
                        OpenDatabase(projectRoot);

                    connection.Open();

                    using SqliteTransaction transaction =
                        connection.BeginTransaction();

                    InternalSeriesTransitionRecord result =
                        InternalSeriesTransitionReceiptVerifier.Verify(
                            projectRoot,
                            connection,
                            transaction,
                            project.ProjectId,
                            receipt.TransitionId);

                    transaction.Rollback();

                    return result;
                }

                InternalSeriesTransitionRecord before =
                    ReadReceipt();

                // Later V-series work creates new governed history.
                ReadinessCheckId laterReadiness =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                Assert.AreNotEqual(
                    originalReadiness,
                    laterReadiness);

                InternalSeriesTransitionRecord after =
                    ReadReceipt();

                Assert.AreEqual(before, after);
                Assert.AreEqual(receipt.TransitionId, after.TransitionId);
                Assert.AreEqual(release.ReleaseId, after.ReleaseId);
                Assert.AreEqual(checkpoint.FreezeId, after.ReleaseFreezeId);
                Assert.AreEqual(originalReadiness, after.ReadinessCheckId);
                Assert.AreEqual(SeriesId.A, after.ReceivingSeriesId);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void InternalSeriesTransition_LaterRelease_PreservesEarlierReceivingBasis()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId firstReadiness =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord firstRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstReadiness);

                HistoricalCheckpointRecord firstCheckpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        firstRelease.ReleaseId);

                InternalSeriesTransitionRecord firstReceipt =
                  CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        firstRelease.ReleaseId);

                string firstManifestPath =
                    Path.Combine(
                        projectRoot,
                        firstCheckpoint.ManifestRelativePath);

                byte[] originalManifest =
                    File.ReadAllBytes(firstManifestPath);

                ReadinessCheckId secondReadiness =
                    CreateReadinessCheck(
                        projectRoot,
                        project.ProjectId,
                        userId);

                ReleaseRecord secondRelease =
                    ReleaseGateway.RequestRelease(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        secondReadiness);

                HistoricalCheckpointRecord secondCheckpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        secondRelease.ReleaseId);

                InternalSeriesTransitionRecord secondReceipt =
                  CreateTransitionForTest(
                        projectRoot,
                        userId,
                        ProjectRole.Admin,
                        SeriesId.A,
                        secondRelease.ReleaseId);

                using SqliteConnection connection =
                    OpenDatabase(projectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InternalSeriesTransitionRecord verifiedFirst =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        firstReceipt.TransitionId);

                InternalSeriesTransitionRecord verifiedSecond =
                    InternalSeriesTransitionReceiptVerifier.Verify(
                        projectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        secondReceipt.TransitionId);

                Assert.AreNotEqual(
                    verifiedFirst.TransitionId,
                    verifiedSecond.TransitionId);

                Assert.AreEqual(
                    firstRelease.ReleaseId,
                    verifiedFirst.ReleaseId);

                Assert.AreEqual(
                    firstCheckpoint.FreezeId,
                    verifiedFirst.ReleaseFreezeId);

                Assert.AreEqual(
                    firstReadiness,
                    verifiedFirst.ReadinessCheckId);

                Assert.AreEqual(
                    secondRelease.ReleaseId,
                    verifiedSecond.ReleaseId);

                Assert.AreEqual(
                    secondCheckpoint.FreezeId,
                    verifiedSecond.ReleaseFreezeId);

                Assert.AreEqual(
                    secondReadiness,
                    verifiedSecond.ReadinessCheckId);

                CollectionAssert.AreEqual(
                    originalManifest,
                    File.ReadAllBytes(firstManifestPath));

                transaction.Rollback();
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
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
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_NoRelease_ReturnsNotChecked()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityReleaseFreezeVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_UnfrozenRequest_ReturnsPendingWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                _ = CreateIntegrityReleaseRequest(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityReleaseFreezeVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);

                SqliteConnection.ClearAllPools();
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_ValidCheckpoint_VerifiedWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    request.Release.ReleaseId);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityReleaseFreezeVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                SqliteConnection.ClearAllPools();
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_MissingManifest_ReturnsIntegrityFailure()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                File.Delete(
                    Path.Combine(
                        root,
                        checkpoint.ManifestRelativePath));

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityReleaseFreezeVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    report.Items[14].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                Assert.IsFalse(
                    File.Exists(
                        Path.Combine(
                            root,
                            checkpoint.ManifestRelativePath)));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_SuccessWithoutCheckpoint_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    request.Release.ReleaseId);

                using (SqliteConnection connection = OpenDatabase(root))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                DELETE FROM HLAS_Historical_Checkpoints
                WHERE ReleaseId = $releaseId
                  AND CheckpointType = 'RELEASE_FREEZE';
                """;

                    command.Parameters.AddWithValue(
                        "$releaseId",
                        request.Release.ReleaseId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                byte[] before = File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                StringAssert.Contains(
                    report.Items[9].Detail,
                    "no linked RELEASE_FREEZE Historical Checkpoint");

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    report.Items[14].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_CompletedAndPendingReleases_ReportPendingWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    request.Release.ReleaseId);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ReadinessCheckId secondReadiness =
                    CreateReadinessCheck(
                        root,
                        project.ProjectId,
                        request.UserId);

                _ = ReleaseGateway.RequestRelease(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    secondReadiness);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_TamperedManifest_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                string manifestPath = Path.Combine(
                    root,
                    checkpoint.ManifestRelativePath);

                File.AppendAllText(
                    manifestPath,
                    Environment.NewLine);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    report.Items[14].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_TamperedCompletedAndPending_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ReadinessCheckId secondReadiness =
                    CreateReadinessCheck(
                        root,
                        project.ProjectId,
                        request.UserId);

                _ = ReleaseGateway.RequestRelease(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    secondReadiness);

                string manifestPath = Path.Combine(
                    root,
                    checkpoint.ManifestRelativePath);

                File.AppendAllText(
                    manifestPath,
                    Environment.NewLine);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_TamperedControlledEvidence_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string sourcePath = Path.Combine(
                    root,
                    "release-integrity-source.txt");

                File.WriteAllText(
                    sourcePath,
                    "Original developmental evidence.");

                EvidenceCustodyRecord evidence =
                    EvidenceCustodyService.Accept(root, sourcePath);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        root,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        root,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    root,
                    userId,
                    ProjectRole.Admin,
                    release.ReleaseId);

                string controlledPath = Path.Combine(
                    root,
                    evidence.RelativeCustodyPath);

                File.AppendAllText(
                    controlledPath,
                    "Tampered after release.");

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] evidenceBefore =
                    File.ReadAllBytes(controlledPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    evidenceBefore,
                    File.ReadAllBytes(controlledPath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_MissingControlledEvidence_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string sourcePath = Path.Combine(
                    root,
                    "release-missing-evidence.txt");

                File.WriteAllText(
                    sourcePath,
                    "Original developmental evidence.");

                EvidenceCustodyRecord evidence =
                    EvidenceCustodyService.Accept(root, sourcePath);

                UserId userId = UserId.CreateNew();

                ReadinessCheckId readinessId =
                    CreateReadinessCheck(
                        root,
                        project.ProjectId,
                        userId);

                ReleaseRecord release =
                    ReleaseGateway.RequestRelease(
                        root,
                        userId,
                        ProjectRole.Admin,
                        readinessId);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    root,
                    userId,
                    ProjectRole.Admin,
                    release.ReleaseId);

                string controlledPath = Path.Combine(
                    root,
                    evidence.RelativeCustodyPath);

                File.Delete(controlledPath);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                Assert.IsFalse(File.Exists(controlledPath));

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_MissingFrozenDatabase_ReportsFailureWithoutRecreatingFile()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                string frozenDatabasePath = Path.Combine(
                    root,
                    checkpoint.RelativeCheckpointPath,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                File.Delete(frozenDatabasePath);

                Assert.IsFalse(File.Exists(frozenDatabasePath));

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                Assert.IsFalse(File.Exists(frozenDatabasePath));

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReleaseFreeze_TamperedFrozenDatabase_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                string frozenDatabasePath = Path.Combine(
                    root,
                    checkpoint.RelativeCheckpointPath,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                File.AppendAllText(
                    frozenDatabasePath,
                    "Tampered after release.");

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] frozenBefore =
                    File.ReadAllBytes(frozenDatabasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    frozenBefore,
                    File.ReadAllBytes(frozenDatabasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        private static (UserId UserId, ReleaseRecord Release)
            CreateIntegrityReleaseRequest(string root)
        {
            ProjectManifest project =
                ProjectPackageCreator.CreateNew(root);

            UserId userId = UserId.CreateNew();

            ReadinessCheckId readinessId =
                CreateReadinessCheck(
                    root,
                    project.ProjectId,
                    userId);

            ReleaseRecord release =
                ReleaseGateway.RequestRelease(
                    root,
                    userId,
                    ProjectRole.Admin,
                    readinessId);

            return (userId, release);
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_DeletedSuccessfulReceipt_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                using (SqliteConnection connection =
                    OpenDatabase(root))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                DELETE FROM HLAS_Internal_Series_Transitions
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_BrokenReleaseFreezeLink_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    ForeignKeys = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Internal_Series_Transitions
                SET ReleaseFreezeId = $wrongFreezeId
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$wrongFreezeId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_BrokenReleaseLink_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    ForeignKeys = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Internal_Series_Transitions
                SET ReleaseId = $wrongReleaseId
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$wrongReleaseId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_WrongProjectId_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                using (SqliteConnection connection =
                    OpenDatabase(root))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Internal_Series_Transitions
                SET ProjectId = $wrongProjectId
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$wrongProjectId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_MissingReleaseManifest_ReportsBothFailuresWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                _ = CreateTransitionForTest(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    SeriesId.A,
                    request.Release.ReleaseId);

                string manifestPath = Path.Combine(
                    root,
                    checkpoint.ManifestRelativePath);

                File.Delete(manifestPath);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.IsFalse(File.Exists(manifestPath));

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_TamperedReleaseManifest_ReportsBothFailuresWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                _ = CreateTransitionForTest(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    SeriesId.A,
                    request.Release.ReleaseId);

                string manifestPath = Path.Combine(
                    root,
                    checkpoint.ManifestRelativePath);

                File.AppendAllText(
                    manifestPath,
                    Environment.NewLine);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "RELEASE_FREEZE",
                    report.Items[9].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[9].Status);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    report.Items[11].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_MalformedTransitionId_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                using (SqliteConnection connection =
                    OpenDatabase(root))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Internal_Series_Transitions
                SET TransitionId = $invalidId
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$invalidId",
                        "INVALID_TRANSITION_ID");

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_MalformedProjectId_ReportsFailureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    ForeignKeys = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Internal_Series_Transitions
                SET ProjectId = $invalidProjectId
                WHERE TransitionId = $transitionId;
                """;

                    command.Parameters.AddWithValue(
                        "$invalidProjectId",
                        "INVALID_PROJECT_ID");

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectManifest project =
                    ProjectPackageReader.Open(root);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    report.Items[10].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[10].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[9].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[11].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        [TestMethod]
        public void ProjectIntegrityReceipt_NoReceipts_ReturnsNotChecked()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityInternalReceiptVerifier.Check(root);

                Assert.AreEqual(
                    "INTERNAL_SERIES_RECEIPTS",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityReceipt_ValidReceipt_VerifiedWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                CreateIntegrityReceipt(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityInternalReceiptVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityReceipt_BrokenReadinessLink_ReturnsIntegrityFailure()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                InternalSeriesTransitionRecord receipt =
                    CreateIntegrityReceipt(root);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = Path.Combine(
                        root,
                        ProjectPackageCreator.DatabaseFileName),
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    ForeignKeys = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        UPDATE HLAS_Internal_Series_Transitions
                        SET ReadinessCheckId = $wrongId
                        WHERE TransitionId = $transitionId;
                        """;

                    command.Parameters.AddWithValue(
                        "$wrongId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$transitionId",
                        receipt.TransitionId.Value.ToString("D"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityInternalReceiptVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityReceipt_SuccessWithoutReceipt_ReturnsIntegrityFailure()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        root,
                        project.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin,
                        "V_SERIES_INTERNAL_TRANSITION");

                GovernedOperationService.FinalizeSuccess(
                    root,
                    operation.OperationId);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityInternalReceiptVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        private static InternalSeriesTransitionRecord
            CreateIntegrityReceipt(string root)
        {
            var request = CreateIntegrityReleaseRequest(root);

            _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                root,
                request.UserId,
                ProjectRole.Admin,
                request.Release.ReleaseId);

            return CreateTransitionForTest(
                root,
                request.UserId,
                ProjectRole.Admin,
                SeriesId.A,
                request.Release.ReleaseId);
        }
        [TestMethod]
        public void ProjectIntegrityHistoricalCoverage_NoCheckpoints_ReturnsNotChecked()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityHistoricalCheckpointCoverageVerifier.Check(root);

                Assert.AreEqual(
                    "HISTORICAL_CHECKPOINT_COVERAGE",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityHistoricalCoverage_ReleaseFreeze_VerifiesStructureWithoutWrites()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                _ = ReleaseFreezeGateway.CreateReleaseFreeze(
                    root,
                    request.UserId,
                    ProjectRole.Admin,
                    request.Release.ReleaseId);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityHistoricalCheckpointCoverageVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityHistoricalCoverage_UnsupportedType_ReturnsNotChecked()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        root,
                        project.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin,
                        "DEVELOPMENTAL_TEST");

                GovernedOperationService.FinalizeSuccess(
                    root,
                    operation.OperationId);

                using (SqliteConnection connection =
                    OpenDatabase(root))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        INSERT INTO HLAS_Historical_Checkpoints
                        (
                            FreezeId,
                            OperationId,
                            CheckpointType,
                            ReleaseId,
                            RelativeCheckpointPath,
                            ManifestRelativePath,
                            ManifestSha256Hex,
                            FrozenUtc
                        )
                        VALUES
                        (
                            $freezeId,
                            $operationId,
                            'FUTURE_CHECKPOINT',
                            NULL,
                            'HLAS_Frozen_States/future-checkpoint',
                            'HLAS_Frozen_States/future-checkpoint/manifest.json',
                            $hash,
                            $frozenUtc
                        );
                        """;

                    command.Parameters.AddWithValue(
                        "$freezeId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operation.OperationId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$hash",
                        new string('a', 64));

                    command.Parameters.AddWithValue(
                        "$frozenUtc",
                        DateTimeOffset.UtcNow.ToString("O"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityHistoricalCheckpointCoverageVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }

        [TestMethod]
        public void ProjectIntegrityHistoricalCoverage_InvalidHash_ReturnsIntegrityFailure()
        {
            string root = CreateTemporaryProjectRoot();

            try
            {
                var request = CreateIntegrityReleaseRequest(root);

                HistoricalCheckpointRecord checkpoint =
                    ReleaseFreezeGateway.CreateReleaseFreeze(
                        root,
                        request.UserId,
                        ProjectRole.Admin,
                        request.Release.ReleaseId);

                using (SqliteConnection connection =
                    OpenDatabase(root))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        UPDATE HLAS_Historical_Checkpoints
                        SET ManifestSha256Hex = $badHash
                        WHERE FreezeId = $freezeId;
                        """;

                    command.Parameters.AddWithValue(
                        "$badHash",
                        new string('Z', 64));

                    command.Parameters.AddWithValue(
                        "$freezeId",
                        checkpoint.FreezeId.Value.ToString("D"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityHistoricalCheckpointCoverageVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteTemporaryProjectRoot(root);
            }
        }
        private static InternalSeriesTransitionRecord CreateTransitionForTest(
           string projectRoot,
           UserId userId,
           ProjectRole projectRole,
           SeriesId receivingSeriesId,
           ReleaseId releaseId)
        {
            InternalSeriesTransitionAuthority authority =
                InternalSeriesTransitionAuthority.Create(
                    projectRoot,
                    userId,
                    projectRole);

            return InternalSeriesTransitionGateway.CreateTransition(
                projectRoot,
                authority,
                receivingSeriesId,
                releaseId);
        }
        private static ReadinessCheckId CreateReadinessCheck(
            string projectRoot,
            ProjectId projectId,
            UserId userId)
        {
            if (ProjectAuthorizationStore.ResolveRole(
                   projectRoot,
                   userId) is null)
            {
                ProjectAuthorizationStore.AddAuthorization(
                    projectRoot,
                    userId,
                    ProjectRole.Admin);
            }
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