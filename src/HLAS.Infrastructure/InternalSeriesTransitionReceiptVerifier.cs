using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionReceiptVerifier
    {
        internal static InternalSeriesTransitionRecord Verify(
            string projectRoot,
            SqliteConnection connection,
            SqliteTransaction transaction,
            ProjectId projectId,
            InternalSeriesTransitionId transitionId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            InternalSeriesTransitionRecord receipt =
                InternalSeriesTransitionReceiptReader.Read(
                    connection,
                    transaction,
                    projectId,
                    transitionId);

            InternalSeriesTransitionBasisReference basis =
                InternalSeriesTransitionBasisVerifier.Verify(
                    projectRoot,
                    connection,
                    transaction,
                    projectId,
                    receipt.ReleaseId);

            if (receipt.ReleaseFreezeId != basis.ReleaseFreezeId ||
                receipt.ReadinessCheckId != basis.ReadinessCheckId ||
                receipt.SourceSeriesId != SeriesId.V)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Internal transition receipt does not match the verified released V-series basis.");
            }

            return receipt;
        }
    }
}