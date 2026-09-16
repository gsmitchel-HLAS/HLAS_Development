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
        public EvidenceCustodyRetrievalResult Retrieve(
    string projectRoot,
    EvidenceId evidenceId)
        {
            EvidenceCustodyRetrievedFile retrieved =
                EvidenceCustodyRetrievalService.Retrieve(
                    projectRoot,
                    evidenceId);

            return new EvidenceCustodyRetrievalResult(
                retrieved.EvidenceRecord,
                retrieved.ControlledFilePath);
        }
    }
}