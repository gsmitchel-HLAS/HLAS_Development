using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionEvidenceVerifier
    {
        internal static void Verify(
            string projectRoot,
            ReleaseFreezeManifest manifest)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(manifest);

            if (manifest.Evidence is null)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Released Source Evidence list is missing.");
            }

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            string evidenceRoot =
                Path.GetFullPath(
                    Path.Combine(
                        fullProjectRoot,
                        EvidenceCustodyService.SourceEvidenceDirectoryName));

            string evidenceRootPrefix =
                evidenceRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            HashSet<Guid> seenEvidenceIds = new();

            foreach (ReleaseFreezeManifestEvidenceEntry entry
                in manifest.Evidence)
            {
                if (entry is null)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Released Source Evidence metadata is invalid.");
                }

                if (!Guid.TryParse(entry.EvidenceId, out Guid evidenceId) ||
                    evidenceId == Guid.Empty ||
                    !seenEvidenceIds.Add(evidenceId) ||
                    string.IsNullOrWhiteSpace(entry.RelativeCustodyPath) ||
                    Path.IsPathRooted(entry.RelativeCustodyPath) ||
                    entry.FileSizeBytes < 0 ||
                    string.IsNullOrWhiteSpace(entry.Sha256Hex) ||
                    entry.Sha256Hex.Length != 64 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        entry.Sha256Hex,
                        @"\A[0-9A-Fa-f]{64}\z"))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Released Source Evidence metadata is invalid.");
                }
                string controlledFilePath =
                    Path.GetFullPath(
                        Path.Combine(
                            fullProjectRoot,
                            entry.RelativeCustodyPath));

                string expectedEvidenceDirectory =
                    Path.Combine(
                        evidenceRoot,
                        evidenceId.ToString("D"));

                if (!controlledFilePath.StartsWith(
                        evidenceRootPrefix,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        Path.GetDirectoryName(controlledFilePath),
                        expectedEvidenceDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Released Source Evidence custody path is invalid.");
                }

                try
                {
                    using FileStream stream =
                        File.OpenRead(controlledFilePath);

                    long actualSize = stream.Length;

                    string actualHash =
                        Convert.ToHexString(
                            SHA256.HashData(stream));

                    if (actualSize != entry.FileSizeBytes ||
                        !string.Equals(
                            actualHash,
                            entry.Sha256Hex,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "SAFE-STOP: Released Source Evidence integrity failed.");
                    }
                }
                catch (Exception exception) when (
                    exception is IOException or
                    UnauthorizedAccessException)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Released Source Evidence could not be verified.",
                        exception);
                }
            }
        }
    }
}