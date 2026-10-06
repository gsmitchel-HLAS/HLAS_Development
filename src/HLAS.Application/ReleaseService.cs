using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class ReleaseService
    {
        private readonly ProjectAuthorizationService _authorizationService;
        private readonly IReadinessGateway _readinessGateway;
        private readonly IReleaseGateway _releaseGateway;

        public ReleaseService(
            ProjectAuthorizationService authorizationService,
            IReadinessGateway readinessGateway,
            IReleaseGateway releaseGateway)
        {
            ArgumentNullException.ThrowIfNull(authorizationService);
            ArgumentNullException.ThrowIfNull(readinessGateway);
            ArgumentNullException.ThrowIfNull(releaseGateway);

            _authorizationService = authorizationService;
            _readinessGateway = readinessGateway;
            _releaseGateway = releaseGateway;
        }

        public ReleaseRecord RequestRelease(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);

            if (seriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release is permitted only in V-series.");
            }

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            if (authorizedIdentity.ProjectRole != ProjectRole.Senior &&
                authorizedIdentity.ProjectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release requires Senior or Admin authority.");
            }

            ReadinessCheckResult readinessResult =
                _readinessGateway.Evaluate(
                    projectRoot,
                    authorizedIdentity);

            if (readinessResult.Check.OverallStatus != ReadinessStatus.Ready)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release requires a fresh READY result.");
            }

            return _releaseGateway.RequestRelease(
                projectRoot,
                authorizedIdentity,
                readinessResult.Check.ReadinessCheckId);
        }
    }
}