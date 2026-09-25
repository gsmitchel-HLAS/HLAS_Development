using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class LocalAuthenticationGatewayAdapter
        : IAuthenticationGateway
    {
        public AuthenticationGatewayResult Authenticate(
            string loginName,
            string secret)
        {
            UserId? userId =
                LocalIdentityStore.Authenticate(
                    loginName,
                    secret);

            return userId is null
                ? AuthenticationGatewayResult.Rejected(
                    "HLAS authentication was rejected.")
                : AuthenticationGatewayResult.Succeeded(
                    userId.Value);
        }
    }
}