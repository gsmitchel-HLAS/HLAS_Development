using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class EvidenceCustodyServiceTests
    {
        [TestMethod]
        public void Accept_ValidSource_PreservesVerifiedCopyAndCustodyRecord()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(testRoot, "Project");
            string sourceDirectory = Path.Combine(testRoot, "Original");

            Directory.CreateDirectory(sourceDirectory);

            string sourcePath =
                Path.Combine(sourceDirectory, "Original Source.txt");

            byte[] originalBytes =
                "HLAS governed source evidence."u8.ToArray();

            File.WriteAllBytes(
                sourcePath,
                originalBytes);

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord record =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                Assert.IsTrue(
                    File.Exists(sourcePath));

                CollectionAssert.AreEqual(
                    originalBytes,
                    File.ReadAllBytes(sourcePath));

                string expectedRelativePath =
                    Path.Combine(
                        EvidenceCustodyService.SourceEvidenceDirectoryName,
                        record.EvidenceId.Value.ToString("D"),
                        "Original Source.txt");

                Assert.AreEqual(
                    expectedRelativePath,
                    record.RelativeCustodyPath);

                string custodyPath =
                    Path.Combine(
                        projectRoot,
                        record.RelativeCustodyPath);

                Assert.IsTrue(
                    File.Exists(custodyPath));

                CollectionAssert.AreEqual(
                    originalBytes,
                    File.ReadAllBytes(custodyPath));

                Assert.AreEqual(
                    originalBytes.LongLength,
                    record.FileSizeBytes);

                Assert.AreEqual(
                    ComputeSha256(sourcePath),
                    record.Sha256Hex);

                Assert.AreEqual(
                    ComputeSha256(custodyPath),
                    record.Sha256Hex);

                VerifyDatabaseCustodyRecord(
                    projectRoot,
                    record);
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }

        [TestMethod]
        public void Accept_SameSourceTwice_CreatesSeparateEvidenceIds()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(testRoot, "Project");
            string sourceDirectory = Path.Combine(testRoot, "Original");

            Directory.CreateDirectory(sourceDirectory);

            string sourcePath =
                Path.Combine(sourceDirectory, "Repeated Source.txt");

            File.WriteAllText(
                sourcePath,
                "Same source accepted twice.");

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord first =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                EvidenceCustodyRecord second =
                    EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath);

                Assert.AreNotEqual(
                    first.EvidenceId,
                    second.EvidenceId);

                Assert.AreNotEqual(
                    first.RelativeCustodyPath,
                    second.RelativeCustodyPath);

                Assert.IsTrue(File.Exists(Path.Combine(
                    projectRoot,
                    first.RelativeCustodyPath)));

                Assert.IsTrue(File.Exists(Path.Combine(
                    projectRoot,
                    second.RelativeCustodyPath)));

                Assert.AreEqual(
                    2L,
                    ReadCustodyRecordCount(projectRoot));
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }

        [TestMethod]
        public void Accept_MissingSource_SafeStopsWithoutCustodyRecord()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(testRoot, "Project");

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                string missingSourcePath =
                    Path.Combine(
                        testRoot,
                        "DoesNotExist.pdf");

                Assert.ThrowsExactly<FileNotFoundException>(
                    () => EvidenceCustodyService.Accept(
                        projectRoot,
                        missingSourcePath));

                Assert.AreEqual(
                    0L,
                    ReadCustodyRecordCount(projectRoot));

                string evidenceRoot =
                    Path.Combine(
                        projectRoot,
                        EvidenceCustodyService.SourceEvidenceDirectoryName);

                Assert.IsFalse(
                    Directory.Exists(evidenceRoot));
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }

        [TestMethod]
        public void Accept_DatabaseWriteFailure_RemovesFailedCustodyCopyAndPreservesOriginal()
        {
            string testRoot = CreateTemporaryTestRoot();
            string projectRoot = Path.Combine(testRoot, "Project");
            string sourceDirectory = Path.Combine(testRoot, "Original");

            Directory.CreateDirectory(sourceDirectory);

            string sourcePath =
                Path.Combine(sourceDirectory, "Failure Source.txt");

            const string originalContent =
                "Original must remain unchanged.";

            File.WriteAllText(
                sourcePath,
                originalContent);

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                RemoveCustodyTable(projectRoot);

                Assert.ThrowsExactly<SqliteException>(
                    () => EvidenceCustodyService.Accept(
                        projectRoot,
                        sourcePath));

                Assert.IsTrue(
                    File.Exists(sourcePath));

                Assert.AreEqual(
                    originalContent,
                    File.ReadAllText(sourcePath));

                string evidenceRoot =
                    Path.Combine(
                        projectRoot,
                        EvidenceCustodyService.SourceEvidenceDirectoryName);

                if (Directory.Exists(evidenceRoot))
                {
                    Assert.AreEqual(
                        0,
                        Directory
                            .EnumerateFileSystemEntries(evidenceRoot)
                            .Count());
                }
            }
            finally
            {
                DeleteTemporaryTestRoot(testRoot);
            }
        }

        private static void VerifyDatabaseCustodyRecord(
            string projectRoot,
            EvidenceCustodyRecord expected)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    OriginalFileName,
                    RelativeCustodyPath,
                    FileSizeBytes,
                    Sha256Hex,
                    AcceptedUtc
                FROM HLAS_Evidence_Custody
                WHERE EvidenceId = $evidenceId;
                """;

            command.Parameters.AddWithValue(
                "$evidenceId",
                expected.EvidenceId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(reader.Read());

            Assert.AreEqual(
                expected.OriginalFileName,
                reader.GetString(0));

            Assert.AreEqual(
                expected.RelativeCustodyPath,
                reader.GetString(1));

            Assert.AreEqual(
                expected.FileSizeBytes,
                reader.GetInt64(2));

            Assert.AreEqual(
                expected.Sha256Hex,
                reader.GetString(3));

            Assert.AreEqual(
                expected.AcceptedUtc.Value.ToString("O"),
                reader.GetString(4));

            Assert.IsFalse(reader.Read());
        }

        private static long ReadCustodyRecordCount(
            string projectRoot)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT COUNT(*)
                FROM HLAS_Evidence_Custody;
                """;

            return Convert.ToInt64(
                command.ExecuteScalar());
        }

        private static void RemoveCustodyTable(
            string projectRoot)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            using SqliteConnection connection =
                OpenDatabase(databasePath);

            connection.Open();

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                DROP TABLE HLAS_Evidence_Custody;
                """;

            command.ExecuteNonQuery();

            transaction.Commit();
        }

        private static SqliteConnection OpenDatabase(
            string databasePath)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            return new SqliteConnection(
                builder.ToString());
        }

        private static string ComputeSha256(
            string filePath)
        {
            using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            using SHA256 hasher =
                SHA256.Create();

            byte[] hash =
                hasher.ComputeHash(stream);

            return Convert
                .ToHexString(hash)
                .ToLowerInvariant();
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