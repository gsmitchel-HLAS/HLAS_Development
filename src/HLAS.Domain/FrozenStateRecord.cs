using System;

namespace HLAS.Domain
{
    public sealed record FrozenStateRecord
    {
        public const string PreChangeFreezeType =
            "PRE-CHANGE";

        public FreezeId FreezeId { get; }
        public OperationId OperationId { get; }
        public EvidenceId TargetEvidenceId { get; }
        public string FreezeType { get; }
        public string RelativeFreezePath { get; }
        public long FileSizeBytes { get; }
        public string Sha256Hex { get; }
        public GovernedTimestamp FrozenUtc { get; }

        public FrozenStateRecord(
            FreezeId freezeId,
            OperationId operationId,
            EvidenceId targetEvidenceId,
            string freezeType,
            string relativeFreezePath,
            long fileSizeBytes,
            string sha256Hex,
            GovernedTimestamp frozenUtc)
        {
            if (freezeId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "FreezeId may not be empty.",
                    nameof(freezeId));
            }

            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (targetEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "TargetEvidenceId may not be empty.",
                    nameof(targetEvidenceId));
            }

            if (!string.Equals(
                freezeType,
                PreChangeFreezeType,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "FreezeType must be PRE-CHANGE for this bounded foundation.",
                    nameof(freezeType));
            }

            if (string.IsNullOrWhiteSpace(relativeFreezePath))
            {
                throw new ArgumentException(
                    "RelativeFreezePath may not be blank.",
                    nameof(relativeFreezePath));
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

            if (frozenUtc.Value == default)
            {
                throw new ArgumentException(
                    "FrozenUtc must be populated.",
                    nameof(frozenUtc));
            }

            FreezeId = freezeId;
            OperationId = operationId;
            TargetEvidenceId = targetEvidenceId;
            FreezeType = freezeType;
            RelativeFreezePath = relativeFreezePath;
            FileSizeBytes = fileSizeBytes;
            Sha256Hex = sha256Hex;
            FrozenUtc = frozenUtc;
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