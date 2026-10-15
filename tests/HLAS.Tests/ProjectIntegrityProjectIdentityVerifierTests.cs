using System;
using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityProjectIdentityVerifierTests
    {
        [TestMethod]
        public void Check_MatchingIdentity_ReturnsVerifiedWithoutChanges()
        {
            string root = NewProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                string manifestPath = Path.Combine(
                    root,
                    ProjectPackageCreator.ManifestFileName);

                string databasePath = Path.Combine(
                    root,
                    ProjectPackageCreator.DatabaseFileName);
                SqliteConnection.ClearAllPools();
                byte[] manifestBefore =
                    File.ReadAllBytes(manifestPath);

                byte[] databaseBefore =
                    File.ReadAllBytes(databasePath);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectIdentityVerifier.Check(
                        root,
                        project.ProjectId);

                Assert.AreEqual(
                    "PROJECT_IDENTITY",
                    result.CheckCode);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.Verified,
                    result.Status);

                CollectionAssert.AreEqual(
                    manifestBefore,
                    File.ReadAllBytes(manifestPath));

                CollectionAssert.AreEqual(
                    databaseBefore,
                    File.ReadAllBytes(databasePath));
            }
            finally
            {
                DeleteProjectRoot(root);
            }
        }

        [TestMethod]
        public void Check_DifferentSelectedProject_ReturnsIntegrityFailure()
        {
            string root = NewProjectRoot();

            try
            {
                ProjectManifest project =
                    ProjectPackageCreator.CreateNew(root);

                ProjectId differentProject =
                    ProjectId.CreateNew();

                Assert.AreNotEqual(
                    project.ProjectId,
                    differentProject);

                ProjectIntegrityCheckItem result =
                    ProjectIntegrityProjectIdentityVerifier.Check(
                        root,
                        differentProject);

                Assert.AreEqual(
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Status);
            }
            finally
            {
                DeleteProjectRoot(root);
            }
        }

        [TestMethod]
        public void Check_MissingProject_SafeStops()
        {
            string root = NewProjectRoot();

            try
            {
                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => ProjectIntegrityProjectIdentityVerifier.Check(
                            root,
                            ProjectId.CreateNew()));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");
            }
            finally
            {
                DeleteProjectRoot(root);
            }
        }

        private static string NewProjectRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "HLAS_Tests",
                Guid.NewGuid().ToString("N"));
        }

        private static void DeleteProjectRoot(string root)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}