using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegritySourceEvidenceRequirementsVerifier
    {
        private const string CheckCode = "SOURCE_EVIDENCE_REQUIREMENTS";

        internal static ProjectIntegrityCheckItem Check(string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            ProjectManifest project = ProjectPackageReader.Open(projectRoot);
            string fullRoot = Path.GetFullPath(projectRoot);
            string databasePath = Path.Combine(
                fullRoot,
                ProjectPackageCreator.DatabaseFileName);

            List<(string Key, EvidenceId Id)> received = new();
            int items = 0;
            int pending = 0;
            int superseded = 0;

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using (SqliteConnection connection = new(builder.ToString()))
            {
                connection.Open();

                string revisionId;
                using (SqliteCommand revision = connection.CreateCommand())
                {
                    revision.CommandText =
                        """
                        SELECT revision.RequirementRevisionId,
                               revision.ApprovedUtc,
                               operation.CompletedUtc,
                               operation.Outcome,
                               operation.ProjectId
                        FROM HLAS_Source_Evidence_Requirement_Revisions AS revision
                        LEFT JOIN HLAS_Governed_Operations AS operation
                            ON operation.OperationId = revision.OperationId
                        ORDER BY revision.RevisionNumber DESC
                        LIMIT 1;
                        """;

                    using SqliteDataReader reader = revision.ExecuteReader();
                    if (!reader.Read())
                    {
                        return Pending("No governed Source Evidence requirement revision exists.");
                    }

                    if (reader.IsDBNull(0) ||
                        !Guid.TryParse(reader.GetString(0), out Guid id) ||
                        id == Guid.Empty ||
                        reader.IsDBNull(1) ||
                        !DateTimeOffset.TryParse(reader.GetString(1), out _) ||
                        reader.IsDBNull(2) ||
                        !DateTimeOffset.TryParse(reader.GetString(2), out _) ||
                        reader.IsDBNull(3) ||
                        reader.GetString(3) != "SUCCESS" ||
                        reader.IsDBNull(4) ||
                        !Guid.TryParse(reader.GetString(4), out Guid projectId) ||
                        projectId != project.ProjectId.Value)
                    {
                        return Failure("The latest requirement revision has invalid identity, approval time, or successful project-operation linkage.");
                    }

                    revisionId = reader.GetString(0);
                }

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        """
                        SELECT item.RequirementKey,
                               item.RequirementState,
                               item.EvidenceId,
                               item.NotRequiredReason,
                               catalog.LifecycleState,
                               custody.EvidenceId
                        FROM HLAS_Source_Evidence_Requirement_Items AS item
                        LEFT JOIN HLAS_Source_Evidence_Catalog AS catalog
                            ON catalog.EvidenceId = item.EvidenceId
                        LEFT JOIN HLAS_Evidence_Custody AS custody
                            ON custody.EvidenceId = item.EvidenceId
                        WHERE item.RequirementRevisionId = $revisionId
                        ORDER BY item.RequirementKey;
                        """;
                    command.Parameters.AddWithValue("$revisionId", revisionId);

                    using SqliteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        items++;
                        if (reader.IsDBNull(0) ||
                            string.IsNullOrWhiteSpace(reader.GetString(0)) ||
                            reader.IsDBNull(1))
                        {
                            return Failure("A requirement item has no valid key or state.");
                        }

                        string key = reader.GetString(0);
                        string state = reader.GetString(1);

                        if (state == "PENDING")
                        {
                            if (!reader.IsDBNull(2) || !reader.IsDBNull(3))
                                return Failure($"Pending requirement has contradictory evidence: {key}.");

                            pending++;
                            continue;
                        }

                        if (state == "NOT_REQUIRED")
                        {
                            if (!reader.IsDBNull(2) || reader.IsDBNull(3) ||
                                string.IsNullOrWhiteSpace(reader.GetString(3)))
                                return Failure($"Not-required decision is incomplete: {key}.");

                            continue;
                        }

                        if (state != "RECEIVED" ||
                            reader.IsDBNull(2) ||
                            !Guid.TryParse(reader.GetString(2), out Guid evidenceId) ||
                            evidenceId == Guid.Empty ||
                            !reader.IsDBNull(3) ||
                            reader.IsDBNull(4) ||
                            reader.IsDBNull(5))
                        {
                            return Failure($"Received requirement has invalid catalog or custody linkage: {key}.");
                        }

                        string lifecycle = reader.GetString(4);
                        if (lifecycle == "Superseded")
                            superseded++;
                        else if (lifecycle != "Active")
                            return Failure($"Requirement references an invalid Source Evidence lifecycle: {key}.");

                        received.Add((key, new EvidenceId(evidenceId)));
                    }
                }
            }

            if (items == 0)
                return Pending("The latest approved requirement revision has no requirement items.");

            foreach ((string key, EvidenceId evidenceId) in received)
            {
                try
                {
                    _ = EvidenceCustodyRetrievalService.Retrieve(fullRoot, evidenceId);
                }
                catch (FileNotFoundException)
                {
                    return Failure($"Controlled evidence for requirement {key} is missing.");
                }
                catch (InvalidOperationException)
                {
                    return Failure($"Controlled evidence for requirement {key} failed custody integrity validation.");
                }
                catch (ArgumentException)
                {
                    return Failure($"Controlled evidence for requirement {key} has invalid custody metadata.");
                }
                catch (FormatException)
                {
                    return Failure($"Controlled evidence for requirement {key} has invalid recorded metadata.");
                }
                catch (IOException)
                {
                    return NotChecked($"Controlled evidence for requirement {key} could not be read.");
                }
                catch (UnauthorizedAccessException)
                {
                    return NotChecked($"Access to controlled evidence for requirement {key} was denied.");
                }
            }

            if (pending > 0 || superseded > 0)
            {
                return Pending(
                    $"{pending} requirement(s) remain PENDING and {superseded} reference superseded evidence awaiting revalidation. This is not automatically corruption.");
            }

            return new ProjectIntegrityCheckItem(
                CheckCode,
                ProjectIntegrityCheckStatus.Verified,
                $"{items} current requirement item(s) checked; referenced controlled evidence passed custody validation. Historical revision lineage is checked separately. This does not establish READY.");
        }

        private static ProjectIntegrityCheckItem Failure(string detail) =>
            new(CheckCode, ProjectIntegrityCheckStatus.IntegrityFailure, detail);

        private static ProjectIntegrityCheckItem Pending(string detail) =>
            new(CheckCode, ProjectIntegrityCheckStatus.Pending, detail);

        private static ProjectIntegrityCheckItem NotChecked(string detail) =>
            new(CheckCode, ProjectIntegrityCheckStatus.NotChecked, detail);
    }
}