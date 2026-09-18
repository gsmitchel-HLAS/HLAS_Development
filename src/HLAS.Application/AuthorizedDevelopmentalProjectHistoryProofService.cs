using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class AuthorizedDevelopmentalProjectHistoryProofService
    {
        private readonly ProjectAuthorizationService _authorizationService;
        private readonly DevelopmentalProjectHistoryProofService _historyService;

        public AuthorizedDevelopmentalProjectHistoryProofService(
            ProjectAuthorizationService authorizationService,
            DevelopmentalProjectHistoryProofService historyService)
        {
            ArgumentNullException.ThrowIfNull(authorizationService);
            ArgumentNullException.ThrowIfNull(historyService);

            _authorizationService = authorizationService;
            _historyService = historyService;
        }

        public DevelopmentalProjectHistoryProofResult Execute(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId seriesId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);

            if (seriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Developmental project-history proof is permitted only in V-series.");
            }

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            ProjectRole projectRole =
                authorizedIdentity.ProjectRole;

            if (projectRole != ProjectRole.Technician &&
                projectRole != ProjectRole.Senior &&
                projectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Effective project role is not recognized.");
            }

            return _historyService.Execute(
    projectRoot,
    authorizedIdentity,
    seriesId);
        }
    }
}