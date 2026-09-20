using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class FrozenStateRecordTests
    {
        [TestMethod]
        public void Constructor_ValidValues_PreservesGovernedRecord()
        {
            FreezeId freezeId = FreezeId.CreateNew();
            OperationId operationId = OperationId.CreateNew();
            EvidenceId evidenceId = EvidenceId.CreateNew();
            GovernedTimestamp frozenUtc =
                GovernedTimestamp.CreateNow();

            const string relativeFreezePath =
                @"HLAS_Frozen_States\freeze-id\Source.pdf";

            const long fileSizeBytes = 12345;

            const string sha256Hex =
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            FrozenStateRecord record = new(
                freezeId,
                operationId,
                evidenceId,
                FrozenStateRecord.PreChangeFreezeType,
                relativeFreezePath,
                fileSizeBytes,
                sha256Hex,
                frozenUtc);

            Assert.AreEqual(freezeId, record.FreezeId);
            Assert.AreEqual(operationId, record.OperationId);
            Assert.AreEqual(evidenceId, record.TargetEvidenceId);
            Assert.AreEqual(
                FrozenStateRecord.PreChangeFreezeType,
                record.FreezeType);
            Assert.AreEqual(
                relativeFreezePath,
                record.RelativeFreezePath);
            Assert.AreEqual(
                fileSizeBytes,
                record.FileSizeBytes);
            Assert.AreEqual(
                sha256Hex,
                record.Sha256Hex);
            Assert.AreEqual(
                frozenUtc,
                record.FrozenUtc);
        }

        [TestMethod]
        public void Constructor_InvalidFreezeType_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new FrozenStateRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    EvidenceId.CreateNew(),
                    "RELEASE",
                    @"HLAS_Frozen_States\x\Source.pdf",
                    1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_BlankRelativeFreezePath_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new FrozenStateRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    EvidenceId.CreateNew(),
                    FrozenStateRecord.PreChangeFreezeType,
                    "   ",
                    1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_NegativeFileSize_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new FrozenStateRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    EvidenceId.CreateNew(),
                    FrozenStateRecord.PreChangeFreezeType,
                    @"HLAS_Frozen_States\x\Source.pdf",
                    -1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_InvalidSha256_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new FrozenStateRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    EvidenceId.CreateNew(),
                    FrozenStateRecord.PreChangeFreezeType,
                    @"HLAS_Frozen_States\x\Source.pdf",
                    1,
                    "not-a-valid-sha256",
                    GovernedTimestamp.CreateNow()));
        }

        private static string ValidSha256()
        {
            return
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        }
    }
}