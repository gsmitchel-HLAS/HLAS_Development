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
        public void InitializeNewDatabase_CommittedTransaction_PersistsVersion3AndCommonTables()
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

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
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

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion1ToVersion2_CommittedTransaction_AdvancesToVersion2Only()
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
                    ProjectDatabaseSchema.Version2,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Evidence_Custody"));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
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

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
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

        [TestMethod]
        public void MigrateVersion2ToVersion3_CommittedTransaction_AdvancesSchema()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();

                using SqliteConnection connection =
                    OpenReadWriteCreateConnection(databasePath);

                connection.Open();

                CreateVersion2Database(
                    connection,
                    projectId);

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
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

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion2ToVersion3_RolledBackTransaction_LeavesVersion2()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();

                using SqliteConnection connection =
                    OpenReadWriteCreateConnection(databasePath);

                connection.Open();

                CreateVersion2Database(
                    connection,
                    projectId);

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        transaction);

                    transaction.Rollback();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version2,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Evidence_Custody"));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion2ToVersion3_NonVersion2_SafeStops()
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
                        () => ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                            connection,
                            migrationTransaction));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                migrationTransaction.Rollback();

                Assert.AreEqual(
                    ProjectDatabaseSchema.CurrentDatabaseSchemaVersion,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Governed_Operations"));
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

            CreateMetadataTable(
                connection,
                transaction);

            InsertMetadata(
                connection,
                transaction,
                projectId,
                ProjectDatabaseSchema.Version1);

            transaction.Commit();
        }

        private static void CreateVersion2Database(
            SqliteConnection connection,
            ProjectId projectId)
        {
            using SqliteTransaction transaction =
                connection.BeginTransaction();

            CreateMetadataTable(
                connection,
                transaction);

            using SqliteCommand createCustodyTable =
                connection.CreateCommand();

            createCustodyTable.Transaction = transaction;
            createCustodyTable.CommandText =
                """
                CREATE TABLE HLAS_Evidence_Custody
                (
                    EvidenceId TEXT NOT NULL
                        PRIMARY KEY,
                    OriginalFileName TEXT NOT NULL,
                    RelativeCustodyPath TEXT NOT NULL,
                    FileSizeBytes INTEGER NOT NULL
                        CHECK (FileSizeBytes >= 0),
                    Sha256Hex TEXT NOT NULL
                        CHECK (length(Sha256Hex) = 64),
                    AcceptedUtc TEXT NOT NULL
                );
                """;

            createCustodyTable.ExecuteNonQuery();

            InsertMetadata(
                connection,
                transaction,
                projectId,
                ProjectDatabaseSchema.Version2);

            transaction.Commit();
        }

        private static void CreateMetadataTable(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
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

            command.ExecuteNonQuery();
        }

        private static void InsertMetadata(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ProjectId projectId,
            int databaseSchemaVersion)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
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

            command.Parameters.AddWithValue(
                "$projectId",
                projectId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$databaseSchemaVersion",
                databaseSchemaVersion);

            command.ExecuteNonQuery();
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