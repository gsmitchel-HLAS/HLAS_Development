using System;
using System.IO;
using System.Linq;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityProjectReportBuilderTests
    {
        [TestMethod]
        public void Build_NewProject_ReportsCoverageWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(project.ProjectId, report.ProjectId);
                Assert.HasCount(15, report.Items);

                Assert.AreEqual(
                    report.Items.Count,
                    report.Items.Select(x => x.CheckCode)
                        .Distinct(StringComparer.Ordinal)
                        .Count());

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "HISTORICAL_REQUIREMENT_APPLICABILITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "BACKUP_RESTORE_COVERAGE").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);

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
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Build_WrongProjectId_PreservesFailureAndSkipsOtherChecks()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                ProjectId wrongId = ProjectId.CreateNew();

                Assert.AreNotEqual(project.ProjectId, wrongId);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        wrongId);

                Assert.HasCount(15, report.Items);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    if (item.CheckCode == "PROJECT_IDENTITY")
                        continue;

                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Dependent check was not safely skipped: {item.CheckCode}");
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_NewProject_PreservesMaintenanceVerifierResult()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                ProjectIntegrityCheckItem expected =
                    ProjectIntegrityMaintenanceVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    expected.Status);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                ProjectIntegrityCheckItem actual =
                    GetItem(
                        report,
                        "SOURCE_EVIDENCE_MAINTENANCE");

                Assert.AreEqual(expected.Status, actual.Status);
                Assert.AreEqual(expected.Detail, actual.Detail);
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_PendingAndDamagedOperation_PreservesBothWithoutWrites()
        {
            string root = NewRoot();

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

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

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
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "GOVERNED_OPERATIONS").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_ManifestDatabaseIdentityMismatch_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root,
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

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        UPDATE HLAS_Project_Metadata
                        SET ProjectId = $wrongProjectId
                        WHERE SingletonId = 1;
                        """;

                    command.Parameters.AddWithValue(
                        "$wrongProjectId",
                        ProjectId.CreateNew().Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] before = File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MissingManifest_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                File.Delete(manifestPath);

                SqliteConnection.ClearAllPools();

                byte[] before =
                    File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(databasePath));

                Assert.IsFalse(File.Exists(manifestPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_InvalidManifestProjectId_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                manifest["ProjectId"] = "INVALID-GUID";

                File.WriteAllText(
                    manifestPath,
                    manifest.ToJsonString());

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
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MissingManifestFormatVersion_UsesFailureMarkerWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                Assert.IsTrue(
                    manifest.AsObject().Remove("ProjectFormatVersion"));

                File.WriteAllText(
                    manifestPath,
                    manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(root));

                Assert.IsTrue(
                    ProjectPackageFailureMarker.TryRead(
                        exception,
                        out ProjectPackageIssue issue));

                Assert.AreEqual(
                    ProjectPackageIssue.InvalidManifestFormatMetadata,
                    issue);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MalformedManifest_UsesFailureMarkerWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                File.WriteAllText(
                    manifestPath,
                    "{ malformed JSON");

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(root));

                Assert.IsTrue(
                    ProjectPackageFailureMarker.TryRead(
                        exception,
                        out ProjectPackageIssue issue));

                Assert.AreEqual(
                    ProjectPackageIssue.MalformedManifest,
                    issue);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_TamperedRegisteredEvidence_ReportsFailureWithoutWrites()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            try
            {
                Directory.CreateDirectory(root);

                File.WriteAllText(
                    sourcePath,
                    "Developmental registered evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                string controlledPath = Path.Combine(
                    projectRoot,
                    evidence.RelativeCustodyPath);

                byte[] corrupted = File.ReadAllBytes(controlledPath);
                Assert.IsNotEmpty(corrupted);

                corrupted[0] = (byte)(corrupted[0] ^ 1);
                File.WriteAllBytes(controlledPath, corrupted);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);
                byte[] evidenceBefore = File.ReadAllBytes(controlledPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MissingRegisteredEvidence_PreservesPendingAndFailureWithoutWrites()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            try
            {
                Directory.CreateDirectory(root);

                File.WriteAllText(
                    sourcePath,
                    "Developmental registered evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                string controlledPath = Path.Combine(
                    projectRoot,
                    evidence.RelativeCustodyPath);

                Assert.IsTrue(File.Exists(controlledPath));
                File.Delete(controlledPath);
                Assert.IsFalse(File.Exists(controlledPath));

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                Assert.IsFalse(File.Exists(controlledPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_InvalidManifestRoot_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                File.WriteAllText(manifestPath, "[]");

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(root));

                Assert.IsTrue(
                    ProjectPackageFailureMarker.TryRead(
                        exception,
                        out ProjectPackageIssue issue));

                Assert.AreEqual(
                    ProjectPackageIssue.MalformedManifest,
                    issue);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_InvalidManifestCreatedUtcType_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                manifest["CreatedUtc"] = 42;

                File.WriteAllText(
                    manifestPath,
                    manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(root));

                Assert.IsTrue(
                    ProjectPackageFailureMarker.TryRead(
                        exception,
                        out ProjectPackageIssue issue));

                Assert.AreEqual(
                    ProjectPackageIssue.InvalidManifestCreatedUtc,
                    issue);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

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
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_InvalidManifestFormatType_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                manifest["ProjectFormatVersion"] = "invalid-type";

                File.WriteAllText(
                    manifestPath,
                    manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(root));

                Assert.IsTrue(
                    ProjectPackageFailureMarker.TryRead(
                        exception,
                        out ProjectPackageIssue issue));

                Assert.AreEqual(
                    ProjectPackageIssue.InvalidManifestFormatMetadata,
                    issue);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

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
                DeleteRoot(root);
            }
        }
        private static ProjectIntegrityCheckItem GetItem(
            ProjectIntegrityCheckReport report,
            string checkCode)
        {
            return report.Items.Single(
                item => item.CheckCode == checkCode);
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