using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityMaintenanceVerifierTests
    {
        [TestMethod]
        public void Check_NoMaintenance_VerifiedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(root);

                Assert.AreEqual(
                    "SOURCE_EVIDENCE_MAINTENANCE",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_OpenMaintenance_ReturnsPending()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                _ = GovernedOperationService.Begin(
                    root,
                    project.ProjectId,
                    UserId.CreateNew(),
                    SeriesId.V,
                    ProjectRole.Admin,
                    "SOURCE_EVIDENCE_CORRECT");

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_LegitimateSafeStop_IsNotCorruption()
        {
            string root = NewRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        root,
                        project.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin,
                        "SOURCE_EVIDENCE_CORRECT");

                GovernedOperationService.FinalizeSafeStop(
                    root,
                    operation.OperationId,
                    new DecisionRecord(
                        "SAFE-STOP",
                        "Developmental prerequisite not satisfied."));

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_UnresolvedMaintenance_ReturnsPending()
        {
            var sample = CreateCorrectedProject();

            try
            {
                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
                    result.Status);
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }

        [TestMethod]
        public void Check_RevalidatedMaintenance_VerifiedWithoutWrites()
        {
            var sample = CreateCorrectedProject();

            try
            {
                _ = SourceEvidenceMaintenanceGateway.RevalidateMaintenance(
                    sample.ProjectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    sample.MaintenanceOperationId);

                byte[] before =
                    ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                CollectionAssert.AreEqual(
                    before,
                    ReadDatabaseBytes(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }

        [TestMethod]
        public void Check_FailedResolutionOperation_ReturnsIntegrityFailure()
        {
            var sample = CreateCorrectedProject();

            try
            {
                _ = SourceEvidenceMaintenanceGateway.RevalidateMaintenance(
                    sample.ProjectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    sample.MaintenanceOperationId);

                using (SqliteConnection connection =
                    OpenWrite(sample.ProjectRoot))
                {
                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        UPDATE HLAS_Governed_Operations
                        SET Outcome = 'SAFE-STOP',
                            Decision = 'REJECT',
                            Reason = 'Developmental tamper.'
                        WHERE OperationId =
                        (
                            SELECT ResolutionOperationId
                            FROM HLAS_Source_Evidence_Maintenance_Resolutions
                            WHERE MaintenanceOperationId = $maintenanceId
                        );
                        """;

                    command.Parameters.AddWithValue(
                        "$maintenanceId",
                        sample.MaintenanceOperationId.Value.ToString("D"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }

        [TestMethod]
        public void Check_OrphanResolution_ReturnsIntegrityFailure()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                using (SqliteConnection connection =
                    OpenWrite(root, foreignKeys: false))
                {
                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        INSERT INTO HLAS_Source_Evidence_Maintenance_Resolutions
                        (
                            MaintenanceOperationId,
                            ResolutionOperationId,
                            ResolvedUtc
                        )
                        VALUES
                        (
                            $maintenanceId,
                            $resolutionId,
                            $timestamp
                        );
                        """;

                    command.Parameters.AddWithValue(
                        "$maintenanceId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$resolutionId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$timestamp",
                        DateTimeOffset.UtcNow.ToString("O"));

                    Assert.AreEqual(
                        1,
                        command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityMaintenanceVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static (
            string Root,
            string ProjectRoot,
            OperationId MaintenanceOperationId)
            CreateCorrectedProject()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            File.WriteAllText(
                sourcePath,
                "Developmental maintenance source evidence.");

            ProjectPackageCreator.CreateNew(projectRoot);

            EvidenceCustodyRecord evidence =
                ProductionSourceEvidenceIntakeGateway.Accept(
                    projectRoot,
                    sourcePath);

            SourceEvidenceMaintenanceRecord maintenance =
                SourceEvidenceMaintenanceGateway.Correct(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    evidence.EvidenceId,
                    new SourceEvidenceCorrectionRequest(
                        SourceEvidenceMetadataMutation.Set(
                            "Developmental maintenance label"),
                        SourceEvidenceMetadataMutation.Keep(),
                        "Developmental maintenance verification."));

            return (
                root,
                projectRoot,
                maintenance.OperationId);
        }

        private static SqliteConnection OpenWrite(
            string root,
            bool foreignKeys = true)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = DatabasePath(root),
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                ForeignKeys = foreignKeys
            };

            SqliteConnection connection =
                new(builder.ToString());

            connection.Open();
            return connection;
        }

        private static byte[] ReadDatabaseBytes(string root)
        {
            SqliteConnection.ClearAllPools();

            return File.ReadAllBytes(
                DatabasePath(root));
        }

        private static string DatabasePath(string root)
        {
            return Path.Combine(
                root,
                ProjectPackageCreator.DatabaseFileName);
        }

        private static string NewRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}