using HLAS.Domain;

namespace HLAS.Application
{
    public interface IDevelopmentalProjectHistoryProofGateway
    {
        DevelopmentalProjectHistoryProofResult Execute(
            string projectRoot,
            UserId userId,
            SeriesId seriesId,
            ProjectRole projectRole,
            DecisionRecord decisionRecord);
    }

    public sealed record DevelopmentalProjectHistoryProofResult(
        ProjectId ProjectId,
        OperationId OperationId,
        string ProjectRoot);

    public sealed class DevelopmentalProjectHistoryProofService
    {
        private readonly IDevelopmentalProjectHistoryProofGateway _gateway;

        public DevelopmentalProjectHistoryProofService(
            IDevelopmentalProjectHistoryProofGateway gateway)
        {
            ArgumentNullException.ThrowIfNull(gateway);
            _gateway = gateway;
        }

        public DevelopmentalProjectHistoryProofResult Execute(
            string projectRoot,
            UserId userId,
            SeriesId seriesId,
            ProjectRole projectRole)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            DecisionRecord decisionRecord = new(
                "DEVELOPMENTAL PROJECT HISTORY PROOF",
                "Harmless governed project-history transaction used to prove Build Foundation 0.1E-A and 0.1G-B.");

            return _gateway.Execute(
                projectRoot,
                userId,
                seriesId,
                projectRole,
                decisionRecord);
        }
    }
}