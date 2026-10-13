using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionReceiptReader
    {
        internal static InternalSeriesTransitionRecord Read(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ProjectId projectId,
            InternalSeriesTransitionId transitionId)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    receipt.OperationId,
                    receipt.ReceivingSeriesId,
                    receipt.ReleaseId,
                    receipt.ReleaseFreezeId,
                    receipt.ReadinessCheckId,
                    receipt.ReceivedUtc
                FROM HLAS_Internal_Series_Transitions AS receipt
                INNER JOIN HLAS_Governed_Operations AS operation
                    ON operation.OperationId = receipt.OperationId
                INNER JOIN HLAS_Release_Records AS release
                    ON release.ReleaseId = receipt.ReleaseId
                INNER JOIN HLAS_Historical_Checkpoints AS checkpoint
                    ON checkpoint.FreezeId = receipt.ReleaseFreezeId
                WHERE
                    receipt.TransitionId = $transitionId
                    AND receipt.ProjectId = $projectId
                    AND receipt.SourceSeriesId = 'V'
                    AND operation.ProjectId = receipt.ProjectId
                    AND operation.SeriesId = 'V'
                    AND operation.OperationKind = 'V_SERIES_INTERNAL_TRANSITION'
                    AND operation.Outcome = 'SUCCESS'
                    AND operation.CompletedUtc IS NOT NULL
                    AND release.ReadinessCheckId = receipt.ReadinessCheckId
                    AND checkpoint.ReleaseId = receipt.ReleaseId
                    AND checkpoint.CheckpointType = 'RELEASE_FREEZE';
                """;

            command.Parameters.AddWithValue(
                "$transitionId",
                transitionId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$projectId",
                projectId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: A successful internal transition receipt was not found.");
            }

            if (!Guid.TryParse(reader.GetString(0), out Guid operationId) ||
                operationId == Guid.Empty ||
                !Guid.TryParse(reader.GetString(2), out Guid releaseId) ||
                releaseId == Guid.Empty ||
                !Guid.TryParse(reader.GetString(3), out Guid freezeId) ||
                freezeId == Guid.Empty ||
                !Guid.TryParse(reader.GetString(4), out Guid readinessId) ||
                readinessId == Guid.Empty ||
                !DateTimeOffset.TryParse(
                    reader.GetString(5),
                    out DateTimeOffset receivedUtc) ||
                receivedUtc == default)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Internal transition receipt contains invalid identity or time.");
            }

            SeriesId receivingSeries =
                reader.GetString(1) switch
                {
                    "A" => SeriesId.A,
                    "I" => SeriesId.I,
                    _ => throw new InvalidOperationException(
                        "SAFE-STOP: Receiving series identity is unsupported.")
                };

            return new InternalSeriesTransitionRecord(
                transitionId,
                projectId,
                new OperationId(operationId),
                receivingSeries,
                new ReleaseId(releaseId),
                new FreezeId(freezeId),
                new ReadinessCheckId(readinessId),
                GovernedTimestamp.FromRecorded(receivedUtc));
        }
    }
}