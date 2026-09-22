using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProjectDatabaseSchema
    {
        public const int Version1 = 1;
        public const int Version2 = 2;
        public const int Version3 = 3;
        public const int Version4 = 4;
        public const int Version5 = 5;
        public const int Version6 = 6;
        public const int Version7 = 7;
        public const int Version8 = 8;
        public const int CurrentDatabaseSchemaVersion = Version8;
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

            CreateGovernedOperationsTable(
                connection,
                transaction);

            CreateSourceEvidenceCatalogTable(
                connection,
                transaction);
            AddSourceEvidenceCatalogLifecycleState(
    connection,
    transaction);
            CreateProjectAuthorizationTable(
    connection,
    transaction);
            CreateFrozenStatesTable(
    connection,
    transaction);
            CreateSourceEvidenceMaintenanceTable(
                connection,
                transaction);
            CreateSourceEvidenceMaintenanceChangesTable(
    connection,
    transaction);

            CreateSourceEvidenceMetadataVersionsTable(
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

            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version1,
                Version2);
        }

        public static void MigrateVersion2ToVersion3(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version2)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 2 to Version 3 migration requires database schema version 2.");
            }

            CreateGovernedOperationsTable(
                connection,
                transaction);

            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version2,
                Version3);
        }

        public static void MigrateVersion3ToVersion4(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version3)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 3 to Version 4 migration requires database schema version 3.");
            }

            CreateSourceEvidenceCatalogTable(
                connection,
                transaction);

            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version3,
                Version4);
        }
        public static void MigrateVersion4ToVersion5(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version4)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 4 to Version 5 migration requires database schema version 4.");
            }

            CreateProjectAuthorizationTable(
                connection,
                transaction);

            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version4,
                Version5);
        }
        public static void MigrateVersion5ToVersion6(
    SqliteConnection connection,
    SqliteTransaction transaction)

        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version5)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 5 to Version 6 migration requires database schema version 5.");
            }

            CreateFrozenStatesTable(
                connection,
                transaction);

            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version5,
                Version6);
        }
        public static void MigrateVersion6ToVersion7(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version6)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 6 to Version 7 migration requires database schema version 6.");
            }

            AddSourceEvidenceCatalogLifecycleState(
    connection,
    transaction);

            CreateSourceEvidenceMaintenanceTable(
                connection,
                transaction);
            CreateSourceEvidenceMaintenanceChangesTable(
    connection,
    transaction);
            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version6,
                Version7);
        }
        public static void MigrateVersion7ToVersion8(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            int currentVersion =
                ReadDatabaseSchemaVersion(
                    connection,
                    transaction);

            if (currentVersion != Version7)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Version 7 to Version 8 migration requires database schema version 7.");
            }

            CreateSourceEvidenceMetadataVersionsTable(
                connection,
                transaction);

            UpdateDatabaseSchemaVersion(
                connection,
                transaction,
                Version7,
                Version8);
        }
        private static void CreateFrozenStatesTable(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
        CREATE TABLE HLAS_Frozen_States
        (
            FreezeId TEXT NOT NULL
                PRIMARY KEY,
            OperationId TEXT NOT NULL,
            TargetEvidenceId TEXT NOT NULL,
            FreezeType TEXT NOT NULL
                CHECK (FreezeType = 'PRE-CHANGE'),
            RelativeFreezePath TEXT NOT NULL
                UNIQUE
                CHECK (length(trim(RelativeFreezePath)) > 0),
            FileSizeBytes INTEGER NOT NULL
                CHECK (FileSizeBytes >= 0),
            Sha256Hex TEXT NOT NULL
                CHECK (length(Sha256Hex) = 64),
            FrozenUtc TEXT NOT NULL,

            FOREIGN KEY (OperationId)
                REFERENCES HLAS_Governed_Operations(OperationId),

            FOREIGN KEY (TargetEvidenceId)
                REFERENCES HLAS_Evidence_Custody(EvidenceId)
        );
        """;

            command.ExecuteNonQuery();
        }
        private static void CreateSourceEvidenceMaintenanceTable(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
        CREATE TABLE HLAS_Source_Evidence_Maintenance
        (
            OperationId TEXT NOT NULL
                PRIMARY KEY,
            FreezeId TEXT NOT NULL,
            PriorEvidenceId TEXT NOT NULL,
            ResultingEvidenceId TEXT NOT NULL,
            MaintenanceType TEXT NOT NULL
                CHECK
                (
                    MaintenanceType IN
                    (
                        'CORRECT',
                        'REPLACE'
                    )
                ),
            MaintainedUtc TEXT NOT NULL,

            CHECK
            (
                (
                    MaintenanceType = 'CORRECT'
                    AND PriorEvidenceId = ResultingEvidenceId
                )
                OR
                (
                    MaintenanceType = 'REPLACE'
                    AND PriorEvidenceId <> ResultingEvidenceId
                )
            ),

            FOREIGN KEY (OperationId)
                REFERENCES HLAS_Governed_Operations(OperationId),

            FOREIGN KEY (FreezeId)
                REFERENCES HLAS_Frozen_States(FreezeId),

            FOREIGN KEY (PriorEvidenceId)
                REFERENCES HLAS_Evidence_Custody(EvidenceId),

            FOREIGN KEY (ResultingEvidenceId)
                REFERENCES HLAS_Evidence_Custody(EvidenceId)
        );
        """;

            command.ExecuteNonQuery();
        }
        private static void CreateSourceEvidenceMaintenanceChangesTable(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
        CREATE TABLE HLAS_Source_Evidence_Maintenance_Changes
        (
            OperationId TEXT NOT NULL,
            ChangeSequence INTEGER NOT NULL
                CHECK (ChangeSequence > 0),
            FieldName TEXT NOT NULL
                CHECK (length(trim(FieldName)) > 0),
            PriorValue TEXT,
            ResultingValue TEXT,

            PRIMARY KEY
                (
                    OperationId,
                    ChangeSequence
                ),

            FOREIGN KEY (OperationId)
                REFERENCES HLAS_Source_Evidence_Maintenance(OperationId)
        );
        """;

            command.ExecuteNonQuery();
        }
        private static void CreateSourceEvidenceMetadataVersionsTable(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
        CREATE TABLE HLAS_Source_Evidence_Metadata_Versions
        (
            MetadataVersionId TEXT NOT NULL
                PRIMARY KEY,
            EvidenceId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL
                CHECK (VersionNumber > 0),
            PriorMetadataVersionId TEXT NULL,
            DisplayLabel TEXT NULL
                CHECK
                (
                    DisplayLabel IS NULL
                    OR length(trim(DisplayLabel)) > 0
                ),
            AdministrativeDescription TEXT NULL
                CHECK
                (
                    AdministrativeDescription IS NULL
                    OR length(trim(AdministrativeDescription)) > 0
                ),
            CorrectionReason TEXT NOT NULL
                CHECK (length(trim(CorrectionReason)) > 0),
            OperationId TEXT NOT NULL
                UNIQUE,
            VersionedUtc TEXT NOT NULL,

            CHECK
            (
                (
                    VersionNumber = 1
                    AND PriorMetadataVersionId IS NULL
                )
                OR
                (
                    VersionNumber > 1
                    AND PriorMetadataVersionId IS NOT NULL
                )
            ),

            CHECK
            (
                PriorMetadataVersionId IS NULL
                OR PriorMetadataVersionId <> MetadataVersionId
            ),

            UNIQUE
            (
                EvidenceId,
                VersionNumber
            ),

            UNIQUE
            (
                PriorMetadataVersionId
            ),

            FOREIGN KEY (EvidenceId)
                REFERENCES HLAS_Source_Evidence_Catalog(EvidenceId),

            FOREIGN KEY (PriorMetadataVersionId)
                REFERENCES HLAS_Source_Evidence_Metadata_Versions(MetadataVersionId),

            FOREIGN KEY (OperationId)
                REFERENCES HLAS_Source_Evidence_Maintenance(OperationId)
        );
        """;

            command.ExecuteNonQuery();
        }
        private static void CreateProjectAuthorizationTable(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
        CREATE TABLE HLAS_Project_Authorization
        (
            UserId TEXT NOT NULL
                PRIMARY KEY,
            ProjectRole TEXT NOT NULL
                CHECK
                (
                    ProjectRole IN
                    (
                        'Technician',
                        'Senior',
                        'Admin'
                    )
                ),
            AuthorizedUtc TEXT NOT NULL
        );
        """;

            command.ExecuteNonQuery();
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

        private static void CreateGovernedOperationsTable(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                CREATE TABLE HLAS_Governed_Operations
                (
                    OperationId TEXT NOT NULL
                        PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    UserId TEXT NOT NULL,
                    SeriesId TEXT NOT NULL,
                    ProjectRole TEXT NOT NULL,
                    StartedUtc TEXT NOT NULL,
                    CompletedUtc TEXT NULL,
                    Outcome TEXT NULL
                        CHECK
                        (
                            Outcome IS NULL
                            OR Outcome IN
                            (
                                'SUCCESS',
                                'SAFE-STOP',
                                'TECHNICAL FAILURE'
                            )
                        ),
                    Decision TEXT NULL,
                    Reason TEXT NULL,

                    CHECK
                    (
                        (
                            CompletedUtc IS NULL
                            AND Outcome IS NULL
                            AND Decision IS NULL
                            AND Reason IS NULL
                        )
                        OR
                        (
                            CompletedUtc IS NOT NULL
                            AND Outcome = 'SUCCESS'
                            AND
                            (
                                (
                                    Decision IS NULL
                                    AND Reason IS NULL
                                )
                                OR
                                (
                                    Decision IS NOT NULL
                                    AND Reason IS NOT NULL
                                )
                            )
                        )
                        OR
                        (
                            CompletedUtc IS NOT NULL
                            AND Outcome IN
                            (
                                'SAFE-STOP',
                                'TECHNICAL FAILURE'
                            )
                            AND Decision IS NOT NULL
                            AND Reason IS NOT NULL
                        )
                    )
                );
                """;

            command.ExecuteNonQuery();
        }

        private static void CreateSourceEvidenceCatalogTable(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                CREATE TABLE HLAS_Source_Evidence_Catalog
                (
                    EvidenceId TEXT NOT NULL
                        PRIMARY KEY,
                    SourceClass TEXT NOT NULL
                        CHECK (length(trim(SourceClass)) > 0),
                    CatalogedUtc TEXT NOT NULL,
                

                    FOREIGN KEY (EvidenceId)
                        REFERENCES HLAS_Evidence_Custody(EvidenceId)
                );
                """;

            command.ExecuteNonQuery();
        }
        private static void AddSourceEvidenceCatalogLifecycleState(
    SqliteConnection connection,
    SqliteTransaction transaction)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
        ALTER TABLE HLAS_Source_Evidence_Catalog
        ADD COLUMN LifecycleState TEXT NOT NULL
            DEFAULT 'Active'
            CHECK
            (
                LifecycleState IN
                (
                    'Active',
                    'Superseded'
                )
            );
        """;

            command.ExecuteNonQuery();
        }
        private static void UpdateDatabaseSchemaVersion(
            SqliteConnection connection,
            SqliteTransaction transaction,
            int oldVersion,
            int newVersion)
        {
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
                newVersion);

            updateVersion.Parameters.AddWithValue(
                "$oldVersion",
                oldVersion);

            int changedRows =
                updateVersion.ExecuteNonQuery();

            if (changedRows != 1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Database schema version could not be advanced safely.");
            }
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