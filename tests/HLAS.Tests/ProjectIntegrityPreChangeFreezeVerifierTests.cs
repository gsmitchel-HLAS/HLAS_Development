using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityPreChangeFreezeVerifierTests
    {
        [TestMethod]
        public void Check_NoFreeze_ReturnsNotCheckedWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityPreChangeFreezeVerifier.Check(root);

                Assert.AreEqual(
                    "PRE_CHANGE_FREEZE",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.NotChecked,
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
        public void Check_ValidFreeze_VerifiedWithoutWrites()
        {
            var sample = CreateProjectWithFreeze();

            try
            {
                byte[] before =
                    ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityPreChangeFreezeVerifier.Check(
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
        public void Check_MissingFrozenFile_ReturnsIntegrityFailure()
        {
            var sample = CreateProjectWithFreeze();

            try
            {
                File.Delete(
                    FrozenFilePath(sample.ProjectRoot, sample.Freeze));

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityPreChangeFreezeVerifier.Check(
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
        public void Check_TamperedFrozenFile_ReturnsIntegrityFailure()
        {
            var sample = CreateProjectWithFreeze();

            try
            {
                File.AppendAllText(
                    FrozenFilePath(sample.ProjectRoot, sample.Freeze),
                    "TAMPERED");

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityPreChangeFreezeVerifier.Check(
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
        public void Check_BrokenOperationLink_ReturnsIntegrityFailure()
        {
            var sample = CreateProjectWithFreeze();

            try
            {
                BreakLink(
                    sample.ProjectRoot,
                    sample.Freeze.FreezeId,
                    "OperationId");

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityPreChangeFreezeVerifier.Check(
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
        public void Check_BrokenCustodyLink_ReturnsIntegrityFailure()
        {
            var sample = CreateProjectWithFreeze();

            try
            {
                BreakLink(
                    sample.ProjectRoot,
                    sample.Freeze.FreezeId,
                    "TargetEvidenceId");

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityPreChangeFreezeVerifier.Check(
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
            FrozenStateRecord Freeze)
            CreateProjectWithFreeze()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            File.WriteAllText(
                sourcePath,
                "Developmental Pre-Change Freeze evidence.");

            ProjectManifest project =
                ProjectPackageCreator.CreateNew(projectRoot);

            EvidenceCustodyRecord evidence =
                EvidenceCustodyService.Accept(
                    projectRoot,
                    sourcePath);

            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    projectRoot,
                    project.ProjectId,
                    UserId.CreateNew(),
                    SeriesId.V,
                    ProjectRole.Admin);

            FrozenStateRecord freeze =
                PreChangeFreezeService.Create(
                    projectRoot,
                    operation.OperationId,
                    evidence.EvidenceId);

            return (root, projectRoot, freeze);
        }

        private static void BreakLink(
            string projectRoot,
            FreezeId freezeId,
            string column)
        {
            string allowedColumn = column switch
            {
                "OperationId" => "OperationId",
                "TargetEvidenceId" => "TargetEvidenceId",
                _ => throw new ArgumentException(
                    "Unsupported test column.",
                    nameof(column))
            };

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = DatabasePath(projectRoot),
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                ForeignKeys = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                $"""
                UPDATE HLAS_Frozen_States
                SET {allowedColumn} = $wrongId
                WHERE FreezeId = $freezeId;
                """;

            command.Parameters.AddWithValue(
                "$wrongId",
                Guid.NewGuid().ToString("D"));

            command.Parameters.AddWithValue(
                "$freezeId",
                freezeId.Value.ToString("D"));

            Assert.AreEqual(1, command.ExecuteNonQuery());
        }

        private static string FrozenFilePath(
            string projectRoot,
            FrozenStateRecord freeze)
        {
            return Path.Combine(
                projectRoot,
                freeze.RelativeFreezePath);
        }

        private static byte[] ReadDatabaseBytes(string root)
        {
            SqliteConnection.ClearAllPools();
            return File.ReadAllBytes(DatabasePath(root));
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