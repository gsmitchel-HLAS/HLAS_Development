using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProjectDatabaseSchema
    {
        public const int CurrentDatabaseSchemaVersion = 2;

        public const int Version1 = 1;

        public static void InitializeNewDatabase(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ProjectId projectId)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            if (projectId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ProjectId may not be empty.",
                    nameof(projectId));
            }

            CreateProjectMetadataTable(
                connection,
                transaction);

            CreateEvidenceCustodyTable(
                connection,
                transaction);

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
                CurrentDatabaseSchemaVersion);

            insertMetadata.ExecuteNonQuery();
        }

        public static void MigrateVersion1ToVersion2(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 1 to Version 2 migration requires database schema version 1.");
            }

            CreateEvidenceCustodyTable(
                connection,
                transaction);

            using SqliteCommand updateVersion =
                connection.CreateCommand();

            updateVersion.Transaction = transaction;
            updateVersion.CommandText =
                """
                UPDATE HLAS_Project_Metadata
                SET DatabaseSchemaVersion = $newVersion
                WHERE
                    SingletonId = 1
                    AND DatabaseSchemaVersion = $oldVersion;
                """;

            updateVersion.Parameters.AddWithValue(
                "$newVersion",
                CurrentDatabaseSchemaVersion);

            updateVersion.Parameters.AddWithValue(
                "$oldVersion",
                Version1);

            int changedRows =
                updateVersion.ExecuteNonQuery();

            if (changedRows != 1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Database schema version could not be advanced safely.");
            }
        }

        private static void CreateProjectMetadataTable(
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

        private static void CreateEvidenceCustodyTable(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
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

            command.ExecuteNonQuery();
        }

        private static int ReadDatabaseSchemaVersion(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT DatabaseSchemaVersion
                FROM HLAS_Project_Metadata
                WHERE SingletonId = 1;
                """;

            object? result =
                command.ExecuteScalar();

            if (result is not long version)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Database schema version is missing or invalid.");
            }

            return checked((int)version);
        }
    }
}