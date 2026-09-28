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
    public sealed class OperationalAuthorityIntegrationTests
    {
        [TestMethod]
        public void AuthenticateAndAuthorize_RealStores_ReturnsGovernedIdentityAndRole()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string identityDatabasePath =
                Path.Combine(
                    testRoot,
                    "Identity",
                    LocalIdentityStore.DatabaseFileName);

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            try
            {
                UserId userId =
                    LocalIdentityStore.BootstrapInitialIdentity(
                        identityDatabasePath,
                        "Greg",
                        "development-secret");

                ProjectManifest manifest =
                    ProjectPackageCreator.CreateNew(
                        projectRoot);

                ProjectAuthorizationStore.AddAuthorization(
                    projectRoot,
                    userId,
                    ProjectRole.Admin);

                AuthenticationService authenticationService =
                    new(
                        new LocalIdentityGateway(
                            identityDatabasePath));

                AuthenticatedIdentity authenticatedIdentity =
                    authenticationService.Authenticate(
                        "Greg",
                        "development-secret");

                ProjectAuthorizationService authorizationService =
                    new(
                        new ProjectAuthorizationGateway());

                AuthorizedProjectIdentity authorizedIdentity =
                    authorizationService.Authorize(
                        projectRoot,
                        authenticatedIdentity);

                Assert.AreEqual(
                    userId,
                    authenticatedIdentity.UserId);

                Assert.AreEqual(
                    userId,
                    authorizedIdentity.UserId);

                Assert.AreEqual(
                    ProjectRole.Admin,
                    authorizedIdentity.ProjectRole);

                Assert.AreEqual(
                    manifest.ProjectId,
                    ProjectPackageReader.Open(
                        projectRoot).ProjectId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }

        [TestMethod]
        public void AuthenticateAndAuthorize_UserNotAuthorizedForProject_SafeStops()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string identityDatabasePath =
                Path.Combine(
                    testRoot,
                    "Identity",
                    LocalIdentityStore.DatabaseFileName);

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            try
            {
                _ = LocalIdentityStore.BootstrapInitialIdentity(
                    identityDatabasePath,
                    "Greg",
                    "development-secret");

                _ = ProjectPackageCreator.CreateNew(
                    projectRoot);

                AuthenticationService authenticationService =
                    new(
                        new LocalIdentityGateway(
                            identityDatabasePath));

                AuthenticatedIdentity authenticatedIdentity =
                    authenticationService.Authenticate(
                        "Greg",
                        "development-secret");

                ProjectAuthorizationService authorizationService =
                    new(
                        new ProjectAuthorizationGateway());

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => authorizationService.Authorize(
                            projectRoot,
                            authenticatedIdentity));

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

        private sealed class LocalIdentityGateway
            : IAuthenticationGateway
        {
            private readonly string _databasePath;

            public LocalIdentityGateway(
                string databasePath)
            {
                _databasePath = databasePath;
            }

            public AuthenticationGatewayResult Authenticate(
                string loginName,
                string secret)
            {
                UserId? userId =
                    LocalIdentityStore.Authenticate(
                        _databasePath,
                        loginName,
                        secret);

                return userId is null
                    ? AuthenticationGatewayResult.Rejected(
                        "HLAS authentication was rejected.")
                    : AuthenticationGatewayResult.Succeeded(
                        userId.Value);
            }
        }

        private sealed class ProjectAuthorizationGateway
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
                        "Authenticated HLAS identity is not authorized for this project.")
                    : ProjectAuthorizationGatewayResult.Authorized(
                        role.Value);
            }
        }

        private static string CreateTemporaryTestRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
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