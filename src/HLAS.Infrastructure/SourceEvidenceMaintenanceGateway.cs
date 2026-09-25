using System.IO;
using System.Security.Cryptography;
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
    public sealed record SourceEvidenceReplacementRequest
    {
        public string SelectedReplacementSourceFilePath { get; }
        public SourceEvidenceMetadataMutation DisplayLabel { get; }
        public SourceEvidenceMetadataMutation AdministrativeDescription { get; }
        public string ReplacementReason { get; }

        public SourceEvidenceReplacementRequest(
            string selectedReplacementSourceFilePath,
            SourceEvidenceMetadataMutation displayLabel,
            SourceEvidenceMetadataMutation administrativeDescription,
            string replacementReason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                selectedReplacementSourceFilePath);
            ArgumentNullException.ThrowIfNull(displayLabel);
            ArgumentNullException.ThrowIfNull(administrativeDescription);
            ArgumentException.ThrowIfNullOrWhiteSpace(replacementReason);

            SelectedReplacementSourceFilePath =
                selectedReplacementSourceFilePath;
            DisplayLabel = displayLabel;
            AdministrativeDescription = administrativeDescription;
            ReplacementReason = replacementReason;
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
        public static SourceEvidenceMaintenanceRecord Replace(
    string projectRoot,
    UserId userId,
    ProjectRole projectRole,
    EvidenceId priorEvidenceId,
    SourceEvidenceReplacementRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(request);

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            string fullReplacementSourceFilePath =
                Path.GetFullPath(
                    request.SelectedReplacementSourceFilePath);

            ProjectManifest manifest =
                ProjectPackageReader.Open(
                    fullProjectRoot);

            string priorSourceClass =
                ReadActiveSourceEvidenceSourceClass(
                    fullProjectRoot,
                    priorEvidenceId);

            if (!File.Exists(
                    fullReplacementSourceFilePath))
            {
                throw new FileNotFoundException(
                    "SAFE-STOP: The selected replacement Source Evidence file does not exist.",
                    fullReplacementSourceFilePath);
            }
            SafeStopIfReplacementMatchesPriorEvidence(
    fullProjectRoot,
    priorEvidenceId,
    fullReplacementSourceFilePath);
            CurrentMetadataState currentMetadata =
                ReadCurrentMetadataState(
                    fullProjectRoot,
                    priorEvidenceId);

            string? resultingDisplayLabel =
                ResolveResultingValue(
                    currentMetadata.DisplayLabel,
                    request.DisplayLabel);

            string? resultingAdministrativeDescription =
                ResolveResultingValue(
                    currentMetadata.AdministrativeDescription,
                    request.AdministrativeDescription);

            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    fullProjectRoot,
                    manifest.ProjectId,
                    userId,
                    SeriesId.V,
                    projectRole);

            EvidenceId resultingEvidenceId =
                EvidenceId.CreateNew();

            string resultingEvidenceDirectoryPath =
                Path.Combine(
                    fullProjectRoot,
                    EvidenceCustodyService.SourceEvidenceDirectoryName,
                    resultingEvidenceId.Value.ToString("D"));

            bool replacementCustodyPrepared = false;

            try
            {
                FrozenStateRecord freeze =
                    PreChangeFreezeService.Create(
                        fullProjectRoot,
                        operation.OperationId,
                        priorEvidenceId);

                EvidenceCustodyRecord successorCustody =
                    CreateReplacementCustodyCopy(
                        fullProjectRoot,
                        fullReplacementSourceFilePath,
                        resultingEvidenceId);

                replacementCustodyPrepared = true;

                GovernedTimestamp maintainedUtc =
                    GovernedTimestamp.CreateNow();

                SourceEvidenceMaintenanceRecord maintenanceRecord =
                    new(
                        operation.OperationId,
                        freeze.FreezeId,
                        priorEvidenceId,
                        resultingEvidenceId,
                        SourceEvidenceMaintenanceRecord.ReplaceMaintenanceType,
                        maintainedUtc,
                        request.ReplacementReason);

                using SqliteConnection connection =
                    OpenDatabase(fullProjectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertCustodyRecord(
                    connection,
                    transaction,
                    successorCustody);

                InsertCatalogRecord(
                    connection,
                    transaction,
                    resultingEvidenceId,
                    priorSourceClass,
                    maintainedUtc);

                MarkSourceEvidenceSuperseded(
                    connection,
                    transaction,
                    priorEvidenceId);

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

                CurrentMetadataState successorInitialMetadata =
                    new(
                        null,
                        0,
                        null,
                        null);

                InsertMetadataVersion(
                    connection,
                    transaction,
                    resultingEvidenceId,
                    successorInitialMetadata,
                    resultingDisplayLabel,
                    resultingAdministrativeDescription,
                    null,
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
                Exception recordedException =
                    exception;

                if (replacementCustodyPrepared &&
                    Directory.Exists(
                        resultingEvidenceDirectoryPath))
                {
                    try
                    {
                        Directory.Delete(
                            resultingEvidenceDirectoryPath,
                            recursive: true);
                    }
                    catch (Exception cleanupException)
                    {
                        recordedException =
                            new InvalidOperationException(
                                "REPLACE technical failure: failed-attempt successor custody cleanup could not be completed safely.",
                                new AggregateException(
                                    exception,
                                    cleanupException));
                    }
                }

                DecisionRecord decisionRecord =
                    new(
                        IsSafeStopException(recordedException)
                            ? "REPLACE SAFE-STOP"
                            : "REPLACE TECHNICAL FAILURE",
                        recordedException.Message);

                if (IsSafeStopException(recordedException))
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

                if (!ReferenceEquals(
                        recordedException,
                        exception))
                {
                    throw recordedException;
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
    private static string ReadActiveSourceEvidenceSourceClass(
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
            SourceClass,
            LifecycleState
        FROM HLAS_Source_Evidence_Catalog
        WHERE EvidenceId = $evidenceId;
        """;

        command.Parameters.AddWithValue(
            "$evidenceId",
            evidenceId.Value.ToString("D"));

        using SqliteDataReader reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "SAFE-STOP: Source Evidence was not found in the governed catalog.");
        }

        string sourceClass =
            reader.GetString(0);

        string lifecycleState =
            reader.GetString(1);

        if (!string.Equals(
                lifecycleState,
                "Active",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "SAFE-STOP: REPLACE requires active Source Evidence.");
        }

        return sourceClass;
    }
        private static void SafeStopIfReplacementMatchesPriorEvidence(
    string fullProjectRoot,
    EvidenceId priorEvidenceId,
    string fullReplacementSourceFilePath)
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
            FileSizeBytes,
            Sha256Hex
        FROM HLAS_Evidence_Custody
        WHERE EvidenceId = $evidenceId;
        """;

            command.Parameters.AddWithValue(
                "$evidenceId",
                priorEvidenceId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Prior Source Evidence custody record was not found.");
            }

            long priorFileSize =
                reader.GetInt64(0);

            string priorSha256 =
                reader.GetString(1);

            FileInfo replacementFile =
                new(fullReplacementSourceFilePath);

            if (replacementFile.Length != priorFileSize)
            {
                return;
            }

            string replacementSha256 =
                ComputeSha256(
                    fullReplacementSourceFilePath);

            if (string.Equals(
                    replacementSha256,
                    priorSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: REPLACE requires a physically different Source Evidence file.");
            }
        }
        private static EvidenceCustodyRecord CreateReplacementCustodyCopy(
        string fullProjectRoot,
        string fullReplacementSourceFilePath,
        EvidenceId resultingEvidenceId)
    {
        string originalFileName =
            Path.GetFileName(
                fullReplacementSourceFilePath);

        if (string.IsNullOrWhiteSpace(
                originalFileName))
        {
            throw new InvalidOperationException(
                "SAFE-STOP: The selected replacement Source Evidence filename is invalid.");
        }

        long sourceFileSize;

        using (FileStream sourceReadStream = new(
            fullReplacementSourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read))
        {
            sourceFileSize =
                sourceReadStream.Length;
        }

        string sourceSha256 =
            ComputeSha256(
                fullReplacementSourceFilePath);

        string evidenceIdText =
            resultingEvidenceId.Value.ToString("D");

        string relativeCustodyPath =
            Path.Combine(
                EvidenceCustodyService.SourceEvidenceDirectoryName,
                evidenceIdText,
                originalFileName);

        string sourceEvidenceRootPath =
            Path.Combine(
                fullProjectRoot,
                EvidenceCustodyService.SourceEvidenceDirectoryName);

        string evidenceDirectoryPath =
            Path.Combine(
                sourceEvidenceRootPath,
                evidenceIdText);

        string custodyFilePath =
            Path.Combine(
                evidenceDirectoryPath,
                originalFileName);

        bool evidenceDirectoryCreated = false;

        try
        {
            Directory.CreateDirectory(
                sourceEvidenceRootPath);

            if (Directory.Exists(
                    evidenceDirectoryPath))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The generated replacement EvidenceId custody folder already exists.");
            }

            Directory.CreateDirectory(
                evidenceDirectoryPath);

            evidenceDirectoryCreated = true;

            File.Copy(
                fullReplacementSourceFilePath,
                custodyFilePath,
                overwrite: false);

            FileInfo custodyFile =
                new(custodyFilePath);

            string custodySha256 =
                ComputeSha256(
                    custodyFilePath);

            if (sourceFileSize != custodyFile.Length ||
                !string.Equals(
                    sourceSha256,
                    custodySha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The replacement controlled custody copy does not match the selected official source.");
            }

            return new EvidenceCustodyRecord(
                resultingEvidenceId,
                originalFileName,
                relativeCustodyPath,
                custodyFile.Length,
                custodySha256,
                GovernedTimestamp.CreateNow());
        }
        catch (Exception exception)
        {
            if (evidenceDirectoryCreated &&
                Directory.Exists(
                    evidenceDirectoryPath))
            {
                try
                {
                    Directory.Delete(
                        evidenceDirectoryPath,
                        recursive: true);
                }
                catch (Exception cleanupException)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Replacement Source Evidence custody failed and failed-attempt cleanup could not be completed safely.",
                        new AggregateException(
                            exception,
                            cleanupException));
                }
            }

            throw;
        }
    }

    private static void InsertCustodyRecord(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EvidenceCustodyRecord record)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
        INSERT INTO HLAS_Evidence_Custody
        (
            EvidenceId,
            OriginalFileName,
            RelativeCustodyPath,
            FileSizeBytes,
            Sha256Hex,
            AcceptedUtc
        )
        VALUES
        (
            $evidenceId,
            $originalFileName,
            $relativeCustodyPath,
            $fileSizeBytes,
            $sha256Hex,
            $acceptedUtc
        );
        """;

        command.Parameters.AddWithValue(
            "$evidenceId",
            record.EvidenceId.Value.ToString("D"));

        command.Parameters.AddWithValue(
            "$originalFileName",
            record.OriginalFileName);

        command.Parameters.AddWithValue(
            "$relativeCustodyPath",
            record.RelativeCustodyPath);

        command.Parameters.AddWithValue(
            "$fileSizeBytes",
            record.FileSizeBytes);

        command.Parameters.AddWithValue(
            "$sha256Hex",
            record.Sha256Hex);

        command.Parameters.AddWithValue(
            "$acceptedUtc",
            record.AcceptedUtc.Value.ToString("O"));

        command.ExecuteNonQuery();
    }

    private static void InsertCatalogRecord(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EvidenceId evidenceId,
        string sourceClass,
        GovernedTimestamp catalogedUtc)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
        INSERT INTO HLAS_Source_Evidence_Catalog
        (
            EvidenceId,
            SourceClass,
            CatalogedUtc
        )
        VALUES
        (
            $evidenceId,
            $sourceClass,
            $catalogedUtc
        );
        """;

        command.Parameters.AddWithValue(
            "$evidenceId",
            evidenceId.Value.ToString("D"));

        command.Parameters.AddWithValue(
            "$sourceClass",
            sourceClass);

        command.Parameters.AddWithValue(
            "$catalogedUtc",
            catalogedUtc.Value.ToString("O"));

        command.ExecuteNonQuery();
    }

    private static void MarkSourceEvidenceSuperseded(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EvidenceId priorEvidenceId)
    {
        using SqliteCommand command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
        UPDATE HLAS_Source_Evidence_Catalog
        SET LifecycleState = 'Superseded'
        WHERE
            EvidenceId = $evidenceId
            AND LifecycleState = 'Active';
        """;

        command.Parameters.AddWithValue(
            "$evidenceId",
            priorEvidenceId.Value.ToString("D"));

        int changedRows =
            command.ExecuteNonQuery();

        if (changedRows != 1)
        {
            throw new InvalidOperationException(
                "SAFE-STOP: REPLACE could not supersede the prior active Source Evidence safely.");
        }
    }

    private static string ComputeSha256(
        string filePath)
    {
        using FileStream stream =
            new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        using SHA256 hasher =
            SHA256.Create();

        byte[] hash =
            hasher.ComputeHash(
                stream);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
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
        ReplacementReason,
        MaintainedUtc
        )
        VALUES
        (
            $operationId,
            $freezeId,
            $priorEvidenceId,
            $resultingEvidenceId,
            $maintenanceType,
        $replacementReason,
        $maintainedUtc
        );
        """;
            command.Parameters.AddWithValue(
    "$replacementReason",
    (object?)record.ReplacementReason ?? DBNull.Value);
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
    string? correctionReason,
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
(object?)correctionReason ?? DBNull.Value);

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