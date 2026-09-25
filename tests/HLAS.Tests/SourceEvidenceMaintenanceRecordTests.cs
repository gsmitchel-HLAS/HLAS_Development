using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class SourceEvidenceMaintenanceRecordTests
    {
        [TestMethod]
        public void Constructor_Correct_PreservesSameEvidenceAndLineage()
        {
            OperationId operationId = OperationId.CreateNew();
            FreezeId freezeId = FreezeId.CreateNew();
            EvidenceId evidenceId = EvidenceId.CreateNew();
            GovernedTimestamp maintainedUtc =
                GovernedTimestamp.CreateNow();

            SourceEvidenceMaintenanceRecord record = new(
                operationId,
                freezeId,
                evidenceId,
                evidenceId,
                SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                maintainedUtc);
            Assert.IsNull(record.ReplacementReason);
            Assert.AreEqual(operationId, record.OperationId);
            Assert.AreEqual(freezeId, record.FreezeId);
            Assert.AreEqual(evidenceId, record.PriorEvidenceId);
            Assert.AreEqual(evidenceId, record.ResultingEvidenceId);
            Assert.AreEqual(
                SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                record.MaintenanceType);
            Assert.AreEqual(maintainedUtc, record.MaintainedUtc);
        }

        [TestMethod]
        public void Constructor_Replace_PreservesOldAndNewEvidenceLineage()
        {
            EvidenceId priorEvidenceId = EvidenceId.CreateNew();
            EvidenceId resultingEvidenceId = EvidenceId.CreateNew();

            SourceEvidenceMaintenanceRecord record = new(
                OperationId.CreateNew(),
                FreezeId.CreateNew(),
                priorEvidenceId,
                resultingEvidenceId,
                SourceEvidenceMaintenanceRecord.ReplaceMaintenanceType,
GovernedTimestamp.CreateNow(),
"Governed replacement reason");
            Assert.AreEqual(
    "Governed replacement reason",
    record.ReplacementReason);
            Assert.AreEqual(
                priorEvidenceId,
                record.PriorEvidenceId);
            Assert.AreEqual(
                resultingEvidenceId,
                record.ResultingEvidenceId);
        }

        [TestMethod]
        public void Constructor_CorrectWithDifferentEvidenceId_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceMaintenanceRecord(
                    OperationId.CreateNew(),
                    FreezeId.CreateNew(),
                    EvidenceId.CreateNew(),
                    EvidenceId.CreateNew(),
                    SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_ReplaceWithSameEvidenceId_IsRejected()
        {
            EvidenceId evidenceId = EvidenceId.CreateNew();

            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceMaintenanceRecord(
                    OperationId.CreateNew(),
                    FreezeId.CreateNew(),
                    evidenceId,
                    evidenceId,
                    SourceEvidenceMaintenanceRecord.ReplaceMaintenanceType,
                    GovernedTimestamp.CreateNow()));
        }
        [TestMethod]
        public void Constructor_ReplaceBlankReason_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceMaintenanceRecord(
                    OperationId.CreateNew(),
                    FreezeId.CreateNew(),
                    EvidenceId.CreateNew(),
                    EvidenceId.CreateNew(),
                    SourceEvidenceMaintenanceRecord.ReplaceMaintenanceType,
                    GovernedTimestamp.CreateNow(),
                    " "));
        }

        [TestMethod]
        public void Constructor_CorrectWithReplacementReason_IsRejected()
        {
            EvidenceId evidenceId =
                EvidenceId.CreateNew();

            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceMaintenanceRecord(
                    OperationId.CreateNew(),
                    FreezeId.CreateNew(),
                    evidenceId,
                    evidenceId,
                    SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                    GovernedTimestamp.CreateNow(),
                    "Not allowed for CORRECT"));
        }
    }
}