using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionManifestReader
    {
        internal static ReleaseFreezeManifest Read(
            string projectRoot,
            InternalSeriesTransitionBasisReference basis)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(basis);

            string expectedCheckpointPath =
                Path.Combine(
                    PreChangeFreezeService.FrozenStatesDirectoryName,
                    basis.ReleaseFreezeId.Value.ToString("D"));

            string expectedManifestPath =
                Path.Combine(
                    expectedCheckpointPath,
                    "release-freeze-manifest.json");

            if (!string.Equals(
                    basis.RelativeCheckpointPath,
                    expectedCheckpointPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    basis.ManifestRelativePath,
                    expectedManifestPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze checkpoint paths are inconsistent.");
            }

            string manifestPath =
                Path.Combine(
                    Path.GetFullPath(projectRoot),
                    expectedManifestPath);

            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze manifest is missing.");
            }

            try
            {
                using FileStream stream =
                    File.OpenRead(manifestPath);

                string actualHash =
                    Convert.ToHexString(
                        SHA256.HashData(stream));

                if (!string.Equals(
                        actualHash,
                        basis.ManifestSha256Hex,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Release Freeze manifest integrity failed.");
                }

                ReleaseFreezeManifest? manifest =
                    JsonSerializer.Deserialize<ReleaseFreezeManifest>(
                        File.ReadAllText(manifestPath));

                if (manifest is null ||
     !string.Equals(
         manifest.CheckpointType,
         "RELEASE_FREEZE",
         StringComparison.Ordinal) ||
     !string.Equals(
         manifest.ProjectId,
         basis.ProjectId.Value.ToString("D"),
         StringComparison.OrdinalIgnoreCase) ||
     !string.Equals(
         manifest.ReleaseId,
         basis.ReleaseId.Value.ToString("D"),
         StringComparison.OrdinalIgnoreCase) ||
     !string.Equals(
         manifest.FreezeId,
         basis.ReleaseFreezeId.Value.ToString("D"),
         StringComparison.OrdinalIgnoreCase) ||
     !string.Equals(
         manifest.ReadinessCheckId,
         basis.ReadinessCheckId.Value.ToString("D"),
         StringComparison.OrdinalIgnoreCase) ||
     !string.Equals(
         manifest.OperationId,
         basis.ReleaseFreezeOperationId.Value.ToString("D"),
         StringComparison.OrdinalIgnoreCase) ||
     !DateTimeOffset.TryParse(
         manifest.FrozenUtc,
         out DateTimeOffset actualFrozenUtc) ||
     actualFrozenUtc != basis.FrozenUtc.Value ||
     manifest.Evidence is null)
                {
                    throw new InvalidOperationException(
                       "SAFE-STOP: Release Freeze manifest identity is inconsistent.");
                }

                return manifest;

            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                JsonException)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze manifest could not be verified.",
                    exception);
            }
        }
    }
}