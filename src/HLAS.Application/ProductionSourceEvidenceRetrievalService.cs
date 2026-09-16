using HLAS.Domain;

namespace HLAS.Application
{
    public sealed class ProductionSourceEvidenceRetrievalService
    {
        private readonly IEvidenceCustodyGateway _evidenceCustodyGateway;

        public ProductionSourceEvidenceRetrievalService(
            IEvidenceCustodyGateway evidenceCustodyGateway)
        {
            ArgumentNullException.ThrowIfNull(evidenceCustodyGateway);

            _evidenceCustodyGateway = evidenceCustodyGateway;
        }

        public EvidenceCustodyRetrievalResult Retrieve(
            string projectRoot,
            ProductionSourceEvidenceRetrievalRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(request);

            if (request.SeriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "Production Source Evidence retrieval belongs to V-series.");
            }

            return _evidenceCustodyGateway.Retrieve(
                projectRoot,
                request.EvidenceId);
        }
    }
}