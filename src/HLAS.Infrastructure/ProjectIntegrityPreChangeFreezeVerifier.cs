using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class ProjectIntegrityPreChangeFreezeVerifier
    {
        internal static ProjectIntegrityCheckItem Check(
            string projectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string fullRoot = Path.GetFullPath(projectRoot);

            _ = ProjectPackageReader.Open(fullRoot);

            string databasePath = Path.Combine(
                fullRoot,
                ProjectPackageCreator.DatabaseFileName);

            List<FreezeId> freezeIds = new();

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
                        freeze.FreezeId,
                        freeze.FreezeType,
                        operation.OperationId,
                        custody.EvidenceId
                    FROM HLAS_Frozen_States AS freeze
                    LEFT JOIN HLAS_Governed_Operations AS operation
                        ON operation.OperationId = freeze.OperationId
                    LEFT JOIN HLAS_Evidence_Custody AS custody
                        ON custody.EvidenceId = freeze.TargetEvidenceId
                    ORDER BY freeze.FrozenUtc, freeze.FreezeId;
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
                        reader.GetString(1) !=
                            FrozenStateRecord.PreChangeFreezeType ||
                        reader.IsDBNull(2) ||
                        reader.IsDBNull(3))
                    {
                        return Failure(
                            "A Pre-Change Freeze has invalid identity, type, operation linkage, or evidence custody linkage.");
                    }

                    freezeIds.Add(new FreezeId(parsedId));
                }
            }

            if (freezeIds.Count == 0)
            {
                return new ProjectIntegrityCheckItem(
                    "PRE_CHANGE_FREEZE",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "No governed Pre-Change Freeze records exist to verify.");
            }

            foreach (FreezeId freezeId in freezeIds)
            {
                try
                {
                    _ = PreChangeFreezeRetrievalService.Retrieve(
                        fullRoot,
                        freezeId);
                }
                catch (InvalidOperationException exception)
                {
                    return Failure(
                        $"Pre-Change Freeze {freezeId.Value:D} failed verification: {exception.Message}");
                }
                catch (ArgumentException)
                {
                    return Failure(
                        $"Pre-Change Freeze {freezeId.Value:D} contains invalid recorded metadata.");
                }
                catch (FormatException)
                {
                    return Failure(
                        $"Pre-Change Freeze {freezeId.Value:D} contains invalid recorded formatting.");
                }
                catch (IOException)
                {
                    return NotChecked(
                        $"Pre-Change Freeze {freezeId.Value:D} could not be read.");
                }
                catch (UnauthorizedAccessException)
                {
                    return NotChecked(
                        $"Access was denied while verifying Pre-Change Freeze {freezeId.Value:D}.");
                }
            }

            return new ProjectIntegrityCheckItem(
                "PRE_CHANGE_FREEZE",
                ProjectIntegrityCheckStatus.Verified,
                $"{freezeIds.Count} Pre-Change Freeze record(s) passed controlled-file integrity verification. Maintenance outcomes and Release Freeze checkpoints are verified separately.");
        }

        private static ProjectIntegrityCheckItem Failure(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "PRE_CHANGE_FREEZE",
                ProjectIntegrityCheckStatus.IntegrityFailure,
                detail);
        }

        private static ProjectIntegrityCheckItem NotChecked(
            string detail)
        {
            return new ProjectIntegrityCheckItem(
                "PRE_CHANGE_FREEZE",
                ProjectIntegrityCheckStatus.NotChecked,
                detail);
        }
    }
}