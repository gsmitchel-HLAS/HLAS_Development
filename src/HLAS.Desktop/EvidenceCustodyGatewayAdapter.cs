using HLAS.Application;
using HLAS.Domain;
using HLAS.Infrastructure;

namespace HLAS.Desktop
{
    internal sealed class EvidenceCustodyGatewayAdapter
        : IEvidenceCustodyGateway
    {
        public EvidenceCustodyRecord Accept(
            string projectRoot,
            string sourceFilePath)
        {
            return EvidenceCustodyService.Accept(
                projectRoot,
                sourceFilePath);
        }
    }
}