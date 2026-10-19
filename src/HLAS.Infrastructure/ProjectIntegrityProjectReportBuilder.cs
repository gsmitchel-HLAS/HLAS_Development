using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityProjectReportBuilder
    {
        internal static ProjectIntegrityCheckReport Build(
    string projectRoot,
    ProjectId expectedProjectId)
        {
            return BuildCore(projectRoot, expectedProjectId, null);
        }

        internal static ProjectIntegrityCheckReport BuildWithTestObserver(
            string projectRoot,
            ProjectId expectedProjectId,
            Action<string> afterCheck)
        {
            ArgumentNullException.ThrowIfNull(afterCheck);
            return BuildCore(projectRoot, expectedProjectId, afterCheck);
        }

        private static ProjectIntegrityCheckReport BuildCore(
            string projectRoot,
            ProjectId expectedProjectId,
            Action<string>? afterCheck)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            if (expectedProjectId.Value == Guid.Empty)
                throw new ArgumentException(
                    "Expected ProjectId may not be empty.",
                    nameof(expectedProjectId));

            List<ProjectIntegrityCheckItem> items = new();

            ProjectIntegrityCheckItem identity = Run(
                "PROJECT_IDENTITY",
                () => ProjectIntegrityProjectIdentityVerifier.Check(
                    projectRoot, expectedProjectId));

            items.Add(identity);

            List<(string Code, Func<ProjectIntegrityCheckItem> Verify)>
                checks = new()
                {
                    ("SOURCE_EVIDENCE_REQUIREMENTS",
                        () => ProjectIntegritySourceEvidenceRequirementsVerifier.Check(projectRoot)),
                    ("SOURCE_EVIDENCE_CUSTODY",
                        () => ProjectIntegrityRegisteredSourceEvidenceVerifier.Check(projectRoot)),
                    ("LINEAGE_STRUCTURE",
                        () => ProjectIntegrityLineageVerifier.Check(projectRoot)),
                    ("GOVERNED_OPERATIONS",
                        () => ProjectIntegrityGovernedOperationsVerifier.Check(projectRoot)),
                    ("PROJECT_JMF_TRUTH",
                        () => ProjectIntegrityProjectJmfVerifier.Check(projectRoot)),
                    ("PROJECT_JMF_REVIEW",
                        () => ProjectIntegrityProjectJmfReviewVerifier.Check(projectRoot)),
                   ("SOURCE_EVIDENCE_MAINTENANCE",
                        () => ProjectIntegrityMaintenanceVerifier.Check(projectRoot)),
                    ("PRE_CHANGE_FREEZE",
                        () => ProjectIntegrityPreChangeFreezeVerifier.Check(projectRoot)),
                    ("RELEASE_FREEZE",
                        () => ProjectIntegrityReleaseFreezeVerifier.Check(projectRoot)),
                    ("INTERNAL_SERIES_RECEIPTS",
                        () => ProjectIntegrityInternalReceiptVerifier.Check(projectRoot)),
                    ("HISTORICAL_CHECKPOINT_COVERAGE",
                        () => ProjectIntegrityHistoricalCheckpointCoverageVerifier.Check(projectRoot))
                };

            foreach (var check in checks)
            {
                if (identity.Status != ProjectIntegrityCheckStatus.Verified)
                {
                    items.Add(NotChecked(
                        check.Code,
                        "Project identity verification did not pass. This check was not attempted."));
                }
                else
                {
                    items.Add(Run(check.Code, check.Verify));
                    afterCheck?.Invoke(check.Code);
                }
            }

            items.Add(NotChecked(
                "HISTORICAL_REQUIREMENT_APPLICABILITY",
                "Full historical requirement-item applicability and Data-As-Of reconstruction have not been independently verified. Structural revision lineage is checked separately."));

            items.Add(NotChecked(
                "BACKUP_RESTORE_COVERAGE",
                "Future backup/restore integrity specifics remain outside this bounded verification foundation."));

            items.Add(NotChecked(
                "PROJECT_WIDE_SNAPSHOT_CONSISTENCY",
                "Independent read-only checks are not yet proven to observe one transactionally consistent project-wide snapshot."));

            return new ProjectIntegrityCheckReport(
                expectedProjectId,
                items);
        }

        private static ProjectIntegrityCheckItem Run(
            string checkCode,
            Func<ProjectIntegrityCheckItem> verify)
        {
            try
            {
                ProjectIntegrityCheckItem item = verify();

                if (item is null ||
                    !string.Equals(
                        item.CheckCode,
                        checkCode,
                        StringComparison.Ordinal))
                {
                    return NotChecked(
                        checkCode,
                        "The verifier returned an unexpected coverage result.");
                }

                return item;
            }
            catch (UnauthorizedAccessException exception)
            {
                return NotChecked(checkCode,
                    $"Access prevented verification: {exception.Message}");
            }
            catch (IOException exception)
            {
                return NotChecked(checkCode,
                    $"An I/O failure prevented verification: {exception.Message}");
            }
            catch (SqliteException exception)
            {
                return NotChecked(checkCode,
                    $"A database read failed: {exception.Message}");
            }
            catch (InvalidOperationException exception)
            {
                if (checkCode == "PROJECT_IDENTITY" &&
                    ProjectPackageFailureMarker.TryRead(
                        exception,
                        out ProjectPackageIssue issue))
                {
                    switch (issue)
                    {
                        case ProjectPackageIssue.MissingRequiredFiles:
                        case ProjectPackageIssue.ProjectIdMismatch:
                        case ProjectPackageIssue.InvalidManifestProjectId:
                        case ProjectPackageIssue.InvalidManifestFormatMetadata:
                        case ProjectPackageIssue.InvalidManifestCreatedUtc:
                        case ProjectPackageIssue.MalformedManifest:
                        case ProjectPackageIssue.MissingDatabaseMetadata:
                        case ProjectPackageIssue.InvalidDatabaseProjectId:
                        case ProjectPackageIssue.InvalidDatabaseSchemaMetadata:
                            return new ProjectIntegrityCheckItem(
                                checkCode,
                                ProjectIntegrityCheckStatus.IntegrityFailure,
                                $"Project package integrity failure ({issue}): {exception.Message}");

                        case ProjectPackageIssue.UnsupportedProjectFormat:
                        case ProjectPackageIssue.UnsupportedDatabaseSchema:
                            return NotChecked(
                                checkCode,
                                $"Project format/schema is unsupported ({issue}): {exception.Message}");
                    }
                }

                return NotChecked(
                    checkCode,
                    $"Verification safe-stopped: {exception.Message}");
            }
            catch (ArgumentException exception)
            {
                return NotChecked(checkCode,
                    $"Verification could not complete: {exception.Message}");
            }
            catch (FormatException exception)
            {
                return NotChecked(checkCode,
                    $"Recorded data could not be interpreted: {exception.Message}");
            }
        }

        private static ProjectIntegrityCheckItem NotChecked(
            string checkCode,
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                checkCode,
                ProjectIntegrityCheckStatus.NotChecked,
                detail);
        }
    }
}