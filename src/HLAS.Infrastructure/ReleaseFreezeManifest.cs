using System;
using System.Collections.Generic;

namespace HLAS.Infrastructure
{
    internal sealed record ReleaseFreezeManifest(
        string FreezeId,
        string CheckpointType,
        string ProjectId,
        string ReleaseId,
        string ReadinessCheckId,
        string OperationId,
        string FrozenUtc,
        string DatabaseFileName,
        long DatabaseFileSizeBytes,
        string DatabaseSha256Hex,
        string ProjectManifestFileName,
        long ProjectManifestFileSizeBytes,
        string ProjectManifestSha256Hex,
        IReadOnlyList<ReleaseFreezeManifestEvidenceEntry> Evidence);

    internal sealed record ReleaseFreezeManifestEvidenceEntry(
        string EvidenceId,
        string RelativeCustodyPath,
        long FileSizeBytes,
        string Sha256Hex);
}