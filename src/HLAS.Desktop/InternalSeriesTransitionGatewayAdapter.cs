using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class InternalSeriesTransitionGatewayAdapter
        : IInternalSeriesTransitionGateway
    {
        public InternalSeriesTransitionRecord CreateTransition(
            string projectRoot,
            InternalSeriesTransitionAuthority transitionAuthority,
            SeriesId receivingSeriesId,
            ReleaseId releaseId)
        {
            return InternalSeriesTransitionGateway.CreateTransition(
                projectRoot,
                transitionAuthority,
                receivingSeriesId,
                releaseId);
        }
    }
}