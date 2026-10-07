using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class HistoricalCheckpointRecordTests
    {
        [TestMethod]
        public void Constructor_ValidReleaseFreeze_PreservesGovernedRecord()
        {
            FreezeId freezeId = FreezeId.CreateNew();
            OperationId operationId = OperationId.CreateNew();
            ReleaseId releaseId = ReleaseId.CreateNew();
            GovernedTimestamp frozenUtc =
                GovernedTimestamp.CreateNow();

            const string relativeCheckpointPath =
                @"HLAS_Frozen_States\release-freeze";

            const string manifestRelativePath =
                @"HLAS_Frozen_States\release-freeze\manifest.json";

            const string manifestSha256Hex =
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            HistoricalCheckpointRecord record = new(
                freezeId,
                operationId,
                HistoricalCheckpointRecord.ReleaseFreezeType,
                releaseId,
                relativeCheckpointPath,
                manifestRelativePath,
                manifestSha256Hex,
                frozenUtc);

            Assert.AreEqual(freezeId, record.FreezeId);
            Assert.AreEqual(operationId, record.OperationId);
            Assert.AreEqual(
                HistoricalCheckpointRecord.ReleaseFreezeType,
                record.CheckpointType);
            Assert.AreEqual(releaseId, record.ReleaseId);
            Assert.AreEqual(
                relativeCheckpointPath,
                record.RelativeCheckpointPath);
            Assert.AreEqual(
                manifestRelativePath,
                record.ManifestRelativePath);
            Assert.AreEqual(
                manifestSha256Hex,
                record.ManifestSha256Hex);
            Assert.AreEqual(frozenUtc, record.FrozenUtc);
        }

        [TestMethod]
        public void Constructor_ReleaseFreezeWithoutReleaseId_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new HistoricalCheckpointRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    HistoricalCheckpointRecord.ReleaseFreezeType,
                    null,
                    @"HLAS_Frozen_States\x",
                    @"HLAS_Frozen_States\x\manifest.json",
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_InvalidManifestSha256_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new HistoricalCheckpointRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    HistoricalCheckpointRecord.ReleaseFreezeType,
                    ReleaseId.CreateNew(),
                    @"HLAS_Frozen_States\x",
                    @"HLAS_Frozen_States\x\manifest.json",
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