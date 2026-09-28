using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class OperationalUserSession
    {
        public AuthenticatedIdentity AuthenticatedIdentity { get; }

        private OperationalUserSession(
            AuthenticatedIdentity authenticatedIdentity)
        {
            AuthenticatedIdentity = authenticatedIdentity;
        }

        public static OperationalUserSession Authenticate(
            string loginName,
            string secret)
        {
            AuthenticationService authenticationService =
                new(new LocalAuthenticationGatewayAdapter());

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate(
                    loginName,
                    secret);

            return new OperationalUserSession(
                authenticatedIdentity);
        }

        public static OperationalUserSession BootstrapInitialIdentity(
            string loginName,
            string secret)
        {
            if (!LocalIdentityStore.IsInitialBootstrapRequired())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Initial HLAS identity bootstrap is not available.");
            }

            _ = LocalIdentityStore.BootstrapInitialIdentity(
                loginName,
                secret);

            return Authenticate(
                loginName,
                secret);
        }
    }
}