using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectJmfReviewCaseRecordTests
    {
        [TestMethod]
        public void Constructor_OpenCaseWithoutResolution_AcceptsRecord()
        {
            ProjectJmfReviewCaseId reviewCaseId =
                ProjectJmfReviewCaseId.CreateNew();

            ProjectJmfReviewCaseRecord record =
                new(
                    reviewCaseId,
                    EvidenceId.CreateNew(),
                    ProjectJmfRevisionId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow(),
                    null,
                    null);

            Assert.AreEqual(
                reviewCaseId,
                record.ReviewCaseId);

            Assert.IsNull(
                record.ResolvedOperationId);

            Assert.IsNull(
                record.ResolvedUtc);
        }

        [TestMethod]
        public void Constructor_ResolutionOperationWithoutTime_SafeStops()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectJmfReviewCaseRecord(
                    ProjectJmfReviewCaseId.CreateNew(),
                    EvidenceId.CreateNew(),
                    ProjectJmfRevisionId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow(),
                    OperationId.CreateNew(),
                    null));
        }

        [TestMethod]
        public void Constructor_ResolutionTimeWithoutOperation_SafeStops()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectJmfReviewCaseRecord(
                    ProjectJmfReviewCaseId.CreateNew(),
                    EvidenceId.CreateNew(),
                    ProjectJmfRevisionId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow(),
                    null,
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_ResolvedCaseWithOperationAndTime_AcceptsRecord()
        {
            OperationId resolvedOperationId =
                OperationId.CreateNew();

            GovernedTimestamp resolvedUtc =
                GovernedTimestamp.CreateNow();

            ProjectJmfReviewCaseRecord record =
                new(
                    ProjectJmfReviewCaseId.CreateNew(),
                    EvidenceId.CreateNew(),
                    ProjectJmfRevisionId.CreateNew(),
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow(),
                    resolvedOperationId,
                    resolvedUtc);

            Assert.AreEqual(
                resolvedOperationId,
                record.ResolvedOperationId);

            Assert.AreEqual(
                resolvedUtc,
                record.ResolvedUtc);
        }
    }
}