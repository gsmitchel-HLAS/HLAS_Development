using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityProjectJmfReviewVerifier
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

            long caseCount = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Project_JMF_Review_Cases;
                """);

            long orphanItems = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Project_JMF_Review_Items AS item
                LEFT JOIN HLAS_Project_JMF_Review_Cases AS review
                    ON review.ReviewCaseId = item.ReviewCaseId
                WHERE review.ReviewCaseId IS NULL;
                """);

            long emptyCases = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Project_JMF_Review_Cases AS review
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM HLAS_Project_JMF_Review_Items AS item
                    WHERE item.ReviewCaseId = review.ReviewCaseId
                );
                """);

            if (orphanItems > 0 || emptyCases > 0)
            {
                return Failure(
                    "Project/JMF review records contain orphaned items or cases without review items.");
            }

            long invalidClosures = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Project_JMF_Review_Cases AS review
                WHERE review.ResolvedOperationId IS NOT NULL
                  AND EXISTS
                  (
                      SELECT 1
                      FROM HLAS_Project_JMF_Review_Items AS item
                      WHERE item.ReviewCaseId = review.ReviewCaseId
                        AND
                        (
                            item.Decision IS NULL
                            OR item.DecisionReason IS NULL
                            OR item.DecidedOperationId IS NULL
                            OR item.DecidedUtc IS NULL
                        )
                  );
                """);

            if (invalidClosures > 0)
            {
                return Failure(
                    "A resolved Project/JMF review contains incomplete governed decisions.");
            }

            long unresolvedCases = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM HLAS_Project_JMF_Review_Cases AS review
                WHERE review.ResolvedOperationId IS NULL
                   OR review.ResolvedUtc IS NULL
                   OR EXISTS
                   (
                       SELECT 1
                       FROM HLAS_Project_JMF_Review_Items AS item
                       WHERE item.ReviewCaseId = review.ReviewCaseId
                         AND
                         (
                             item.Decision IS NULL
                             OR item.DecisionReason IS NULL
                             OR item.DecidedOperationId IS NULL
                             OR item.DecidedUtc IS NULL
                         )
                   );
                """);

            if (unresolvedCases > 0)
            {
                return new ProjectIntegrityCheckItem(
                    "PROJECT_JMF_REVIEW",
                    ProjectIntegrityCheckStatus.Pending,
                    $"{unresolvedCases} Project/JMF review case(s) remain unresolved. Pending review is not automatically corruption.");
            }

            return new ProjectIntegrityCheckItem(
                "PROJECT_JMF_REVIEW",
                ProjectIntegrityCheckStatus.Verified,
                $"{caseCount} Project/JMF review case(s) inspected with no unresolved reviews or incomplete closures. Detailed historical lineage is verified separately.");
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
                "PROJECT_JMF_REVIEW",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }
    }
}