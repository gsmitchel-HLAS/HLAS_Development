using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class InternalSeriesTransitionGateway
    {
        public const string TransitionOperationKind =
            "V_SERIES_INTERNAL_TRANSITION";

        public static InternalSeriesTransitionRecord CreateTransition(
            string projectRoot,
           InternalSeriesTransitionAuthority transitionAuthority,
            SeriesId receivingSeriesId,
            ReleaseId releaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(transitionAuthority);

            if (!string.Equals(
                    Path.GetFullPath(projectRoot),
                    Path.GetFullPath(transitionAuthority.ProjectRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Transition authority belongs to a different project.");
            }

            UserId userId = transitionAuthority.UserId;
            ProjectRole projectRole = transitionAuthority.ProjectRole;
            if (projectRole != ProjectRole.Senior &&
                projectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Internal transition requires Senior or Admin authority.");
            }

            if (userId.Value == Guid.Empty ||
                string.IsNullOrWhiteSpace(receivingSeriesId.Value) ||
                receivingSeriesId == SeriesId.V ||
                releaseId.Value == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Internal transition identity is invalid.");
            }

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            ProjectManifest project =
                ProjectPackageReader.Open(fullProjectRoot);
            ProjectRole? storedProjectRole =
               ProjectAuthorizationStore.ResolveRole(
                   fullProjectRoot,
                   userId);

            if (storedProjectRole is null ||
                storedProjectRole.Value != projectRole ||
                (storedProjectRole.Value != ProjectRole.Senior &&
                 storedProjectRole.Value != ProjectRole.Admin))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: User lacks the required project-specific Senior/Admin authority.");
            }
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            };

            // Verify before beginning a governed transition.
            using (SqliteConnection preflight =
                new(builder.ToString()))
            {
                preflight.Open();

                using SqliteTransaction preflightTransaction =
                    preflight.BeginTransaction();

                _ = InternalSeriesTransitionBasisVerifier.Verify(
                    fullProjectRoot,
                    preflight,
                    preflightTransaction,
                    project.ProjectId,
                    releaseId);

                preflightTransaction.Rollback();
            }

            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    fullProjectRoot,
                    project.ProjectId,
                    userId,
                    SeriesId.V,
                    projectRole,
                    TransitionOperationKind);

            try
            {
                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                // Verify again against the governed transaction.
                InternalSeriesTransitionBasisReference basis =
                    InternalSeriesTransitionBasisVerifier.Verify(
                        fullProjectRoot,
                        connection,
                        transaction,
                        project.ProjectId,
                        releaseId);

                InternalSeriesTransitionRecord record = new(
                    InternalSeriesTransitionId.CreateNew(),
                    project.ProjectId,
                    operation.OperationId,
                    receivingSeriesId,
                    basis.ReleaseId,
                    basis.ReleaseFreezeId,
                    basis.ReadinessCheckId,
                    GovernedTimestamp.CreateNow());

                InternalSeriesTransitionWriter.Insert(
                    connection,
                    transaction,
                    record);

                GovernedOperationService.FinalizeSuccessInTransaction(
                    connection,
                    transaction,
                    operation.OperationId);

                transaction.Commit();

                return record;
            }
            catch (Exception exception)
            {
                bool safeStop =
                    exception is InvalidOperationException &&
                    exception.Message.StartsWith(
                        "SAFE-STOP:",
                        StringComparison.Ordinal);

                DecisionRecord decision = new(
                    safeStop
                        ? "INTERNAL TRANSITION SAFE-STOP"
                        : "INTERNAL TRANSITION TECHNICAL FAILURE",
                    exception.Message);

                if (safeStop)
                {
                    GovernedOperationService.FinalizeSafeStop(
                        fullProjectRoot,
                        operation.OperationId,
                        decision);
                }
                else
                {
                    GovernedOperationService.FinalizeTechnicalFailure(
                        fullProjectRoot,
                        operation.OperationId,
                        decision);
                }

                throw;
            }
        }
    }
}