using HLAS.Domain;

namespace HLAS.Application
{
    public interface IReleaseGateway
    {
        ReleaseRecord RequestRelease(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            ReadinessCheckId readinessCheckId);
    }
}