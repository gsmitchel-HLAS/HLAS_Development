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
        public void Build_InvalidManifest_PreservesEntireProjectFileSet()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                string manifestPath = Path.Combine(
                    projectRoot, ProjectPackageCreator.ManifestFileName);

                File.WriteAllText(manifestPath, "[]");

                SqliteConnection.ClearAllPools();

                string[] beforeFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(projectRoot, path)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, beforeFiles[i])),
                        beforeFiles[i]);
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_InvalidProjectPath_RemainsNotChecked()
        {
            int observedChecks = 0;

            ProjectIntegrityCheckReport report =
                ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                    "invalid\0project",
                    ProjectId.CreateNew(),
                    _ => observedChecks++);

            Assert.AreEqual(0, observedChecks);

            foreach (ProjectIntegrityCheckItem item in report.Items)
            {
                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    item.Status,
                    $"Unexpected status: {item.CheckCode}");
            }
        }
        [TestMethod]
        public void Build_MissingProjectRoot_RemainsNotCheckedWithoutCreatingFiles()
        {
            string root = NewRoot();

            try
            {
                Assert.IsFalse(Directory.Exists(root));

                int observedChecks = 0;
                ProjectId projectId = ProjectId.CreateNew();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        projectId,
                        _ => observedChecks++);

                Assert.AreEqual(projectId, report.ProjectId);
                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

                Assert.IsFalse(Directory.Exists(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_ProjectRootIsFile_RemainsNotCheckedWithoutWrites()
        {
            string workspace = NewRoot();

            try
            {
                Directory.CreateDirectory(workspace);

                string projectRoot =
                    Path.Combine(workspace, "ProjectAsFile");

                File.WriteAllText(projectRoot, "not a project folder");

                byte[] before = File.ReadAllBytes(projectRoot);
                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        projectRoot,
                        ProjectId.CreateNew(),
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(projectRoot));

                Assert.HasCount(
                    1,
                    Directory.GetFiles(
                        workspace, "*", SearchOption.AllDirectories));
            }
            finally
            {
                DeleteRoot(workspace);
            }
        }
        [TestMethod]
        public void Build_EmptyProjectFolder_ReportsFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                Directory.CreateDirectory(root);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        ProjectId.CreateNew(),
                        _ => observedChecks++);

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
                        $"Unexpected status: {item.CheckCode}");
                }

                Assert.AreEqual(0, observedChecks);

                Assert.HasCount(
                    0,
                    Directory.GetFileSystemEntries(root));
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
                string[] beforeFiles = Directory
                       .GetFiles(root, "*", SearchOption.AllDirectories)
                       .Select(path => Path.GetRelativePath(root, path))
                       .OrderBy(path => path, StringComparer.Ordinal)
                       .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(root, path)))
                    .ToArray();
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
                string[] afterFiles = Directory
    .GetFiles(root, "*", SearchOption.AllDirectories)
    .Select(path => Path.GetRelativePath(root, path))
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(root, beforeFiles[i])),
                        beforeFiles[i]);
                }
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
                string[] beforeFiles = Directory
                    .GetFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(root, path)))
                    .ToArray();
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
                string[] afterFiles = Directory
                    .GetFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(root, beforeFiles[i])),
                        beforeFiles[i]);
                }
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
        public void Build_MissingDatabaseMetadata_ReportsFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

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
                        DELETE FROM HLAS_Project_Metadata
                        WHERE SingletonId = 1;
                        """;

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

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
                        $"Unexpected status: {item.CheckCode}");
                }

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
        public void Build_InvalidDatabaseProjectId_ReportsFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

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
                        SET ProjectId = $invalidId
                        WHERE SingletonId = 1;
                        """;

                    command.Parameters.AddWithValue(
                        "$invalidId", "not-a-valid-guid");

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

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
                        $"Unexpected status: {item.CheckCode}");
                }

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
        public void Build_LockedManifest_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report;

                using (FileStream lockedManifest = new(
                    manifestPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    report =
                        ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                            root,
                            project.ProjectId,
                            _ => observedChecks++);
                }

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_LockedDatabase_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                string[] filesBefore =
                    Directory.GetFiles(
                        root, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                int observedChecks = 0;
                ProjectIntegrityCheckReport report;

                using (FileStream lockedDatabase = new(
                    databasePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    report =
                        ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                            root,
                            project.ProjectId,
                            _ => observedChecks++);
                }

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    filesBefore,
                    Directory.GetFiles(
                        root, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray());
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_UnreadableDatabase_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                File.WriteAllText(
                    databasePath,
                    "This is not a SQLite database.");

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                string[] filesBefore =
                    Directory.GetFiles(
                        root, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    filesBefore,
                    Directory.GetFiles(
                        root, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray());
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
                string[] beforeFiles = Directory
                    .GetFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(root, path)))
                    .ToArray();
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
                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(root, beforeFiles[i])),
                        beforeFiles[i]);
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MissingDatabase_PreservesEntireProjectFileSet()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                string databasePath = Path.Combine(
                    projectRoot, ProjectPackageCreator.DatabaseFileName);

                SqliteConnection.ClearAllPools();
                File.Delete(databasePath);

                string[] beforeFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(projectRoot, path)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, beforeFiles[i])),
                        beforeFiles[i]);
                }

                Assert.IsFalse(File.Exists(databasePath));
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
                string[] beforeFiles = Directory
    .GetFiles(root, "*", SearchOption.AllDirectories)
    .Select(path => Path.GetRelativePath(root, path))
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(root, path)))
                    .ToArray();
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
                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(root, beforeFiles[i])),
                        beforeFiles[i]);
                }
                Assert.IsFalse(File.Exists(manifestPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MissingManifestProjectId_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                Assert.IsTrue(
                    manifest.AsObject().Remove("ProjectId"));

                File.WriteAllText(
                    manifestPath, manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    if (item.CheckCode == "PROJECT_IDENTITY")
                        continue;

                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status);
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
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
        public void Build_UnsupportedManifestFormat_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                manifest["ProjectFormatVersion"] =
                    ProjectManifest.CurrentProjectFormatVersion + 1;

                File.WriteAllText(
                    manifestPath, manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_InvalidDatabaseSchemaVersion_ReportsFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

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
                       "PRAGMA ignore_check_constraints = ON;";
                    command.ExecuteNonQuery();
                    command.CommandText =
                        """
                        UPDATE HLAS_Project_Metadata
                        SET DatabaseSchemaVersion = 0
                        WHERE SingletonId = 1;
                        """;

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    if (item.CheckCode == "PROJECT_IDENTITY")
                        continue;

                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status);
                }

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
        public void Build_UnsupportedDatabaseSchema_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

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
                        SET DatabaseSchemaVersion = $version
                        WHERE SingletonId = 1;
                        """;

                    command.Parameters.AddWithValue(
                        "$version",
                        ProjectDatabaseSchema.CurrentDatabaseSchemaVersion + 1);

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status,
                        $"Unexpected status: {item.CheckCode}");
                }

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
        public void Build_HealthyEvidence_PreservesEntireProjectFileSet()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourcePath = Path.Combine(root, "Source.txt");
                File.WriteAllText(sourcePath, "Developmental evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                _ = ProductionSourceEvidenceIntakeGateway.Accept(
                    projectRoot, sourcePath);

                SqliteConnection.ClearAllPools();

                string[] beforeFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(projectRoot, path)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, beforeFiles[i])),
                        beforeFiles[i]);
                }

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_DeletedOriginalSource_UsesControlledEvidenceWithoutWrites()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string originalPath = Path.Combine(root, "Source.txt");
                File.WriteAllText(originalPath, "Developmental evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, originalPath);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                File.Delete(originalPath);

                Assert.IsFalse(File.Exists(originalPath));
                Assert.IsTrue(File.Exists(controlledPath));

                SqliteConnection.ClearAllPools();

                string[] filesBefore = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(projectRoot, x))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                byte[][] bytesBefore = filesBefore
                    .Select(x => File.ReadAllBytes(
                        Path.Combine(projectRoot, x)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                SqliteConnection.ClearAllPools();

                string[] filesAfter = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(projectRoot, x))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(filesBefore, filesAfter);

                for (int i = 0; i < filesBefore.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        bytesBefore[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, filesBefore[i])));
                }

                Assert.IsFalse(File.Exists(originalPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_ChangedOriginalSource_UsesControlledEvidenceWithoutWrites()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string originalPath = Path.Combine(root, "Source.txt");
                File.WriteAllText(originalPath, "Original evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, originalPath);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                File.WriteAllText(
                    originalPath, "Changed after intake.");

                SqliteConnection.ClearAllPools();

                string[] filesBefore = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(projectRoot, x))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                byte[][] bytesBefore = filesBefore
                    .Select(x => File.ReadAllBytes(
                        Path.Combine(projectRoot, x)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    "Changed after intake.",
                    File.ReadAllText(originalPath));

                Assert.AreEqual(
                    "Original evidence.",
                    File.ReadAllText(controlledPath));

                SqliteConnection.ClearAllPools();

                string[] filesAfter = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(projectRoot, x))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(filesBefore, filesAfter);

                for (int i = 0; i < filesBefore.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        bytesBefore[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, filesBefore[i])));
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_HealthyRegisteredEvidence_DoesNotWrite()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourcePath = Path.Combine(root, "Source.txt");
                File.WriteAllText(sourcePath, "Developmental source.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, sourcePath);

                string databasePath = Path.Combine(
                    projectRoot, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    projectRoot, ProjectPackageCreator.ManifestFileName);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);
                byte[] evidenceBefore = File.ReadAllBytes(controlledPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore, File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore, File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    evidenceBefore, File.ReadAllBytes(controlledPath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_TamperedEvidence_PreservesEntireProjectFileSet()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourcePath = Path.Combine(root, "Source.txt");
                File.WriteAllText(sourcePath, "Developmental evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, sourcePath);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                byte[] tampered = File.ReadAllBytes(controlledPath);
                Assert.IsNotEmpty(tampered);

                tampered[0] ^= 1;
                File.WriteAllBytes(controlledPath, tampered);

                SqliteConnection.ClearAllPools();

                string[] beforeFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(projectRoot, path)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);

                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, beforeFiles[i])),
                        beforeFiles[i]);
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_LockedRegisteredEvidence_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourcePath = Path.Combine(root, "Source.txt");
                File.WriteAllText(sourcePath, "Developmental evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, sourcePath);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                string databasePath = Path.Combine(
                    projectRoot, ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    projectRoot, ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);
                byte[] evidenceBefore = File.ReadAllBytes(controlledPath);

                string[] filesBefore = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                ProjectIntegrityCheckReport report;

                using (FileStream lockedEvidence = new(
                    controlledPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    report = ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);
                }

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);
                ProjectIntegrityCheckReport unlockedReport =
                   ProjectIntegrityProjectReportBuilder.Build(
                       projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(unlockedReport, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(unlockedReport, "SOURCE_EVIDENCE_REQUIREMENTS").Status);
                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore, File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore, File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    evidenceBefore, File.ReadAllBytes(controlledPath));

                CollectionAssert.AreEqual(
                    filesBefore,
                    Directory.GetFiles(
                        projectRoot, "*", SearchOption.AllDirectories)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray());
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_OneOfTwoEvidenceFilesLocked_RemainsNotCheckedWithoutWrites()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourceA = Path.Combine(root, "SourceA.txt");
                string sourceB = Path.Combine(root, "SourceB.txt");

                File.WriteAllText(sourceA, "First evidence.");
                File.WriteAllText(sourceB, "Second evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                _ = ProductionSourceEvidenceIntakeGateway.Accept(
                    projectRoot, sourceA);

                EvidenceCustodyRecord second =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, sourceB);

                string lockedPath = Path.Combine(
                    projectRoot, second.RelativeCustodyPath);

                SqliteConnection.ClearAllPools();

                string[] filesBefore = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(projectRoot, x))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                byte[][] bytesBefore = filesBefore
                    .Select(x => File.ReadAllBytes(
                        Path.Combine(projectRoot, x)))
                    .ToArray();

                ProjectIntegrityCheckReport report;

                using (FileStream lockedFile = new(
                    lockedPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None))
                {
                    report = ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);
                }

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);
                ProjectIntegrityCheckReport unlockedReport =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(unlockedReport, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(unlockedReport, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);
                SqliteConnection.ClearAllPools();

                string[] filesAfter = Directory.GetFiles(
                    projectRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(projectRoot, x))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(filesBefore, filesAfter);

                for (int i = 0; i < filesBefore.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        bytesBefore[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, filesBefore[i])));
                }
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
                Assert.IsTrue(File.Exists(sourcePath));

                Assert.AreEqual(
                    "Developmental registered evidence.",
                    File.ReadAllText(sourcePath));
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
        public void Build_MissingEvidence_PreservesEntireProjectFileSet()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourcePath = Path.Combine(root, "Source.txt");
                File.WriteAllText(sourcePath, "Developmental evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, sourcePath);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                Assert.IsTrue(File.Exists(controlledPath));
                File.Delete(controlledPath);

                SqliteConnection.ClearAllPools();

                string[] beforeFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                byte[][] beforeBytes = beforeFiles
                    .Select(path => File.ReadAllBytes(
                        Path.Combine(projectRoot, path)))
                    .ToArray();

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        projectRoot, project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);

                SqliteConnection.ClearAllPools();

                string[] afterFiles = Directory
                    .GetFiles(projectRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(projectRoot, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEqual(beforeFiles, afterFiles);

                for (int i = 0; i < beforeFiles.Length; i++)
                {
                    CollectionAssert.AreEqual(
                        beforeBytes[i],
                        File.ReadAllBytes(
                            Path.Combine(projectRoot, beforeFiles[i])),
                        beforeFiles[i]);
                }

                Assert.IsFalse(File.Exists(controlledPath));
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
                Assert.IsTrue(
                    File.Exists(sourcePath),
                    "Original source must remain available while controlled evidence is missing.");
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
        public void Build_InvalidManifestCreatedUtcValue_ReportsFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                manifest["CreatedUtc"] = "not-a-valid-date";

                File.WriteAllText(
                    manifestPath, manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    if (item.CheckCode == "PROJECT_IDENTITY")
                        continue;

                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status);
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_MissingManifestCreatedUtc_ReportsIntegrityFailureWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root, ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root, ProjectPackageCreator.DatabaseFileName);

                System.Text.Json.Nodes.JsonNode manifest =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        File.ReadAllText(manifestPath))!;

                Assert.IsTrue(
                    manifest.AsObject().Remove("CreatedUtc"));

                File.WriteAllText(
                    manifestPath, manifest.ToJsonString());

                SqliteConnection.ClearAllPools();

                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        _ => observedChecks++);

                Assert.HasCount(15, report.Items);
                Assert.AreEqual(0, observedChecks);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                foreach (ProjectIntegrityCheckItem item in report.Items)
                {
                    if (item.CheckCode == "PROJECT_IDENTITY")
                        continue;

                    Assert.AreEqual(
                        ProjectIntegrityCheckStatus.NotChecked,
                        item.Status);
                }

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
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
        [TestMethod]
        public void Build_SeparateEvidenceObservations_DoNotClaimProjectWideSnapshot()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            try
            {
                Directory.CreateDirectory(root);

                File.WriteAllText(
                    sourcePath,
                    "Developmental snapshot observation evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                ProjectIntegrityCheckItem first =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        projectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    first.Status);

                string controlledPath = Path.Combine(
                    projectRoot,
                    evidence.RelativeCustodyPath);

                byte[] changed = File.ReadAllBytes(controlledPath);
                Assert.IsNotEmpty(changed);

                changed[0] = (byte)(changed[0] ^ 1);
                File.WriteAllBytes(controlledPath, changed);

                ProjectIntegrityCheckItem second =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        projectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    second.Status);

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
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

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
        public void Build_SeparateDatabaseObservations_DoNotClaimProjectWideSnapshot()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                ProjectIntegrityCheckItem first =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    first.Status);

                GovernedOperationService.Begin(
                    root,
                    project.ProjectId,
                    UserId.CreateNew(),
                    SeriesId.V,
                    ProjectRole.Admin,
                    "DEVELOPMENTAL_TEST");

                ProjectIntegrityCheckItem second =
                    ProjectIntegrityGovernedOperationsVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    second.Status);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.Build(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "GOVERNED_OPERATIONS").Status);

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
        public void Build_EvidenceChangesDuringReport_PreservesSnapshotNotChecked()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");

            try
            {
                Directory.CreateDirectory(root);

                string sourcePath = Path.Combine(root, "Source.txt");
                File.WriteAllText(sourcePath, "Original developmental evidence.");

                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot, sourcePath);

                string controlledPath = Path.Combine(
                    projectRoot, evidence.RelativeCustodyPath);

                int changes = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        projectRoot,
                        project.ProjectId,
                        code =>
                        {
                            if (code == "SOURCE_EVIDENCE_CUSTODY")
                            {
                                File.AppendAllText(
                                    controlledPath,
                                    "Changed during report.");
                                changes++;
                            }
                        });

                Assert.AreEqual(1, changes);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    GetItem(report, "SOURCE_EVIDENCE_CUSTODY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    ProjectIntegrityRegisteredSourceEvidenceVerifier
                        .Check(projectRoot).Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(
                        report,
                        "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_OperationChangesDuringReport_PreservesSnapshotNotChecked()
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

                int changes = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        code =>
                        {
                            if (code == "GOVERNED_OPERATIONS")
                            {
                                GovernedOperationService.FinalizeSuccess(
                                    root,
                                    operation.OperationId);
                                changes++;
                            }
                        });

                Assert.AreEqual(1, changes);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    GetItem(report, "GOVERNED_OPERATIONS").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    ProjectIntegrityGovernedOperationsVerifier
                        .Check(root).Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(
                        report,
                        "PROJECT_WIDE_SNAPSHOT_CONSISTENCY").Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]
        public void Build_HistoricalRequirementCheck_InvokesTestObserver()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                int observed = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        project.ProjectId,
                        code =>
                        {
                            if (code == "HISTORICAL_REQUIREMENT_APPLICABILITY")
                                observed++;
                        });

                Assert.AreEqual(1, observed);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "HISTORICAL_REQUIREMENT_APPLICABILITY").Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }
        [TestMethod]

        public void Build_SkippedChecks_DoNotInvokeTestObserver()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                ProjectId wrongId = ProjectId.CreateNew();

                Assert.AreNotEqual(project.ProjectId, wrongId);

                int observedChecks = 0;

                ProjectIntegrityCheckReport report =
                    ProjectIntegrityProjectReportBuilder.BuildWithTestObserver(
                        root,
                        wrongId,
                        _ => observedChecks++);

                Assert.AreEqual(0, observedChecks);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    GetItem(report, "PROJECT_IDENTITY").Status);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    GetItem(report, "SOURCE_EVIDENCE_REQUIREMENTS").Status);
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