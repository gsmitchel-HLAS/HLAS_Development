using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionBasisReader
    {
        internal static InternalSeriesTransitionBasisReference Read(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ProjectId projectId,
            ReleaseId releaseId)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT
                    checkpoint.FreezeId,
                    checkpoint.OperationId,
                    release.ReadinessCheckId,
                    checkpoint.FrozenUtc,
                    checkpoint.RelativeCheckpointPath,
                    checkpoint.ManifestRelativePath,
                    checkpoint.ManifestSha256Hex
                FROM HLAS_Release_Records AS release
                INNER JOIN HLAS_Historical_Checkpoints AS checkpoint
                    ON checkpoint.ReleaseId = release.ReleaseId
                INNER JOIN HLAS_Governed_Operations AS freezeOperation
                    ON freezeOperation.OperationId = checkpoint.OperationId
                INNER JOIN HLAS_Governed_Operations AS releaseOperation
                    ON releaseOperation.OperationId = release.OperationId
                INNER JOIN HLAS_Readiness_Checks AS readiness
                    ON readiness.ReadinessCheckId = release.ReadinessCheckId
                INNER JOIN HLAS_Governed_Operations AS readinessOperation
                    ON readinessOperation.OperationId = readiness.OperationId
                WHERE
                    release.ReleaseId = $releaseId
                    AND checkpoint.CheckpointType = 'RELEASE_FREEZE'
                    AND freezeOperation.ProjectId = $projectId
                    AND freezeOperation.SeriesId = 'V'
                    AND freezeOperation.OperationKind = 'V_SERIES_RELEASE_FREEZE'
                    AND freezeOperation.Outcome = 'SUCCESS'
                    AND freezeOperation.CompletedUtc IS NOT NULL
                    AND releaseOperation.ProjectId = $projectId
                    AND releaseOperation.SeriesId = 'V'
                    AND releaseOperation.OperationKind = 'V_SERIES_RELEASE_REQUEST'
                    AND releaseOperation.Outcome = 'SUCCESS'
                    AND releaseOperation.CompletedUtc IS NOT NULL
                    AND readinessOperation.ProjectId = $projectId
                    AND readinessOperation.SeriesId = 'V'
                    AND readinessOperation.Outcome = 'SUCCESS'
                    AND readinessOperation.CompletedUtc IS NOT NULL
                    AND readiness.OverallStatus = 'READY'
                    AND
                    (
                        SELECT COUNT(*)
                        FROM HLAS_Readiness_Check_Items AS item
                        WHERE item.ReadinessCheckId = release.ReadinessCheckId
                    ) = 7
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM HLAS_Readiness_Check_Items AS item
                        WHERE
                            item.ReadinessCheckId = release.ReadinessCheckId
                            AND item.GateStatus <> 'PASS'
                    );
                """;

            command.Parameters.AddWithValue(
                "$releaseId",
                releaseId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$projectId",
                projectId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: A verified RELEASED V-series basis is required.");
            }

            if (!Guid.TryParse(reader.GetString(0), out Guid freezeId) ||
                freezeId == Guid.Empty ||
                !Guid.TryParse(reader.GetString(1), out Guid operationId) ||
                operationId == Guid.Empty ||
                !Guid.TryParse(reader.GetString(2), out Guid readinessId) ||
                readinessId == Guid.Empty ||
                !DateTimeOffset.TryParse(
                    reader.GetString(3),
                    out DateTimeOffset frozenUtc) ||
                frozenUtc == default)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Released-basis identity or governed time is invalid.");
            }

            return new InternalSeriesTransitionBasisReference(
                projectId,
                releaseId,
                new FreezeId(freezeId),
                new ReadinessCheckId(readinessId),
                new OperationId(operationId),
                GovernedTimestamp.FromRecorded(frozenUtc),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6));
        }
    }
}