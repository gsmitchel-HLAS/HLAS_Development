using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class ProductionSourceEvidenceIntakeService
    {
        private readonly IProductionSourceEvidenceIntakeGateway
            _productionIntakeGateway;

        public ProductionSourceEvidenceIntakeService(
            IProductionSourceEvidenceIntakeGateway productionIntakeGateway)
        {
            ArgumentNullException.ThrowIfNull(productionIntakeGateway);

            _productionIntakeGateway =
                productionIntakeGateway;
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
                _productionIntakeGateway.Accept(
                    projectRoot,
                    request.SelectedSourceFilePath);

            return new ProductionSourceEvidenceIntakeResult(
                request.ProjectId,
                evidenceRecord,
                "Developmental Production Source Evidence custody completed.");
        }
    }
}