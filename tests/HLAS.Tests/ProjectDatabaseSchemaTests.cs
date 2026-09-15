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
        public void InitializeNewDatabase_CommittedTransaction_PersistsVersion2AndCustodySchema()
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

                Assert.AreEqual(
                    projectId.Value.ToString("D"),
                    ReadProjectId(connection));

                Assert.AreEqual(
                    ProjectDatabaseSchema.CurrentDatabaseSchemaVersion,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Evidence_Custody"));
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

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Project_Metadata"));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Evidence_Custody"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion1ToVersion2_CommittedTransaction_AdvancesSchema()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();

                using SqliteConnection connection =
                    OpenReadWriteCreateConnection(databasePath);

                connection.Open();

                CreateVersion1Database(
                    connection,
                    projectId);

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion1ToVersion2(
                        connection,
                        transaction);

                    transaction.Commit();
                }

                Assert.AreEqual(
                    projectId.Value.ToString("D"),
                    ReadProjectId(connection));

                Assert.AreEqual(
                    ProjectDatabaseSchema.CurrentDatabaseSchemaVersion,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Evidence_Custody"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion1ToVersion2_RolledBackTransaction_LeavesVersion1()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();

                using SqliteConnection connection =
                    OpenReadWriteCreateConnection(databasePath);

                connection.Open();

                CreateVersion1Database(
                    connection,
                    projectId);

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion1ToVersion2(
                        connection,
                        transaction);

                    transaction.Rollback();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version1,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Evidence_Custody"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion1ToVersion2_NonVersion1_SafeStops()
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

                    transaction.Commit();
                }

                using SqliteTransaction migrationTransaction =
                    connection.BeginTransaction();

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectDatabaseSchema.MigrateVersion1ToVersion2(
                            connection,
                            migrationTransaction));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                migrationTransaction.Rollback();

                Assert.AreEqual(
                    ProjectDatabaseSchema.CurrentDatabaseSchemaVersion,
                    ReadDatabaseSchemaVersion(connection));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        private static void CreateVersion1Database(
            SqliteConnection connection,
            ProjectId projectId)
        {
            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand createMetadata =
                connection.CreateCommand();

            createMetadata.Transaction = transaction;
            createMetadata.CommandText =
                """
                CREATE TABLE HLAS_Project_Metadata
                (
                    SingletonId INTEGER NOT NULL
                        PRIMARY KEY
                        CHECK (SingletonId = 1),
                    ProjectId TEXT NOT NULL,
                    DatabaseSchemaVersion INTEGER NOT NULL
                        CHECK (DatabaseSchemaVersion >= 1)
                );
                """;

            createMetadata.ExecuteNonQuery();

            using SqliteCommand insertMetadata =
                connection.CreateCommand();

            insertMetadata.Transaction = transaction;
            insertMetadata.CommandText =
                """
                INSERT INTO HLAS_Project_Metadata
                    (
                        SingletonId,
                        ProjectId,
                        DatabaseSchemaVersion
                    )
                VALUES
                    (
                        1,
                        $projectId,
                        $databaseSchemaVersion
                    );
                """;

            insertMetadata.Parameters.AddWithValue(
                "$projectId",
                projectId.Value.ToString("D"));

            insertMetadata.Parameters.AddWithValue(
                "$databaseSchemaVersion",
                ProjectDatabaseSchema.Version1);

            insertMetadata.ExecuteNonQuery();

            transaction.Commit();
        }

        private static string ReadProjectId(
            SqliteConnection connection)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT ProjectId
                FROM HLAS_Project_Metadata
                WHERE SingletonId = 1;
                """;

            return (string)(
                command.ExecuteScalar()
                ?? throw new InvalidOperationException(
                    "ProjectId is missing."));
        }

        private static int ReadDatabaseSchemaVersion(
            SqliteConnection connection)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT DatabaseSchemaVersion
                FROM HLAS_Project_Metadata
                WHERE SingletonId = 1;
                """;

            object result =
                command.ExecuteScalar()
                ?? throw new InvalidOperationException(
                    "DatabaseSchemaVersion is missing.");

            return Convert.ToInt32(result);
        }

        private static bool TableExists(
            SqliteConnection connection,
            string tableName)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE
                    type = 'table'
                    AND name = $tableName;
                """;

            command.Parameters.AddWithValue(
                "$tableName",
                tableName);

            return Convert.ToInt64(
                command.ExecuteScalar()) == 1L;
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