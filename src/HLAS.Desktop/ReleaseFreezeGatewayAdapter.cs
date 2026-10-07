using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class ReleaseFreezeGatewayAdapter
        : IReleaseFreezeGateway
    {
        public HistoricalCheckpointRecord CreateReleaseFreeze(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            ReleaseId releaseId)
        {
            return ReleaseFreezeGateway.CreateReleaseFreeze(
                projectRoot,
                authorizedIdentity.UserId,
                authorizedIdentity.ProjectRole,
                releaseId);
        }
    }
}