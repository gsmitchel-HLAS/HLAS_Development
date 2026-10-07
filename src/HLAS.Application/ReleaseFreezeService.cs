using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class ReleaseFreezeService
    {
        private readonly ProjectAuthorizationService _authorizationService;
        private readonly IReleaseFreezeGateway _releaseFreezeGateway;

        public ReleaseFreezeService(
            ProjectAuthorizationService authorizationService,
            IReleaseFreezeGateway releaseFreezeGateway)
        {
            ArgumentNullException.ThrowIfNull(authorizationService);
            ArgumentNullException.ThrowIfNull(releaseFreezeGateway);

            _authorizationService = authorizationService;
            _releaseFreezeGateway = releaseFreezeGateway;
        }

        public HistoricalCheckpointRecord CreateReleaseFreeze(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId,
            ReleaseId releaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);

            if (seriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze is permitted only in V-series.");
            }

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            if (authorizedIdentity.ProjectRole != ProjectRole.Senior &&
                authorizedIdentity.ProjectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze requires Senior or Admin authority.");
            }

            return _releaseFreezeGateway.CreateReleaseFreeze(
                projectRoot,
                authorizedIdentity,
                releaseId);
        }
    }
}