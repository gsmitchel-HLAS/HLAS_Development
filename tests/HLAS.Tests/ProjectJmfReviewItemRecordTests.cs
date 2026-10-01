using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectJmfReviewItemRecordTests
    {
        [TestMethod]
        public void Constructor_UndecidedItem_AcceptsRecord()
        {
            ProjectJmfReviewItemRecord record =
                new(
                    ProjectJmfReviewCaseId.CreateNew(),
                    "PJT-001",
                    "CURRENT",
                    "NEW",
                    null,
                    null,
                    null,
                    null);

            Assert.IsNull(record.Decision);
            Assert.IsNull(record.DecisionReason);
            Assert.IsNull(record.DecidedOperationId);
            Assert.IsNull(record.DecidedUtc);
        }

        [TestMethod]
        public void Constructor_DecisionWithoutReason_SafeStops()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectJmfReviewItemRecord(
                    ProjectJmfReviewCaseId.CreateNew(),
                    "PJT-001",
                    "CURRENT",
                    "NEW",
                    ProjectJmfReviewDecision.KeepCurrent,
                    null,
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_DecisionWithoutOperation_SafeStops()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectJmfReviewItemRecord(
                    ProjectJmfReviewCaseId.CreateNew(),
                    "PJT-001",
                    "CURRENT",
                    "NEW",
                    ProjectJmfReviewDecision.AcceptNew,
                    "Approved difference.",
                    null,
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_CompleteDecision_AcceptsRecord()
        {
            OperationId operationId =
                OperationId.CreateNew();

            GovernedTimestamp decidedUtc =
                GovernedTimestamp.CreateNow();

            ProjectJmfReviewItemRecord record =
                new(
                    ProjectJmfReviewCaseId.CreateNew(),
                    "JMF-001",
                    "CURRENT",
                    "NEW",
                    ProjectJmfReviewDecision.AcceptNew,
                    "Accept official source value.",
                    operationId,
                    decidedUtc);

            Assert.AreEqual(
                ProjectJmfReviewDecision.AcceptNew,
                record.Decision);

            Assert.AreEqual(
                operationId,
                record.DecidedOperationId);

            Assert.AreEqual(
                decidedUtc,
                record.DecidedUtc);
        }
    }
}
