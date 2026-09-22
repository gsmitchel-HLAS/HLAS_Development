using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class SourceEvidenceMaintenanceChangeRecordTests
    {
        [TestMethod]
        public void Constructor_ValidChange_PreservesExactValues()
        {
            OperationId operationId = OperationId.CreateNew();

            SourceEvidenceMaintenanceChangeRecord record = new(
                operationId,
                1,
                "OriginalFileName",
                "old.pdf",
                "corrected.pdf");

            Assert.AreEqual(operationId, record.OperationId);
            Assert.AreEqual(1, record.ChangeSequence);
            Assert.AreEqual("OriginalFileName", record.FieldName);
            Assert.AreEqual("old.pdf", record.PriorValue);
            Assert.AreEqual("corrected.pdf", record.ResultingValue);
        }

        [TestMethod]
        public void Constructor_NonPositiveSequence_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new SourceEvidenceMaintenanceChangeRecord(
                    OperationId.CreateNew(),
                    0,
                    "OriginalFileName",
                    "old.pdf",
                    "corrected.pdf"));
        }

        [TestMethod]
        public void Constructor_BlankFieldName_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceMaintenanceChangeRecord(
                    OperationId.CreateNew(),
                    1,
                    " ",
                    "old.pdf",
                    "corrected.pdf"));
        }

        [TestMethod]
        public void Constructor_UnchangedValue_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceMaintenanceChangeRecord(
                    OperationId.CreateNew(),
                    1,
                    "OriginalFileName",
                    "same.pdf",
                    "same.pdf"));
        }
    }
}