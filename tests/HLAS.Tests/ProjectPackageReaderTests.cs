using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectPackageReaderTests
    {
        [TestMethod]
        public void Open_ValidPackage_ReconstructsSameIdentity()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectManifest created =
                    ProjectPackageCreator.CreateNew(projectRoot);

                ProjectManifest reopened =
                    ProjectPackageReader.Open(projectRoot);

                Assert.AreEqual(
                    created.ProjectId.Value,
                    reopened.ProjectId.Value);

                Assert.AreEqual(
                    created.ProjectFormatVersion,
                    reopened.ProjectFormatVersion);

                Assert.AreEqual(
                    created.CreatedUtc.Value,
                    reopened.CreatedUtc.Value);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void Open_MissingManifest_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                string manifestPath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.ManifestFileName);

                File.Delete(manifestPath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(projectRoot));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void Open_MissingDatabase_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                SqliteConnection.ClearAllPools();

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                File.Delete(databasePath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(projectRoot));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void Open_MismatchedProjectIds_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                ReplaceDatabaseProjectId(
                    databasePath,
                    Guid.NewGuid());

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(projectRoot));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                StringAssert.Contains(
                    exception.Message,
                    "does not match");
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

        private static void ReplaceDatabaseProjectId(
            string databasePath,
            Guid replacementProjectId)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                UPDATE HLAS_Project_Metadata
                SET ProjectId = $projectId
                WHERE SingletonId = 1;
                """;

            command.Parameters.AddWithValue(
                "$projectId",
                replacementProjectId.ToString("D"));

            command.ExecuteNonQuery();
        }
    }
}