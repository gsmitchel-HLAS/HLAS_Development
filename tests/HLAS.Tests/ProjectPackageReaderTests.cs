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

        [TestMethod]
        public void Open_UnsupportedDatabaseSchemaVersion_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                ReplaceDatabaseSchemaVersion(
                    databasePath,
                    999);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectPackageReader.Open(projectRoot));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                StringAssert.Contains(
                    exception.Message,
                    "schema version is unsupported");
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void Open_MissingDatabaseSchemaVersion_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRootPath();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                string databasePath = Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                RemoveDatabaseSchemaVersion(databasePath);

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

        private static void ReplaceDatabaseSchemaVersion(
            string databasePath,
            int replacementVersion)
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
                SET DatabaseSchemaVersion = $version
                WHERE SingletonId = 1;
                """;

            command.Parameters.AddWithValue(
                "$version",
                replacementVersion);

            command.ExecuteNonQuery();
        }

        private static void RemoveDatabaseSchemaVersion(
            string databasePath)
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

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand renameTable =
                connection.CreateCommand();

            renameTable.Transaction = transaction;
            renameTable.CommandText =
                """
                ALTER TABLE HLAS_Project_Metadata
                RENAME TO HLAS_Project_Metadata_Old;
                """;

            renameTable.ExecuteNonQuery();

            using SqliteCommand createTable =
                connection.CreateCommand();

            createTable.Transaction = transaction;
            createTable.CommandText =
                """
                CREATE TABLE HLAS_Project_Metadata
                (
                    SingletonId INTEGER NOT NULL
                        PRIMARY KEY
                        CHECK (SingletonId = 1),
                    ProjectId TEXT NOT NULL
                );
                """;

            createTable.ExecuteNonQuery();

            using SqliteCommand copyData =
                connection.CreateCommand();

            copyData.Transaction = transaction;
            copyData.CommandText =
                """
                INSERT INTO HLAS_Project_Metadata
                    (SingletonId, ProjectId)
                SELECT
                    SingletonId,
                    ProjectId
                FROM HLAS_Project_Metadata_Old;
                """;

            copyData.ExecuteNonQuery();

            using SqliteCommand dropOldTable =
                connection.CreateCommand();

            dropOldTable.Transaction = transaction;
            dropOldTable.CommandText =
                """
                DROP TABLE HLAS_Project_Metadata_Old;
                """;

            dropOldTable.ExecuteNonQuery();

            transaction.Commit();
        }
    }
}