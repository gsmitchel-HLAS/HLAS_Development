using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class GovernedOperationRecordTests
    {
        [TestMethod]
        public void Begin_CreatesOpenOperationWithAuthorityContext()
        {
            ProjectId projectId = ProjectId.CreateNew();
            UserId userId = UserId.CreateNew();

            GovernedOperationRecord operation =
                GovernedOperationRecord.Begin(
                    projectId,
                    userId,
                    SeriesId.V,
                    ProjectRole.Admin);

            Assert.AreNotEqual(
                Guid.Empty,
                operation.OperationId.Value);

            Assert.AreEqual(
                projectId,
                operation.ProjectId);

            Assert.AreEqual(
                userId,
                operation.UserId);

            Assert.AreEqual(
                SeriesId.V,
                operation.SeriesId);

            Assert.AreEqual(
                ProjectRole.Admin,
                operation.ProjectRole);

            Assert.IsFalse(
                operation.IsFinalized);

            Assert.IsFalse(
                operation.CompletedUtc.HasValue);

            Assert.IsFalse(
                operation.Outcome.HasValue);

            Assert.IsFalse(
                operation.DecisionRecord.HasValue);
        }

        [TestMethod]
        public void FinalizeSuccess_WithoutDecision_FinalizesOnce()
        {
            GovernedOperationRecord open =
                CreateOpenOperation();

            GovernedOperationRecord finalized =
                open.FinalizeSuccess();

            Assert.IsTrue(
                finalized.IsFinalized);

            Assert.AreEqual(
                OperationOutcome.Success,
                finalized.Outcome);

            Assert.IsTrue(
                finalized.CompletedUtc.HasValue);

            Assert.IsFalse(
                finalized.DecisionRecord.HasValue);

            Assert.AreEqual(
                open.OperationId,
                finalized.OperationId);

            Assert.AreEqual(
                open.ProjectId,
                finalized.ProjectId);

            Assert.AreEqual(
                open.UserId,
                finalized.UserId);

            Assert.AreEqual(
                open.SeriesId,
                finalized.SeriesId);

            Assert.AreEqual(
                open.ProjectRole,
                finalized.ProjectRole);

            Assert.AreEqual(
                open.StartedUtc,
                finalized.StartedUtc);

            Assert.ThrowsExactly<InvalidOperationException>(
                () => finalized.FinalizeSuccess());
        }

        [TestMethod]
        public void FinalizeSuccess_WithDecision_PreservesExactDecision()
        {
            GovernedOperationRecord open =
                CreateOpenOperation();

            DecisionRecord decision = new(
                "  APPROVED  ",
                "  Exact governed reason.  ");

            GovernedOperationRecord finalized =
                open.FinalizeSuccess(decision);

            Assert.AreEqual(
                OperationOutcome.Success,
                finalized.Outcome);

            Assert.AreEqual(
                decision,
                finalized.DecisionRecord);
        }

        [TestMethod]
        public void FinalizeSafeStop_RequiresAndPreservesDecision()
        {
            GovernedOperationRecord open =
                CreateOpenOperation();

            DecisionRecord decision = new(
                "SAFE-STOP",
                "Required governed condition was not satisfied.");

            GovernedOperationRecord finalized =
                open.FinalizeSafeStop(decision);

            Assert.IsTrue(
                finalized.IsFinalized);

            Assert.AreEqual(
                OperationOutcome.SafeStop,
                finalized.Outcome);

            Assert.AreEqual(
                decision,
                finalized.DecisionRecord);

            Assert.IsTrue(
                finalized.CompletedUtc.HasValue);
        }

        [TestMethod]
        public void FinalizeTechnicalFailure_RequiresAndPreservesDecision()
        {
            GovernedOperationRecord open =
                CreateOpenOperation();

            DecisionRecord decision = new(
                "TECHNICAL FAILURE",
                "A bounded technical failure prevented completion.");

            GovernedOperationRecord finalized =
                open.FinalizeTechnicalFailure(decision);

            Assert.IsTrue(
                finalized.IsFinalized);

            Assert.AreEqual(
                OperationOutcome.TechnicalFailure,
                finalized.Outcome);

            Assert.AreEqual(
                decision,
                finalized.DecisionRecord);

            Assert.IsTrue(
                finalized.CompletedUtc.HasValue);
        }

        [TestMethod]
        public void SeparateAttempts_ReceiveSeparateOperationIds()
        {
            ProjectId projectId = ProjectId.CreateNew();
            UserId userId = UserId.CreateNew();

            GovernedOperationRecord first =
                GovernedOperationRecord.Begin(
                    projectId,
                    userId,
                    SeriesId.V,
                    ProjectRole.Technician);

            GovernedOperationRecord second =
                GovernedOperationRecord.Begin(
                    projectId,
                    userId,
                    SeriesId.V,
                    ProjectRole.Technician);

            Assert.AreNotEqual(
                first.OperationId,
                second.OperationId);
        }

        private static GovernedOperationRecord CreateOpenOperation()
        {
            return GovernedOperationRecord.Begin(
                ProjectId.CreateNew(),
                UserId.CreateNew(),
                SeriesId.V,
                ProjectRole.Senior);
        }
    }
}