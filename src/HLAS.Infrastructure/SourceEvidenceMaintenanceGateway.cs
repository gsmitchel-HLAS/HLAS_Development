using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;
namespace HLAS.Infrastructure
{
    public enum SourceEvidenceMetadataMutationAction
    {
        Keep,
        Set,
        Clear
    }

    public sealed record SourceEvidenceMetadataMutation
    {
        public SourceEvidenceMetadataMutationAction Action { get; }
        public string? Value { get; }

        private SourceEvidenceMetadataMutation(
            SourceEvidenceMetadataMutationAction action,
            string? value)
        {
            Action = action;
            Value = value;
        }

        public static SourceEvidenceMetadataMutation Keep()
        {
            return new SourceEvidenceMetadataMutation(
                SourceEvidenceMetadataMutationAction.Keep,
                null);
        }

        public static SourceEvidenceMetadataMutation Set(
            string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            return new SourceEvidenceMetadataMutation(
                SourceEvidenceMetadataMutationAction.Set,
                value);
        }

        public static SourceEvidenceMetadataMutation Clear()
        {
            return new SourceEvidenceMetadataMutation(
                SourceEvidenceMetadataMutationAction.Clear,
                null);
        }
    }

    public sealed record SourceEvidenceCorrectionRequest
    {
        public SourceEvidenceMetadataMutation DisplayLabel { get; }
        public SourceEvidenceMetadataMutation AdministrativeDescription { get; }
        public string CorrectionReason { get; }

        public SourceEvidenceCorrectionRequest(
            SourceEvidenceMetadataMutation displayLabel,
            SourceEvidenceMetadataMutation administrativeDescription,
            string correctionReason)
        {
            ArgumentNullException.ThrowIfNull(displayLabel);
            ArgumentNullException.ThrowIfNull(administrativeDescription);
            ArgumentException.ThrowIfNullOrWhiteSpace(correctionReason);

            DisplayLabel = displayLabel;
            AdministrativeDescription = administrativeDescription;
            CorrectionReason = correctionReason;
        }
    }

