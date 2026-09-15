using System;

namespace HLAS.Domain
{
    public sealed record EvidenceCustodyRecord
    {
        public EvidenceId EvidenceId { get; }
        public string OriginalFileName { get; }
        public string RelativeCustodyPath { get; }
        public long FileSizeBytes { get; }
        public string Sha256Hex { get; }
        public GovernedTimestamp AcceptedUtc { get; }

        public EvidenceCustodyRecord(
            EvidenceId evidenceId,
            string originalFileName,
            string relativeCustodyPath,
            long fileSizeBytes,
            string sha256Hex,
            GovernedTimestamp acceptedUtc)
        {
            if (evidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "EvidenceId may not be empty.",
                    nameof(evidenceId));
            }

            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                throw new ArgumentException(
                    "OriginalFileName may not be blank.",
                    nameof(originalFileName));
            }

            if (string.IsNullOrWhiteSpace(relativeCustodyPath))
            {
                throw new ArgumentException(
                    "RelativeCustodyPath may not be blank.",
                    nameof(relativeCustodyPath));
            }

            if (fileSizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fileSizeBytes),
                    "FileSizeBytes may not be negative.");
            }

            if (!IsValidSha256Hex(sha256Hex))
            {
                throw new ArgumentException(
                    "Sha256Hex must contain exactly 64 hexadecimal characters.",
                    nameof(sha256Hex));
            }

            if (acceptedUtc.Value == default)
            {
                throw new ArgumentException(
                    "AcceptedUtc must be populated.",
                    nameof(acceptedUtc));
            }

            EvidenceId = evidenceId;
            OriginalFileName = originalFileName;
            RelativeCustodyPath = relativeCustodyPath;
            FileSizeBytes = fileSizeBytes;
            Sha256Hex = sha256Hex;
            AcceptedUtc = acceptedUtc;
        }

        private static bool IsValidSha256Hex(string value)
        {
            if (value is null || value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool isDigit =
                    character >= '0' &&
                    character <= '9';

                bool isLowerHex =
                    character >= 'a' &&
                    character <= 'f';

                bool isUpperHex =
                    character >= 'A' &&
                    character <= 'F';

                if (!isDigit &&
                    !isLowerHex &&
                    !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}