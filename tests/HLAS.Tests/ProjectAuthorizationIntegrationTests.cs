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
    public sealed class ProjectAuthorizationIntegrationTests
    {
        [TestMethod]
        public void Authorize_ProjectStoredRoleSuppliesEffectiveRole()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ProjectAuthorizationStore.AddAuthorization(
                    projectRoot,
                    userId,
                    ProjectRole.Senior);

                AuthenticatedIdentity authenticatedIdentity =
                    new AuthenticationService(
                        new FakeAuthenticationGateway(
                            AuthenticationGatewayResult.Succeeded(userId)))
                    .Authenticate(
    "development-user",
    "development-secret");

                ProjectAuthorizationService service =
                    new(new StoreGateway());

                AuthorizedProjectIdentity result =
                    service.Authorize(
                        projectRoot,
                        authenticatedIdentity);

                Assert.AreEqual(userId, result.UserId);
                Assert.AreEqual(
                    ProjectRole.Senior,
                    result.ProjectRole);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

         [TestMethod]
        public void Authorize_DifferentAuthenticatedUsers_ResolveTheirOwnStoredProjectRoles()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                UserId firstUserId = UserId.CreateNew();
                UserId secondUserId = UserId.CreateNew();

                ProjectAuthorizationStore.AddAuthorization(
                    projectRoot,
                    firstUserId,
                    ProjectRole.Admin);

                ProjectAuthorizationStore.AddAuthorization(
                    projectRoot,
                    secondUserId,
                    ProjectRole.Technician);

                AuthenticatedIdentity firstIdentity =
                    new AuthenticationService(
                        new FakeAuthenticationGateway(
                            AuthenticationGatewayResult.Succeeded(firstUserId)))
                    .Authenticate(
                        "first-development-user",
                        "first-development-secret");

                AuthenticatedIdentity secondIdentity =
                    new AuthenticationService(
                        new FakeAuthenticationGateway(
                            AuthenticationGatewayResult.Succeeded(secondUserId)))
                    .Authenticate(
                        "second-development-user",
                        "second-development-secret");

                ProjectAuthorizationService service =
                    new(new StoreGateway());

                AuthorizedProjectIdentity firstResult =
                    service.Authorize(
                        projectRoot,
                        firstIdentity);

                AuthorizedProjectIdentity secondResult =
                    service.Authorize(
                        projectRoot,
                        secondIdentity);

                Assert.AreEqual(
                    ProjectRole.Admin,
                    firstResult.ProjectRole);
                Assert.AreEqual(
                    ProjectRole.Technician,
                    secondResult.ProjectRole);

                Assert.AreEqual(
                    secondUserId,
                    secondResult.UserId);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void Authorize_UserAbsentFromProject_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                AuthenticatedIdentity authenticatedIdentity =
                    new AuthenticationService(
                        new FakeAuthenticationGateway(
                            AuthenticationGatewayResult.Succeeded(userId)))
                    .Authenticate(
    "development-user",
    "development-secret");

                ProjectAuthorizationService service =
                    new(new StoreGateway());

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => service.Authorize(
                            projectRoot,
                            authenticatedIdentity));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        private sealed class StoreGateway
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

        private sealed class FakeAuthenticationGateway
            : IAuthenticationGateway
        {
            private readonly AuthenticationGatewayResult _result;

            public FakeAuthenticationGateway(
                AuthenticationGatewayResult result)
            {
                _result = result;
            }

            public AuthenticationGatewayResult Authenticate(
     string loginName,
     string secret)
            {
                return _result;
            }
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