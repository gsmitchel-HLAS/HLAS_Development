using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegritySourceEvidenceRequirementsVerifierTests
    {
        [TestMethod]
        public void Build_NoEarlierRequirementRevisions_RemainsNotCheckedWithoutWrites()
        {
            var sample = CreateProject("PENDING");

            try
            {
                ProjectManifest project =
                    ProjectPackageReader.Open(sample.ProjectRoot);

                byte[] before = ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        sample.ProjectRoot,
                        project.ProjectId);

                Assert.AreEqual(
                    "HISTORICAL_REQUIREMENT_APPLICABILITY",
                    report.Items[12].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    report.Items[12].Status);

                StringAssert.Contains(
                    report.Items[12].Detail,
                    "0 earlier requirement revisions examined");

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_NoRevision_ReturnsPendingWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(root);

                Assert.AreEqual(
                    "SOURCE_EVIDENCE_REQUIREMENTS",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_PendingRequirement_ReturnsPending()
        {
            var sample = CreateProject("PENDING");

            try
            {
                byte[] before = ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }

        [TestMethod]
        public void Check_NotRequiredDecision_ReturnsVerified()
        {
            var sample = CreateProject("NOT_REQUIRED");

            try
            {
                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }

        [TestMethod]
        public void Check_ReceivedEvidence_ReturnsVerifiedWithoutWrites()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                byte[] before = ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_MissingEvidence_ReturnsIntegrityFailureWithoutWrites()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                Assert.IsNotNull(sample.ControlledPath);

                File.Delete(sample.ControlledPath);

                byte[] before =
                    ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_TamperedEvidence_ReturnsIntegrityFailure()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                Assert.IsNotNull(sample.ControlledPath);

                File.AppendAllText(
                    sample.ControlledPath,
                    "Tampered developmental evidence.");

                byte[] before = ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_SupersededReceivedEvidence_ReturnsPendingWithoutWrites()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = Path.Combine(
                        sample.ProjectRoot,
                        ProjectPackageCreator.DatabaseFileName),
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
                        WHERE EvidenceId =
                        (
                            SELECT EvidenceId
                            FROM HLAS_Source_Evidence_Requirement_Items
                            WHERE RequirementKey = 'DEVELOPMENTAL_SOURCE'
                            LIMIT 1
                        );
                        """;

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                byte[] before =
                    ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);

                StringAssert.Contains(
                    result.Detail,
                    "superseded");

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Build_ReceivedSource_PreservesVerifiedChecksWithoutWrites()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                Assert.IsNotNull(sample.ControlledPath);
                string originalPath =
                   Path.Combine(sample.Root, "Source.txt");

                Assert.IsTrue(File.Exists(originalPath));
                File.Delete(originalPath);
                Assert.IsFalse(File.Exists(originalPath));
                ProjectManifest project =
                    ProjectPackageReader.Open(sample.ProjectRoot);

                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.ManifestFileName);

                string controlledPath = sample.ControlledPath!;

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] evidenceBefore =
                    File.ReadAllBytes(controlledPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        sample.ProjectRoot,
                        project.ProjectId);

                Assert.HasCount(15, report.Items);

                Assert.AreEqual(
                    "PROJECT_IDENTITY",
                    report.Items[0].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[0].Status);

                Assert.AreEqual(
                    "SOURCE_EVIDENCE_REQUIREMENTS",
                    report.Items[1].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[1].Status);

                Assert.AreEqual(
                    "SOURCE_EVIDENCE_CUSTODY",
                    report.Items[2].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[2].Status);

                Assert.AreEqual(
                    "PROJECT_WIDE_SNAPSHOT_CONSISTENCY",
                    report.Items[14].CheckCode);

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

                CollectionAssert.AreEqual(
                    evidenceBefore,
                    File.ReadAllBytes(controlledPath));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_CrossVerifierEvidenceChange_LeavesSnapshotNotChecked()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                Assert.IsNotNull(sample.ControlledPath);

                ProjectIntegrityCheckItem requirementsBefore =
                    ProjectIntegritySourceEvidenceRequirementsVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    requirementsBefore.Status);

                File.AppendAllText(
                    sample.ControlledPath!,
                    "Changed between independent verifier reads.");

                ProjectIntegrityCheckItem custodyAfter =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    custodyAfter.Status);

                ProjectManifest project =
                    ProjectPackageReader.Open(sample.ProjectRoot);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        sample.ProjectRoot,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[1].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[2].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    report.Items[14].Status);
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Build_EvidenceChangesBetweenChecks_PreservesMixedResults()
        {
            var sample = CreateProject("RECEIVED");

            try
            {
                Assert.IsNotNull(sample.ControlledPath);

                ProjectManifest project =
                    ProjectPackageReader.Open(sample.ProjectRoot);

                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);

                int changes = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        sample.ProjectRoot,
                        project.ProjectId,
                        code =>
                        {
                            if (code == "SOURCE_EVIDENCE_REQUIREMENTS")
                            {
                                File.AppendAllText(
                                    sample.ControlledPath!,
                                    "Changed between checks.");
                                changes++;
                            }
                        });

                Assert.AreEqual(1, changes);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[1].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                     report.Items[2].Status);

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
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Build_LaterRequirementRevision_DoesNotClaimHistoricalApplicability()
        {
            var sample = CreateProject("PENDING");

            try
            {
                ProjectManifest project =
                    ProjectPackageReader.Open(sample.ProjectRoot);

                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

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

                    string operationId = Guid.NewGuid().ToString("D");
                    string revisionId = Guid.NewGuid().ToString("D");
                    string timestamp =
                        DateTimeOffset.UtcNow.AddSeconds(1).ToString("O");

                    using SqliteTransaction transaction =
                        connection.BeginTransaction();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.Transaction = transaction;
                    command.CommandText =
                        """
                INSERT INTO HLAS_Governed_Operations
                (OperationId, ProjectId, UserId, SeriesId,
                 ProjectRole, StartedUtc, CompletedUtc, Outcome)
                VALUES
                ($operationId, $projectId, $userId, 'V',
                 'Admin', $timestamp, $timestamp, 'SUCCESS');

                INSERT INTO HLAS_Source_Evidence_Requirement_Revisions
                (RequirementRevisionId, RevisionNumber,
                 PriorRequirementRevisionId, OperationId, ApprovedUtc)
                SELECT $revisionId, 2, RequirementRevisionId,
                       $operationId, $timestamp
                FROM HLAS_Source_Evidence_Requirement_Revisions
                WHERE RevisionNumber = 1;

                INSERT INTO HLAS_Source_Evidence_Requirement_Items
                (RequirementRevisionId, RequirementKey,
                 RequirementState, EvidenceId, NotRequiredReason)
                VALUES
                ($revisionId, 'DEVELOPMENTAL_SOURCE',
                 'NOT_REQUIRED', NULL, 'Developmental revision.');
                """;

                    command.Parameters.AddWithValue(
                        "$operationId", operationId);
                    command.Parameters.AddWithValue(
                        "$revisionId", revisionId);
                    command.Parameters.AddWithValue(
                        "$projectId", project.ProjectId.Value.ToString("D"));
                    command.Parameters.AddWithValue(
                        "$userId", Guid.NewGuid().ToString("D"));
                    command.Parameters.AddWithValue(
                        "$timestamp", timestamp);

                    command.ExecuteNonQuery();
                    transaction.Commit();
                }
                ProjectIntegrityCheckReport intact =
    ProjectIntegrityProjectReportBuilder.Build(
        sample.ProjectRoot,
        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    intact.Items[1].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    intact.Items[3].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    intact.Items[12].Status);
                StringAssert.Contains(
    intact.Items[12].Detail,
    "1 earlier requirement revisions examined for item presence");
                using (SqliteConnection connection = new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand removeItem =
                        connection.CreateCommand();

                    removeItem.CommandText =
                        """
        DELETE FROM HLAS_Source_Evidence_Requirement_Items
        WHERE RequirementRevisionId =
        (
            SELECT RequirementRevisionId
            FROM HLAS_Source_Evidence_Requirement_Revisions
            WHERE RevisionNumber = 1
        );
        """;

                    Assert.AreEqual(1, removeItem.ExecuteNonQuery());
                }
                ProjectIntegrityCheckItem historicalCoverage =
    ProjectIntegrityHistoricalRequirementCoverageVerifier.Check(
        sample.ProjectRoot);

                Assert.AreEqual(
                    "HISTORICAL_REQUIREMENT_APPLICABILITY",
                    historicalCoverage.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    historicalCoverage.Status);

                StringAssert.Contains(
                    historicalCoverage.Detail,
                    "Historical requirement revisions without recorded items: 1.");
                ProjectIntegrityCheckReport missingHistoricalItem =
                    ProjectIntegrityProjectReportBuilder.Build(
                        sample.ProjectRoot,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    missingHistoricalItem.Items[1].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    missingHistoricalItem.Items[3].Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    missingHistoricalItem.Items[12].Status);
                StringAssert.Contains(
    missingHistoricalItem.Items[12].Detail,
    "Historical requirement revisions without recorded items: 1.");
                using (SqliteConnection connection = new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand historical =
                        connection.CreateCommand();
                      historical.CommandText =
                        """
        UPDATE HLAS_Governed_Operations
        SET Outcome = 'SAFE-STOP',
            Decision = 'REJECTED',
            Reason = 'Intentional developmental history test.'
        WHERE OperationId =
        (
            SELECT OperationId
            FROM HLAS_Source_Evidence_Requirement_Revisions
            WHERE RevisionNumber = 1
        );
        """;

                    Assert.AreEqual(1, historical.ExecuteNonQuery());
                }
                SqliteConnection.ClearAllPools();
                byte[] databaseBefore = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        sample.ProjectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    report.Items[1].Status);
                Assert.AreEqual(
    "LINEAGE_STRUCTURE",
    report.Items[3].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    report.Items[3].Status);
                Assert.AreEqual(
                    "HISTORICAL_REQUIREMENT_APPLICABILITY",
                    report.Items[12].CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    report.Items[12].Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
                int injected = 0;

                ProjectIntegrityCheckReport observed =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        sample.ProjectRoot,
                        project.ProjectId,
                        code =>
                        {
                            if (code != "HISTORICAL_REQUIREMENT_APPLICABILITY")
                                return;

                            using SqliteConnection changeConnection =
                                new(builder.ToString());

                            changeConnection.Open();

                            using SqliteCommand restore =
                                changeConnection.CreateCommand();

                            restore.CommandText =
                                """
                INSERT INTO HLAS_Source_Evidence_Requirement_Items
                (RequirementRevisionId, RequirementKey,
                 RequirementState, EvidenceId, NotRequiredReason)
                SELECT RequirementRevisionId, 'DEVELOPMENTAL_SOURCE',
                       'NOT_REQUIRED', NULL, 'Test-only history change.'
                FROM HLAS_Source_Evidence_Requirement_Revisions
                WHERE RevisionNumber = 1;
                """;

                            Assert.AreEqual(1, restore.ExecuteNonQuery());
                            injected++;
                        });

                Assert.AreEqual(1, injected);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    observed.Items[12].Status);

                StringAssert.Contains(
                    observed.Items[12].Detail,
                    "Historical requirement revisions without recorded items: 1.");

                ProjectIntegrityCheckItem afterChange =
                    ProjectIntegrityHistoricalRequirementCoverageVerifier.Check(
                        sample.ProjectRoot);

                StringAssert.Contains(
                    afterChange.Detail,
                    "1 earlier requirement revisions examined for item presence");
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        private static (
            string Root,
            string ProjectRoot,
            string? ControlledPath)
            CreateProject(string state)
        {
            string root = NewRoot();
            Directory.CreateDirectory(root);

            string projectRoot = Path.Combine(root, "Project");

            ProjectManifest manifest =
                ProjectPackageCreator.CreateNew(projectRoot);

            string? evidenceId = null;
            string? controlledPath = null;

            if (state == "RECEIVED")
            {
                string sourcePath =
                    Path.Combine(root, "Source.txt");

                File.WriteAllText(
                    sourcePath,
                    "Governed developmental source.");

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                evidenceId =
                    evidence.EvidenceId.Value.ToString("D");

                controlledPath = Path.Combine(
                    projectRoot,
                    evidence.RelativeCustodyPath);
            }

            string revisionId =
                Guid.NewGuid().ToString("D");

            string operationId =
                Guid.NewGuid().ToString("D");

            string timestamp =
                DateTimeOffset.UtcNow.ToString("O");

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName),
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
                        $revisionId,
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
                        $revisionId,
                        'DEVELOPMENTAL_SOURCE',
                        $state,
                        $evidenceId,
                        $reason
                    );
                    """;

                command.Parameters.AddWithValue(
                    "$operationId", operationId);

                command.Parameters.AddWithValue(
                    "$projectId",
                    manifest.ProjectId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$userId",
                    Guid.NewGuid().ToString("D"));

                command.Parameters.AddWithValue(
                    "$timestamp", timestamp);

                command.Parameters.AddWithValue(
                    "$revisionId", revisionId);

                command.Parameters.AddWithValue(
                    "$state", state);

                command.Parameters.AddWithValue(
                    "$evidenceId",
                    (object?)evidenceId ?? DBNull.Value);

                command.Parameters.AddWithValue(
                    "$reason",
                    state == "NOT_REQUIRED"
                        ? "Approved developmental exception."
                        : DBNull.Value);

                command.ExecuteNonQuery();
            }

            return (root, projectRoot, controlledPath);
        }

        private static byte[] ReadDatabaseBytes(string root)
        {
            SqliteConnection.ClearAllPools();

            return File.ReadAllBytes(
                Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName));
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