using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class ProductionSourceEvidenceIntakeService
    {
        private readonly IEvidenceCustodyGateway _evidenceCustodyGateway;

        public ProductionSourceEvidenceIntakeService(
            IEvidenceCustodyGateway evidenceCustodyGateway)
        {
            ArgumentNullException.ThrowIfNull(evidenceCustodyGateway);

            _evidenceCustodyGateway = evidenceCustodyGateway;
        }

        public ProductionSourceEvidenceIntakeResult Intake(
            string projectRoot,
            ProductionSourceEvidenceIntakeRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(request);

            if (request.SeriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "Production Source Evidence intake belongs to V-series.");
            }

            EvidenceCustodyRecord evidenceRecord =
                _evidenceCustodyGateway.Accept(
                    projectRoot,
                    request.SelectedSourceFilePath);

            return new ProductionSourceEvidenceIntakeResult(
                request.ProjectId,
                evidenceRecord,
                "Developmental Production Source Evidence custody completed.");
        }
    }
}