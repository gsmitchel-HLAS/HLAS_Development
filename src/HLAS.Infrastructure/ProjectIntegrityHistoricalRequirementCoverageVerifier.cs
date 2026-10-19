using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityHistoricalRequirementCoverageVerifier
    {
        internal static ProjectIntegrityCheckItem Check(string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string databasePath = Path.Combine(
                Path.GetFullPath(projectRoot),
                ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            List<long> emptyRevisions = new();
            int historicalRevisions = 0;

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT revision.RevisionNumber,
                       COUNT(item.RequirementKey)
                FROM HLAS_Source_Evidence_Requirement_Revisions AS revision
                LEFT JOIN HLAS_Source_Evidence_Requirement_Items AS item
                    ON item.RequirementRevisionId =
                       revision.RequirementRevisionId
                WHERE revision.RevisionNumber <
                (
                    SELECT MAX(RevisionNumber)
                    FROM HLAS_Source_Evidence_Requirement_Revisions
                )
                GROUP BY revision.RevisionNumber
                ORDER BY revision.RevisionNumber;
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            while (reader.Read())
            {
                historicalRevisions++;

                if (reader.GetInt64(1) == 0)
                    emptyRevisions.Add(reader.GetInt64(0));
            }

            string detail = emptyRevisions.Count > 0
                ? $"Historical requirement revisions without recorded items: {string.Join(", ", emptyRevisions)}. Full historical applicability remains unverified."
                : $"{historicalRevisions} earlier requirement revisions examined for item presence. Historical applicability and Data-As-Of remain unverified.";

            return new ProjectIntegrityCheckItem(
                "HISTORICAL_REQUIREMENT_APPLICABILITY",
                ProjectIntegrityCheckStatus.NotChecked,
                detail);
        }
    }
}