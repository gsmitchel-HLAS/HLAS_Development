using HLAS.Domain;

namespace HLAS.Infrastructure
{
    internal sealed record InternalSeriesTransitionBasisReference(
        ProjectId ProjectId,
        ReleaseId ReleaseId,
        FreezeId ReleaseFreezeId,
        ReadinessCheckId ReadinessCheckId,
        OperationId ReleaseFreezeOperationId,
        GovernedTimestamp FrozenUtc,
        string RelativeCheckpointPath,
        string ManifestRelativePath,
        string ManifestSha256Hex);
}