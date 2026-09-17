using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProductionSourceEvidenceVerticalSliceTests
    {
        [TestMethod]
        public void Intake_DevelopmentalSource_UsesRealCustodyAndPreservesProjectIdentity()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(
                testRoot,
                "Project");

            string sourceDirectory = Path.Combine(
                testRoot,
                "Original");

            Directory.CreateDirectory(sourceDirectory);

            string sourcePath = Path.Combine(
                sourceDirectory,
                "Developmental Production Source.txt");

            byte[] originalBytes =
                "HLAS developmental Production Source Evidence."u8.ToArray();

            File.WriteAllBytes(
                sourcePath,
                originalBytes);

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(projectRoot);

                ProductionSourceEvidenceIntakeRequest request = new(
                    manifest.ProjectId,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    SeriesId.V,
                    sourcePath);

                ProductionSourceEvidenceIntakeService service = new(
                    new RealEvidenceCustodyGateway());

                ProductionSourceEvidenceIntakeResult result =
                    service.Intake(
                        projectRoot,
                        request);

                Assert.AreEqual(
                    manifest.ProjectId,
                    result.ProjectId);

                Assert.AreEqual(
                    "Developmental Production Source.txt",
                    result.EvidenceRecord.OriginalFileName);

                Assert.IsTrue(
                    File.Exists(sourcePath));

                CollectionAssert.AreEqual(
                    originalBytes,
                    File.ReadAllBytes(sourcePath));

                string custodyPath = Path.Combine(
                    projectRoot,
                    result.EvidenceRecord.RelativeCustodyPath);

                Assert.IsTrue(
                    File.Exists(custodyPath));

                CollectionAssert.AreEqual(
                    originalBytes,
                    File.ReadAllBytes(custodyPath));

                Assert.AreEqual(
                    "Developmental Production Source Evidence custody completed.",
                    result.Message);
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }
        [TestMethod]
        public void Intake_SameProductionSourceTwice_SafeStopsBeforeSecondCustody()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(
                testRoot,
                "Project");

            string sourceDirectory = Path.Combine(
                testRoot,
                "Original");

            Directory.CreateDirectory(sourceDirectory);

            string sourcePath = Path.Combine(
                sourceDirectory,
                "Duplicate Production Source.txt");

            File.WriteAllBytes(
                sourcePath,
                "HLAS duplicate Production Source Evidence."u8.ToArray());

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(projectRoot);

                ProductionSourceEvidenceIntakeRequest request = new(
                    manifest.ProjectId,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    SeriesId.V,
                    sourcePath);

                ProductionSourceEvidenceIntakeService service = new(
                    new RealEvidenceCustodyGateway());

                _ = service.Intake(
                    projectRoot,
                    request);

                long custodyCountBefore =
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody");

                long catalogCountBefore =
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog");

                string sourceEvidenceRoot = Path.Combine(
                    projectRoot,
                    EvidenceCustodyService.SourceEvidenceDirectoryName);

                int custodyDirectoryCountBefore =
                    Directory.GetDirectories(sourceEvidenceRoot).Length;

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => service.Intake(
                            projectRoot,
                            request));

                Assert.AreEqual(
                    "SAFE-STOP: This Production Source Evidence is already in governed custody.",
                    exception.Message);

                Assert.AreEqual(
                    custodyCountBefore,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    catalogCountBefore,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog"));

                Assert.HasCount(
      custodyDirectoryCountBefore,
      Directory.GetDirectories(sourceEvidenceRoot));
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }
        [TestMethod]
        public void Intake_RenamedIdenticalProductionSource_SafeStopsBeforeSecondCustody()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(
                testRoot,
                "Project");

            string sourceDirectory = Path.Combine(
                testRoot,
                "Original");

            Directory.CreateDirectory(sourceDirectory);

            byte[] identicalBytes =
                "HLAS renamed identical Production Source Evidence."u8.ToArray();

            string firstSourcePath = Path.Combine(
                sourceDirectory,
                "Production Source A.txt");

            string renamedSourcePath = Path.Combine(
                sourceDirectory,
                "Production Source Renamed.txt");

            File.WriteAllBytes(
                firstSourcePath,
                identicalBytes);

            File.WriteAllBytes(
                renamedSourcePath,
                identicalBytes);

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                ProjectManifest manifest =
                    ProjectPackageReader.Open(projectRoot);

                ProductionSourceEvidenceIntakeService service = new(
                    new RealEvidenceCustodyGateway());

                ProductionSourceEvidenceIntakeRequest firstRequest = new(
                    manifest.ProjectId,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    SeriesId.V,
                    firstSourcePath);

                _ = service.Intake(
                    projectRoot,
                    firstRequest);

                long custodyCountBefore =
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody");

                long catalogCountBefore =
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog");

                string sourceEvidenceRoot = Path.Combine(
                    projectRoot,
                    EvidenceCustodyService.SourceEvidenceDirectoryName);

                int custodyDirectoryCountBefore =
                    Directory.GetDirectories(sourceEvidenceRoot).Length;

                ProductionSourceEvidenceIntakeRequest renamedRequest = new(
                    manifest.ProjectId,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    SeriesId.V,
                    renamedSourcePath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => service.Intake(
                            projectRoot,
                            renamedRequest));

                Assert.AreEqual(
                    "SAFE-STOP: This Production Source Evidence is already in governed custody.",
                    exception.Message);

                Assert.AreEqual(
                    custodyCountBefore,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    catalogCountBefore,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog"));

                Assert.HasCount(
                    custodyDirectoryCountBefore,
                    Directory.GetDirectories(sourceEvidenceRoot));
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }
        private sealed class RealEvidenceCustodyGateway
    : IProductionSourceEvidenceIntakeGateway
        {
            public EvidenceCustodyRecord Accept(
                string projectRoot,
                string sourceFilePath)
            {
                return ProductionSourceEvidenceIntakeGateway.Accept(
                    projectRoot,
                    sourceFilePath);
            }

                        
        }

        private static string CreateTemporaryTestRoot()
        {
            string testRoot = Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(testRoot);

            return testRoot;
        }
        private static long ReadRecordCount(
    string projectRoot,
    string tableName)
        {
            string databasePath = Path.Combine(
                projectRoot,
                ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                $"SELECT COUNT(*) FROM {tableName};";

            return Convert.ToInt64(
                command.ExecuteScalar());
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