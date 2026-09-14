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
    public sealed class ProjectPackageCreatorTests
    {
        [TestMethod]
        public void CreateNew_NewFolder_CreatesCoherentPackage()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                string manifestPath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                Assert.IsTrue(Directory.Exists(projectRoot));
                Assert.IsTrue(File.Exists(manifestPath));
                Assert.IsTrue(File.Exists(databasePath));

                Guid manifestFileProjectId =
                    ReadManifestProjectId(manifestPath);

                Guid databaseProjectId =
                    ReadDatabaseProjectId(databasePath);

                Assert.AreEqual(
                    manifest.ProjectId.Value,
                    manifestFileProjectId);

                Assert.AreEqual(
                    manifest.ProjectId.Value,
                    databaseProjectId);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void CreateNew_ExistingEmptyFolder_CreatesPackage()
        {
            string projectRoot = CreateTemporaryProjectRootPath();
            Directory.CreateDirectory(projectRoot);

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                Assert.AreNotEqual(
                    Guid.Empty,
                    manifest.ProjectId.Value);

                Assert.IsTrue(File.Exists(Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName)));

                Assert.IsTrue(File.Exists(Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName)));
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void CreateNew_NonEmptyFolder_SafeStopsAndPreservesContent()
        {
            string projectRoot = CreateTemporaryProjectRootPath();
            Directory.CreateDirectory(projectRoot);

            string existingFilePath =
                Path.Combine(projectRoot, "ExistingUserFile.txt");

            const string existingContent =
                "This content must remain unchanged.";

            File.WriteAllText(
                existingFilePath,
                existingContent);

            try
            {
                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageCreator.CreateNew(
                            projectRoot));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.IsTrue(File.Exists(existingFilePath));

                Assert.AreEqual(
                    existingContent,
                    File.ReadAllText(existingFilePath));

                Assert.IsFalse(File.Exists(Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName)));

                Assert.IsFalse(File.Exists(Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName)));
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void CreateNew_SecondAttempt_SafeStopsWithoutChangingIdentity()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectManifest firstManifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                string manifestPath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                Guid manifestProjectIdBefore =
                    ReadManifestProjectId(manifestPath);

                Guid databaseProjectIdBefore =
                    ReadDatabaseProjectId(databasePath);

                Assert.ThrowsExactly<InvalidOperationException>(
                    () => ProjectPackageCreator.CreateNew(
                        projectRoot));

                Assert.AreEqual(
                    firstManifest.ProjectId.Value,
                    ReadManifestProjectId(manifestPath));

                Assert.AreEqual(
                    manifestProjectIdBefore,
                    ReadManifestProjectId(manifestPath));

                Assert.AreEqual(
                    databaseProjectIdBefore,
                    ReadDatabaseProjectId(databasePath));
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        private static string CreateTemporaryProjectRootPath()
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
                Directory.Delete(projectRoot, recursive: true);
            }
        }

        private static Guid ReadManifestProjectId(
            string manifestPath)
        {
            using JsonDocument document =
                JsonDocument.Parse(
                    File.ReadAllText(manifestPath));

            string projectIdText =
                document.RootElement
                    .GetProperty("ProjectId")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Manifest ProjectId is missing.");

            return Guid.Parse(projectIdText);
        }

        private static Guid ReadDatabaseProjectId(
            string databasePath)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT ProjectId
                FROM HLAS_Project_Metadata
                WHERE SingletonId = 1;
                """;

            object? result = command.ExecuteScalar();

            if (result is not string projectIdText)
            {
                throw new InvalidOperationException(
                    "Database ProjectId is missing.");
            }

            return Guid.Parse(projectIdText);
        }
    }
}