using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class CommonIdentityPrimitiveTests
    {
        [TestMethod]
        public void ProjectId_CreateNew_IsNotEmpty()
        {
            ProjectId id = ProjectId.CreateNew();

            Assert.AreNotEqual(Guid.Empty, id.Value);
        }

        [TestMethod]
        public void EvidenceId_CreateNew_IsNotEmpty()
        {
            EvidenceId id = EvidenceId.CreateNew();

            Assert.AreNotEqual(Guid.Empty, id.Value);
        }
        [TestMethod]
        public void FreezeId_CreateNew_IsNotEmpty()
        {
            FreezeId id = FreezeId.CreateNew();

            Assert.AreNotEqual(Guid.Empty, id.Value);
        }
        [TestMethod]
        public void UserId_CreateNew_IsNotEmpty()
        {
            UserId id = UserId.CreateNew();

            Assert.AreNotEqual(Guid.Empty, id.Value);
        }

        [TestMethod]
        public void OperationId_CreateNew_IsNotEmpty()
        {
            OperationId id = OperationId.CreateNew();

            Assert.AreNotEqual(Guid.Empty, id.Value);
        }

        [TestMethod]
        public void SeriesId_ApprovedValues_AreExact()
        {
            Assert.AreEqual("V", SeriesId.V.Value);
            Assert.AreEqual("A", SeriesId.A.Value);
            Assert.AreEqual("I", SeriesId.I.Value);
        }

        [TestMethod]
        public void ProjectRole_ApprovedValues_AreExact()
        {
            Assert.AreEqual("Technician", ProjectRole.Technician.Value);
            Assert.AreEqual("Senior", ProjectRole.Senior.Value);
            Assert.AreEqual("Admin", ProjectRole.Admin.Value);
        }

        [TestMethod]
        public void LifecycleState_ApprovedValues_AreExact()
        {
            Assert.AreEqual("Active", LifecycleState.Active.Value);
            Assert.AreEqual("Superseded", LifecycleState.Superseded.Value);
            Assert.AreEqual("Closed", LifecycleState.Closed.Value);
        }

        [TestMethod]
        public void OperationOutcome_ApprovedValues_AreExact()
        {
            Assert.AreEqual("SUCCESS", OperationOutcome.Success.Value);
            Assert.AreEqual("SAFE-STOP", OperationOutcome.SafeStop.Value);
            Assert.AreEqual(
                "TECHNICAL FAILURE",
                OperationOutcome.TechnicalFailure.Value);
        }

        [TestMethod]
        public void GovernedTimestamp_CreateNow_IsUtc()
        {
            GovernedTimestamp timestamp = GovernedTimestamp.CreateNow();

            Assert.AreEqual(TimeSpan.Zero, timestamp.Value.Offset);
        }

        [TestMethod]
        public void DecisionRecord_PreservesExactText()
        {
            const string decision = "  KEEP CURRENT  ";
            const string reason = "  Exact governed reason.  ";

            DecisionRecord record = new(decision, reason);

            Assert.AreEqual(decision, record.Decision);
            Assert.AreEqual(reason, record.Reason);
        }

        [TestMethod]
        public void DecisionRecord_BlankDecision_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new DecisionRecord("   ", "Valid reason"));
        }

        [TestMethod]
        public void DecisionRecord_BlankReason_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new DecisionRecord("Valid decision", "   "));
        }
    }
}