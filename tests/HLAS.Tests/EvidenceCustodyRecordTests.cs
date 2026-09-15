using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class EvidenceCustodyRecordTests
    {
        [TestMethod]
        public void Constructor_ValidValues_PreservesGovernedRecord()
        {
            EvidenceId evidenceId = EvidenceId.CreateNew();
            GovernedTimestamp acceptedUtc =
                GovernedTimestamp.CreateNow();

            const string originalFileName =
                "Original Source.pdf";

            const string relativeCustodyPath =
                @"HLAS_Source_Evidence\evidence-id\Original Source.pdf";

            const long fileSizeBytes = 12345;

            const string sha256Hex =
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            EvidenceCustodyRecord record = new(
                evidenceId,
                originalFileName,
                relativeCustodyPath,
                fileSizeBytes,
                sha256Hex,
                acceptedUtc);

            Assert.AreEqual(
                evidenceId,
                record.EvidenceId);

            Assert.AreEqual(
                originalFileName,
                record.OriginalFileName);

            Assert.AreEqual(
                relativeCustodyPath,
                record.RelativeCustodyPath);

            Assert.AreEqual(
                fileSizeBytes,
                record.FileSizeBytes);

            Assert.AreEqual(
                sha256Hex,
                record.Sha256Hex);

            Assert.AreEqual(
                acceptedUtc,
                record.AcceptedUtc);
        }

        [TestMethod]
        public void Constructor_EmptyEvidenceId_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new EvidenceCustodyRecord(
                    default,
                    "Source.pdf",
                    @"HLAS_Source_Evidence\x\Source.pdf",
                    1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_BlankOriginalFileName_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new EvidenceCustodyRecord(
                    EvidenceId.CreateNew(),
                    "   ",
                    @"HLAS_Source_Evidence\x\Source.pdf",
                    1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_BlankRelativeCustodyPath_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new EvidenceCustodyRecord(
                    EvidenceId.CreateNew(),
                    "Source.pdf",
                    "   ",
                    1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_NegativeFileSize_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new EvidenceCustodyRecord(
                    EvidenceId.CreateNew(),
                    "Source.pdf",
                    @"HLAS_Source_Evidence\x\Source.pdf",
                    -1,
                    ValidSha256(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_InvalidSha256_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new EvidenceCustodyRecord(
                    EvidenceId.CreateNew(),
                    "Source.pdf",
                    @"HLAS_Source_Evidence\x\Source.pdf",
                    1,
                    "not-a-valid-sha256",
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_MissingAcceptedUtc_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new EvidenceCustodyRecord(
                    EvidenceId.CreateNew(),
                    "Source.pdf",
                    @"HLAS_Source_Evidence\x\Source.pdf",
                    1,
                    ValidSha256(),
                    default));
        }

        private static string ValidSha256()
        {
            return
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        }
    }
}