using HLAS.Domain;

namespace HLAS.Application
{
    public interface IProductionSourceEvidenceIntakeGateway
    {
        EvidenceCustodyRecord Accept(
            string projectRoot,
            string sourceFilePath);
    }
}