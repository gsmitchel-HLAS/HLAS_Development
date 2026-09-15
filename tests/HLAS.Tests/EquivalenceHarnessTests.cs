using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class EquivalenceHarnessTests
    {
        [TestMethod]
        public void Harness_PreservesHistoryAndReportsCoverage()
        {
            EquivalenceHarness harness = new();

            EquivalenceTestDefinition definition = new(
                "EQ-001",
                EquivalenceClass.E,
                "Excel governed baseline",
                "Approved specification reference",
                "Governed state must match the approved baseline.");

            harness.RegisterTest(definition);

            EquivalenceTestAttempt initialAttempt = new(
                "EQ-001",
                1,
                EquivalenceAttemptType.Initial,
                EquivalenceOutcome.Fail,
                "Developmental comparison did not match.",
                new DateTimeOffset(
                    2026, 9, 15, 6, 0, 0, TimeSpan.Zero));

            EquivalenceTestAttempt retestAttempt = new(
                "EQ-001",
                2,
                EquivalenceAttemptType.Retest,
                EquivalenceOutcome.Pass,
                "Developmental retest matched.",
                new DateTimeOffset(
                    2026, 9, 15, 7, 0, 0, TimeSpan.Zero));

            harness.RecordAttempt(initialAttempt);
            harness.RecordAttempt(retestAttempt);

            harness.RegisterCoverageItem(
                new EquivalenceCoverageItem(
                    "COV-001",
                    "EQ-001",
                    "Developmental evidence obligation 1",
                    DevelopmentalEvidenceStatus.Recorded));

            harness.RegisterCoverageItem(
                new EquivalenceCoverageItem(
                    "COV-002",
                    "EQ-001",
                    "Developmental evidence obligation 2",
                    DevelopmentalEvidenceStatus.NotRecorded));

            IReadOnlyList<EquivalenceTestAttempt> attempts =
                harness.GetAttempts("EQ-001");

            EquivalenceCoverageSummary summary =
                harness.GetCoverageSummary();

            Assert.AreEqual(
                1,
                harness.Definitions.Count);

            Assert.AreEqual(
                2,
                attempts.Count);

            Assert.AreEqual(
                EquivalenceOutcome.Fail,
                attempts[0].Outcome);

            Assert.AreEqual(
                EquivalenceOutcome.Pass,
                attempts[1].Outcome);

            Assert.AreEqual(
                2,
                summary.TotalItems);

            Assert.AreEqual(
                1,
                summary.EvidenceRecorded);

            Assert.AreEqual(
                1,
                summary.EvidenceNotRecorded);
        }
    }
}