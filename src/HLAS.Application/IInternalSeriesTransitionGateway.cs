using HLAS.Domain;

namespace HLAS.Application
{
    public interface IInternalSeriesTransitionGateway
    {
        InternalSeriesTransitionRecord CreateTransition(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            SeriesId receivingSeriesId,
            ReleaseId releaseId);
    }
}