using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class ReadinessGatewayAdapter
        : IReadinessGateway
    {
        public ReadinessCheckResult Evaluate(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity)
        {
            return ReadinessGateway.Evaluate(
                projectRoot,
                authorizedIdentity.UserId,
                authorizedIdentity.ProjectRole);
        }
    }
}