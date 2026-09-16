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

        private sealed class RealEvidenceCustodyGateway
            : IEvidenceCustodyGateway
        {
            public EvidenceCustodyRecord Accept(
                string projectRoot,
                string sourceFilePath)
            {
                return EvidenceCustodyService.Accept(
                    projectRoot,
                    sourceFilePath);
            }

            public EvidenceCustodyRetrievalResult Retrieve(
    string projectRoot,
    EvidenceId evidenceId)
            {
                EvidenceCustodyRetrievedFile retrieved =
                    EvidenceCustodyRetrievalService.Retrieve(
                        projectRoot,
                        evidenceId);

                return new EvidenceCustodyRetrievalResult(
                    retrieved.EvidenceRecord,
                    retrieved.ControlledFilePath);
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