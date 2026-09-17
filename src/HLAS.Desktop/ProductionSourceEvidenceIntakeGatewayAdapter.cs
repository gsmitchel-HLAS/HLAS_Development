using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class ProductionSourceEvidenceIntakeGatewayAdapter
        : IProductionSourceEvidenceIntakeGateway
    {
        public EvidenceCustodyRecord Accept(
            string projectRoot,
            string sourceFilePath)
        {
            return ProductionSourceEvidenceIntakeGateway.Accept(
                projectRoot,
                sourceFilePath);
        }
    }
}