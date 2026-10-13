using HLAS.Domain;

namespace HLAS.Application
{
    public interface IInternalSeriesTransitionGateway
    {
        InternalSeriesTransitionRecord CreateTransition(
            string projectRoot,
            InternalSeriesTransitionAuthority transitionAuthority,
            SeriesId receivingSeriesId,
            ReleaseId releaseId);
    }
}