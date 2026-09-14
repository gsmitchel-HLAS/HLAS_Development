using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectManifestTests
    {
        [TestMethod]
        public void CreateNew_CreatesCoherentProjectIdentity()
        {
            ProjectManifest manifest = ProjectManifest.CreateNew();

            Assert.AreNotEqual(Guid.Empty, manifest.ProjectId.Value);
            Assert.AreEqual(
                ProjectManifest.CurrentProjectFormatVersion,
                manifest.ProjectFormatVersion);
            Assert.AreEqual(
                TimeSpan.Zero,
                manifest.CreatedUtc.Value.Offset);
        }

        [TestMethod]
        public void Constructor_EmptyProjectId_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectManifest(
                    default,
                    ProjectManifest.CurrentProjectFormatVersion,
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_UnsupportedFormatVersion_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new ProjectManifest(
                    ProjectId.CreateNew(),
                    999,
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_MissingCreatedUtc_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectManifest(
                    ProjectId.CreateNew(),
                    ProjectManifest.CurrentProjectFormatVersion,
                    default));
        }
    }
}