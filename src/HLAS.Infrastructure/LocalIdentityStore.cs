using System;
using System.IO;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class LocalIdentityStore
    {
        public const string DatabaseFileName =
            "HLAS_Identity.db";

        public const int CurrentSchemaVersion = 1;
        public const string IdentityAdministratorCapability =
    "IDENTITY_ADMINISTRATOR";

        private const string CredentialAlgorithm =
            "PBKDF2-SHA256";

        private const int CredentialIterations =
            210000;

        private const int CredentialSaltSizeBytes =
            32;

        private const int CredentialHashSizeBytes =
            32;
        public static string GetDefaultDatabasePath()
        {
            string localApplicationData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(localApplicationData))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Local application-data location is unavailable.");
            }

            string identityDirectory =
                Path.Combine(
                    localApplicationData,
                    "HLAS",
                    "Identity");

            Directory.CreateDirectory(identityDirectory);

            return Path.Combine(
                identityDirectory,
                DatabaseFileName);
        }

        public static void EnsureCreated()
        {
            EnsureCreated(
                GetDefaultDatabasePath());
        }

        public static void EnsureCreated(
            string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                databasePath);

            databasePath =
                Path.GetFullPath(databasePath);

            string? databaseDirectory =
                Path.GetDirectoryName(databasePath);

            if (string.IsNullOrWhiteSpace(databaseDirectory))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: HLAS identity database directory is unavailable.");
            }

            Directory.CreateDirectory(
                databaseDirectory);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                ForeignKeys = true
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS HLAS_Identity_Metadata
                (
                    SingletonId INTEGER NOT NULL PRIMARY KEY
                        CHECK (SingletonId = 1),
                    SchemaVersion INTEGER NOT NULL
                        CHECK (SchemaVersion > 0)
                );

                CREATE TABLE IF NOT EXISTS HLAS_Users
                (
                    UserId TEXT NOT NULL PRIMARY KEY,
                    LoginName TEXT NOT NULL,
                    NormalizedLoginName TEXT NOT NULL UNIQUE,
                    CredentialAlgorithm TEXT NOT NULL,
                    CredentialSalt BLOB NOT NULL,
                    CredentialHash BLOB NOT NULL,
                    CredentialIterations INTEGER NOT NULL
                        CHECK (CredentialIterations > 0),
                    IsEnabled INTEGER NOT NULL
                        CHECK (IsEnabled IN (0, 1)),
                    CreatedUtc TEXT NOT NULL
                );
                 CREATE TABLE IF NOT EXISTS HLAS_Identity_Authorization
                (
                    UserId TEXT NOT NULL,
                    Capability TEXT NOT NULL,
                    GrantedUtc TEXT NOT NULL,

                    PRIMARY KEY
                    (
                        UserId,
                        Capability
                    ),

                    FOREIGN KEY (UserId)
                        REFERENCES HLAS_Users (UserId)
                );
                INSERT OR IGNORE INTO HLAS_Identity_Metadata
                    (
                        SingletonId,
                        SchemaVersion
                    )
                VALUES
                    (
                        1,
                        1
                    );
                """;

            command.ExecuteNonQuery();

            using SqliteCommand versionCommand =
                connection.CreateCommand();

            versionCommand.Transaction = transaction;
            versionCommand.CommandText =
                """
                SELECT SchemaVersion
                FROM HLAS_Identity_Metadata
                WHERE SingletonId = 1;
                """;

            object? result =
                versionCommand.ExecuteScalar();

            if (result is null ||
                Convert.ToInt32(result) != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: HLAS identity database schema version is unsupported.");
            }

            transaction.Commit();
        }
        public static bool IsInitialBootstrapRequired()
        {
            return IsInitialBootstrapRequired(
                GetDefaultDatabasePath());
        }

        public static bool IsInitialBootstrapRequired(
            string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                databasePath);

            databasePath =
                Path.GetFullPath(databasePath);

            EnsureCreated(databasePath);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                ForeignKeys = true
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT COUNT(*)
        FROM HLAS_Users;
        """;

            return Convert.ToInt64(
                command.ExecuteScalar()) == 0;
        }
        public static UserId BootstrapInitialIdentity(
     string loginName,
     string secret)
        {
            return BootstrapInitialIdentity(
                GetDefaultDatabasePath(),
                loginName,
                secret);
        }

        public static UserId BootstrapInitialIdentity(
            string databasePath,
            string loginName,
            string secret)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                databasePath);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                loginName);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                secret);

            databasePath =
                Path.GetFullPath(databasePath);

            EnsureCreated(databasePath);

            string trimmedLoginName =
                        loginName.Trim();

            string normalizedLoginName =
                trimmedLoginName.ToUpperInvariant();

            

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                ForeignKeys = true
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand countCommand =
                connection.CreateCommand();

            countCommand.Transaction = transaction;
            countCommand.CommandText =
                """
        SELECT COUNT(*)
        FROM HLAS_Users;
        """;

            long userCount =
                (long)countCommand.ExecuteScalar()!;

            if (userCount != 0)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Initial HLAS identity bootstrap is available only when no HLAS users exist.");
            }

            UserId userId =
                UserId.CreateNew();

            byte[] salt =
                RandomNumberGenerator.GetBytes(
                    CredentialSaltSizeBytes);

            byte[] credentialHash =
                Rfc2898DeriveBytes.Pbkdf2(
                    secret,
                    salt,
                    CredentialIterations,
                    HashAlgorithmName.SHA256,
                    CredentialHashSizeBytes);

            string createdUtc =
                GovernedTimestamp.CreateNow()
                    .Value
                    .ToString("O");

            using SqliteCommand userInsert =
                connection.CreateCommand();

            userInsert.Transaction = transaction;
            userInsert.CommandText =
                """
        INSERT INTO HLAS_Users
            (
                UserId,
                LoginName,
                NormalizedLoginName,
                CredentialAlgorithm,
                CredentialSalt,
                CredentialHash,
                CredentialIterations,
                IsEnabled,
                CreatedUtc
            )
        VALUES
            (
                $userId,
                $loginName,
                $normalizedLoginName,
                $credentialAlgorithm,
                $credentialSalt,
                $credentialHash,
                $credentialIterations,
                1,
                $createdUtc
            );
        """;

            userInsert.Parameters.AddWithValue(
                "$userId",
                userId.Value.ToString("D"));

            userInsert.Parameters.AddWithValue(
                "$loginName",
                trimmedLoginName);

            userInsert.Parameters.AddWithValue(
                "$normalizedLoginName",
                normalizedLoginName);

            userInsert.Parameters.AddWithValue(
                "$credentialAlgorithm",
                CredentialAlgorithm);

            userInsert.Parameters.AddWithValue(
                "$credentialSalt",
                salt);

            userInsert.Parameters.AddWithValue(
                "$credentialHash",
                credentialHash);

            userInsert.Parameters.AddWithValue(
                "$credentialIterations",
                CredentialIterations);

            userInsert.Parameters.AddWithValue(
                "$createdUtc",
                createdUtc);

            userInsert.ExecuteNonQuery();

            using SqliteCommand authorizationInsert =
                connection.CreateCommand();

            authorizationInsert.Transaction = transaction;
            authorizationInsert.CommandText =
                """
        INSERT INTO HLAS_Identity_Authorization
            (
                UserId,
                Capability,
                GrantedUtc
            )
        VALUES
            (
                $userId,
                $capability,
                $grantedUtc
            );
        """;

            authorizationInsert.Parameters.AddWithValue(
                "$userId",
                userId.Value.ToString("D"));

            authorizationInsert.Parameters.AddWithValue(
                "$capability",
                IdentityAdministratorCapability);

            authorizationInsert.Parameters.AddWithValue(
                "$grantedUtc",
                createdUtc);

            authorizationInsert.ExecuteNonQuery();

            transaction.Commit();

            return userId;
        }
        public static UserId? Authenticate(
    string loginName,
    string secret)
        {
            return Authenticate(
                GetDefaultDatabasePath(),
                loginName,
                secret);
        }

        public static UserId? Authenticate(
            string databasePath,
            string loginName,
            string secret)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                databasePath);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                loginName);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                secret);

            databasePath =
                Path.GetFullPath(databasePath);

            EnsureCreated(databasePath);

            string normalizedLoginName =
                loginName.Trim().ToUpperInvariant();

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                ForeignKeys = true
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            UserId,
            CredentialAlgorithm,
            CredentialSalt,
            CredentialHash,
            CredentialIterations,
            IsEnabled
        FROM HLAS_Users
        WHERE NormalizedLoginName = $normalizedLoginName;
        """;

            command.Parameters.AddWithValue(
                "$normalizedLoginName",
                normalizedLoginName);

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            string storedUserId =
                reader.GetString(0);

            string storedAlgorithm =
                reader.GetString(1);

            byte[] storedSalt =
                (byte[])reader[2];

            byte[] storedHash =
                (byte[])reader[3];

            int storedIterations =
                reader.GetInt32(4);

            bool isEnabled =
                reader.GetInt64(5) == 1;

            if (!isEnabled)
            {
                return null;
            }

            if (!string.Equals(
                storedAlgorithm,
                CredentialAlgorithm,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Stored HLAS credential algorithm is unsupported.");
            }

            byte[] suppliedHash =
                Rfc2898DeriveBytes.Pbkdf2(
                    secret,
                    storedSalt,
                    storedIterations,
                    HashAlgorithmName.SHA256,
                    storedHash.Length);

            if (!CryptographicOperations.FixedTimeEquals(
                suppliedHash,
                storedHash))
            {
                return null;
            }

            if (!Guid.TryParse(
                storedUserId,
                out Guid userGuid) ||
                userGuid == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Stored HLAS UserId is invalid.");
            }

            return new UserId(
                userGuid);
        }
    }
}