using System;

namespace HLAS.Domain
{
    public sealed record HistoricalCheckpointRecord
    {
        public const string ReleaseFreezeType =
            "RELEASE_FREEZE";

        public FreezeId FreezeId { get; }
        public OperationId OperationId { get; }
        public string CheckpointType { get; }
        public ReleaseId? ReleaseId { get; }
        public string RelativeCheckpointPath { get; }
        public string ManifestRelativePath { get; }
        public string ManifestSha256Hex { get; }
        public GovernedTimestamp FrozenUtc { get; }

        public HistoricalCheckpointRecord(
            FreezeId freezeId,
            OperationId operationId,
            string checkpointType,
            ReleaseId? releaseId,
            string relativeCheckpointPath,
            string manifestRelativePath,
            string manifestSha256Hex,
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

            if (string.IsNullOrWhiteSpace(checkpointType))
            {
                throw new ArgumentException(
                    "CheckpointType may not be blank.",
                    nameof(checkpointType));
            }

            if (string.Equals(
                    checkpointType,
                    ReleaseFreezeType,
                    StringComparison.Ordinal) &&
                releaseId is null)
            {
                throw new ArgumentException(
                    "Release Freeze requires ReleaseId.",
                    nameof(releaseId));
            }

            if (string.IsNullOrWhiteSpace(relativeCheckpointPath))
            {
                throw new ArgumentException(
                    "RelativeCheckpointPath may not be blank.",
                    nameof(relativeCheckpointPath));
            }

            if (string.IsNullOrWhiteSpace(manifestRelativePath))
            {
                throw new ArgumentException(
                    "ManifestRelativePath may not be blank.",
                    nameof(manifestRelativePath));
            }

            if (!IsValidSha256Hex(manifestSha256Hex))
            {
                throw new ArgumentException(
                    "ManifestSha256Hex must contain exactly 64 hexadecimal characters.",
                    nameof(manifestSha256Hex));
            }

            if (frozenUtc.Value == default)
            {
                throw new ArgumentException(
                    "FrozenUtc must be populated.",
                    nameof(frozenUtc));
            }

            FreezeId = freezeId;
            OperationId = operationId;
            CheckpointType = checkpointType;
            ReleaseId = releaseId;
            RelativeCheckpointPath = relativeCheckpointPath;
            ManifestRelativePath = manifestRelativePath;
            ManifestSha256Hex = manifestSha256Hex;
            FrozenUtc = frozenUtc;
        }

        private static bool IsValidSha256Hex(
            string value)
        {
            if (value is null ||
                value.Length != 64)
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