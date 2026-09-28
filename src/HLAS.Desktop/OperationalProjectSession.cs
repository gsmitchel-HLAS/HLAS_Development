using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class OperationalProjectSession
    {
        public string ProjectRoot { get; }
        public ProjectManifest Manifest { get; }
        public AuthenticatedIdentity AuthenticatedIdentity { get; }
        public AuthorizedProjectIdentity AuthorizedProjectIdentity { get; }
        public ShellContext Context { get; }

        private OperationalProjectSession(
            string projectRoot,
            ProjectManifest manifest,
            AuthenticatedIdentity authenticatedIdentity,
            AuthorizedProjectIdentity authorizedProjectIdentity,
            ShellContext context)
        {
            ProjectRoot = projectRoot;
            Manifest = manifest;
            AuthenticatedIdentity = authenticatedIdentity;
            AuthorizedProjectIdentity = authorizedProjectIdentity;
            Context = context;
        }
        public static OperationalProjectSession CreateNew(
    string projectRoot,
    AuthenticatedIdentity authenticatedIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                projectRoot);

            ArgumentNullException.ThrowIfNull(
                authenticatedIdentity);

            ProjectManifest manifest =
                ProjectPackageCreator.CreateNew(
                    projectRoot,
                    authenticatedIdentity.UserId);

            ProjectAuthorizationService authorizationService =
                new(new ProjectAuthorizationGatewayAdapter());

            AuthorizedProjectIdentity authorizedProjectIdentity =
                authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            ShellContext context =
                new(
                    manifest.ProjectId,
                    authorizedProjectIdentity.UserId,
                    authorizedProjectIdentity.ProjectRole,
                    SeriesId.V);

            return new OperationalProjectSession(
                projectRoot,
                manifest,
                authenticatedIdentity,
                authorizedProjectIdentity,
                context);
        }
        public static OperationalProjectSession OpenExisting(
     string projectRoot,
     AuthenticatedIdentity authenticatedIdentity)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                projectRoot);

            ArgumentNullException.ThrowIfNull(
                authenticatedIdentity);

            ProjectAuthorizationService authorizationService =
                new(new ProjectAuthorizationGatewayAdapter());

            AuthorizedProjectIdentity authorizedProjectIdentity =
                authorizationService.Authorize(
                    projectRoot,
                    authenticatedIdentity);

            ProjectManifest manifest =
                ProjectPackageReader.Open(
                    projectRoot);

            ShellContext context =
                new(
                    manifest.ProjectId,
                    authorizedProjectIdentity.UserId,
                    authorizedProjectIdentity.ProjectRole,
                    SeriesId.V);

            return new OperationalProjectSession(
                projectRoot,
                manifest,
                authenticatedIdentity,
                authorizedProjectIdentity,
                context);
        }
    }
}