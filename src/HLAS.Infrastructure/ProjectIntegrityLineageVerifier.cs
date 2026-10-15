using System;
using HLAS.Domain;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityLineageVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            _ = ProjectPackageReader.Open(projectRoot);

            ReadinessCheckItemRecord result =
                LineageReadinessEvaluator.Evaluate(
                    projectRoot,
                    ReadinessCheckId.CreateNew());

            if (result.GateStatus == ReadinessGateStatus.Pass)
            {
                return new ProjectIntegrityCheckItem(
                    "LINEAGE_STRUCTURE",
                    ProjectIntegrityCheckStatus.Verified,
                    "Governed lineage structural checks passed. This is not a Readiness determination.");
            }

            if (result.GateStatus == ReadinessGateStatus.Blocked)
            {
                return new ProjectIntegrityCheckItem(
                    "LINEAGE_STRUCTURE",
                    ProjectIntegrityCheckStatus.IntegrityFailure,
                    result.Detail ??
                    "Governed lineage is structurally inconsistent.");
            }

            throw new InvalidOperationException(
                "SAFE-STOP: Lineage verification returned an unsupported outcome.");
        }
    }
}