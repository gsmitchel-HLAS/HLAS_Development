using System;
using System.IO;
using System.Security.Cryptography;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionSnapshotVerifier
    {
        internal static void Verify(
            string projectRoot,
            InternalSeriesTransitionBasisReference basis,
            ReleaseFreezeManifest manifest)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(basis);
            ArgumentNullException.ThrowIfNull(manifest);

            string checkpointDirectory =
                Path.Combine(
                    Path.GetFullPath(projectRoot),
                    PreChangeFreezeService.FrozenStatesDirectoryName,
                    basis.ReleaseFreezeId.Value.ToString("D"));

            VerifyFile(
                checkpointDirectory,
                manifest.DatabaseFileName,
                ProjectPackageCreator.DatabaseFileName,
                manifest.DatabaseFileSizeBytes,
                manifest.DatabaseSha256Hex);

            VerifyFile(
                checkpointDirectory,
                manifest.ProjectManifestFileName,
                ProjectPackageCreator.ManifestFileName,
                manifest.ProjectManifestFileSizeBytes,
                manifest.ProjectManifestSha256Hex);
        }

        private static void VerifyFile(
            string checkpointDirectory,
            string recordedFileName,
            string expectedFileName,
            long expectedSize,
            string expectedHash)
        {
            if (!string.Equals(
                    recordedFileName,
                    expectedFileName,
                    StringComparison.Ordinal) ||
                expectedSize <= 0 ||
                string.IsNullOrWhiteSpace(expectedHash) ||
                expectedHash.Length != 64)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Historical checkpoint file metadata is invalid.");
            }

            string filePath =
                Path.Combine(
                    checkpointDirectory,
                    expectedFileName);

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Historical checkpoint file is missing.");
            }

            try
            {
                using FileStream stream =
                    File.OpenRead(filePath);

                long actualSize = stream.Length;

                string actualHash =
                    Convert.ToHexString(
                        SHA256.HashData(stream));

                if (actualSize != expectedSize ||
                    !string.Equals(
                        actualHash,
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Historical checkpoint file integrity failed.");
                }
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Historical checkpoint file could not be verified.",
                    exception);
            }
        }
    }
}