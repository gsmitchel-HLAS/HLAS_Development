using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class EquivalenceTestAttemptTests
    {
        [TestMethod]
        public void InitialFailureAndLaterRetestPassRemainSeparateHistory()
        {
            DateTimeOffset initialTime =
                new(2026, 9, 15, 6, 0, 0, TimeSpan.Zero);

            DateTimeOffset retestTime =
                new(2026, 9, 15, 7, 0, 0, TimeSpan.Zero);

            EquivalenceTestAttempt initialAttempt = new(
                "EQ-001",
                1,
                EquivalenceAttemptType.Initial,
                EquivalenceOutcome.Fail,
                "Developmental comparison did not match.",
                initialTime);

            EquivalenceTestAttempt retestAttempt = new(
                "EQ-001",
                2,
                EquivalenceAttemptType.Retest,
                EquivalenceOutcome.Pass,
                "Developmental retest matched.",
                retestTime);

            Assert.AreEqual(
                "EQ-001",
                initialAttempt.TestId);

            Assert.AreEqual(
                1,
                initialAttempt.AttemptNumber);

            Assert.AreEqual(
                EquivalenceAttemptType.Initial,
                initialAttempt.AttemptType);

            Assert.AreEqual(
                EquivalenceOutcome.Fail,
                initialAttempt.Outcome);

            Assert.AreEqual(
                "Developmental comparison did not match.",
                initialAttempt.ObservedResult);

            Assert.AreEqual(
                "EQ-001",
                retestAttempt.TestId);

            Assert.AreEqual(
                2,
                retestAttempt.AttemptNumber);

            Assert.AreEqual(
                EquivalenceAttemptType.Retest,
                retestAttempt.AttemptType);

            Assert.AreEqual(
                EquivalenceOutcome.Pass,
                retestAttempt.Outcome);

            Assert.AreEqual(
                "Developmental retest matched.",
                retestAttempt.ObservedResult);

            Assert.AreEqual(
                EquivalenceOutcome.Fail,
                initialAttempt.Outcome);
        }
    }
}