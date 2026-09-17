using System.IO;
using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class DevelopmentalProjectHistoryProofGatewayAdapter
        : IDevelopmentalProjectHistoryProofGateway
    {
        public DevelopmentalProjectHistoryProofResult Execute(
            string projectRoot,
            UserId userId,
            SeriesId seriesId,
            ProjectRole projectRole,
            DecisionRecord decisionRecord)
        {
            ProjectManifest manifest;

            if (Directory.Exists(projectRoot) &&
                Directory.GetFileSystemEntries(projectRoot).Length > 0)
            {
                manifest =
                    ProjectPackageReader.Open(projectRoot);
            }
            else
            {
                manifest =
                    ProjectPackageCreator.CreateNew(projectRoot);
            }

            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    projectRoot,
                    manifest.ProjectId,
                    userId,
                    seriesId,
                    projectRole);

            GovernedOperationService.FinalizeSuccess(
                projectRoot,
                operation.OperationId,
                decisionRecord);

            return new DevelopmentalProjectHistoryProofResult(
                manifest.ProjectId,
                operation.OperationId,
                projectRoot);
        }
    }
}