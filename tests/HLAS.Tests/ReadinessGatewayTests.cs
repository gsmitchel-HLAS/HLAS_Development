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
                Assert.AreEqual(
    ReadinessGateCode.SourceEvidence,
    result.Items[1].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[1].GateStatus);

                Assert.AreEqual(
                    "No governed Source Evidence Requirement revision exists.",
                    result.Items[1].Detail);
                Assert.AreEqual(
     ReadinessGateStatus.Blocked,
     result.Items[2].GateStatus);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[3].GateStatus);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[4].GateStatus);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[5].GateStatus);

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
        [TestMethod]
        public void Evaluate_SourceEvidenceNotRequiredRequirement_PassesSourceEvidenceGate()
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

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
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

                string operationId =
                    Guid.NewGuid().ToString("D");

                string requirementRevisionId =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.ToString("O");

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

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
                    Outcome
                )
                VALUES
                (
                    $operationId,
                    $projectId,
                    $userId,
                    'V',
                    'Admin',
                    $timestamp,
                    $timestamp,
                    'SUCCESS'
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Revisions
                (
                    RequirementRevisionId,
                    RevisionNumber,
                    PriorRequirementRevisionId,
                    OperationId,
                    ApprovedUtc
                )
                VALUES
                (
                    $requirementRevisionId,
                    1,
                    NULL,
                    $operationId,
                    $timestamp
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Items
                (
                    RequirementRevisionId,
                    RequirementKey,
                    RequirementState,
                    EvidenceId,
                    NotRequiredReason
                )
                VALUES
                (
                    $requirementRevisionId,
                    'DEVELOPMENTAL_OPTIONAL_SOURCE',
                    'NOT_REQUIRED',
                    NULL,
                    'Developmental governed NOT_REQUIRED proof.'
                );
                """;

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operationId);

                    command.Parameters.AddWithValue(
                        "$projectId",
                        manifest.ProjectId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$userId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$requirementRevisionId",
                        requirementRevisionId);

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        timestamp);

                    command.ExecuteNonQuery();
                }

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.SourceEvidence,
                    result.Items[1].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[1].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_SourceEvidencePendingRequirement_BlocksSourceEvidenceGate()
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

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
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

                string operationId =
                    Guid.NewGuid().ToString("D");

                string requirementRevisionId =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.ToString("O");

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

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
                    Outcome
                )
                VALUES
                (
                    $operationId,
                    $projectId,
                    $userId,
                    'V',
                    'Admin',
                    $timestamp,
                    $timestamp,
                    'SUCCESS'
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Revisions
                (
                    RequirementRevisionId,
                    RevisionNumber,
                    PriorRequirementRevisionId,
                    OperationId,
                    ApprovedUtc
                )
                VALUES
                (
                    $requirementRevisionId,
                    1,
                    NULL,
                    $operationId,
                    $timestamp
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Items
                (
                    RequirementRevisionId,
                    RequirementKey,
                    RequirementState,
                    EvidenceId,
                    NotRequiredReason
                )
                VALUES
                (
                    $requirementRevisionId,
                    'DEVELOPMENTAL_REQUIRED_SOURCE',
                    'PENDING',
                    NULL,
                    NULL
                );
                """;

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operationId);

                    command.Parameters.AddWithValue(
                        "$projectId",
                        manifest.ProjectId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$userId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$requirementRevisionId",
                        requirementRevisionId);

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        timestamp);

                    command.ExecuteNonQuery();
                }

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.SourceEvidence,
                    result.Items[1].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[1].GateStatus);

                Assert.AreEqual(
                    "Required Source Evidence remains pending: DEVELOPMENTAL_REQUIRED_SOURCE.",
                    result.Items[1].Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_SourceEvidenceReceivedRequirementWithValidCustody_PassesSourceEvidenceGate()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourceFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Required Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental required Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
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

                string operationId =
                    Guid.NewGuid().ToString("D");

                string requirementRevisionId =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.ToString("O");

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

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
                    Outcome
                )
                VALUES
                (
                    $operationId,
                    $projectId,
                    $userId,
                    'V',
                    'Admin',
                    $timestamp,
                    $timestamp,
                    'SUCCESS'
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Revisions
                (
                    RequirementRevisionId,
                    RevisionNumber,
                    PriorRequirementRevisionId,
                    OperationId,
                    ApprovedUtc
                )
                VALUES
                (
                    $requirementRevisionId,
                    1,
                    NULL,
                    $operationId,
                    $timestamp
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Items
                (
                    RequirementRevisionId,
                    RequirementKey,
                    RequirementState,
                    EvidenceId,
                    NotRequiredReason
                )
                VALUES
                (
                    $requirementRevisionId,
                    'DEVELOPMENTAL_REQUIRED_SOURCE',
                    'RECEIVED',
                    $evidenceId,
                    NULL
                );
                """;

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operationId);

                    command.Parameters.AddWithValue(
                        "$projectId",
                        manifest.ProjectId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$userId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$requirementRevisionId",
                        requirementRevisionId);

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        timestamp);

                    command.ExecuteNonQuery();
                }

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.SourceEvidence,
                    result.Items[1].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[1].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_SourceEvidenceReceivedRequirementWithTamperedCustody_BlocksSourceEvidenceGate()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourceFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Tampered Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental original Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
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

                string operationId =
                    Guid.NewGuid().ToString("D");

                string requirementRevisionId =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.ToString("O");

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

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
                    Outcome
                )
                VALUES
                (
                    $operationId,
                    $projectId,
                    $userId,
                    'V',
                    'Admin',
                    $timestamp,
                    $timestamp,
                    'SUCCESS'
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Revisions
                (
                    RequirementRevisionId,
                    RevisionNumber,
                    PriorRequirementRevisionId,
                    OperationId,
                    ApprovedUtc
                )
                VALUES
                (
                    $requirementRevisionId,
                    1,
                    NULL,
                    $operationId,
                    $timestamp
                );

                INSERT INTO HLAS_Source_Evidence_Requirement_Items
                (
                    RequirementRevisionId,
                    RequirementKey,
                    RequirementState,
                    EvidenceId,
                    NotRequiredReason
                )
                VALUES
                (
                    $requirementRevisionId,
                    'DEVELOPMENTAL_REQUIRED_SOURCE',
                    'RECEIVED',
                    $evidenceId,
                    NULL
                );
                """;

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operationId);

                    command.Parameters.AddWithValue(
                        "$projectId",
                        manifest.ProjectId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$userId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$requirementRevisionId",
                        requirementRevisionId);

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        timestamp);

                    command.ExecuteNonQuery();
                }

                string controlledFilePath =
                    Path.Combine(
                        projectRoot,
                        evidence.RelativeCustodyPath);

                File.WriteAllText(
                    controlledFilePath,
                    "Tampered controlled Source Evidence.");

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[1].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_CompleteProjectJmfTruth_PassesProjectJmfTruthGate()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourceFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Project JMF Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental Project/JMF source evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
                        projectRoot);

                AddCompleteProjectJmfTruth(
                    projectRoot,
                    manifest,
                    evidence.EvidenceId);

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.ProjectJmfTruth,
                    result.Items[2].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[2].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_IncompleteProjectJmfTruth_BlocksProjectJmfTruthGate()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourceFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Incomplete Project JMF Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental incomplete Project/JMF source evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
                        projectRoot);

                AddIncompleteProjectJmfTruth(
                    projectRoot,
                    manifest,
                    evidence.EvidenceId);

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.ProjectJmfTruth,
                    result.Items[2].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[2].GateStatus);

                Assert.AreEqual(
                    "The current Project/JMF revision does not contain the complete governed 20-field set.",
                    result.Items[2].Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_UnresolvedProjectJmfReview_BlocksProjectJmfReviewGate()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourceFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Project JMF Review Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental Project/JMF review source evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
                        projectRoot);

                AddCompleteProjectJmfTruth(
                    projectRoot,
                    manifest,
                    evidence.EvidenceId);

                AddUnresolvedProjectJmfReview(
                    projectRoot,
                    manifest,
                    evidence.EvidenceId);

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.ProjectJmfReview,
                    result.Items[4].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[4].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_ResolvedProjectJmfReview_PassesProjectJmfReviewGate()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourceFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Resolved Project JMF Review Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental resolved Project/JMF review source evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
                        projectRoot);

                AddCompleteProjectJmfTruth(
                    projectRoot,
                    manifest,
                    evidence.EvidenceId);

                AddResolvedProjectJmfReview(
                    projectRoot,
                    manifest,
                    evidence.EvidenceId);

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.ProjectJmfReview,
                    result.Items[4].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[4].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        private static void AddResolvedProjectJmfReview(
    string projectRoot,
    ProjectManifest manifest,
    EvidenceId sourceEvidenceId)
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

            string openedOperationId =
                Guid.NewGuid().ToString("D");

            string resolvedOperationId =
                Guid.NewGuid().ToString("D");

            string reviewCaseId =
                Guid.NewGuid().ToString("D");

            string openedTimestamp =
                DateTimeOffset.UtcNow.ToString("O");

            string resolvedTimestamp =
                DateTimeOffset.UtcNow.AddSeconds(1).ToString("O");

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            string baselineRevisionId;

            using (SqliteCommand revisionCommand =
                connection.CreateCommand())
            {
                revisionCommand.Transaction = transaction;
                revisionCommand.CommandText =
                    """
            SELECT ProjectJmfRevisionId
            FROM HLAS_Project_JMF_Revisions
            ORDER BY RevisionNumber DESC
            LIMIT 1;
            """;

                baselineRevisionId =
                    Convert.ToString(
                        revisionCommand.ExecuteScalar())
                    ?? throw new InvalidOperationException(
                        "SAFE-STOP: No Project/JMF revision exists for developmental review test.");
            }

            using (SqliteCommand openedOperationCommand =
                connection.CreateCommand())
            {
                openedOperationCommand.Transaction = transaction;
                openedOperationCommand.CommandText =
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
                Outcome
            )
            VALUES
            (
                $operationId,
                $projectId,
                $userId,
                'V',
                'Admin',
                $timestamp,
                $timestamp,
                'SUCCESS'
            );
            """;

                openedOperationCommand.Parameters.AddWithValue(
                    "$operationId",
                    openedOperationId);

                openedOperationCommand.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                openedOperationCommand.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                openedOperationCommand.Parameters.AddWithValue(
                    "$timestamp",
                    openedTimestamp);

                openedOperationCommand.ExecuteNonQuery();
            }

            using (SqliteCommand resolvedOperationCommand =
                connection.CreateCommand())
            {
                resolvedOperationCommand.Transaction = transaction;
                resolvedOperationCommand.CommandText =
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
                Outcome
            )
            VALUES
            (
                $operationId,
                $projectId,
                $userId,
                'V',
                'Admin',
                $timestamp,
                $timestamp,
                'SUCCESS'
            );
            """;

                resolvedOperationCommand.Parameters.AddWithValue(
                    "$operationId",
                    resolvedOperationId);

                resolvedOperationCommand.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                resolvedOperationCommand.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                resolvedOperationCommand.Parameters.AddWithValue(
                    "$timestamp",
                    resolvedTimestamp);

                resolvedOperationCommand.ExecuteNonQuery();
            }

            using (SqliteCommand caseCommand =
                connection.CreateCommand())
            {
                caseCommand.Transaction = transaction;
                caseCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Review_Cases
            (
                ReviewCaseId,
                SourceEvidenceId,
                BaselineProjectJmfRevisionId,
                OpenedOperationId,
                OpenedUtc,
                ResolvedOperationId,
                ResolvedUtc
            )
            VALUES
            (
                $reviewCaseId,
                $sourceEvidenceId,
                $baselineRevisionId,
                $openedOperationId,
                $openedTimestamp,
                $resolvedOperationId,
                $resolvedTimestamp
            );
            """;

                caseCommand.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                caseCommand.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    sourceEvidenceId.Value.ToString("D"));

                caseCommand.Parameters.AddWithValue(
                    "$baselineRevisionId",
                    baselineRevisionId);

                caseCommand.Parameters.AddWithValue(
                    "$openedOperationId",
                    openedOperationId);

                caseCommand.Parameters.AddWithValue(
                    "$openedTimestamp",
                    openedTimestamp);

                caseCommand.Parameters.AddWithValue(
                    "$resolvedOperationId",
                    resolvedOperationId);

                caseCommand.Parameters.AddWithValue(
                    "$resolvedTimestamp",
                    resolvedTimestamp);

                caseCommand.ExecuteNonQuery();
            }

            using (SqliteCommand itemCommand =
                connection.CreateCommand())
            {
                itemCommand.Transaction = transaction;
                itemCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Review_Items
            (
                ReviewCaseId,
                FieldId,
                CurrentApprovedValue,
                NewOfficialSourceValue,
                Decision,
                DecisionReason,
                DecidedOperationId,
                DecidedUtc
            )
            VALUES
            (
                $reviewCaseId,
                'JMF-001',
                'CURRENT',
                'NEW',
                'KEEP CURRENT',
                'Developmental governed review decision.',
                $resolvedOperationId,
                $resolvedTimestamp
            );
            """;

                itemCommand.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                itemCommand.Parameters.AddWithValue(
                    "$resolvedOperationId",
                    resolvedOperationId);

                itemCommand.Parameters.AddWithValue(
                    "$resolvedTimestamp",
                    resolvedTimestamp);

                itemCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        private static void AddUnresolvedProjectJmfReview(
    string projectRoot,
    ProjectManifest manifest,
    EvidenceId sourceEvidenceId)
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

            string operationId =
                Guid.NewGuid().ToString("D");

            string reviewCaseId =
                Guid.NewGuid().ToString("D");

            string timestamp =
                DateTimeOffset.UtcNow.ToString("O");

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            string baselineRevisionId;

            using (SqliteCommand revisionCommand =
                connection.CreateCommand())
            {
                revisionCommand.Transaction = transaction;
                revisionCommand.CommandText =
                    """
            SELECT ProjectJmfRevisionId
            FROM HLAS_Project_JMF_Revisions
            ORDER BY RevisionNumber DESC
            LIMIT 1;
            """;

                baselineRevisionId =
                    Convert.ToString(
                        revisionCommand.ExecuteScalar())
                    ?? throw new InvalidOperationException(
                        "SAFE-STOP: No Project/JMF revision exists for developmental review test.");
            }

            using (SqliteCommand operationCommand =
                connection.CreateCommand())
            {
                operationCommand.Transaction = transaction;
                operationCommand.CommandText =
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
                Outcome
            )
            VALUES
            (
                $operationId,
                $projectId,
                $userId,
                'V',
                'Admin',
                $timestamp,
                $timestamp,
                'SUCCESS'
            );
            """;

                operationCommand.Parameters.AddWithValue(
                    "$operationId",
                    operationId);

                operationCommand.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                operationCommand.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                operationCommand.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                operationCommand.ExecuteNonQuery();
            }

            using (SqliteCommand caseCommand =
                connection.CreateCommand())
            {
                caseCommand.Transaction = transaction;
                caseCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Review_Cases
            (
                ReviewCaseId,
                SourceEvidenceId,
                BaselineProjectJmfRevisionId,
                OpenedOperationId,
                OpenedUtc,
                ResolvedOperationId,
                ResolvedUtc
            )
            VALUES
            (
                $reviewCaseId,
                $sourceEvidenceId,
                $baselineRevisionId,
                $operationId,
                $timestamp,
                NULL,
                NULL
            );
            """;

                caseCommand.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                caseCommand.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    sourceEvidenceId.Value.ToString("D"));

                caseCommand.Parameters.AddWithValue(
                    "$baselineRevisionId",
                    baselineRevisionId);

                caseCommand.Parameters.AddWithValue(
                    "$operationId",
                    operationId);

                caseCommand.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                caseCommand.ExecuteNonQuery();
            }

            using (SqliteCommand itemCommand =
                connection.CreateCommand())
            {
                itemCommand.Transaction = transaction;
                itemCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Review_Items
            (
                ReviewCaseId,
                FieldId,
                CurrentApprovedValue,
                NewOfficialSourceValue,
                Decision,
                DecisionReason,
                DecidedOperationId,
                DecidedUtc
            )
            VALUES
            (
                $reviewCaseId,
                'JMF-001',
                'CURRENT',
                'NEW',
                NULL,
                NULL,
                NULL,
                NULL
            );
            """;

                itemCommand.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                itemCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        private static void AddIncompleteProjectJmfTruth(
    string projectRoot,
    ProjectManifest manifest,
    EvidenceId sourceEvidenceId)
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

            string operationId =
                Guid.NewGuid().ToString("D");

            string revisionId =
                Guid.NewGuid().ToString("D");

            string timestamp =
                DateTimeOffset.UtcNow.ToString("O");

            string[] fieldIds =
            [
                "PJT-001",
        "PJT-002",
        "PJT-003",
        "PJT-004",
        "PJT-005",
        "PJT-006",
        "PJT-007",
        "JMF-001",
        "JMF-002",
        "JMF-003",
        "JMF-004",
        "JMF-005",
        "JMF-006",
        "JMF-007",
        "JMF-008",
        "JMF-009",
        "JMF-010",
        "JMF-011",
        "JMF-012"
            ];

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using (SqliteCommand operationCommand =
                connection.CreateCommand())
            {
                operationCommand.Transaction = transaction;
                operationCommand.CommandText =
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
                Outcome
            )
            VALUES
            (
                $operationId,
                $projectId,
                $userId,
                'V',
                'Admin',
                $timestamp,
                $timestamp,
                'SUCCESS'
            );
            """;

                operationCommand.Parameters.AddWithValue(
                    "$operationId",
                    operationId);

                operationCommand.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                operationCommand.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                operationCommand.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                operationCommand.ExecuteNonQuery();
            }

            using (SqliteCommand revisionCommand =
                connection.CreateCommand())
            {
                revisionCommand.Transaction = transaction;
                revisionCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Revisions
            (
                ProjectJmfRevisionId,
                RevisionNumber,
                PriorProjectJmfRevisionId,
                SourceEvidenceId,
                OperationId,
                ApprovedUtc
            )
            VALUES
            (
                $revisionId,
                1,
                NULL,
                $sourceEvidenceId,
                $operationId,
                $timestamp
            );
            """;

                revisionCommand.Parameters.AddWithValue(
                    "$revisionId",
                    revisionId);

                revisionCommand.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    sourceEvidenceId.Value.ToString("D"));

                revisionCommand.Parameters.AddWithValue(
                    "$operationId",
                    operationId);

                revisionCommand.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                revisionCommand.ExecuteNonQuery();
            }

            foreach (string fieldId in fieldIds)
            {
                using SqliteCommand fieldCommand =
                    connection.CreateCommand();

                fieldCommand.Transaction = transaction;
                fieldCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Revision_Fields
            (
                ProjectJmfRevisionId,
                FieldId,
                FieldValue,
                SourceEvidenceId
            )
            VALUES
            (
                $revisionId,
                $fieldId,
                $fieldValue,
                $sourceEvidenceId
            );
            """;

                fieldCommand.Parameters.AddWithValue(
                    "$revisionId",
                    revisionId);

                fieldCommand.Parameters.AddWithValue(
                    "$fieldId",
                    fieldId);

                fieldCommand.Parameters.AddWithValue(
                    "$fieldValue",
                    $"VALUE-{fieldId}");

                fieldCommand.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    sourceEvidenceId.Value.ToString("D"));

                fieldCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        private static void AddCompleteProjectJmfTruth(
    string projectRoot,
    ProjectManifest manifest,
    EvidenceId sourceEvidenceId)
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

            string operationId =
                Guid.NewGuid().ToString("D");

            string revisionId =
                Guid.NewGuid().ToString("D");

            string timestamp =
                DateTimeOffset.UtcNow.ToString("O");

            string[] fieldIds =
            [
                "PJT-001",
        "PJT-002",
        "PJT-003",
        "PJT-004",
        "PJT-005",
        "PJT-006",
        "PJT-007",
        "JMF-001",
        "JMF-002",
        "JMF-003",
        "JMF-004",
        "JMF-005",
        "JMF-006",
        "JMF-007",
        "JMF-008",
        "JMF-009",
        "JMF-010",
        "JMF-011",
        "JMF-012",
        "JMF-013"
            ];

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using (SqliteCommand operationCommand =
                connection.CreateCommand())
            {
                operationCommand.Transaction = transaction;
                operationCommand.CommandText =
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
                Outcome
            )
            VALUES
            (
                $operationId,
                $projectId,
                $userId,
                'V',
                'Admin',
                $timestamp,
                $timestamp,
                'SUCCESS'
            );
            """;

                operationCommand.Parameters.AddWithValue(
                    "$operationId",
                    operationId);

                operationCommand.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                operationCommand.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                operationCommand.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                operationCommand.ExecuteNonQuery();
            }

            using (SqliteCommand revisionCommand =
                connection.CreateCommand())
            {
                revisionCommand.Transaction = transaction;
                revisionCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Revisions
            (
                ProjectJmfRevisionId,
                RevisionNumber,
                PriorProjectJmfRevisionId,
                SourceEvidenceId,
                OperationId,
                ApprovedUtc
            )
            VALUES
            (
                $revisionId,
                1,
                NULL,
                $sourceEvidenceId,
                $operationId,
                $timestamp
            );
            """;

                revisionCommand.Parameters.AddWithValue(
                    "$revisionId",
                    revisionId);

                revisionCommand.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    sourceEvidenceId.Value.ToString("D"));

                revisionCommand.Parameters.AddWithValue(
                    "$operationId",
                    operationId);

                revisionCommand.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                revisionCommand.ExecuteNonQuery();
            }

            foreach (string fieldId in fieldIds)
            {
                using SqliteCommand fieldCommand =
                    connection.CreateCommand();

                fieldCommand.Transaction = transaction;
                fieldCommand.CommandText =
                    """
            INSERT INTO HLAS_Project_JMF_Revision_Fields
            (
                ProjectJmfRevisionId,
                FieldId,
                FieldValue,
                SourceEvidenceId
            )
            VALUES
            (
                $revisionId,
                $fieldId,
                $fieldValue,
                $sourceEvidenceId
            );
            """;

                fieldCommand.Parameters.AddWithValue(
                    "$revisionId",
                    revisionId);

                fieldCommand.Parameters.AddWithValue(
                    "$fieldId",
                    fieldId);

                fieldCommand.Parameters.AddWithValue(
                    "$fieldValue",
                    $"VALUE-{fieldId}");

                fieldCommand.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    sourceEvidenceId.Value.ToString("D"));

                fieldCommand.ExecuteNonQuery();
            }

            transaction.Commit();
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