using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class InternalSeriesTransitionService
    {
        private readonly ProjectAuthorizationService _authorizationService;
        private readonly IInternalSeriesTransitionGateway _transitionGateway;

        public InternalSeriesTransitionService(
            ProjectAuthorizationService authorizationService,
            IInternalSeriesTransitionGateway transitionGateway)
        {
            ArgumentNullException.ThrowIfNull(authorizationService);
            ArgumentNullException.ThrowIfNull(transitionGateway);

            _authorizationService = authorizationService;
            _transitionGateway = transitionGateway;
        }

        public InternalSeriesTransitionRecord CreateTransition(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity,
            SeriesId receivingSeriesId,
            ReleaseId releaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);

            if (string.IsNullOrWhiteSpace(receivingSeriesId.Value) ||
                receivingSeriesId == SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Receiving series must be a valid non-V series.");
            }

            if (releaseId.Value == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: A valid Release ID is required.");
            }

            AuthorizedProjectIdentity authorizedIdentity =
                _authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            if (authorizedIdentity.ProjectRole != ProjectRole.Senior &&
                authorizedIdentity.ProjectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Internal series transition requires Senior or Admin authority.");
            }
            InternalSeriesTransitionAuthority transitionAuthority =
                InternalSeriesTransitionAuthority.Create(
                    projectRoot,
                    authorizedIdentity.UserId,
                    authorizedIdentity.ProjectRole);
            return _transitionGateway.CreateTransition(
                projectRoot,
               transitionAuthority,
                receivingSeriesId,
                releaseId);
        }
    }
}