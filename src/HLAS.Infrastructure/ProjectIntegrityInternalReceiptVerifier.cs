using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityInternalReceiptVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string fullRoot = Path.GetFullPath(projectRoot);

            ProjectManifest project =
                ProjectPackageReader.Open(fullRoot);

            string databasePath = Path.Combine(
                fullRoot,
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

            using SqliteTransaction transaction =
                connection.BeginTransaction(deferred: true);

            List<InternalSeriesTransitionId> receiptIds = new();

            using (SqliteCommand command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText =
                    """
                    SELECT TransitionId, ProjectId
                    FROM HLAS_Internal_Series_Transitions
                    ORDER BY ReceivedUtc, TransitionId;
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
                        !Guid.TryParse(
                            reader.GetString(1),
                            out Guid recordedProjectId) ||
                        recordedProjectId != project.ProjectId.Value)
                    {
                        return Failure(
                            "An internal receiving receipt has invalid transition or project identity.");
                    }

                    receiptIds.Add(
                        new InternalSeriesTransitionId(parsedId));
                }
            }

            using (SqliteCommand command =
                connection.CreateCommand())
            {
                command.Transaction = transaction;

                command.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Governed_Operations AS operation
                    WHERE operation.ProjectId = $projectId
                      AND operation.OperationKind =
                          'V_SERIES_INTERNAL_TRANSITION'
                      AND operation.Outcome = 'SUCCESS'
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM HLAS_Internal_Series_Transitions AS receipt
                          WHERE receipt.OperationId =
                              operation.OperationId
                      );
                    """;

                command.Parameters.AddWithValue(
                    "$projectId",
                    project.ProjectId.Value.ToString("D"));

                long missingReceipts =
                    Convert.ToInt64(command.ExecuteScalar());

                if (missingReceipts > 0)
                {
                    return Failure(
                        "A successful internal series transition has no permanent receiving receipt.");
                }
            }

            if (receiptIds.Count == 0)
            {
                return new ProjectIntegrityCheckItem(
                    "INTERNAL_SERIES_RECEIPTS",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "No internal receiving-series receipts exist to verify.");
            }

            foreach (InternalSeriesTransitionId receiptId in receiptIds)
            {
                try
                {
                    _ = InternalSeriesTransitionReceiptVerifier.Verify(
                        fullRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        receiptId);
                }
                catch (FileNotFoundException)
                {
                    return Failure(
                        $"Receiving receipt {receiptId.Value:D} references missing historical evidence.");
                }
                catch (DirectoryNotFoundException)
                {
                    return Failure(
                        $"Receiving receipt {receiptId.Value:D} references missing historical storage.");
                }
                catch (UnauthorizedAccessException)
                {
                    return NotChecked(
                        $"Access was denied while checking receiving receipt {receiptId.Value:D}.");
                }
                catch (IOException)
                {
                    return NotChecked(
                        $"Receiving receipt {receiptId.Value:D} could not be fully read.");
                }
                catch (InvalidOperationException exception)
                {
                    return Failure(
                        $"Receiving receipt {receiptId.Value:D} failed integrity verification: {exception.Message}");
                }
            }

            return new ProjectIntegrityCheckItem(
                "INTERNAL_SERIES_RECEIPTS",
                ProjectIntegrityCheckStatus.Verified,
                $"{receiptIds.Count} internal receiving receipt(s) passed released V-basis and historical integrity verification. Receiving-series analysis is outside this check.");
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "INTERNAL_SERIES_RECEIPTS",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }

        private static ProjectIntegrityCheckItem NotChecked(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "INTERNAL_SERIES_RECEIPTS",
                ProjectIntegrityCheckStatus.NotChecked,
                detail);
        }
    }
}