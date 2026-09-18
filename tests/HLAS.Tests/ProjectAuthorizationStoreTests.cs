using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectAuthorizationStoreTests
    {
        [TestMethod]
        public void AddAuthorization_ThenResolveRole_ReturnsStoredRole()
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

                ProjectRole? result =
                    ProjectAuthorizationStore.ResolveRole(
                        projectRoot,
                        userId);

                Assert.IsNotNull(result);
                Assert.AreEqual(
                    ProjectRole.Senior,
                    result.Value);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void AddAuthorization_SameUserTwice_SafeStops()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                UserId userId = UserId.CreateNew();

                ProjectAuthorizationStore.AddAuthorization(
                    projectRoot,
                    userId,
                    ProjectRole.Technician);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectAuthorizationStore.AddAuthorization(
                            projectRoot,
                            userId,
                            ProjectRole.Admin));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    ProjectRole.Technician,
                    ProjectAuthorizationStore.ResolveRole(
                        projectRoot,
                        userId));
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }

        [TestMethod]
        public void ResolveRole_UserNotAuthorized_ReturnsNull()
        {
            string projectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(projectRoot);

                ProjectRole? result =
                    ProjectAuthorizationStore.ResolveRole(
                        projectRoot,
                        UserId.CreateNew());

                Assert.IsNull(result);
            }
            finally
            {
                DeleteTemporaryProjectRoot(projectRoot);
            }
        }
        [TestMethod]
        public void SameIdentity_DifferentProjects_CanHaveDifferentRoles()
        {
            string firstProjectRoot = CreateTemporaryProjectRoot();
            string secondProjectRoot = CreateTemporaryProjectRoot();

            try
            {
                ProjectPackageCreator.CreateNew(firstProjectRoot);
                ProjectPackageCreator.CreateNew(secondProjectRoot);

                UserId userId = UserId.CreateNew();

                ProjectAuthorizationStore.AddAuthorization(
                    firstProjectRoot,
                    userId,
                    ProjectRole.Technician);

                ProjectAuthorizationStore.AddAuthorization(
                    secondProjectRoot,
                    userId,
                    ProjectRole.Admin);

                Assert.AreEqual(
                    ProjectRole.Technician,
                    ProjectAuthorizationStore.ResolveRole(
                        firstProjectRoot,
                        userId));

                Assert.AreEqual(
                    ProjectRole.Admin,
                    ProjectAuthorizationStore.ResolveRole(
                        secondProjectRoot,
                        userId));
            }
            finally
            {
                DeleteTemporaryProjectRoot(firstProjectRoot);
                DeleteTemporaryProjectRoot(secondProjectRoot);
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