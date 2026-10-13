using System;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionBasisVerifier
    {
        internal static InternalSeriesTransitionBasisReference Verify(
            string projectRoot,
            SqliteConnection connection,
            SqliteTransaction transaction,
            ProjectId projectId,
            ReleaseId releaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            InternalSeriesTransitionBasisReference basis =
                InternalSeriesTransitionBasisReader.Read(
                    connection,
                    transaction,
                    projectId,
                    releaseId);

            ReleaseFreezeManifest manifest =
                InternalSeriesTransitionManifestReader.Read(
                    projectRoot,
                    basis);

            InternalSeriesTransitionSnapshotVerifier.Verify(
                projectRoot,
                basis,
                manifest);

            InternalSeriesTransitionEvidenceVerifier.Verify(
                projectRoot,
                manifest);

            InternalSeriesTransitionFrozenCustodyVerifier.Verify(
                projectRoot,
                basis,
                manifest);

            return basis;
        }
    }
}