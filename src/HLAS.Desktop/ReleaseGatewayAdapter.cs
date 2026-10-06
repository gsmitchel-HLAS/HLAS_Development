using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class ReleaseGatewayAdapter
        : IReleaseGateway
    {
        public ReleaseRecord RequestRelease(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            ReadinessCheckId readinessCheckId)
        {
            return ReleaseGateway.RequestRelease(
                projectRoot,
                authorizedIdentity.UserId,
                authorizedIdentity.ProjectRole,
                readinessCheckId);
        }
    }
}