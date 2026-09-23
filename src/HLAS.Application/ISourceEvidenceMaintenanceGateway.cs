using System.Collections.Generic;
using HLAS.Domain;

namespace HLAS.Application
{
    public interface ISourceEvidenceMaintenanceGateway
    {
        SourceEvidenceMaintenanceRecord Correct(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            EvidenceId evidenceId,
            SourceEvidenceCorrectionChange change);

        SourceEvidenceMaintenanceRecord Replace(
            string projectRoot,
            AuthorizedProjectIdentity authorizedIdentity,
            EvidenceId priorEvidenceId,
            string selectedReplacementSourceFilePath);
    }
}