using System.IO;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class DevelopmentalProjectSession : IDisposable
    {
        public string SessionRoot { get; }
        public string ProjectRoot { get; }
        public ProjectManifest Manifest { get; }

        private DevelopmentalProjectSession(
            string sessionRoot,
            string projectRoot,
            ProjectManifest manifest)
        {
            SessionRoot = sessionRoot;
            ProjectRoot = projectRoot;
            Manifest = manifest;
        }

        public static DevelopmentalProjectSession Create()
        {
            string sessionRoot = Path.Combine(
                Path.GetTempPath(),
                "HLAS_Desktop_Development",
                Guid.NewGuid().ToString("N"));

            string projectRoot = Path.Combine(
                sessionRoot,
                "Project");

            ProjectPackageCreator.CreateNew(projectRoot);

            ProjectManifest manifest =
                ProjectPackageReader.Open(projectRoot);

            return new DevelopmentalProjectSession(
                sessionRoot,
                projectRoot,
                manifest);
        }

        public void Dispose()
        {
            DevelopmentalSessionCleanup.Delete(
                SessionRoot);

        }
    }
}