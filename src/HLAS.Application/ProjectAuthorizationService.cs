using HLAS.Domain;

namespace HLAS.Application
{
    public interface IProjectAuthorizationGateway
    {
        ProjectAuthorizationGatewayResult Resolve(
            string projectRoot,
            UserId userId);
    }

    public sealed record ProjectAuthorizationGatewayResult
    {
        public bool IsAuthorized { get; }
        public ProjectRole? ProjectRole { get; }
        public string? FailureReason { get; }

        private ProjectAuthorizationGatewayResult(
            bool isAuthorized,
            ProjectRole? projectRole,
            string? failureReason)
        {
            IsAuthorized = isAuthorized;
            ProjectRole = projectRole;
            FailureReason = failureReason;
        }

        public static ProjectAuthorizationGatewayResult Authorized(
            ProjectRole projectRole)
        {
            return new ProjectAuthorizationGatewayResult(
                true,
                projectRole,
                null);
        }

        public static ProjectAuthorizationGatewayResult Rejected(
            string failureReason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

            return new ProjectAuthorizationGatewayResult(
                false,
                null,
                failureReason);
        }
    }

    public sealed record AuthorizedProjectIdentity
    {
        public UserId UserId { get; }
        public ProjectRole ProjectRole { get; }

        private AuthorizedProjectIdentity(
            UserId userId,
            ProjectRole projectRole)
        {
            UserId = userId;
            ProjectRole = projectRole;
        }

        internal static AuthorizedProjectIdentity Create(
            UserId userId,
            ProjectRole projectRole)
        {
            return new AuthorizedProjectIdentity(
                userId,
                projectRole);
        }
    }

    public sealed class ProjectAuthorizationService
    {
        private readonly IProjectAuthorizationGateway _gateway;

        public ProjectAuthorizationService(
            IProjectAuthorizationGateway gateway)
        {
            ArgumentNullException.ThrowIfNull(gateway);
            _gateway = gateway;
        }

        public AuthorizedProjectIdentity Authorize(
            string projectRoot,
            AuthenticatedIdentity authenticatedIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(authenticatedIdentity);

            ProjectAuthorizationGatewayResult result =
                _gateway.Resolve(
                    projectRoot,
                    authenticatedIdentity.UserId);

            if (!result.IsAuthorized ||
                result.ProjectRole is null)
            {
                string reason =
                    string.IsNullOrWhiteSpace(result.FailureReason)
                        ? "Project did not authorize the authenticated HLAS identity."
                        : result.FailureReason;

                throw new InvalidOperationException(
                    $"SAFE-STOP: {reason}");
            }

            return AuthorizedProjectIdentity.Create(
                authenticatedIdentity.UserId,
                result.ProjectRole.Value);
        }
    }
}