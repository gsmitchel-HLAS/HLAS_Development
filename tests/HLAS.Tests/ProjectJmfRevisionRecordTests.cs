using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectJmfRevisionRecordTests
    {
        [TestMethod]
        public void Constructor_Revision1WithoutPrior_AcceptsRecord()
        {
            ProjectJmfRevisionId revisionId =
                ProjectJmfRevisionId.CreateNew();

            ProjectJmfRevisionRecord record =
                new(
                    revisionId,
                    1,
                    null,
                    EvidenceId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow());

            Assert.AreEqual(
                revisionId,
                record.ProjectJmfRevisionId);

            Assert.AreEqual(
                1,
                record.RevisionNumber);

            Assert.IsNull(
                record.PriorProjectJmfRevisionId);
        }

        [TestMethod]
        public void Constructor_Revision1WithPrior_SafeStops()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectJmfRevisionRecord(
                    ProjectJmfRevisionId.CreateNew(),
                    1,
                    ProjectJmfRevisionId.CreateNew(),
                    EvidenceId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_LaterRevisionWithoutPrior_SafeStops()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectJmfRevisionRecord(
                    ProjectJmfRevisionId.CreateNew(),
                    2,
                    null,
                    EvidenceId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_LaterRevisionWithPrior_AcceptsRecord()
        {
            ProjectJmfRevisionId priorRevisionId =
                ProjectJmfRevisionId.CreateNew();

            ProjectJmfRevisionRecord record =
                new(
                    ProjectJmfRevisionId.CreateNew(),
                    2,
                    priorRevisionId,
                    EvidenceId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow());

            Assert.AreEqual(
                priorRevisionId,
                record.PriorProjectJmfRevisionId);
        }
    }
}