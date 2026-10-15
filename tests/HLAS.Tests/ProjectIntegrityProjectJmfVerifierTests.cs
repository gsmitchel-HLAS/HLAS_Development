using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityProjectJmfVerifierTests
    {
        private static readonly string[] FieldIds =
        {
            "PJT-001", "PJT-002", "PJT-003",
            "PJT-004", "PJT-005", "PJT-006",
            "PJT-007",
            "JMF-001", "JMF-002", "JMF-003",
            "JMF-004", "JMF-005", "JMF-006",
            "JMF-007", "JMF-008", "JMF-009",
            "JMF-010", "JMF-011", "JMF-012",
            "JMF-013"
        };

        [TestMethod]
        public void Check_NoRevision_ReturnsPendingWithoutWrites()
        {
            string root = NewRoot();

            try
            {
                ProjectPackageCreator.CreateNew(root);

                byte[] before = ReadDatabaseBytes(root);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfVerifier.Check(root);

                Assert.AreEqual(
                    "PROJECT_JMF_TRUTH",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Pending,
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
        public void Check_CompleteRevision_VerifiedWithoutWrites()
        {
            var sample = CreateCompleteRevisionProject();

            try
            {
                byte[] before =
                    ReadDatabaseBytes(sample.ProjectRoot);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfVerifier.Check(
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
        public void Check_MissingField_ReturnsIntegrityFailure()
        {
            var sample = CreateCompleteRevisionProject();

            try
            {
                ExecuteTamper(
                    sample.ProjectRoot,
                    """
                    DELETE FROM HLAS_Project_JMF_Revision_Fields
                    WHERE ProjectJmfRevisionId = $id
                      AND FieldId = 'JMF-013';
                    """,
                    sample.RevisionId);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfVerifier.Check(
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
        public void Check_FailedApprovalOperation_ReturnsIntegrityFailure()
        {
            var sample = CreateCompleteRevisionProject();

            try
            {
                ExecuteTamper(
                    sample.ProjectRoot,
                    """
                    UPDATE HLAS_Governed_Operations
                    SET Outcome = 'SAFE-STOP',
                        Decision = 'REJECT',
                        Reason = 'Developmental test outcome.'
                    WHERE OperationId = $id;
                    """,
                    sample.OperationId);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfVerifier.Check(
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
        public void Check_BrokenFieldSourceLink_ReturnsIntegrityFailure()
        {
            var sample = CreateCompleteRevisionProject();

            try
            {
                ExecuteTamper(
                    sample.ProjectRoot,
                    """
                    UPDATE HLAS_Project_JMF_Revision_Fields
                    SET SourceEvidenceId = $value
                    WHERE ProjectJmfRevisionId = $id
                      AND FieldId = 'JMF-013';
                    """,
                    sample.RevisionId,
                    Guid.NewGuid().ToString("D"));

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectJmfVerifier.Check(
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
            string RevisionId,
            string OperationId)
            CreateCompleteRevisionProject()
        {
            string root = NewRoot();
            string projectRoot = Path.Combine(root, "Project");
            string sourcePath = Path.Combine(root, "Source.txt");
            Directory.CreateDirectory(root);

            File.WriteAllText(
                sourcePath,
                "Developmental Project/JMF source evidence.");

            ProjectManifest project =
                ProjectPackageCreator.CreateNew(projectRoot);

            EvidenceCustodyRecord evidence =
                ProductionSourceEvidenceIntakeGateway.Accept(
                    projectRoot,
                    sourcePath);

            string revisionId = Guid.NewGuid().ToString("D");
            string operationId = Guid.NewGuid().ToString("D");
            string timestamp = DateTimeOffset.UtcNow.ToString("O");

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = DatabasePath(projectRoot),
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            using (SqliteConnection connection =
                new(builder.ToString()))
            {
                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                using (SqliteCommand command =
                    connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText =
                        """
                        INSERT INTO HLAS_Governed_Operations
                        (
                            OperationId, ProjectId, UserId,
                            SeriesId, ProjectRole,
                            StartedUtc, CompletedUtc, Outcome
                        )
                        VALUES
                        (
                            $operationId, $projectId, $userId,
                            'V', 'Admin',
                            $timestamp, $timestamp, 'SUCCESS'
                        );

                        INSERT INTO HLAS_Project_JMF_Revisions
                        (
                            ProjectJmfRevisionId, RevisionNumber,
                            PriorProjectJmfRevisionId, SourceEvidenceId,
                            OperationId, ApprovedUtc
                        )
                        VALUES
                        (
                            $revisionId, 1, NULL, $sourceId,
                            $operationId, $timestamp
                        );
                        """;

                    command.Parameters.AddWithValue(
                        "$operationId", operationId);

                    command.Parameters.AddWithValue(
                        "$projectId",
                        project.ProjectId.Value.ToString("D"));

                    command.Parameters.AddWithValue(
                        "$userId",
                        Guid.NewGuid().ToString("D"));

                    command.Parameters.AddWithValue(
                        "$timestamp", timestamp);

                    command.Parameters.AddWithValue(
                        "$revisionId", revisionId);

                    command.Parameters.AddWithValue(
                        "$sourceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    command.ExecuteNonQuery();
                }

                using (SqliteCommand fieldCommand =
                    connection.CreateCommand())
                {
                    fieldCommand.Transaction = transaction;

                    fieldCommand.CommandText =
                        """
                        INSERT INTO HLAS_Project_JMF_Revision_Fields
                        (
                            ProjectJmfRevisionId, FieldId,
                            FieldValue, SourceEvidenceId
                        )
                        VALUES
                        (
                            $revisionId, $fieldId,
                            $fieldValue, $sourceId
                        );
                        """;

                    fieldCommand.Parameters.AddWithValue(
                        "$revisionId", revisionId);

                    fieldCommand.Parameters.AddWithValue(
                        "$sourceId",
                        evidence.EvidenceId.Value.ToString("D"));

                    SqliteParameter fieldIdParameter =
                        fieldCommand.Parameters.Add(
                            "$fieldId", SqliteType.Text);

                    SqliteParameter fieldValueParameter =
                        fieldCommand.Parameters.Add(
                            "$fieldValue", SqliteType.Text);

                    foreach (string fieldId in FieldIds)
                    {
                        fieldIdParameter.Value = fieldId;
                        fieldValueParameter.Value = $"VALUE-{fieldId}";

                        Assert.AreEqual(
                            1,
                            fieldCommand.ExecuteNonQuery());
                    }
                }

                transaction.Commit();
            }

            return (root, projectRoot, revisionId, operationId);
        }

        private static void ExecuteTamper(
            string projectRoot,
            string sql,
            string id,
            string? value = null)
        {
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

            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);

            if (value is not null)
            {
                command.Parameters.AddWithValue("$value", value);
            }

            Assert.AreEqual(1, command.ExecuteNonQuery());
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
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
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
