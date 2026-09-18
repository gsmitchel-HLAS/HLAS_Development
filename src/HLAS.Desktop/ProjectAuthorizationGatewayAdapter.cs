using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class ProjectAuthorizationGatewayAdapter
        : IProjectAuthorizationGateway
    {
        public ProjectAuthorizationGatewayResult Resolve(
            string projectRoot,
            UserId userId)
        {
            ProjectRole? role =
                ProjectAuthorizationStore.ResolveRole(
                    projectRoot,
                    userId);

            return role is null
                ? ProjectAuthorizationGatewayResult.Rejected(
                    "Authenticated HLAS identity is not authorized for this project.")
                : ProjectAuthorizationGatewayResult.Authorized(
                    role.Value);
        }
    }
}