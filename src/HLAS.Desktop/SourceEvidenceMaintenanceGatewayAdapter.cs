using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;
using static HLAS.Application.SourceEvidenceCorrectionChange;

namespace HLAS.Desktop
{
    internal sealed class SourceEvidenceMaintenanceGatewayAdapter
        : ISourceEvidenceMaintenanceGateway
    {
        public SourceEvidenceMaintenanceRecord Correct(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            EvidenceId evidenceId,
            SourceEvidenceCorrectionChange change)
        {
            SourceEvidenceCorrectionRequest infrastructureRequest =
                new(
                    Translate(change.DisplayLabel),
                    Translate(change.AdministrativeDescription),
                    change.CorrectionReason);

            return SourceEvidenceMaintenanceGateway.Correct(
                projectRoot,
                authorizedIdentity.UserId,
                authorizedIdentity.ProjectRole,
                evidenceId,
                infrastructureRequest);
        }

        public SourceEvidenceMaintenanceRecord Replace(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            EvidenceId priorEvidenceId,
            SourceEvidenceReplacementChange change)
        {
            SourceEvidenceReplacementRequest infrastructureRequest =
                new(
                    change.SelectedReplacementSourceFilePath,
                    Translate(change.DisplayLabel),
                    Translate(change.AdministrativeDescription),
                    change.ReplacementReason);

            return SourceEvidenceMaintenanceGateway.Replace(
                projectRoot,
                authorizedIdentity.UserId,
                authorizedIdentity.ProjectRole,
                priorEvidenceId,
                infrastructureRequest);
        }

        private static SourceEvidenceMetadataMutation Translate(
            SourceEvidenceMetadataFieldChange change)
        {
            return change.Action switch
            {
                SourceEvidenceMetadataChangeAction.Keep =>
                    SourceEvidenceMetadataMutation.Keep(),

                SourceEvidenceMetadataChangeAction.Set =>
                    SourceEvidenceMetadataMutation.Set(
                        change.Value!),

                SourceEvidenceMetadataChangeAction.Clear =>
                    SourceEvidenceMetadataMutation.Clear(),

                _ => throw new InvalidOperationException(
                    "SAFE-STOP: Source Evidence metadata change action is invalid.")
            };
        }
    }
}