using HLAS.Domain;
using static HLAS.Application.SourceEvidenceCorrectionChange;

namespace HLAS.Application
{
    public sealed class SourceEvidenceMaintenanceService
    {
        private readonly ProjectAuthorizationService _authorizationService;
        private readonly ISourceEvidenceMaintenanceGateway _maintenanceGateway;

        public SourceEvidenceMaintenanceService(
            ProjectAuthorizationService authorizationService,
            ISourceEvidenceMaintenanceGateway maintenanceGateway)
        {
            ArgumentNullException.ThrowIfNull(authorizationService);
            ArgumentNullException.ThrowIfNull(maintenanceGateway);

            _authorizationService = authorizationService;
            _maintenanceGateway = maintenanceGateway;
        }

        public SourceEvidenceMaintenanceRecord Correct(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId,
            EvidenceId evidenceId,
            SourceEvidenceCorrectionChange change)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);
            ArgumentNullException.ThrowIfNull(change);

            VerifyVSeries(seriesId);

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            VerifyMaintenanceRole(
                authorizedIdentity.ProjectRole);

            return _maintenanceGateway.Correct(
                projectRoot,
                authorizedIdentity,
                evidenceId,
                change);
        }

        public SourceEvidenceMaintenanceRecord Replace(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId,
            EvidenceId priorEvidenceId,
            SourceEvidenceReplacementChange change)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);
            ArgumentNullException.ThrowIfNull(change);

            VerifyVSeries(seriesId);

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            VerifyMaintenanceRole(
                authorizedIdentity.ProjectRole);

            return _maintenanceGateway.Replace(
                projectRoot,
                authorizedIdentity,
                priorEvidenceId,
                change);
        }

        private static void VerifyVSeries(
            SeriesId seriesId)
        {
            if (seriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Source Evidence maintenance is permitted only in V-series.");
            }
        }

        private static void VerifyMaintenanceRole(
            ProjectRole projectRole)
        {
            if (projectRole != ProjectRole.Senior &&
                projectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Source Evidence CORRECT/REPLACE requires Senior or Admin authority.");
            }
        }
    }
}