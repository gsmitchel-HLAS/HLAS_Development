using HLAS.Domain;

namespace HLAS.Application
{
    public interface IReleaseFreezeGateway
    {
        HistoricalCheckpointRecord CreateReleaseFreeze(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            ReleaseId releaseId);
    }
}