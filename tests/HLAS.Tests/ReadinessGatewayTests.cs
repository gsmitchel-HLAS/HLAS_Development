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
        public void LineageReadinessEvaluator_EmptyGovernedProject_PassesLineageGate()
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

                ReadinessCheckId readinessCheckId =
                    ReadinessCheckId.CreateNew();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        readinessCheckId);

                Assert.AreEqual(
                    ReadinessGateCode.Lineage,
                    result.GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.GateStatus);

                Assert.AreEqual(
                    readinessCheckId,
                    result.ReadinessCheckId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_SupersededEvidenceWithoutSuccessor_BlocksLineageGate()
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
                    "Developmental Lineage Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental lineage Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

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
                UPDATE HLAS_Source_Evidence_Catalog
                SET LifecycleState = 'Superseded'
                WHERE EvidenceId = $evidenceId;
                """;

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    command.ExecuteNonQuery();
                }

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "Superseded Source Evidence has no successful governed replacement successor.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_BrokenMetadataPredecessor_BlocksLineageGate()
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

            string sourceFileA =
                Path.Combine(
                    sourceDirectory,
                    "Lineage Source A.txt");

            string sourceFileB =
                Path.Combine(
                    sourceDirectory,
                    "Lineage Source B.txt");

            File.WriteAllText(
                sourceFileA,
                "Source A");

            File.WriteAllText(
                sourceFileB,
                "Source B");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceA =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFileA);

                EvidenceCustodyRecord evidenceB =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFileB);

                _ = SourceEvidenceMaintenanceGateway.Correct(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    evidenceA.EvidenceId,
                    new SourceEvidenceCorrectionRequest(
                        SourceEvidenceMetadataMutation.Set("A1"),
                        SourceEvidenceMetadataMutation.Keep(),
                        "A version 1"));

                _ = SourceEvidenceMaintenanceGateway.Correct(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    evidenceA.EvidenceId,
                    new SourceEvidenceCorrectionRequest(
                        SourceEvidenceMetadataMutation.Set("A2"),
                        SourceEvidenceMetadataMutation.Keep(),
                        "A version 2"));

                _ = SourceEvidenceMaintenanceGateway.Correct(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    evidenceB.EvidenceId,
                    new SourceEvidenceCorrectionRequest(
                        SourceEvidenceMetadataMutation.Set("B1"),
                        SourceEvidenceMetadataMutation.Keep(),
                        "B version 1"));

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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string evidenceBVersion1Id;

                using (SqliteCommand readCommand =
                    connection.CreateCommand())
                {
                    readCommand.CommandText =
                        """
                SELECT MetadataVersionId
                FROM HLAS_Source_Evidence_Metadata_Versions
                WHERE
                    EvidenceId = $evidenceId
                    AND VersionNumber = 1;
                """;

                    readCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceB.EvidenceId.Value.ToString("D"));

                    evidenceBVersion1Id =
                        Convert.ToString(
                            readCommand.ExecuteScalar())
                        ?? throw new InvalidOperationException(
                            "Developmental metadata version was not found.");
                }

                using (SqliteCommand updateCommand =
                    connection.CreateCommand())
                {
                    updateCommand.CommandText =
                        """
                UPDATE HLAS_Source_Evidence_Metadata_Versions
                SET PriorMetadataVersionId = $wrongPriorId
                WHERE
                    EvidenceId = $evidenceId
                    AND VersionNumber = 2;
                """;

                    updateCommand.Parameters.AddWithValue(
                        "$wrongPriorId",
                        evidenceBVersion1Id);

                    updateCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceA.EvidenceId.Value.ToString("D"));

                    updateCommand.ExecuteNonQuery();
                }

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "Source Evidence metadata prior-version lineage does not point to the immediately preceding version.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_ValidReplacementChain_PassesLineageGate()
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

            string originalFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Original Source.txt");

            string replacementFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Replacement Source.txt");

            File.WriteAllText(
                originalFilePath,
                "Original governed evidence.");

            File.WriteAllText(
                replacementFilePath,
                "Replacement governed evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord originalEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        originalFilePath);

                _ = SourceEvidenceMaintenanceGateway.Replace(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    originalEvidence.EvidenceId,
                    new SourceEvidenceReplacementRequest(
                        replacementFilePath,
                        SourceEvidenceMetadataMutation.Keep(),
                        SourceEvidenceMetadataMutation.Keep(),
                        "Developmental valid replacement-lineage proof"));

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_MaintenanceFreezeTargetMismatch_BlocksLineageGate()
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

            string sourceFileA =
                Path.Combine(
                    sourceDirectory,
                    "Freeze Lineage Source A.txt");

            string sourceFileB =
                Path.Combine(
                    sourceDirectory,
                    "Freeze Lineage Source B.txt");

            File.WriteAllText(
                sourceFileA,
                "Source A");

            File.WriteAllText(
                sourceFileB,
                "Source B");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceA =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFileA);

                EvidenceCustodyRecord evidenceB =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFileB);

                SourceEvidenceMaintenanceRecord maintenance =
                    SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidenceA.EvidenceId,
                        new SourceEvidenceCorrectionRequest(
                            SourceEvidenceMetadataMutation.Set(
                                "Freeze lineage developmental label"),
                            SourceEvidenceMetadataMutation.Keep(),
                            "Developmental freeze-lineage proof"));

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
                UPDATE HLAS_Frozen_States
                SET TargetEvidenceId = $wrongEvidenceId
                WHERE FreezeId = $freezeId;
                """;

                    command.Parameters.AddWithValue(
                        "$wrongEvidenceId",
                        evidenceB.EvidenceId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$freezeId",
                        maintenance.FreezeId.Value.ToString("D"));

                    command.ExecuteNonQuery();
                }

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "Source Evidence maintenance Pre-Change Freeze lineage is inconsistent.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_BrokenRequirementRevisionChain_BlocksLineageGate()
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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string operationId1 =
                    Guid.NewGuid().ToString("D");

                string operationId2 =
                    Guid.NewGuid().ToString("D");

                string revisionId1 =
                    Guid.NewGuid().ToString("D");

                string revisionId2 =
                    Guid.NewGuid().ToString("D");

                string wrongPriorRevisionId =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.ToString("O");

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
            PRAGMA foreign_keys = OFF;

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
                $operationId1,
                $projectId,
                $userId1,
                'V',
                'Admin',
                $timestamp,
                $timestamp,
                'SUCCESS'
            );

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
                $operationId2,
                $projectId,
                $userId2,
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
                $revisionId1,
                1,
                NULL,
                $operationId1,
                $timestamp
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
                $revisionId2,
                2,
                $wrongPriorRevisionId,
                $operationId2,
                $timestamp
            );

            PRAGMA foreign_keys = ON;
            """;

                command.Parameters.AddWithValue(
                    "$operationId1",
                    operationId1);

                command.Parameters.AddWithValue(
                    "$operationId2",
                    operationId2);

                command.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$userId1",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$userId2",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$revisionId1",
                    revisionId1);

                command.Parameters.AddWithValue(
                    "$revisionId2",
                    revisionId2);

                command.Parameters.AddWithValue(
                    "$wrongPriorRevisionId",
                    wrongPriorRevisionId);

                command.Parameters.AddWithValue(
                    "$timestamp",
                    timestamp);

                command.ExecuteNonQuery();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "Source Evidence Requirement prior-revision lineage does not point to the immediately preceding revision.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_DamagedHistoricalProjectJmfRevision_BlocksLineageGate()
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
                    "Historical Project JMF Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental historical Project/JMF source.");

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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string revision1Id;

                using (SqliteCommand readCommand =
                    connection.CreateCommand())
                {
                    readCommand.CommandText =
                        """
                SELECT ProjectJmfRevisionId
                FROM HLAS_Project_JMF_Revisions
                WHERE RevisionNumber = 1;
                """;

                    revision1Id =
                        Convert.ToString(
                            readCommand.ExecuteScalar())
                        ?? throw new InvalidOperationException(
                            "Developmental Project/JMF revision 1 was not found.");
                }

                string operationId2 =
                    Guid.NewGuid().ToString("D");

                string revision2Id =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.AddSeconds(1).ToString("O");

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                using (SqliteCommand command =
                    connection.CreateCommand())
                {
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
                    $revision2Id,
                    2,
                    $revision1Id,
                    $sourceEvidenceId,
                    $operationId,
                    $timestamp
                );

                INSERT INTO HLAS_Project_JMF_Revision_Fields
                (
                    ProjectJmfRevisionId,
                    FieldId,
                    FieldValue,
                    SourceEvidenceId
                )
                SELECT
                    $revision2Id,
                    FieldId,
                    FieldValue,
                    SourceEvidenceId
                FROM HLAS_Project_JMF_Revision_Fields
                WHERE ProjectJmfRevisionId = $revision1Id;

                DELETE FROM HLAS_Project_JMF_Revision_Fields
                WHERE
                    ProjectJmfRevisionId = $revision1Id
                    AND FieldId = 'JMF-013';
                """;

                    command.Parameters.AddWithValue(
                        "$operationId",
                        operationId2);

                    command.Parameters.AddWithValue(
                        "$projectId",
                        manifest.ProjectId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$userId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        timestamp);

                    command.Parameters.AddWithValue(
                        "$revision2Id",
                        revision2Id);

                    command.Parameters.AddWithValue(
                        "$revision1Id",
                        revision1Id);

                    command.Parameters.AddWithValue(
                        "$sourceEvidenceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    command.ExecuteNonQuery();
                }

                transaction.Commit();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "A historical Project/JMF revision does not contain the complete governed 20-field set.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_ResolvedKeepCurrentReview_PassesLineageGate()
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
                    "Resolved Keep Current Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental resolved review source.");

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

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_AllKeepCurrentWithResultingRevision_BlocksLineageGate()
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
                    "Keep Current Review Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental KEEP CURRENT review source.");

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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string baselineRevisionId;
                string resolvedOperationId;

                using (SqliteCommand readCommand =
                    connection.CreateCommand())
                {
                    readCommand.CommandText =
                        """
                SELECT
                    BaselineProjectJmfRevisionId,
                    ResolvedOperationId
                FROM HLAS_Project_JMF_Review_Cases
                LIMIT 1;
                """;

                    using SqliteDataReader reader =
                        readCommand.ExecuteReader();

                    Assert.IsTrue(
                        reader.Read());

                    baselineRevisionId =
                        reader.GetString(0);

                    resolvedOperationId =
                        reader.GetString(1);
                }

                string revision2Id =
                    Guid.NewGuid().ToString("D");

                string timestamp =
                    DateTimeOffset.UtcNow.AddSeconds(2).ToString("O");

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                using (SqliteCommand command =
                    connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText =
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
                    $revision2Id,
                    2,
                    $baselineRevisionId,
                    $sourceEvidenceId,
                    $resolvedOperationId,
                    $timestamp
                );

                INSERT INTO HLAS_Project_JMF_Revision_Fields
                (
                    ProjectJmfRevisionId,
                    FieldId,
                    FieldValue,
                    SourceEvidenceId
                )
                SELECT
                    $revision2Id,
                    FieldId,
                    FieldValue,
                    SourceEvidenceId
                FROM HLAS_Project_JMF_Revision_Fields
                WHERE ProjectJmfRevisionId = $baselineRevisionId;
                """;

                    command.Parameters.AddWithValue(
                        "$revision2Id",
                        revision2Id);

                    command.Parameters.AddWithValue(
                        "$baselineRevisionId",
                        baselineRevisionId);

                    command.Parameters.AddWithValue(
                        "$sourceEvidenceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$resolvedOperationId",
                        resolvedOperationId);

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        timestamp);

                    command.ExecuteNonQuery();
                }

                transaction.Commit();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "All KEEP CURRENT Project/JMF review created an unexpected resulting revision.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_AcceptNewWithoutResultingRevision_BlocksLineageGate()
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
                    "Accept New Review Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental ACCEPT NEW review source.");

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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string baselineRevisionId;

                using (SqliteCommand revisionCommand =
                    connection.CreateCommand())
                {
                    revisionCommand.CommandText =
                        """
                SELECT ProjectJmfRevisionId
                FROM HLAS_Project_JMF_Revisions
                WHERE RevisionNumber = 1;
                """;

                    baselineRevisionId =
                        Convert.ToString(
                            revisionCommand.ExecuteScalar())
                        ?? throw new InvalidOperationException(
                            "Developmental baseline Project/JMF revision was not found.");
                }

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
                $openedOperationId,
                $projectId,
                $openedUserId,
                'V',
                'Admin',
                $openedTimestamp,
                $openedTimestamp,
                'SUCCESS'
            );

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
                $resolvedOperationId,
                $projectId,
                $resolvedUserId,
                'V',
                'Admin',
                $resolvedTimestamp,
                $resolvedTimestamp,
                'SUCCESS'
            );

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
                'ACCEPT NEW',
                'Developmental ACCEPT NEW proof.',
                $resolvedOperationId,
                $resolvedTimestamp
            );
            """;

                command.Parameters.AddWithValue(
                    "$openedOperationId",
                    openedOperationId);

                command.Parameters.AddWithValue(
                    "$resolvedOperationId",
                    resolvedOperationId);

                command.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$openedUserId",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$resolvedUserId",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$openedTimestamp",
                    openedTimestamp);

                command.Parameters.AddWithValue(
                    "$resolvedTimestamp",
                    resolvedTimestamp);

                command.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                command.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    evidence.EvidenceId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$baselineRevisionId",
                    baselineRevisionId);

                command.ExecuteNonQuery();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "Project/JMF review accepted new values but has no resulting governed Project/JMF revision.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_CorrectAcceptNewResultingRevision_PassesLineageGate()
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

            string baselineFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Baseline Project JMF Source.txt");

            string reviewFilePath =
                Path.Combine(
                    sourceDirectory,
                    "Review Project JMF Source.txt");

            File.WriteAllText(
                baselineFilePath,
                "HLAS developmental baseline Project/JMF source.");

            File.WriteAllText(
                reviewFilePath,
                "HLAS developmental later Project/JMF review source.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord baselineEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        baselineFilePath);

                EvidenceCustodyRecord reviewEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        reviewFilePath);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(
                        projectRoot);

                AddCompleteProjectJmfTruth(
                    projectRoot,
                    manifest,
                    baselineEvidence.EvidenceId);

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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string baselineRevisionId;

                using (SqliteCommand readCommand =
                    connection.CreateCommand())
                {
                    readCommand.CommandText =
                        """
                SELECT ProjectJmfRevisionId
                FROM HLAS_Project_JMF_Revisions
                WHERE RevisionNumber = 1;
                """;

                    baselineRevisionId =
                        Convert.ToString(
                            readCommand.ExecuteScalar())
                        ?? throw new InvalidOperationException(
                            "Developmental baseline Project/JMF revision was not found.");
                }

                string openedOperationId =
                    Guid.NewGuid().ToString("D");

                string resolvedOperationId =
                    Guid.NewGuid().ToString("D");

                string reviewCaseId =
                    Guid.NewGuid().ToString("D");

                string revision2Id =
                    Guid.NewGuid().ToString("D");

                string openedTimestamp =
                    DateTimeOffset.UtcNow.ToString("O");

                string resolvedTimestamp =
                    DateTimeOffset.UtcNow.AddSeconds(1).ToString("O");

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
                Outcome
            )
            VALUES
            (
                $openedOperationId,
                $projectId,
                $openedUserId,
                'V',
                'Admin',
                $openedTimestamp,
                $openedTimestamp,
                'SUCCESS'
            );

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
                $resolvedOperationId,
                $projectId,
                $resolvedUserId,
                'V',
                'Admin',
                $resolvedTimestamp,
                $resolvedTimestamp,
                'SUCCESS'
            );

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
                $reviewEvidenceId,
                $baselineRevisionId,
                $openedOperationId,
                $openedTimestamp,
                $resolvedOperationId,
                $resolvedTimestamp
            );

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
                'VALUE-JMF-001',
                'NEW-JMF-001',
                'ACCEPT NEW',
                'Developmental ACCEPT NEW proof.',
                $resolvedOperationId,
                $resolvedTimestamp
            );

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
                $revision2Id,
                2,
                $baselineRevisionId,
                $reviewEvidenceId,
                $resolvedOperationId,
                $resolvedTimestamp
            );

            INSERT INTO HLAS_Project_JMF_Revision_Fields
            (
                ProjectJmfRevisionId,
                FieldId,
                FieldValue,
                SourceEvidenceId
            )
            SELECT
                $revision2Id,
                FieldId,
                FieldValue,
                SourceEvidenceId
            FROM HLAS_Project_JMF_Revision_Fields
            WHERE ProjectJmfRevisionId = $baselineRevisionId;

            UPDATE HLAS_Project_JMF_Revision_Fields
            SET
                FieldValue = 'NEW-JMF-001',
                SourceEvidenceId = $reviewEvidenceId
            WHERE
                ProjectJmfRevisionId = $revision2Id
                AND FieldId = 'JMF-001';
            """;

                command.Parameters.AddWithValue(
                    "$openedOperationId",
                    openedOperationId);

                command.Parameters.AddWithValue(
                    "$resolvedOperationId",
                    resolvedOperationId);

                command.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$openedUserId",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$resolvedUserId",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$openedTimestamp",
                    openedTimestamp);

                command.Parameters.AddWithValue(
                    "$resolvedTimestamp",
                    resolvedTimestamp);

                command.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                command.Parameters.AddWithValue(
                    "$reviewEvidenceId",
                    reviewEvidence.EvidenceId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$baselineRevisionId",
                    baselineRevisionId);

                command.Parameters.AddWithValue(
                    "$revision2Id",
                    revision2Id);

                command.ExecuteNonQuery();

                transaction.Commit();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void LineageReadinessEvaluator_BackwardProjectJmfRevisionTime_BlocksLineageGate()
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
                    "Temporal Project JMF Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental temporal Project/JMF source.");

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

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                string revision1Id;

                using (SqliteCommand readCommand =
                    connection.CreateCommand())
                {
                    readCommand.CommandText =
                        """
                SELECT ProjectJmfRevisionId
                FROM HLAS_Project_JMF_Revisions
                WHERE RevisionNumber = 1;
                """;

                    revision1Id =
                        Convert.ToString(
                            readCommand.ExecuteScalar())
                        ?? throw new InvalidOperationException(
                            "Developmental Project/JMF revision 1 was not found.");
                }

                string operationId2 =
                    Guid.NewGuid().ToString("D");

                string revision2Id =
                    Guid.NewGuid().ToString("D");

                string earlierTimestamp =
                    DateTimeOffset.UtcNow.AddHours(-1).ToString("O");

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
                $revision2Id,
                2,
                $revision1Id,
                $sourceEvidenceId,
                $operationId,
                $timestamp
            );

            INSERT INTO HLAS_Project_JMF_Revision_Fields
            (
                ProjectJmfRevisionId,
                FieldId,
                FieldValue,
                SourceEvidenceId
            )
            SELECT
                $revision2Id,
                FieldId,
                FieldValue,
                SourceEvidenceId
            FROM HLAS_Project_JMF_Revision_Fields
            WHERE ProjectJmfRevisionId = $revision1Id;
            """;

                command.Parameters.AddWithValue(
                    "$operationId",
                    operationId2);

                command.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$timestamp",
                    earlierTimestamp);

                command.Parameters.AddWithValue(
                    "$revision2Id",
                    revision2Id);

                command.Parameters.AddWithValue(
                    "$revision1Id",
                    revision1Id);

                command.Parameters.AddWithValue(
                    "$sourceEvidenceId",
                    evidence.EvidenceId.Value.ToString("D"));

                command.ExecuteNonQuery();

                transaction.Commit();

                ReadinessCheckItemRecord result =
                    LineageReadinessEvaluator.Evaluate(
                        projectRoot,
                        ReadinessCheckId.CreateNew());

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.GateStatus);

                Assert.AreEqual(
                    "Project/JMF revision lineage runs backward in governed time.",
                    result.Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
               [TestMethod]
        public void Evaluate_CompleteDevelopmentalProject_AllSevenGatesPassAndReturnsReady()
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
                    "Developmental Ready Project JMF Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental complete READY proof source.");

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
                    'DEVELOPMENTAL_READY_REQUIREMENT',
                    'NOT_REQUIRED',
                    NULL,
                    'Developmental seven-gate READY proof.'
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
                    ReadinessStatus.Ready,
                    result.Check.OverallStatus);

                Assert.HasCount(
                    7,
                    result.Items);

                foreach (ReadinessCheckItemRecord item in result.Items)
                {
                    Assert.AreEqual(
                        ReadinessGateStatus.Pass,
                        item.GateStatus);
                }
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
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
     ReadinessGateStatus.Pass,
     result.Items[3].GateStatus);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[4].GateStatus);

                Assert.AreEqual(
     ReadinessGateStatus.Pass,
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
        [TestMethod]
        public void Evaluate_OpenMaintenanceOperation_BlocksMaintenanceGate()
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

                _ = GovernedOperationService.Begin(
                    projectRoot,
                    manifest.ProjectId,
                    UserId.CreateNew(),
                    SeriesId.V,
                    ProjectRole.Admin,
                    "SOURCE_EVIDENCE_CORRECT");

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.Maintenance,
                    result.Items[3].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[3].GateStatus);

                Assert.AreEqual(
                    "A governed Source Evidence maintenance or revalidation operation remains open.",
                    result.Items[3].Detail);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_SuccessfulMaintenanceWithoutResolution_BlocksMaintenanceGate()
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
                    "Developmental Maintenance Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental maintenance Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                _ = SourceEvidenceMaintenanceGateway.Correct(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    evidence.EvidenceId,
                    new SourceEvidenceCorrectionRequest(
                        SourceEvidenceMetadataMutation.Set(
                            "Maintenance gate developmental label"),
                        SourceEvidenceMetadataMutation.Keep(),
                        "Developmental unresolved maintenance proof"));

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.Maintenance,
                    result.Items[3].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Blocked,
                    result.Items[3].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_FailedMaintenanceWithoutAcceptedRecord_DoesNotBlockMaintenanceGate()
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
                    "Developmental Failed Maintenance Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental failed maintenance Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

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
                CREATE TRIGGER HLAS_Test_ForceReadinessMaintenanceFailure
                BEFORE INSERT ON HLAS_Source_Evidence_Maintenance
                BEGIN
                    SELECT RAISE(
                        ABORT,
                        'forced developmental maintenance failure');
                END;
                """;

                    command.ExecuteNonQuery();
                }

                Assert.ThrowsExactly<SqliteException>(
                    () => SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidence.EvidenceId,
                        new SourceEvidenceCorrectionRequest(
                            SourceEvidenceMetadataMutation.Set(
                                "Failed maintenance developmental label"),
                            SourceEvidenceMetadataMutation.Keep(),
                            "Developmental failed maintenance readiness proof")));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.Maintenance,
                    result.Items[3].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[3].GateStatus);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Evaluate_RevalidatedMaintenance_PassesMaintenanceGate()
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
                    "Developmental Revalidated Maintenance Source.txt");

            File.WriteAllText(
                sourceFilePath,
                "HLAS developmental revalidated maintenance Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourceFilePath);

                SourceEvidenceMaintenanceRecord maintenance =
                    SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidence.EvidenceId,
                        new SourceEvidenceCorrectionRequest(
                            SourceEvidenceMetadataMutation.Set(
                                "Revalidated maintenance developmental label"),
                            SourceEvidenceMetadataMutation.Keep(),
                            "Developmental maintenance revalidation proof"));

                _ = SourceEvidenceMaintenanceGateway.RevalidateMaintenance(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    maintenance.OperationId);

                ReadinessCheckResult result =
                    ReadinessGateway.Evaluate(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Technician);

                Assert.AreEqual(
                    ReadinessGateCode.Maintenance,
                    result.Items[3].GateCode);

                Assert.AreEqual(
                    ReadinessGateStatus.Pass,
                    result.Items[3].GateStatus);
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