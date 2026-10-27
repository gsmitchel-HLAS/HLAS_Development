using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityRegisteredSourceEvidenceVerifierTests
    {
        [TestMethod]
        public void Check_EmptyProject_ReturnsNotChecked()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                var before = ReadCounts(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(root);

                Assert.AreEqual(
                    "SOURCE_EVIDENCE_CUSTODY",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);

                Assert.AreEqual(before, ReadCounts(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_GenericCustody_DoesNotCountAsRegistered()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Generic.txt");

            try
            {
                File.WriteAllText(
                    sourcePath,
                    "Generic developmental custody only.");

                ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyService.Accept(
                    projectRoot,
                    sourcePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        projectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_RegisteredSource_VerifiedAndCountsPreserved()
        {
            var sample = CreateRegisteredProject();

            try
            {
                var before = ReadCounts(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    "SOURCE_EVIDENCE_CUSTODY",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                Assert.AreEqual(
                    before,
                    ReadCounts(sample.ProjectRoot));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }

        [TestMethod]
        public void Check_MissingControlledFile_ReturnsIntegrityFailure()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string controlledPath = Path.Combine(
                    sample.ProjectRoot,
                    sample.Evidence.RelativeCustodyPath);

                File.Delete(controlledPath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
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
        public void Check_TamperedBytes_ReturnsIntegrityFailure()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string controlledPath = Path.Combine(
                    sample.ProjectRoot,
                    sample.Evidence.RelativeCustodyPath);

                byte[] bytes = File.ReadAllBytes(controlledPath);

                Assert.IsTrue(bytes.Length > 0);

                bytes[0] = (byte)(bytes[0] ^ 1);

                File.WriteAllBytes(controlledPath, bytes);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
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
        public void Check_CustodyPathRedirectedToOriginal_ReturnsFailureWithoutWrites()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.ManifestFileName);

                string originalPath = Path.Combine(
                    sample.Root, "Source.txt");

                string controlledPath = Path.Combine(
                    sample.ProjectRoot,
                    sample.Evidence.RelativeCustodyPath);

                Assert.IsTrue(File.Exists(originalPath));
                Assert.IsTrue(File.Exists(controlledPath));

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Evidence_Custody
                SET RelativeCustodyPath = $path
                WHERE EvidenceId = $evidenceId;
                """;

                    command.Parameters.AddWithValue(
                        "$path",
                        Path.Combine("..", "Source.txt"));

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        sample.Evidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);
                byte[] originalBefore = File.ReadAllBytes(originalPath);
                byte[] controlledBefore = File.ReadAllBytes(controlledPath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore, File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore, File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    originalBefore, File.ReadAllBytes(originalPath));

                CollectionAssert.AreEqual(
                    controlledBefore, File.ReadAllBytes(controlledPath));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_AbsoluteCustodyPathToOriginal_ReturnsFailureWithoutWrites()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.ManifestFileName);

                string originalPath = Path.Combine(
                    sample.Root, "Source.txt");

                string controlledPath = Path.Combine(
                    sample.ProjectRoot,
                    sample.Evidence.RelativeCustodyPath);

                Assert.IsTrue(Path.IsPathRooted(originalPath));

                CollectionAssert.AreEqual(
                    File.ReadAllBytes(originalPath),
                    File.ReadAllBytes(controlledPath));

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Evidence_Custody
                SET RelativeCustodyPath = $path
                WHERE EvidenceId = $evidenceId;
                """;

                    command.Parameters.AddWithValue(
                        "$path", originalPath);

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        sample.Evidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);
                byte[] originalBefore = File.ReadAllBytes(originalPath);
                byte[] controlledBefore = File.ReadAllBytes(controlledPath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore, File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore, File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    originalBefore, File.ReadAllBytes(originalPath));

                CollectionAssert.AreEqual(
                    controlledBefore, File.ReadAllBytes(controlledPath));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_RedirectedCustodyFilename_RejectsIdentitySubstitution()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string controlledPath = Path.Combine(
                    sample.ProjectRoot,
                    sample.Evidence.RelativeCustodyPath);

                string alternatePath = Path.Combine(
                    Path.GetDirectoryName(controlledPath)!,
                    "Alternate-Source.txt");

                File.Copy(controlledPath, alternatePath);

                string alternateRelativePath =
                    Path.GetRelativePath(
                        sample.ProjectRoot, alternatePath);

                Assert.AreNotEqual(
                    sample.Evidence.RelativeCustodyPath,
                    alternateRelativePath);

                CollectionAssert.AreEqual(
                    File.ReadAllBytes(controlledPath),
                    File.ReadAllBytes(alternatePath));

                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Evidence_Custody
                SET RelativeCustodyPath = $alternatePath
                WHERE EvidenceId = $evidenceId;
                """;

                    command.Parameters.AddWithValue(
                        "$alternatePath", alternateRelativePath);

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        sample.Evidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] originalBefore = File.ReadAllBytes(controlledPath);
                byte[] alternateBefore = File.ReadAllBytes(alternatePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore, File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    originalBefore, File.ReadAllBytes(controlledPath));

                CollectionAssert.AreEqual(
                    alternateBefore, File.ReadAllBytes(alternatePath));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_RedirectedEvidenceIdFolder_RejectsIdentitySubstitution()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string controlledPath = Path.Combine(
                    sample.ProjectRoot,
                    sample.Evidence.RelativeCustodyPath);

                EvidenceId otherEvidenceId = EvidenceId.CreateNew();

                string alternateFolder = Path.Combine(
                    sample.ProjectRoot,
                    EvidenceCustodyService.SourceEvidenceDirectoryName,
                    otherEvidenceId.Value.ToString("D"));

                Directory.CreateDirectory(alternateFolder);

                string alternatePath = Path.Combine(
                    alternateFolder,
                    sample.Evidence.OriginalFileName);

                File.Copy(controlledPath, alternatePath);

                CollectionAssert.AreEqual(
                    File.ReadAllBytes(controlledPath),
                    File.ReadAllBytes(alternatePath));

                string alternateRelativePath =
                    Path.GetRelativePath(
                        sample.ProjectRoot, alternatePath);

                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                string manifestPath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.ManifestFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                UPDATE HLAS_Evidence_Custody
                SET RelativeCustodyPath = $alternatePath
                WHERE EvidenceId = $evidenceId;
                """;

                    command.Parameters.AddWithValue(
                        "$alternatePath", alternateRelativePath);

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        sample.Evidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                SqliteConnection.ClearAllPools();

                byte[] databaseBefore = File.ReadAllBytes(databasePath);
                byte[] manifestBefore = File.ReadAllBytes(manifestPath);
                byte[] controlledBefore = File.ReadAllBytes(controlledPath);
                byte[] alternateBefore = File.ReadAllBytes(alternatePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
                        sample.ProjectRoot);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                SqliteConnection.ClearAllPools();

                CollectionAssert.AreEqual(
                    databaseBefore, File.ReadAllBytes(databasePath));

                CollectionAssert.AreEqual(
                    manifestBefore, File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    controlledBefore, File.ReadAllBytes(controlledPath));

                CollectionAssert.AreEqual(
                    alternateBefore, File.ReadAllBytes(alternatePath));
            }
            finally
            {
                DeleteRoot(sample.Root);
            }
        }
        [TestMethod]
        public void Check_MissingCustodyLink_ReturnsIntegrityFailure()
        {
            var sample = CreateRegisteredProject();

            try
            {
                string databasePath = Path.Combine(
                    sample.ProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    ForeignKeys = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                        DELETE FROM HLAS_Evidence_Custody
                        WHERE EvidenceId = $evidenceId;
                        """;

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        sample.Evidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(
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

        private static (
            string Root,
            string ProjectRoot,
            EvidenceCustodyRecord Evidence)
            CreateRegisteredProject()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS registered developmental evidence.");

            ProjectPackageCreator.CreateNew(projectRoot);

            EvidenceCustodyRecord evidence =
                ProductionSourceEvidenceIntakeGateway.Accept(
                    projectRoot,
                    sourcePath);

            return (root, projectRoot, evidence);
        }

        private static (
            long Catalog,
            long Custody,
            long Readiness,
            long Operations)
            ReadCounts(string projectRoot)
        {
            string databasePath = Path.Combine(
                projectRoot,
                ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    (SELECT COUNT(*) FROM HLAS_Source_Evidence_Catalog),
                    (SELECT COUNT(*) FROM HLAS_Evidence_Custody),
                    (SELECT COUNT(*) FROM HLAS_Readiness_Checks),
                    (SELECT COUNT(*) FROM HLAS_Governed_Operations);
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(reader.Read());

            return (
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3));
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