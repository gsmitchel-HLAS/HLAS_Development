using System;
using System.IO;
using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class DevelopmentalProjectHistoryProofTests
    {
        [TestMethod]
        public void Execute_GenericProject_PersistsHarmlessGovernedHistoryWithoutSourceEvidence()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId =
                    UserId.CreateNew();
                ProjectAuthorizationStore.AddAuthorization(
    projectRoot,
    userId,
    ProjectRole.Admin);

                AuthenticatedIdentity authenticatedIdentity =
                    new AuthenticationService(
                        new TestAuthenticationGateway(
                            AuthenticationGatewayResult.Succeeded(userId)))
                    .Authenticate();

                ProjectAuthorizationService authorizationService =
                    new(new StoredProjectAuthorizationGateway());

                AuthorizedProjectIdentity authorizedIdentity =
                    authorizationService.Authorize(
                        projectRoot,
                        authenticatedIdentity);
                DevelopmentalProjectHistoryProofService service = new(
                    new RealDevelopmentalProjectHistoryProofGateway());

                DevelopmentalProjectHistoryProofResult result =
                    service.Execute(
    projectRoot,
    authorizedIdentity,
    SeriesId.V);

                Assert.AreEqual(
                    manifest.ProjectId,
                    result.ProjectId);

                Assert.AreEqual(
                    projectRoot,
                    result.ProjectRoot);

                string databasePath = Path.Combine(
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
                        ProjectId,
                        UserId,
                        SeriesId,
                        ProjectRole,
                        CompletedUtc,
                        Outcome,
                        Decision,
                        Reason
                    FROM HLAS_Governed_Operations
                    WHERE OperationId = $operationId;
                    """;

                command.Parameters.AddWithValue(
                    "$operationId",
                    result.OperationId.Value.ToString("D"));

                using SqliteDataReader reader =
                    command.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.AreEqual(
                    manifest.ProjectId.Value.ToString("D"),
                    reader.GetString(0));

                Assert.AreEqual(
                    userId.Value.ToString("D"),
                    reader.GetString(1));

                Assert.AreEqual(
                    "V",
                    reader.GetString(2));

                Assert.AreEqual(
                    "Admin",
                    reader.GetString(3));

                Assert.IsFalse(
                    reader.IsDBNull(4));

                Assert.AreEqual(
                    "SUCCESS",
                    reader.GetString(5));

                Assert.AreEqual(
                    "DEVELOPMENTAL PROJECT HISTORY PROOF",
                    reader.GetString(6));

                Assert.AreEqual(
                    "Harmless governed project-history transaction used to prove Build Foundation 0.1E-A and 0.1G-B.",
                    reader.GetString(7));

                Assert.IsFalse(
                    reader.Read());

                reader.Close();

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        connection,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        connection,
                        "HLAS_Source_Evidence_Catalog"));
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        private sealed class TestAuthenticationGateway
    : IAuthenticationGateway
        {
            private readonly AuthenticationGatewayResult _result;

            public TestAuthenticationGateway(
                AuthenticationGatewayResult result)
            {
                _result = result;
            }

            public AuthenticationGatewayResult Authenticate()
            {
                return _result;
            }
        }

        private sealed class StoredProjectAuthorizationGateway
            : IProjectAuthorizationGateway
        {
            public ProjectAuthorizationGatewayResult Resolve(
                string projectRoot,
                UserId userId)
            {
                ProjectRole? role =
                    ProjectAuthorizationStore.ResolveRole(
                        projectRoot,
                        userId);

                return role is null
                    ? ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")
                    : ProjectAuthorizationGatewayResult.Authorized(
                        role.Value);
            }
        }
        private sealed class RealDevelopmentalProjectHistoryProofGateway
            : IDevelopmentalProjectHistoryProofGateway
        {
            public DevelopmentalProjectHistoryProofResult Execute(
                string projectRoot,
                UserId userId,
                SeriesId seriesId,
                ProjectRole projectRole,
                DecisionRecord decisionRecord)
            {
                ProjectManifest manifest =
                    ProjectPackageReader.Open(projectRoot);

                GovernedOperationRecord operation =
                    GovernedOperationService.Begin(
                        projectRoot,
                        manifest.ProjectId,
                        userId,
                        seriesId,
                        projectRole);

                GovernedOperationService.FinalizeSuccess(
                    projectRoot,
                    operation.OperationId,
                    decisionRecord);

                return new DevelopmentalProjectHistoryProofResult(
                    manifest.ProjectId,
                    operation.OperationId,
                    projectRoot);
            }
        }

        private static long ReadRecordCount(
            SqliteConnection connection,
            string tableName)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                $"SELECT COUNT(*) FROM {tableName};";

            return Convert.ToInt64(
                command.ExecuteScalar());
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

        private static string CreateTemporaryProjectRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
        }

        private static void DeleteTemporaryProjectRoot(
            string projectRoot)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(
                    projectRoot,
                    recursive: true);
            }
        }
    }
}