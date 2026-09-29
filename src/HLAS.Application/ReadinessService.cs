using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class ReadinessService
    {
        private readonly ProjectAuthorizationService _authorizationService;
        private readonly IReadinessGateway _readinessGateway;

        public ReadinessService(
            ProjectAuthorizationService authorizationService,
            IReadinessGateway readinessGateway)
        {
            ArgumentNullException.ThrowIfNull(authorizationService);
            ArgumentNullException.ThrowIfNull(readinessGateway);

            _authorizationService = authorizationService;
            _readinessGateway = readinessGateway;
        }

        public ReadinessCheckResult Evaluate(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);

            if (seriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Readiness evaluation is permitted only in V-series.");
            }

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            return _readinessGateway.Evaluate(
                projectRoot,
                authorizedIdentity);
        }
    }
}