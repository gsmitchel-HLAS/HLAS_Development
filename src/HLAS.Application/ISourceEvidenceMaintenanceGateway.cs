using HLAS.Domain;
using System.Collections.Generic;
using static HLAS.Application.SourceEvidenceCorrectionChange;

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
            SourceEvidenceReplacementChange change);
    }
}