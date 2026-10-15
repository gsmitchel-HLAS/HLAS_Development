using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityRegisteredSourceEvidenceVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            _ = ProjectPackageReader.Open(projectRoot);

            string fullRoot = Path.GetFullPath(projectRoot);

            string databasePath = Path.Combine(
                fullRoot,
                ProjectPackageCreator.DatabaseFileName);

            List<EvidenceId> registered = new();

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using (SqliteConnection connection =
                new(builder.ToString()))
            {
                connection.Open();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        catalog.EvidenceId,
                        catalog.SourceClass,
                        custody.EvidenceId
                    FROM HLAS_Source_Evidence_Catalog AS catalog
                    LEFT JOIN HLAS_Evidence_Custody AS custody
                        ON custody.EvidenceId = catalog.EvidenceId
                    ORDER BY catalog.EvidenceId;
                    """;

                using SqliteDataReader reader =
                    command.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.IsDBNull(0) ||
                        !Guid.TryParse(
                            reader.GetString(0),
                            out Guid parsedId) ||
                        parsedId == Guid.Empty ||
                        reader.IsDBNull(1) ||
                        string.IsNullOrWhiteSpace(
                            reader.GetString(1)) ||
                        reader.IsDBNull(2))
                    {
                        return new ProjectIntegrityCheckItem(
                            "SOURCE_EVIDENCE_CUSTODY",
                            ProjectIntegrityCheckStatus.IntegrityFailure,
                            "A registered Source Evidence record has invalid identity or missing custody linkage.");
                    }

                    registered.Add(new EvidenceId(parsedId));
                }
            }

            if (registered.Count == 0)
            {
                return new ProjectIntegrityCheckItem(
                    "SOURCE_EVIDENCE_CUSTODY",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "No registered Source Evidence exists to verify. Source requirements were not evaluated.");
            }

            foreach (EvidenceId evidenceId in registered)
            {
                try
                {
                    _ = EvidenceCustodyRetrievalService.Retrieve(
                        fullRoot,
                        evidenceId);
                }
                catch (FileNotFoundException)
                {
                    return new ProjectIntegrityCheckItem(
                        "SOURCE_EVIDENCE_CUSTODY",
                        ProjectIntegrityCheckStatus.IntegrityFailure,
                        $"Controlled Source Evidence is missing for EvidenceId {evidenceId.Value:D}.");
                }
                catch (InvalidOperationException exception)
                {
                    return new ProjectIntegrityCheckItem(
                        "SOURCE_EVIDENCE_CUSTODY",
                        ProjectIntegrityCheckStatus.IntegrityFailure,
                        exception.Message);
                }
                catch (IOException)
                {
                    return new ProjectIntegrityCheckItem(
                        "SOURCE_EVIDENCE_CUSTODY",
                        ProjectIntegrityCheckStatus.NotChecked,
                        $"Controlled Source Evidence could not be read for EvidenceId {evidenceId.Value:D}.");
                }
                catch (UnauthorizedAccessException)
                {
                    return new ProjectIntegrityCheckItem(
                        "SOURCE_EVIDENCE_CUSTODY",
                        ProjectIntegrityCheckStatus.NotChecked,
                        $"Access was denied while verifying EvidenceId {evidenceId.Value:D}.");
                }
            }

            return new ProjectIntegrityCheckItem(
                "SOURCE_EVIDENCE_CUSTODY",
                ProjectIntegrityCheckStatus.Verified,
                $"{registered.Count} registered Source Evidence record(s) passed custody integrity verification. Generic custody and source requirements are outside this check.");
        }
    }
}