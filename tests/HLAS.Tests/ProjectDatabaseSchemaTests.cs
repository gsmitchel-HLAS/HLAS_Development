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
        public void InitializeNewDatabase_CommittedTransaction_PersistsVersion7AndCurrentTables()
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
Assert.IsTrue(
    TableExists(
        connection,
        "HLAS_Source_Evidence_Catalog"));
Assert.IsTrue(
    TableExists(
        connection,
        "HLAS_Project_Authorization"));
                Assert.IsTrue(
    TableExists(
        connection,
        "HLAS_Frozen_States"));
                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Source_Evidence_Maintenance"));
                Assert.IsTrue(
    TableExists(
        connection,
        "HLAS_Source_Evidence_Maintenance_Changes"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void SourceEvidenceCatalog_NewRow_DefaultsLifecycleStateToActive()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();
                EvidenceId evidenceId = EvidenceId.CreateNew();

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

                using (SqliteCommand custodyCommand =
                    connection.CreateCommand())
                {
                    custodyCommand.CommandText =
                        """
                INSERT INTO HLAS_Evidence_Custody
                    (
                        EvidenceId,
                        OriginalFileName,
                        RelativeCustodyPath,
                        FileSizeBytes,
                        Sha256Hex,
                        AcceptedUtc
                    )
                VALUES
                    (
                        $evidenceId,
                        'test.pdf',
                        'HLAS_Source_Evidence/test.pdf',
                        1,
                        $sha256Hex,
                        $acceptedUtc
                    );
                """;

                    custodyCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceId.Value.ToString("D"));

                    custodyCommand.Parameters.AddWithValue(
                        "$sha256Hex",
                        new string('a', 64));

                    custodyCommand.Parameters.AddWithValue(
                        "$acceptedUtc",
                        DateTimeOffset.UtcNow.ToString("O"));

                    custodyCommand.ExecuteNonQuery();
                }

                using (SqliteCommand catalogCommand =
                    connection.CreateCommand())
                {
                    catalogCommand.CommandText =
                        """
                INSERT INTO HLAS_Source_Evidence_Catalog
                    (
                        EvidenceId,
                        SourceClass,
                        CatalogedUtc
                    )
                VALUES
                    (
                        $evidenceId,
                        'PRODUCTION',
                        $catalogedUtc
                    );
                """;

                    catalogCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceId.Value.ToString("D"));

                    catalogCommand.Parameters.AddWithValue(
                        "$catalogedUtc",
                        DateTimeOffset.UtcNow.ToString("O"));

                    catalogCommand.ExecuteNonQuery();
                }

                using SqliteCommand readCommand =
                    connection.CreateCommand();

                readCommand.CommandText =
                    """
            SELECT LifecycleState
            FROM HLAS_Source_Evidence_Catalog
            WHERE EvidenceId = $evidenceId;
            """;

                readCommand.Parameters.AddWithValue(
                    "$evidenceId",
                    evidenceId.Value.ToString("D"));

                string lifecycleState =
                    Convert.ToString(
                        readCommand.ExecuteScalar())!;

                Assert.AreEqual(
                    "Active",
                    lifecycleState);
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

                Assert.IsFalse(
    TableExists(
        connection,
        "HLAS_Source_Evidence_Catalog"));
                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Project_Authorization"));
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
                    ProjectDatabaseSchema.Version3,
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
        [TestMethod]
        public void MigrateVersion3ToVersion4_CommittedTransaction_AdvancesSchemaAndAddsCatalog()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version4,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Source_Evidence_Catalog"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion3ToVersion4_RolledBackTransaction_LeavesVersion3()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Rollback();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version3,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Source_Evidence_Catalog"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion3ToVersion4_NonVersion3_SafeStops()
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
                        () => ProjectDatabaseSchema.MigrateVersion3ToVersion4(
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
                        "HLAS_Source_Evidence_Catalog"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion4ToVersion5_CommittedTransaction_AdvancesSchema()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                using (SqliteTransaction version5Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        version5Transaction);

                    version5Transaction.Commit();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version5,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Project_Authorization"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion4ToVersion5_RolledBackTransaction_LeavesVersion4()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                using (SqliteTransaction version5Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        version5Transaction);

                    version5Transaction.Rollback();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version4,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Project_Authorization"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion4ToVersion5_NonVersion4_SafeStops()
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
                        () => ProjectDatabaseSchema.MigrateVersion4ToVersion5(
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
                        "HLAS_Project_Authorization"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion5ToVersion6_CommittedTransaction_AdvancesSchema()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                using (SqliteTransaction version5Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        version5Transaction);

                    version5Transaction.Commit();
                }

                using (SqliteTransaction version6Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion5ToVersion6(
                        connection,
                        version6Transaction);

                    version6Transaction.Commit();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version6,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Frozen_States"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion5ToVersion6_RolledBackTransaction_LeavesVersion5()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                using (SqliteTransaction version5Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        version5Transaction);

                    version5Transaction.Commit();
                }

                using (SqliteTransaction version6Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion5ToVersion6(
                        connection,
                        version6Transaction);

                    version6Transaction.Rollback();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version5,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Frozen_States"));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Project_Authorization"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }

        [TestMethod]
        public void MigrateVersion5ToVersion6_NonVersion5_SafeStops()
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
                        () => ProjectDatabaseSchema.MigrateVersion5ToVersion6(
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
                        "HLAS_Frozen_States"));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion6ToVersion7_CommittedTransaction_AdvancesSchema()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                using (SqliteTransaction version5Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        version5Transaction);

                    version5Transaction.Commit();
                }

                using (SqliteTransaction version6Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion5ToVersion6(
                        connection,
                        version6Transaction);

                    version6Transaction.Commit();
                }

                using (SqliteTransaction version7Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion6ToVersion7(
                        connection,
                        version7Transaction);

                    version7Transaction.Commit();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version7,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsTrue(
                    TableExists(
                        connection,
                        "HLAS_Source_Evidence_Maintenance"));
                Assert.IsTrue(
    TableExists(
        connection,
        "HLAS_Source_Evidence_Maintenance_Changes"));

                bool lifecycleStateFound = false;

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    "PRAGMA table_info(HLAS_Source_Evidence_Catalog);";

                using SqliteDataReader reader =
                    command.ExecuteReader();

                while (reader.Read())
                {
                    if (string.Equals(
                            reader.GetString(1),
                            "LifecycleState",
                            StringComparison.Ordinal))
                    {
                        lifecycleStateFound = true;
                        break;
                    }
                }

                Assert.IsTrue(lifecycleStateFound);
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion6ToVersion7_ExistingCatalogRow_DefaultsLifecycleStateToActive()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();
                EvidenceId evidenceId = EvidenceId.CreateNew();

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

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        transaction);

                    transaction.Commit();
                }

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        transaction);

                    transaction.Commit();
                }

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion5ToVersion6(
                        connection,
                        transaction);

                    transaction.Commit();
                }

                using (SqliteCommand custodyCommand =
                    connection.CreateCommand())
                {
                    custodyCommand.CommandText =
                        """
                INSERT INTO HLAS_Evidence_Custody
                    (
                        EvidenceId,
                        OriginalFileName,
                        RelativeCustodyPath,
                        FileSizeBytes,
                        Sha256Hex,
                        AcceptedUtc
                    )
                VALUES
                    (
                        $evidenceId,
                        'existing.pdf',
                        'HLAS_Source_Evidence/existing.pdf',
                        1,
                        $sha256Hex,
                        $acceptedUtc
                    );
                """;

                    custodyCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceId.Value.ToString("D"));

                    custodyCommand.Parameters.AddWithValue(
                        "$sha256Hex",
                        new string('a', 64));

                    custodyCommand.Parameters.AddWithValue(
                        "$acceptedUtc",
                        DateTimeOffset.UtcNow.ToString("O"));

                    custodyCommand.ExecuteNonQuery();
                }

                using (SqliteCommand catalogCommand =
                    connection.CreateCommand())
                {
                    catalogCommand.CommandText =
                        """
                INSERT INTO HLAS_Source_Evidence_Catalog
                    (
                        EvidenceId,
                        SourceClass,
                        CatalogedUtc
                    )
                VALUES
                    (
                        $evidenceId,
                        'PRODUCTION',
                        $catalogedUtc
                    );
                """;

                    catalogCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceId.Value.ToString("D"));

                    catalogCommand.Parameters.AddWithValue(
                        "$catalogedUtc",
                        DateTimeOffset.UtcNow.ToString("O"));

                    catalogCommand.ExecuteNonQuery();
                }

                using (SqliteTransaction transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion6ToVersion7(
                        connection,
                        transaction);

                    transaction.Commit();
                }

                using SqliteCommand readCommand =
                    connection.CreateCommand();

                readCommand.CommandText =
                    """
            SELECT LifecycleState
            FROM HLAS_Source_Evidence_Catalog
            WHERE EvidenceId = $evidenceId;
            """;

                readCommand.Parameters.AddWithValue(
                    "$evidenceId",
                    evidenceId.Value.ToString("D"));

                Assert.AreEqual(
                    "Active",
                    Convert.ToString(
                        readCommand.ExecuteScalar()));
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void SourceEvidenceCatalog_InvalidLifecycleState_IsRejected()
        {
            string databasePath = CreateTemporaryDatabasePath();

            try
            {
                ProjectId projectId = ProjectId.CreateNew();
                EvidenceId evidenceId = EvidenceId.CreateNew();

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

                using (SqliteCommand custodyCommand =
                    connection.CreateCommand())
                {
                    custodyCommand.CommandText =
                        """
                INSERT INTO HLAS_Evidence_Custody
                    (
                        EvidenceId,
                        OriginalFileName,
                        RelativeCustodyPath,
                        FileSizeBytes,
                        Sha256Hex,
                        AcceptedUtc
                    )
                VALUES
                    (
                        $evidenceId,
                        'test.pdf',
                        'HLAS_Source_Evidence/test.pdf',
                        1,
                        $sha256Hex,
                        $acceptedUtc
                    );
                """;

                    custodyCommand.Parameters.AddWithValue(
                        "$evidenceId",
                        evidenceId.Value.ToString("D"));

                    custodyCommand.Parameters.AddWithValue(
                        "$sha256Hex",
                        new string('a', 64));

                    custodyCommand.Parameters.AddWithValue(
                        "$acceptedUtc",
                        DateTimeOffset.UtcNow.ToString("O"));

                    custodyCommand.ExecuteNonQuery();
                }

                using SqliteCommand catalogCommand =
                    connection.CreateCommand();

                catalogCommand.CommandText =
                    """
            INSERT INTO HLAS_Source_Evidence_Catalog
                (
                    EvidenceId,
                    SourceClass,
                    CatalogedUtc,
                    LifecycleState
                )
            VALUES
                (
                    $evidenceId,
                    'PRODUCTION',
                    $catalogedUtc,
                    'INVALID'
                );
            """;

                catalogCommand.Parameters.AddWithValue(
                    "$evidenceId",
                    evidenceId.Value.ToString("D"));

                catalogCommand.Parameters.AddWithValue(
                    "$catalogedUtc",
                    DateTimeOffset.UtcNow.ToString("O"));

                Assert.ThrowsExactly<SqliteException>(
                    () => catalogCommand.ExecuteNonQuery());
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion6ToVersion7_RolledBackTransaction_LeavesVersion6()
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

                using (SqliteTransaction version3Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion2ToVersion3(
                        connection,
                        version3Transaction);

                    version3Transaction.Commit();
                }

                using (SqliteTransaction version4Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion3ToVersion4(
                        connection,
                        version4Transaction);

                    version4Transaction.Commit();
                }

                using (SqliteTransaction version5Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion4ToVersion5(
                        connection,
                        version5Transaction);

                    version5Transaction.Commit();
                }

                using (SqliteTransaction version6Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion5ToVersion6(
                        connection,
                        version6Transaction);

                    version6Transaction.Commit();
                }

                using (SqliteTransaction version7Transaction =
                    connection.BeginTransaction())
                {
                    ProjectDatabaseSchema.MigrateVersion6ToVersion7(
                        connection,
                        version7Transaction);

                    version7Transaction.Rollback();
                }

                Assert.AreEqual(
                    ProjectDatabaseSchema.Version6,
                    ReadDatabaseSchemaVersion(connection));

                Assert.IsFalse(
                    TableExists(
                        connection,
                        "HLAS_Source_Evidence_Maintenance"));
                Assert.IsFalse(
    TableExists(
        connection,
        "HLAS_Source_Evidence_Maintenance_Changes"));
                bool lifecycleStateFound = false;

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    "PRAGMA table_info(HLAS_Source_Evidence_Catalog);";

                using SqliteDataReader reader =
                    command.ExecuteReader();

                while (reader.Read())
                {
                    if (string.Equals(
                            reader.GetString(1),
                            "LifecycleState",
                            StringComparison.Ordinal))
                    {
                        lifecycleStateFound = true;
                        break;
                    }
                }

                Assert.IsFalse(lifecycleStateFound);
            }
            finally
            {
                DeleteTemporaryDatabase(databasePath);
            }
        }
        [TestMethod]
        public void MigrateVersion6ToVersion7_NonVersion6_SafeStops()
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
                        () => ProjectDatabaseSchema.MigrateVersion6ToVersion7(
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
                        "HLAS_Source_Evidence_Maintenance"));
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