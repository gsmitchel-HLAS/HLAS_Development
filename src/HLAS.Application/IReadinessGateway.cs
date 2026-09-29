using HLAS.Domain;

namespace HLAS.Application
{
    public interface IReadinessGateway
    {
        ReadinessCheckResult Evaluate(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity);
    }
}