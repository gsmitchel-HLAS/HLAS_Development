using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class LocalIdentityStoreTests
    {
        [TestMethod]
        public void EnsureCreated_ExplicitPath_CreatesVersion1IdentitySchema()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string databasePath =
                Path.Combine(
                    testRoot,
                    LocalIdentityStore.DatabaseFileName);

            try
            {
                LocalIdentityStore.EnsureCreated(
                    databasePath);

                using SqliteConnection connection =
                    OpenDatabase(
                        databasePath);

                connection.Open();

                Assert.AreEqual(
                    LocalIdentityStore.CurrentSchemaVersion,
                    ReadSchemaVersion(
                        connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Users"));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Identity_Authorization"));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void BootstrapInitialIdentity_EmptyStore_CreatesIdentityAdministrator()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string databasePath =
                Path.Combine(
                    testRoot,
                    LocalIdentityStore.DatabaseFileName);

            try
            {
                UserId userId =
                    LocalIdentityStore.BootstrapInitialIdentity(
                        databasePath,
                        " Greg ",
                        "development-secret");

                using SqliteConnection connection =
                    OpenDatabase(
                        databasePath);

                connection.Open();

                using SqliteCommand userCommand =
                    connection.CreateCommand();

                userCommand.CommandText =
                    """
                    SELECT
                        LoginName,
                        NormalizedLoginName,
                        CredentialAlgorithm,
                        length(CredentialSalt),
                        length(CredentialHash),
                        IsEnabled
                    FROM HLAS_Users
                    WHERE UserId = $userId;
                    """;

                userCommand.Parameters.AddWithValue(
                    "$userId",
                    userId.Value.ToString("D"));

                using SqliteDataReader reader =
                    userCommand.ExecuteReader();

                Assert.IsTrue(
                    reader.Read());

                Assert.AreEqual(
                    "Greg",
                    reader.GetString(0));

                Assert.AreEqual(
                    "GREG",
                    reader.GetString(1));

                Assert.AreEqual(
                    "PBKDF2-SHA256",
                    reader.GetString(2));

                Assert.IsGreaterThan(
    0L,
    reader.GetInt64(3));

                Assert.IsGreaterThan(
     0L,
     reader.GetInt64(4));

                Assert.AreEqual(
                    1L,
                    reader.GetInt64(5));

                reader.Close();

                using SqliteCommand authorizationCommand =
                    connection.CreateCommand();

                authorizationCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Identity_Authorization
                    WHERE UserId = $userId
                      AND Capability = $capability;
                    """;

                authorizationCommand.Parameters.AddWithValue(
                    "$userId",
                    userId.Value.ToString("D"));

                authorizationCommand.Parameters.AddWithValue(
                    "$capability",
                    LocalIdentityStore.IdentityAdministratorCapability);

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        authorizationCommand.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void BootstrapInitialIdentity_NonEmptyStore_SafeStopsWithoutSecondUser()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string databasePath =
                Path.Combine(
                    testRoot,
                    LocalIdentityStore.DatabaseFileName);

            try
            {
                _ = LocalIdentityStore.BootstrapInitialIdentity(
                    databasePath,
                    "first-user",
                    "first-development-secret");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => LocalIdentityStore.BootstrapInitialIdentity(
                            databasePath,
                            "second-user",
                            "second-development-secret"));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                using SqliteConnection connection =
                    OpenDatabase(
                        databasePath);

                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Users;
                    """;

                Assert.AreEqual(
                    1L,
                    Convert.ToInt64(
                        command.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Authenticate_ValidCredentials_ReturnsSameUserId()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string databasePath =
                Path.Combine(
                    testRoot,
                    LocalIdentityStore.DatabaseFileName);

            try
            {
                UserId expectedUserId =
                    LocalIdentityStore.BootstrapInitialIdentity(
                        databasePath,
                        "Greg",
                        "development-secret");

                UserId? authenticatedUserId =
                    LocalIdentityStore.Authenticate(
                        databasePath,
                        "gReG",
                        "development-secret");

                Assert.IsNotNull(
                    authenticatedUserId);

                Assert.AreEqual(
                    expectedUserId,
                    authenticatedUserId.Value);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void Authenticate_WrongSecret_ReturnsNoIdentity()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string databasePath =
                Path.Combine(
                    testRoot,
                    LocalIdentityStore.DatabaseFileName);

            try
            {
                _ = LocalIdentityStore.BootstrapInitialIdentity(
                    databasePath,
                    "Greg",
                    "development-secret");

                UserId? authenticatedUserId =
                    LocalIdentityStore.Authenticate(
                        databasePath,
                        "Greg",
                        "wrong-secret");

                Assert.IsNull(
                    authenticatedUserId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void Authenticate_DisabledUser_ReturnsNoIdentity()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string databasePath =
                Path.Combine(
                    testRoot,
                    LocalIdentityStore.DatabaseFileName);

            try
            {
                UserId userId =
                    LocalIdentityStore.BootstrapInitialIdentity(
                        databasePath,
                        "Greg",
                        "development-secret");

                using (SqliteConnection connection =
                    OpenDatabase(databasePath))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Users
                SET IsEnabled = 0
                WHERE UserId = $userId;
                """;

                    command.Parameters.AddWithValue(
                        "$userId",
                        userId.Value.ToString("D"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                UserId? authenticatedUserId =
                    LocalIdentityStore.Authenticate(
                        databasePath,
                        "Greg",
                        "development-secret");

                Assert.IsNull(
                    authenticatedUserId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        private static int ReadSchemaVersion(
            SqliteConnection connection)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT SchemaVersion
                FROM HLAS_Identity_Metadata
                WHERE SingletonId = 1;
                """;

            return Convert.ToInt32(
                command.ExecuteScalar());
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
                WHERE type = 'table'
                  AND name = $tableName;
                """;

            command.Parameters.AddWithValue(
                "$tableName",
                tableName);

            return Convert.ToInt64(
                command.ExecuteScalar()) == 1;
        }

        private static SqliteConnection OpenDatabase(
            string databasePath)
        {
            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

            return new SqliteConnection(
                builder.ToString());
        }

        private static string CreateTemporaryTestRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
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