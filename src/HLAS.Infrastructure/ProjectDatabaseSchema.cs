using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProjectDatabaseSchema
    {
        public const int CurrentDatabaseSchemaVersion = 1;

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

            using SqliteCommand createMetadataTable =
                connection.CreateCommand();

            createMetadataTable.Transaction = transaction;
            createMetadataTable.CommandText =
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

            createMetadataTable.ExecuteNonQuery();

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
    }
}