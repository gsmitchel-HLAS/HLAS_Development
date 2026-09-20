using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class PreChangeFreezeRetrievalServiceTests
    {
        [TestMethod]
        public void Retrieve_ValidFrozenState_ReturnsVerifiedFile()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourcePath =
                Path.Combine(
                    testRoot,
                    "Production Source.txt");

            byte[] originalBytes =
                "HLAS governed freeze retrieval."u8.ToArray();

            File.WriteAllBytes(
                sourcePath,
                originalBytes);

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                EvidenceCustodyRecord evidence =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin);

                FrozenStateRecord freeze =
                    PreChangeFreezeService.Create(
                        projectRoot,
                        operation.OperationId,
                        evidence.EvidenceId);

                PreChangeFreezeRetrievedFile retrieved =
                    PreChangeFreezeRetrievalService.Retrieve(
                        projectRoot,
                        freeze.FreezeId);

                Assert.AreEqual(
                    freeze,
                    retrieved.FrozenState);

                Assert.IsTrue(
                    File.Exists(
                        retrieved.FrozenFilePath));

                CollectionAssert.AreEqual(
                    originalBytes,
                    File.ReadAllBytes(
                        retrieved.FrozenFilePath));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void Retrieve_MissingFrozenFile_SafeStops()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourcePath =
                Path.Combine(
                    testRoot,
                    "Production Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS missing freeze retrieval test.");

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                EvidenceCustodyRecord evidence =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin);

                FrozenStateRecord freeze =
                    PreChangeFreezeService.Create(
                        projectRoot,
                        operation.OperationId,
                        evidence.EvidenceId);

                string frozenFilePath =
                    Path.Combine(
                        projectRoot,
                        freeze.RelativeFreezePath);

                File.Delete(
                    frozenFilePath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => PreChangeFreezeRetrievalService.Retrieve(
                            projectRoot,
                            freeze.FreezeId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void Retrieve_TamperedFrozenFile_SafeStops()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourcePath =
                Path.Combine(
                    testRoot,
                    "Production Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS tamper freeze retrieval test.");

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                EvidenceCustodyRecord evidence =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        UserId.CreateNew(),
                        SeriesId.V,
                        ProjectRole.Admin);

                FrozenStateRecord freeze =
                    PreChangeFreezeService.Create(
                        projectRoot,
                        operation.OperationId,
                        evidence.EvidenceId);

                string frozenFilePath =
                    Path.Combine(
                        projectRoot,
                        freeze.RelativeFreezePath);

                File.AppendAllText(
                    frozenFilePath,
                    "TAMPERED");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => PreChangeFreezeRetrievalService.Retrieve(
                            projectRoot,
                            freeze.FreezeId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        private static string CreateTemporaryTestRoot()
        {
            string path =
                Path.Combine(
                    Path.GetTempPath(),
                    "HLAS_PreChangeFreezeRetrievalTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                path);

            return path;
        }

        private static void DeleteTemporaryTestRoot(
            string testRoot)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(
                testRoot))
            {
                Directory.Delete(
                    testRoot,
                    recursive: true);
            }
        }
    }
}