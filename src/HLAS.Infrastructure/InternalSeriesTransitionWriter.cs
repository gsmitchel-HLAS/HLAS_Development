using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionWriter
    {
        internal static void Insert(
            SqliteConnection connection,
            SqliteTransaction transaction,
            InternalSeriesTransitionRecord record)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(record);

            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO HLAS_Internal_Series_Transitions
                (
                    TransitionId,
                    ProjectId,
                    OperationId,
                    SourceSeriesId,
                    ReceivingSeriesId,
                    ReleaseId,
                    ReleaseFreezeId,
                    ReadinessCheckId,
                    ReceivedUtc
                )
                VALUES
                (
                    $transitionId,
                    $projectId,
                    $operationId,
                    $sourceSeriesId,
                    $receivingSeriesId,
                    $releaseId,
                    $releaseFreezeId,
                    $readinessCheckId,
                    $receivedUtc
                );
                """;

            command.Parameters.AddWithValue(
                "$transitionId",
                record.TransitionId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$projectId",
                record.ProjectId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$sourceSeriesId",
                record.SourceSeriesId.Value);

            command.Parameters.AddWithValue(
                "$receivingSeriesId",
                record.ReceivingSeriesId.Value);

            command.Parameters.AddWithValue(
                "$releaseId",
                record.ReleaseId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$releaseFreezeId",
                record.ReleaseFreezeId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                record.ReadinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$receivedUtc",
                record.ReceivedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }
    }
}