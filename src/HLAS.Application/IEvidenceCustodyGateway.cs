using HLAS.Domain;

namespace HLAS.Application
{
    public interface IEvidenceCustodyGateway
    {
        EvidenceCustodyRecord Accept(
            string projectRoot,
            string sourceFilePath);
    }
}