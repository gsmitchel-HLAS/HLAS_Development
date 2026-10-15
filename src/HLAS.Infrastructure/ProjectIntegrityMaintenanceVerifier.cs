using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityMaintenanceVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            _ = ProjectPackageReader.Open(projectRoot);

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

            long openOperations = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Governed_Operations
                WHERE CompletedUtc IS NULL
                  AND Outcome IS NULL
                  AND OperationKind IN
                  (
                      'SOURCE_EVIDENCE_CORRECT',
                      'SOURCE_EVIDENCE_REPLACE',
                      'SOURCE_EVIDENCE_MAINTENANCE_REVALIDATION'
                  );
                """);

            long orphanResolutions = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Source_Evidence_Maintenance_Resolutions AS resolution
                LEFT JOIN HLAS_Source_Evidence_Maintenance AS maintenance
                    ON maintenance.OperationId =
                       resolution.MaintenanceOperationId
                WHERE maintenance.OperationId IS NULL;
                """);

            if (orphanResolutions > 0)
            {
                return Failure(
                    "A maintenance resolution has no corresponding governed maintenance record.");
            }

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    maintenance.OperationId,
                    maintenance.MaintenanceType,
                    maintenance.MaintainedUtc,
                    maintenanceOperation.OperationKind,
                    maintenanceOperation.CompletedUtc,
                    maintenanceOperation.Outcome,
                    resolution.ResolutionOperationId,
                    resolution.ResolvedUtc,
                    resolutionOperation.OperationKind,
                    resolutionOperation.CompletedUtc,
                    resolutionOperation.Outcome
                FROM HLAS_Source_Evidence_Maintenance AS maintenance
                LEFT JOIN HLAS_Governed_Operations AS maintenanceOperation
                    ON maintenanceOperation.OperationId =
                       maintenance.OperationId
                LEFT JOIN HLAS_Source_Evidence_Maintenance_Resolutions AS resolution
                    ON resolution.MaintenanceOperationId =
                       maintenance.OperationId
                LEFT JOIN HLAS_Governed_Operations AS resolutionOperation
                    ON resolutionOperation.OperationId =
                       resolution.ResolutionOperationId
                ORDER BY maintenance.MaintainedUtc,
                         maintenance.OperationId;
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            int maintenanceCount = 0;
            int unresolvedCount = 0;

            while (reader.Read())
            {
                maintenanceCount++;

                if (reader.IsDBNull(0) ||
                    !Guid.TryParse(
                        reader.GetString(0),
                        out Guid maintenanceId) ||
                    maintenanceId == Guid.Empty ||
                    reader.IsDBNull(1) ||
                    reader.IsDBNull(2) ||
                    !DateTimeOffset.TryParse(
                        reader.GetString(2),
                        out DateTimeOffset maintainedUtc))
                {
                    return Failure(
                        "A maintenance record has invalid identity or governed time.");
                }

                string expectedKind =
                    reader.GetString(1) switch
                    {
                        "CORRECT" => "SOURCE_EVIDENCE_CORRECT",
                        "REPLACE" => "SOURCE_EVIDENCE_REPLACE",
                        _ => ""
                    };

                if (expectedKind.Length == 0 ||
                    reader.IsDBNull(3) ||
                    reader.GetString(3) != expectedKind ||
                    reader.IsDBNull(4) ||
                    !DateTimeOffset.TryParse(
                        reader.GetString(4),
                        out DateTimeOffset maintenanceCompletedUtc) ||
                    maintenanceCompletedUtc < maintainedUtc ||
                    reader.IsDBNull(5) ||
                    reader.GetString(5) != "SUCCESS")
                {
                    return Failure(
                        "Governed maintenance is not linked to a completed successful operation of the expected type.");
                }

                if (reader.IsDBNull(6))
                {
                    unresolvedCount++;
                    continue;
                }

                if (!Guid.TryParse(
                        reader.GetString(6),
                        out Guid resolutionId) ||
                    resolutionId == Guid.Empty ||
                    resolutionId == maintenanceId ||
                    reader.IsDBNull(7) ||
                    !DateTimeOffset.TryParse(
                        reader.GetString(7),
                        out DateTimeOffset resolvedUtc) ||
                    resolvedUtc < maintainedUtc ||
                    reader.IsDBNull(8) ||
                    reader.GetString(8) !=
                        "SOURCE_EVIDENCE_MAINTENANCE_REVALIDATION" ||
                    reader.IsDBNull(9) ||
                    !DateTimeOffset.TryParse(
                        reader.GetString(9),
                        out DateTimeOffset resolutionCompletedUtc) ||
                    resolutionCompletedUtc < resolvedUtc ||
                    reader.IsDBNull(10) ||
                    reader.GetString(10) != "SUCCESS")
                {
                    return Failure(
                        "A maintenance resolution has invalid operation, outcome, or temporal lineage.");
                }
            }

            if (openOperations > 0 || unresolvedCount > 0)
            {
                return new ProjectIntegrityCheckItem(
                    "SOURCE_EVIDENCE_MAINTENANCE",
                    ProjectIntegrityCheckStatus.Pending,
                    $"{openOperations} maintenance/revalidation operation(s) remain open; {unresolvedCount} successful maintenance record(s) await governed revalidation. Outstanding work is not automatically corruption.");
            }

            return new ProjectIntegrityCheckItem(
                "SOURCE_EVIDENCE_MAINTENANCE",
                ProjectIntegrityCheckStatus.Verified,
                $"{maintenanceCount} governed maintenance record(s) have complete successful resolution linkage, with no open maintenance operations. Detailed Source Evidence and Freeze integrity are verified separately.");
        }

        private static long Count(
            SqliteConnection connection,
            string sql)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText = sql;

            return Convert.ToInt64(
                command.ExecuteScalar());
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "SOURCE_EVIDENCE_MAINTENANCE",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }
    }
}