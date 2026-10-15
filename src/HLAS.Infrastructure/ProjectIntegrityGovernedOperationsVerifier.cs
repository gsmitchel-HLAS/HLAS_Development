using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityGovernedOperationsVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            ProjectManifest manifest =
                ProjectPackageReader.Open(projectRoot);

            string databasePath = Path.Combine(
                Path.GetFullPath(projectRoot),
                ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    OperationId,
                    ProjectId,
                    UserId,
                    SeriesId,
                    ProjectRole,
                    StartedUtc,
                    CompletedUtc,
                    Outcome,
                    Decision,
                    Reason,
                    OperationKind
                FROM HLAS_Governed_Operations
                ORDER BY StartedUtc, OperationId;
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            int checkedCount = 0;
            int pendingCount = 0;

            while (reader.Read())
            {
                checkedCount++;

                if (reader.IsDBNull(0) ||
                    !Guid.TryParse(reader.GetString(0), out Guid operationId) ||
                    operationId == Guid.Empty ||
                    reader.IsDBNull(1) ||
                    !Guid.TryParse(reader.GetString(1), out Guid projectId) ||
                    projectId != manifest.ProjectId.Value ||
                    reader.IsDBNull(2) ||
                    !Guid.TryParse(reader.GetString(2), out Guid userId) ||
                    userId == Guid.Empty ||
                    reader.IsDBNull(3) ||
                    string.IsNullOrWhiteSpace(reader.GetString(3)) ||
                    reader.IsDBNull(4) ||
                    string.IsNullOrWhiteSpace(reader.GetString(4)) ||
                    reader.IsDBNull(5) ||
                    !DateTimeOffset.TryParse(
                        reader.GetString(5),
                        out DateTimeOffset startedUtc) ||
                    (!reader.IsDBNull(10) &&
                     string.IsNullOrWhiteSpace(reader.GetString(10))))
                {
                    return Failure(
                        "A governed operation has invalid identity, authority metadata, or start time.");
                }

                bool completed = !reader.IsDBNull(6);
                bool hasOutcome = !reader.IsDBNull(7);
                bool hasDecision = !reader.IsDBNull(8);
                bool hasReason = !reader.IsDBNull(9);

                if (!completed)
                {
                    if (hasOutcome || hasDecision || hasReason)
                    {
                        return Failure(
                            "An open governed operation contains inconsistent completion fields.");
                    }

                    pendingCount++;
                    continue;
                }

                if (!DateTimeOffset.TryParse(
                        reader.GetString(6),
                        out DateTimeOffset completedUtc) ||
                    completedUtc < startedUtc ||
                    !hasOutcome)
                {
                    return Failure(
                        "A completed governed operation has invalid completion time or outcome.");
                }

                string outcome = reader.GetString(7);

                bool validDecisionPair =
                    hasDecision == hasReason &&
                    (!hasDecision ||
                     (!string.IsNullOrWhiteSpace(reader.GetString(8)) &&
                      !string.IsNullOrWhiteSpace(reader.GetString(9))));

                bool validSuccess =
                    outcome == "SUCCESS" &&
                    validDecisionPair;

                bool validStopped =
                    (outcome == "SAFE-STOP" ||
                     outcome == "TECHNICAL FAILURE") &&
                    hasDecision &&
                    hasReason &&
                    !string.IsNullOrWhiteSpace(reader.GetString(8)) &&
                    !string.IsNullOrWhiteSpace(reader.GetString(9));

                if (!validSuccess && !validStopped)
                {
                    return Failure(
                        "A completed governed operation has inconsistent outcome or decision records.");
                }
            }

            if (checkedCount == 0)
            {
                return new ProjectIntegrityCheckItem(
                    "GOVERNED_OPERATIONS",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "No governed operation records exist to inspect.");
            }

            if (pendingCount > 0)
            {
                return new ProjectIntegrityCheckItem(
                    "GOVERNED_OPERATIONS",
                    ProjectIntegrityCheckStatus.Pending,
                    $"{pendingCount} governed operation(s) remain open. Outstanding operations are not automatically classified as corruption.");
            }

            return new ProjectIntegrityCheckItem(
                "GOVERNED_OPERATIONS",
                ProjectIntegrityCheckStatus.Verified,
                $"{checkedCount} governed operation record(s) passed identity and outcome consistency checks. Linked governed records require separate verification.");
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "GOVERNED_OPERATIONS",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }
    }
}