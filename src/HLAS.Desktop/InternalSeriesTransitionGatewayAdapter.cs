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
            AuthorizedProjectIdentity authorizedIdentity,
            SeriesId receivingSeriesId,
            ReleaseId releaseId)
        {
            return InternalSeriesTransitionGateway.CreateTransition(
                projectRoot,
                authorizedIdentity.UserId,
                authorizedIdentity.ProjectRole,
                receivingSeriesId,
                releaseId);
        }
    }
}