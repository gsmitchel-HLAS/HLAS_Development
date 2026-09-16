using HLAS.Domain;

namespace HLAS.Application
{
    public sealed record EvidenceCustodyRetrievalResult(
        EvidenceCustodyRecord EvidenceRecord,
        string ControlledFilePath);
}