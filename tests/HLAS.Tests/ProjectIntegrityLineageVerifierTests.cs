using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityLineageVerifierTests
    {
        [TestMethod]
        public void Check_EmptyProject_VerifiesLineageWithoutWrites()
        {
            string root = NewTestRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                var before = ReadGovernedCounts(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityLineageVerifier.Check(root);

                Assert.AreEqual(
                    "LINEAGE_STRUCTURE",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                Assert.AreEqual(
                    before,
                    ReadGovernedCounts(root));
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [TestMethod]
        public void Check_BrokenSuccessorLineage_ReturnsIntegrityFailure()
        {
            string root = NewTestRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");

            File.WriteAllText(
                sourcePath,
                "Developmental lineage test evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                EvidenceCustodyRecord evidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                string databasePath = Path.Combine(
                    projectRoot,
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
                        UPDATE HLAS_Source_Evidence_Catalog
                        SET LifecycleState = 'Superseded'
                        WHERE EvidenceId = $evidenceId;
                        """;

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(1, command.ExecuteNonQuery());
                }

                var before = ReadGovernedCounts(projectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityLineageVerifier.Check(
                        projectRoot);

                Assert.AreEqual(
                    "LINEAGE_STRUCTURE",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);

                StringAssert.Contains(
                    result.Detail,
                    "replacement successor");

                Assert.AreEqual(
                    before,
                    ReadGovernedCounts(projectRoot));
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        private static (long Readiness, long Operations)
            ReadGovernedCounts(string root)
        {
            string databasePath = Path.Combine(
                root,
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
                    (SELECT COUNT(*) FROM HLAS_Readiness_Checks),
                    (SELECT COUNT(*) FROM HLAS_Governed_Operations);
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(reader.Read());

            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        private static string NewTestRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteTestRoot(string root)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}