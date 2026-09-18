using HLAS.Domain;

namespace HLAS.Application
{
    public interface IAuthenticationGateway
    {
        AuthenticationGatewayResult Authenticate();
    }

    public sealed record AuthenticationGatewayResult
    {
        public bool IsAuthenticated { get; }
        public UserId? UserId { get; }
        public string? FailureReason { get; }

        private AuthenticationGatewayResult(
            bool isAuthenticated,
            UserId? userId,
            string? failureReason)
        {
            IsAuthenticated = isAuthenticated;
            UserId = userId;
            FailureReason = failureReason;
        }

        public static AuthenticationGatewayResult Succeeded(UserId userId)
        {
            return new AuthenticationGatewayResult(true, userId, null);
        }

        public static AuthenticationGatewayResult Rejected(string failureReason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

            return new AuthenticationGatewayResult(
                false,
                null,
                failureReason);
        }
    }

    public sealed class AuthenticationService
    {
        private readonly IAuthenticationGateway _gateway;

        public AuthenticationService(IAuthenticationGateway gateway)
        {
            ArgumentNullException.ThrowIfNull(gateway);
            _gateway = gateway;
        }

        public AuthenticatedIdentity Authenticate()
        {
            AuthenticationGatewayResult result = _gateway.Authenticate();

            if (!result.IsAuthenticated || result.UserId is null)
            {
                string reason =
                    string.IsNullOrWhiteSpace(result.FailureReason)
                        ? "Authentication authority did not establish an authenticated HLAS identity."
                        : result.FailureReason;

                throw new InvalidOperationException(
                    $"SAFE-STOP: {reason}");
            }

            if (result.UserId.Value.Value == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Authentication authority returned an empty UserId.");
            }

            return AuthenticatedIdentity.Create(result.UserId.Value);
        }
    }
}