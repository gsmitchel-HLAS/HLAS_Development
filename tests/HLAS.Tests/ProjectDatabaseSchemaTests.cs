using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectDatabaseSchemaTests
    {
        [TestMethod]
        public void InitializeNewDatabase_CommittedTransaction_PersistsMetadata()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();

                using SqliteConnection connection =
                    OpenReadWriteCreateConnection(databasePath);

                connection.Open();

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.InitializeNewDatabase(
                        connection,
                        transaction,
                        projectId);

                    transaction.Commit();
                }

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        ProjectId,
                        DatabaseSchemaVersion
                    FROM HLAS_Project_Metadata
                    WHERE SingletonId = 1;
                    """;

                using SqliteDataReader reader =
                    command.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.AreEqual(
                    projectId.Value.ToString("D"),
                    reader.GetString(0));

                Assert.AreEqual(
                    ProjectDatabaseSchema.CurrentDatabaseSchemaVersion,
                    reader.GetInt32(1));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void InitializeNewDatabase_RolledBackTransaction_LeavesNoSchema()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                using SqliteConnection connection =
                    OpenReadWriteCreateConnection(databasePath);

                connection.Open();

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.InitializeNewDatabase(
                        connection,
                        transaction,
                        ProjectId.CreateNew());

                    transaction.Rollback();
                }

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM sqlite_master
                    WHERE
                        type = 'table'
                        AND name = 'HLAS_Project_Metadata';
                    """;

                long tableCount =
                    (long)(command.ExecuteScalar() ?? 0L);

                Assert.AreEqual(
                    0L,
                    tableCount);
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        private static string CreateTemporaryDatabasePath()
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directoryPath);

            return Path.Combine(
                directoryPath,
                "HLAS_Project.db");
        }

        private static SqliteConnection OpenReadWriteCreateConnection(
            string databasePath)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };

            return new SqliteConnection(
                builder.ToString());
        }

        private static void DeleteTemporaryDatabase(
            string databasePath)
        {
            SqliteConnection.ClearAllPools();

            string? directoryPath =
                Path.GetDirectoryName(databasePath);

            if (directoryPath is not null &&
                Directory.Exists(directoryPath))
            {
                Directory.Delete(
                    directoryPath,
                    recursive: true);
            }
        }
    }
}