using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityProjectJmfReviewVerifierTests
    {
        [TestMethod]
        public void Check_NoReviews_ReturnsVerifiedWithoutWrites()
        {
            string root = CreateProject();

            try
            {
                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfReviewVerifier.Check(root);

                Assert.AreEqual("PROJECT_JMF_REVIEW", result.CheckCode);
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
        public void Check_OpenReview_ReturnsPending()
        {
            string root = CreateProject();

            try
            {
                InsertReview(root, false, true, false);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfReviewVerifier.Check(root);

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
        public void Check_ResolvedReview_ReturnsVerifiedWithoutWrites()
        {
            string root = CreateProject();

            try
            {
                InsertReview(root, true, true, true);

                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfReviewVerifier.Check(root);

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
        public void Check_ResolvedCaseWithUndecidedItem_ReturnsIntegrityFailure()
        {
            string root = CreateProject();

            try
            {
                InsertReview(root, true, true, false);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfReviewVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_CaseWithoutItems_ReturnsIntegrityFailure()
        {
            string root = CreateProject();

            try
            {
                InsertReview(root, false, false, false);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfReviewVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestMethod]
        public void Check_OrphanItem_ReturnsIntegrityFailure()
        {
            string root = CreateProject();

            try
            {
                using SqliteConnection connection = OpenWrite(root);
                InsertItem(
                    connection,
                    Guid.NewGuid().ToString("D"),
                    false);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfReviewVerifier.Check(root);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static void InsertReview(
            string root,
            bool resolved,
            bool includeItem,
            bool decided)
        {
            string caseId = Guid.NewGuid().ToString("D");
            string timestamp = DateTimeOffset.UtcNow.ToString("O");

            using SqliteConnection connection = OpenWrite(root);

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    INSERT INTO HLAS_Project_JMF_Review_Cases
                    (
                        ReviewCaseId,
                        SourceEvidenceId,
                        BaselineProjectJmfRevisionId,
                        OpenedOperationId,
                        OpenedUtc,
                        ResolvedOperationId,
                        ResolvedUtc
                    )
                    VALUES
                    (
                        $caseId, $sourceId, $revisionId,
                        $openedId, $timestamp,
                        $resolvedId, $resolvedUtc
                    );
                    """;

                command.Parameters.AddWithValue("$caseId", caseId);
                command.Parameters.AddWithValue(
                    "$sourceId", Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue(
                    "$revisionId", Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue(
                    "$openedId", Guid.NewGuid().ToString("D"));
                command.Parameters.AddWithValue("$timestamp", timestamp);
                command.Parameters.AddWithValue(
                    "$resolvedId",
                    resolved
                        ? Guid.NewGuid().ToString("D")
                        : (object)DBNull.Value);
                command.Parameters.AddWithValue(
                    "$resolvedUtc",
                    resolved ? timestamp : (object)DBNull.Value);

                Assert.AreEqual(1, command.ExecuteNonQuery());
            }

            if (includeItem)
            {
                InsertItem(connection, caseId, decided);
            }
        }

        private static void InsertItem(
            SqliteConnection connection,
            string caseId,
            bool decided)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                INSERT INTO HLAS_Project_JMF_Review_Items
                (
                    ReviewCaseId,
                    FieldId,
                    CurrentApprovedValue,
                    NewOfficialSourceValue,
                    Decision,
                    DecisionReason,
                    DecidedOperationId,
                    DecidedUtc
                )
                VALUES
                (
                    $caseId, 'PJT-001',
                    'CURRENT', 'NEW',
                    $decision, $reason,
                    $operationId, $decidedUtc
                );
                """;

            command.Parameters.AddWithValue("$caseId", caseId);
            command.Parameters.AddWithValue(
                "$decision",
                decided ? "KEEP CURRENT" : (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$reason",
                decided ? "Developmental review decision." : (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$operationId",
                decided
                    ? Guid.NewGuid().ToString("D")
                    : (object)DBNull.Value);
            command.Parameters.AddWithValue(
                "$decidedUtc",
                decided
                    ? DateTimeOffset.UtcNow.ToString("O")
                    : (object)DBNull.Value);

            Assert.AreEqual(1, command.ExecuteNonQuery());
        }

        private static SqliteConnection OpenWrite(string root)
        {
            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = DatabasePath(root),
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
                ForeignKeys = false
            };

            SqliteConnection connection =
                new(builder.ToString());

            connection.Open();
            return connection;
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

        private static string CreateProject()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));

            ProjectPackageCreator.CreateNew(root);
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