    public static class SourceEvidenceMaintenanceGateway
    {
        public static SourceEvidenceMaintenanceRecord Correct(
    string projectRoot,
    UserId userId,
    ProjectRole projectRole,
    EvidenceId evidenceId,
    SourceEvidenceCorrectionRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(request);
            string fullProjectRoot =
    Path.GetFullPath(projectRoot);

            ProjectManifest manifest =
                ProjectPackageReader.Open(fullProjectRoot);
            VerifyActiveSourceEvidence(
    fullProjectRoot,
    evidenceId);

            CurrentMetadataState currentMetadata =
                ReadCurrentMetadataState(
                    fullProjectRoot,
                    evidenceId);

            string? resultingDisplayLabel =
                ResolveResultingValue(
                    currentMetadata.DisplayLabel,
                    request.DisplayLabel);

            string? resultingAdministrativeDescription =
                ResolveResultingValue(
                    currentMetadata.AdministrativeDescription,
                    request.AdministrativeDescription);
            if (string.Equals(
        currentMetadata.DisplayLabel,
        resultingDisplayLabel,
        StringComparison.Ordinal) &&
    string.Equals(
        currentMetadata.AdministrativeDescription,
        resultingAdministrativeDescription,
        StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: CORRECT must change at least one approved metadata field.");
            }
            GovernedOperationRecord operation =
    GovernedOperationService.Begin(
        fullProjectRoot,
        manifest.ProjectId,
        userId,
        SeriesId.V,
        projectRole);

            try
            {
                FrozenStateRecord freeze =
     PreChangeFreezeService.Create(
         fullProjectRoot,
         operation.OperationId,
         evidenceId);

                GovernedTimestamp maintainedUtc =
                    GovernedTimestamp.CreateNow();

                SourceEvidenceMaintenanceRecord maintenanceRecord =
                    new(
                        operation.OperationId,
                        freeze.FreezeId,
                        evidenceId,
                        evidenceId,
                        SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                        maintainedUtc);

                using SqliteConnection connection =
                    OpenDatabase(fullProjectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertMaintenanceRecord(
                    connection,
                    transaction,
                    maintenanceRecord);

                int changeSequence = 1;

                if (!string.Equals(
                        currentMetadata.DisplayLabel,
                        resultingDisplayLabel,
                        StringComparison.Ordinal))
                {
                    InsertMaintenanceChangeRecord(
                        connection,
                        transaction,
                        new SourceEvidenceMaintenanceChangeRecord(
                            operation.OperationId,
                            changeSequence,
                            "DisplayLabel",
                            currentMetadata.DisplayLabel,
                            resultingDisplayLabel));

                    changeSequence++;
                }

                if (!string.Equals(
                        currentMetadata.AdministrativeDescription,
                        resultingAdministrativeDescription,
                        StringComparison.Ordinal))
                {
                    InsertMaintenanceChangeRecord(
                        connection,
                        transaction,
                        new SourceEvidenceMaintenanceChangeRecord(
                            operation.OperationId,
                            changeSequence,
                            "AdministrativeDescription",
                            currentMetadata.AdministrativeDescription,
                            resultingAdministrativeDescription));
                }

                InsertMetadataVersion(
                    connection,
                    transaction,
                    evidenceId,
                    currentMetadata,
                    resultingDisplayLabel,
                    resultingAdministrativeDescription,
                    request.CorrectionReason,
                    operation.OperationId,
                    maintainedUtc);

                GovernedOperationService.FinalizeSuccessInTransaction(
                    connection,
                    transaction,
                    operation.OperationId);

                transaction.Commit();

                return maintenanceRecord;
            }
            catch (Exception exception)
            {
                DecisionRecord decisionRecord =
                    new(
                        IsSafeStopException(exception)
                            ? "CORRECT SAFE-STOP"
                            : "CORRECT TECHNICAL FAILURE",
                        exception.Message);

                if (IsSafeStopException(exception))
                {
                    GovernedOperationService.FinalizeSafeStop(
                        fullProjectRoot,
                        operation.OperationId,
                        decisionRecord);
                }
                else
                {
                    GovernedOperationService.FinalizeTechnicalFailure(
                        fullProjectRoot,
                        operation.OperationId,
                        decisionRecord);
                }

                throw;
            }
        }
        private static string? ResolveResultingValue(
        string? priorValue,
        SourceEvidenceMetadataMutation mutation)
        {
            return mutation.Action switch
            {
                SourceEvidenceMetadataMutationAction.Keep =>
                    priorValue,

                SourceEvidenceMetadataMutationAction.Set =>
                    mutation.Value,

                SourceEvidenceMetadataMutationAction.Clear =>
                    null,

                _ => throw new InvalidOperationException(
                    "SAFE-STOP: Source Evidence metadata mutation action is invalid.")
            };
        }
        private sealed record CurrentMetadataState(
    string? MetadataVersionId,
    int VersionNumber,
    string? DisplayLabel,
    string? AdministrativeDescription);
        private static void VerifyActiveSourceEvidence(
    string fullProjectRoot,
    EvidenceId evidenceId)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
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

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT LifecycleState
        FROM HLAS_Source_Evidence_Catalog
        WHERE EvidenceId = $evidenceId;
        """;

            command.Parameters.AddWithValue(
                "$evidenceId",
                evidenceId.Value.ToString("D"));

            object? result =
                command.ExecuteScalar();

            if (result is not string lifecycleState)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Source Evidence was not found in the governed catalog.");
            }

            if (!string.Equals(
                    lifecycleState,
                    "Active",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: CORRECT requires active Source Evidence.");
            }
        }
        private static CurrentMetadataState ReadCurrentMetadataState(
         string fullProjectRoot,
         EvidenceId evidenceId)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
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

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            MetadataVersionId,
            VersionNumber,
            DisplayLabel,
            AdministrativeDescription
        FROM HLAS_Source_Evidence_Metadata_Versions
        WHERE EvidenceId = $evidenceId
        ORDER BY VersionNumber DESC
        LIMIT 1;
        """;

            command.Parameters.AddWithValue(
                "$evidenceId",
                evidenceId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                return new CurrentMetadataState(
                    null,
                    0,
                    null,
                    null);
            }

            return new CurrentMetadataState(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.IsDBNull(2)
                    ? null
                    : reader.GetString(2),
                reader.IsDBNull(3)
                    ? null
                    : reader.GetString(3));
        }
        private static void InsertMaintenanceRecord(
    SqliteConnection connection,
    SqliteTransaction transaction,
    SourceEvidenceMaintenanceRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
        INSERT INTO HLAS_Source_Evidence_Maintenance
        (
            OperationId,
            FreezeId,
            PriorEvidenceId,
            ResultingEvidenceId,
            MaintenanceType,
            MaintainedUtc
        )
        VALUES
        (
            $operationId,
            $freezeId,
            $priorEvidenceId,
            $resultingEvidenceId,
            $maintenanceType,
            $maintainedUtc
        );
        """;

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$freezeId",
                record.FreezeId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$priorEvidenceId",
                record.PriorEvidenceId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$resultingEvidenceId",
                record.ResultingEvidenceId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$maintenanceType",
                record.MaintenanceType);

            command.Parameters.AddWithValue(
                "$maintainedUtc",
                record.MaintainedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }
        private static void InsertMaintenanceChangeRecord(
    SqliteConnection connection,
    SqliteTransaction transaction,
    SourceEvidenceMaintenanceChangeRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
        INSERT INTO HLAS_Source_Evidence_Maintenance_Changes
        (
            OperationId,
            ChangeSequence,
            FieldName,
            PriorValue,
            ResultingValue
        )
        VALUES
        (
            $operationId,
            $changeSequence,
            $fieldName,
            $priorValue,
            $resultingValue
        );
        """;

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$changeSequence",
                record.ChangeSequence);

            command.Parameters.AddWithValue(
                "$fieldName",
                record.FieldName);

            command.Parameters.AddWithValue(
                "$priorValue",
                (object?)record.PriorValue ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "$resultingValue",
                (object?)record.ResultingValue ?? DBNull.Value);

            command.ExecuteNonQuery();
        }
        private static void InsertMetadataVersion(
    SqliteConnection connection,
    SqliteTransaction transaction,
    EvidenceId evidenceId,
    CurrentMetadataState currentMetadata,
    string? resultingDisplayLabel,
    string? resultingAdministrativeDescription,
    string correctionReason,
    OperationId operationId,
    GovernedTimestamp versionedUtc)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
        INSERT INTO HLAS_Source_Evidence_Metadata_Versions
        (
            MetadataVersionId,
            EvidenceId,
            VersionNumber,
            PriorMetadataVersionId,
            DisplayLabel,
            AdministrativeDescription,
            CorrectionReason,
            OperationId,
            VersionedUtc
        )
        VALUES
        (
            $metadataVersionId,
            $evidenceId,
            $versionNumber,
            $priorMetadataVersionId,
            $displayLabel,
            $administrativeDescription,
            $correctionReason,
            $operationId,
            $versionedUtc
        );
        """;

            command.Parameters.AddWithValue(
                "$metadataVersionId",
                Guid.NewGuid().ToString("D"));

            command.Parameters.AddWithValue(
                "$evidenceId",
                evidenceId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$versionNumber",
                currentMetadata.VersionNumber + 1);

            command.Parameters.AddWithValue(
                "$priorMetadataVersionId",
                (object?)currentMetadata.MetadataVersionId ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "$displayLabel",
                (object?)resultingDisplayLabel ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "$administrativeDescription",
                (object?)resultingAdministrativeDescription ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "$correctionReason",
                correctionReason);

            command.Parameters.AddWithValue(
                "$operationId",
                operationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$versionedUtc",
                versionedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }
        private static SqliteConnection OpenDatabase(
    string fullProjectRoot)
        {
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

            return new SqliteConnection(
                builder.ToString());
        }
        private static bool IsSafeStopException(
    Exception exception)
        {
            return exception.Message.StartsWith(
                "SAFE-STOP:",
                StringComparison.Ordinal);
        }
    }
}