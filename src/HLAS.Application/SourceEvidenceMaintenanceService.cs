using System.Collections.Generic;
using HLAS.Domain;

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
            IReadOnlyList<SourceEvidenceCorrectionChange> changes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);
            ArgumentNullException.ThrowIfNull(changes);

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
                changes);
        }

        public SourceEvidenceMaintenanceRecord Replace(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId,
            EvidenceId priorEvidenceId,
            string selectedReplacementSourceFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);
            ArgumentException.ThrowIfNullOrWhiteSpace(
                selectedReplacementSourceFilePath);

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
                selectedReplacementSourceFilePath);
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