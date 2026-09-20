using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class PreChangeFreezeServiceTests
    {
        [TestMethod]
        public void Create_ValidOpenOperation_CreatesVerifiedFrozenState()
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
                "HLAS pre-change freeze evidence."u8.ToArray();

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

                Assert.AreEqual(
                    operation.OperationId,
                    freeze.OperationId);

                Assert.AreEqual(
                    evidence.EvidenceId,
                    freeze.TargetEvidenceId);

                Assert.AreEqual(
                    FrozenStateRecord.PreChangeFreezeType,
                    freeze.FreezeType);

                string frozenFilePath =
                    Path.Combine(
                        projectRoot,
                        freeze.RelativeFreezePath);

                Assert.IsTrue(
                    File.Exists(
                        frozenFilePath));

                CollectionAssert.AreEqual(
                    originalBytes,
                    File.ReadAllBytes(
                        frozenFilePath));

                VerifyFrozenStateRecord(
                    projectRoot,
                    freeze);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void Create_FinalizedOperation_SafeStopsWithoutFreeze()
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
                "HLAS finalized-operation freeze test.");

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

                GovernedOperationService.FinalizeSuccess(
                    projectRoot,
                    operation.OperationId);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => PreChangeFreezeService.Create(
                            projectRoot,
                            operation.OperationId,
                            evidence.EvidenceId));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    0L,
                    ReadFrozenStateCount(
                        projectRoot));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        private static void VerifyFrozenStateRecord(
            string projectRoot,
            FrozenStateRecord expected)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(
                    databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    OperationId,
                    TargetEvidenceId,
                    FreezeType,
                    RelativeFreezePath,
                    FileSizeBytes,
                    Sha256Hex,
                    FrozenUtc
                FROM HLAS_Frozen_States
                WHERE FreezeId = $freezeId;
                """;

            command.Parameters.AddWithValue(
                "$freezeId",
                expected.FreezeId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(
                reader.Read());

            Assert.AreEqual(
                expected.OperationId.Value.ToString("D"),
                reader.GetString(0));

            Assert.AreEqual(
                expected.TargetEvidenceId.Value.ToString("D"),
                reader.GetString(1));

            Assert.AreEqual(
                expected.FreezeType,
                reader.GetString(2));

            Assert.AreEqual(
                expected.RelativeFreezePath,
                reader.GetString(3));

            Assert.AreEqual(
                expected.FileSizeBytes,
                reader.GetInt64(4));

            Assert.AreEqual(
                expected.Sha256Hex,
                reader.GetString(5));

            Assert.AreEqual(
                expected.FrozenUtc.Value.ToString("O"),
                reader.GetString(6));
        }

        private static long ReadFrozenStateCount(
            string projectRoot)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(
                    databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT COUNT(*)
                FROM HLAS_Frozen_States;
                """;

            return Convert.ToInt64(
                command.ExecuteScalar());
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
            string path =
                Path.Combine(
                    Path.GetTempPath(),
                    "HLAS_PreChangeFreezeTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                path);

            return path;
        }

        private static void DeleteTemporaryTestRoot(
            string testRoot)
        {
            SqliteConnection.ClearAllPools();

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