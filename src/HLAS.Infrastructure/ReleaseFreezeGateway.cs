using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ReleaseFreezeGateway
    {
        public const string ReleaseFreezeOperationKind =
            "V_SERIES_RELEASE_FREEZE";
        private sealed record ReleaseFreezeStage(
    FreezeId FreezeId,
    string StagingDirectory,
    string FinalDirectory,
    string RelativeCheckpointPath,
    long DatabaseFileSizeBytes,
    string DatabaseSha256Hex,
    long ProjectManifestFileSizeBytes,
    string ProjectManifestSha256Hex,
    IReadOnlyList<ReleaseFreezeManifestEvidenceEntry> Evidence,
    GovernedTimestamp FrozenUtc);
        public static HistoricalCheckpointRecord CreateReleaseFreeze(
    string projectRoot,
    UserId userId,
    ProjectRole projectRole,
    ReleaseId releaseId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            if (projectRole != ProjectRole.Senior &&
                projectRole != ProjectRole.Admin)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze requires Senior or Admin authority.");
            }

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            ProjectManifest projectManifest =
                ProjectPackageReader.Open(fullProjectRoot);

            ReadinessCheckId readinessCheckId =
                VerifyReleaseBasis(
                    fullProjectRoot,
                    projectManifest.ProjectId,
                    releaseId);

            ReleaseFreezeStage stage =
                PrepareReleaseFreezeStage(
                    fullProjectRoot);

            GovernedOperationRecord? operation =
                null;

            try
            {
                ReadinessCheckId reverifiedReadinessCheckId =
                    VerifyReleaseBasis(
                        fullProjectRoot,
                        projectManifest.ProjectId,
                        releaseId);

                if (reverifiedReadinessCheckId != readinessCheckId)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Release basis changed during Release Freeze preparation.");
                }

                operation =
                    GovernedOperationService.Begin(
                        fullProjectRoot,
                        projectManifest.ProjectId,
                        userId,
                        SeriesId.V,
                        projectRole,
                        ReleaseFreezeOperationKind);

                return CreateReleaseFreezeCore(
                    fullProjectRoot,
                    projectManifest.ProjectId,
                    operation.OperationId,
                    releaseId,
                    readinessCheckId,
                    stage);
            }
            catch (Exception exception)
            {
                if (Directory.Exists(stage.StagingDirectory))
                {
                    Directory.Delete(
                        stage.StagingDirectory,
                        recursive: true);
                }

                if (operation is not null)
                {
                    DecisionRecord decisionRecord =
                        new(
                            exception.Message.StartsWith(
                                "SAFE-STOP:",
                                StringComparison.Ordinal)
                                ? "RELEASE FREEZE SAFE-STOP"
                                : "RELEASE FREEZE TECHNICAL FAILURE",
                            exception.Message);

                    if (exception.Message.StartsWith(
                            "SAFE-STOP:",
                            StringComparison.Ordinal))
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
                }

                throw;
            }
        }

        private static HistoricalCheckpointRecord CreateReleaseFreezeCore(
     string fullProjectRoot,
     ProjectId projectId,
     OperationId operationId,
     ReleaseId releaseId,
     ReadinessCheckId readinessCheckId,
     ReleaseFreezeStage stage)
        {
            (
                string relativeManifestPath,
                string manifestSha256Hex) =
                WriteReleaseFreezeManifest(
                    projectId,
                    operationId,
                    releaseId,
                    readinessCheckId,
                    stage);

            HistoricalCheckpointRecord record =
                new(
                    stage.FreezeId,
                    operationId,
                    HistoricalCheckpointRecord.ReleaseFreezeType,
                    releaseId,
                    stage.RelativeCheckpointPath,
                    relativeManifestPath,
                    manifestSha256Hex,
                    stage.FrozenUtc);

            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

            bool finalDirectoryCreated =
                false;

            try
            {
                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertHistoricalCheckpoint(
                    connection,
                    transaction,
                    record);

                GovernedOperationService.FinalizeSuccessInTransaction(
                    connection,
                    transaction,
                    operationId);

                Directory.Move(
                    stage.StagingDirectory,
                    stage.FinalDirectory);

                finalDirectoryCreated =
                    true;

                VerifyFinalCheckpointFiles(
     fullProjectRoot,
     stage,
     relativeManifestPath,
     manifestSha256Hex);

                transaction.Commit();

                return record;
            }
            catch
            {
                if (finalDirectoryCreated &&
                    Directory.Exists(stage.FinalDirectory))
                {
                    Directory.Delete(
                        stage.FinalDirectory,
                        recursive: true);
                }
                else if (Directory.Exists(stage.StagingDirectory))
                {
                    Directory.Delete(
                        stage.StagingDirectory,
                        recursive: true);
                }

                throw;
            }
        }
        private static void VerifyFinalCheckpointFiles(
    string fullProjectRoot,
    ReleaseFreezeStage stage,
    string relativeManifestPath,
    string manifestSha256Hex)
        {
            string finalDatabasePath =
                Path.Combine(
                    stage.FinalDirectory,
                    ProjectPackageCreator.DatabaseFileName);

            string finalProjectManifestPath =
                Path.Combine(
                    stage.FinalDirectory,
                    ProjectPackageCreator.ManifestFileName);

            string finalReleaseManifestPath =
                Path.Combine(
                    fullProjectRoot,
                    relativeManifestPath);

            if (!File.Exists(finalDatabasePath) ||
                new FileInfo(finalDatabasePath).Length != stage.DatabaseFileSizeBytes ||
                !string.Equals(
                    ComputeSha256(finalDatabasePath),
                    stage.DatabaseSha256Hex,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze database snapshot failed final integrity verification.");
            }

            if (!File.Exists(finalProjectManifestPath) ||
                new FileInfo(finalProjectManifestPath).Length != stage.ProjectManifestFileSizeBytes ||
                !string.Equals(
                    ComputeSha256(finalProjectManifestPath),
                    stage.ProjectManifestSha256Hex,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze project manifest failed final integrity verification.");
            }

            if (!File.Exists(finalReleaseManifestPath) ||
                !string.Equals(
                    ComputeSha256(finalReleaseManifestPath),
                    manifestSha256Hex,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release Freeze manifest failed final integrity verification.");
            }
        }
        private static void InsertHistoricalCheckpoint(
    SqliteConnection connection,
    SqliteTransaction transaction,
    HistoricalCheckpointRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction =
                transaction;

            command.CommandText =
                """
        INSERT INTO HLAS_Historical_Checkpoints
        (
            FreezeId,
            OperationId,
            CheckpointType,
            ReleaseId,
            RelativeCheckpointPath,
            ManifestRelativePath,
            ManifestSha256Hex,
            FrozenUtc
        )
        VALUES
        (
            $freezeId,
            $operationId,
            $checkpointType,
            $releaseId,
            $relativeCheckpointPath,
            $manifestRelativePath,
            $manifestSha256Hex,
            $frozenUtc
        );
        """;

            command.Parameters.AddWithValue(
                "$freezeId",
                record.FreezeId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$checkpointType",
                record.CheckpointType);

            command.Parameters.AddWithValue(
                "$releaseId",
                record.ReleaseId!.Value.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$relativeCheckpointPath",
                record.RelativeCheckpointPath);

            command.Parameters.AddWithValue(
                "$manifestRelativePath",
                record.ManifestRelativePath);

            command.Parameters.AddWithValue(
                "$manifestSha256Hex",
                record.ManifestSha256Hex);

            command.Parameters.AddWithValue(
                "$frozenUtc",
                record.FrozenUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }
        private static ReleaseFreezeStage PrepareReleaseFreezeStage(
    string fullProjectRoot)
        {
            FreezeId freezeId =
                FreezeId.CreateNew();

            string freezeIdText =
                freezeId.Value.ToString("D");

            string frozenStatesRoot =
                Path.Combine(
                    fullProjectRoot,
                    PreChangeFreezeService.FrozenStatesDirectoryName);

            string stagingDirectory =
                Path.Combine(
                    frozenStatesRoot,
                    ".staging-" + freezeIdText);

            string finalDirectory =
                Path.Combine(
                    frozenStatesRoot,
                    freezeIdText);

            string relativeCheckpointPath =
                Path.Combine(
                    PreChangeFreezeService.FrozenStatesDirectoryName,
                    freezeIdText);

            if (Directory.Exists(stagingDirectory) ||
                Directory.Exists(finalDirectory))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Generated Release Freeze folder already exists.");
            }

            Directory.CreateDirectory(
                frozenStatesRoot);

            Directory.CreateDirectory(
                stagingDirectory);

            try
            {
                string sourceDatabasePath =
                    Path.Combine(
                        fullProjectRoot,
                        ProjectPackageCreator.DatabaseFileName);

                string snapshotDatabasePath =
                    Path.Combine(
                        stagingDirectory,
                        ProjectPackageCreator.DatabaseFileName);

                CreateDatabaseSnapshot(
                    sourceDatabasePath,
                    snapshotDatabasePath);

                string sourceManifestPath =
                    Path.Combine(
                        fullProjectRoot,
                        ProjectPackageCreator.ManifestFileName);

                string snapshotManifestPath =
                    Path.Combine(
                        stagingDirectory,
                        ProjectPackageCreator.ManifestFileName);

                File.Copy(
                    sourceManifestPath,
                    snapshotManifestPath,
                    overwrite: false);

                FileInfo databaseFile =
                    new(snapshotDatabasePath);

                FileInfo manifestFile =
                    new(snapshotManifestPath);

                IReadOnlyList<ReleaseFreezeManifestEvidenceEntry> evidence =
                    ReadAndVerifyEvidenceBasis(
                        fullProjectRoot);

                return new ReleaseFreezeStage(
                    freezeId,
                    stagingDirectory,
                    finalDirectory,
                    relativeCheckpointPath,
                    databaseFile.Length,
                    ComputeSha256(snapshotDatabasePath),
                    manifestFile.Length,
                    ComputeSha256(snapshotManifestPath),
                    evidence,
                    GovernedTimestamp.CreateNow());
            }
            catch
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(
                        stagingDirectory,
                        recursive: true);
                }

                throw;
            }
        }
        private static (
    string RelativeManifestPath,
    string ManifestSha256Hex)
    WriteReleaseFreezeManifest(
        ProjectId projectId,
        OperationId operationId,
        ReleaseId releaseId,
        ReadinessCheckId readinessCheckId,
        ReleaseFreezeStage stage)
        {
            const string manifestFileName =
                "release-freeze-manifest.json";

            string manifestPath =
                Path.Combine(
                    stage.StagingDirectory,
                    manifestFileName);

            ReleaseFreezeManifest manifest =
                new(
                    stage.FreezeId.Value.ToString("D"),
                    HistoricalCheckpointRecord.ReleaseFreezeType,
                    projectId.Value.ToString("D"),
                    releaseId.Value.ToString("D"),
                    readinessCheckId.Value.ToString("D"),
                    operationId.Value.ToString("D"),
                    stage.FrozenUtc.Value.ToString("O"),
                    ProjectPackageCreator.DatabaseFileName,
                    stage.DatabaseFileSizeBytes,
                    stage.DatabaseSha256Hex,
                    ProjectPackageCreator.ManifestFileName,
                    stage.ProjectManifestFileSizeBytes,
                    stage.ProjectManifestSha256Hex,
                    stage.Evidence);

            JsonSerializerOptions options =
                new()
                {
                    WriteIndented = true
                };

            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(
                    manifest,
                    options));

            string relativeManifestPath =
                Path.Combine(
                    stage.RelativeCheckpointPath,
                    manifestFileName);

            return (
                relativeManifestPath,
                ComputeSha256(manifestPath));
        }
        private static IReadOnlyList<ReleaseFreezeManifestEvidenceEntry>
    ReadAndVerifyEvidenceBasis(
        string fullProjectRoot)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            List<ReleaseFreezeManifestEvidenceEntry> evidence =
                new();

            SqliteConnectionStringBuilder builder =
                new()
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
            EvidenceId,
            RelativeCustodyPath,
            FileSizeBytes,
            Sha256Hex
        FROM HLAS_Evidence_Custody
        ORDER BY AcceptedUtc, EvidenceId;
        """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            while (reader.Read())
            {
                string evidenceId =
                    reader.GetString(0);

                string relativeCustodyPath =
                    reader.GetString(1);

                long fileSizeBytes =
                    reader.GetInt64(2);

                string sha256Hex =
                    reader.GetString(3);

                string controlledFilePath =
                    Path.GetFullPath(
                        Path.Combine(
                            fullProjectRoot,
                            relativeCustodyPath));
                string projectRootPrefix =
    fullProjectRoot.TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar)
    + Path.DirectorySeparatorChar;

                if (!controlledFilePath.StartsWith(
                        projectRootPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Release Freeze Source Evidence path leaves the HLAS project root.");
                }
                if (!File.Exists(controlledFilePath))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Release Freeze Source Evidence is missing.");
                }

                FileInfo file =
                    new(controlledFilePath);

                string actualSha256 =
                    ComputeSha256(controlledFilePath);

                if (file.Length != fileSizeBytes ||
                    !string.Equals(
                        actualSha256,
                        sha256Hex,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Release Freeze Source Evidence failed integrity verification.");
                }

                evidence.Add(
                    new ReleaseFreezeManifestEvidenceEntry(
                        evidenceId,
                        relativeCustodyPath,
                        fileSizeBytes,
                        sha256Hex));
            }

            return evidence;
        }
        private static void CreateDatabaseSnapshot(
     string sourceDatabasePath,
     string snapshotDatabasePath)
        {
            SqliteConnectionStringBuilder sourceBuilder =
                new()
                {
                    DataSource = sourceDatabasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };

            SqliteConnectionStringBuilder snapshotBuilder =
                new()
                {
                    DataSource = snapshotDatabasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                };

            using SqliteConnection sourceConnection =
                new(sourceBuilder.ToString());

            using SqliteConnection snapshotConnection =
                new(snapshotBuilder.ToString());

            sourceConnection.Open();
            snapshotConnection.Open();

            sourceConnection.BackupDatabase(
                snapshotConnection);
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
                hasher.ComputeHash(stream);

            return Convert
                .ToHexString(hash)
                .ToLowerInvariant();
        }
        private static ReadinessCheckId VerifyReleaseBasis(
    string fullProjectRoot,
    ProjectId projectId,
    
    ReleaseId releaseId)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
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
            release.ReadinessCheckId,
            release.OperationId,
            readiness.EvaluatedUtc
        FROM HLAS_Release_Records AS release
        INNER JOIN HLAS_Governed_Operations AS releaseOperation
            ON releaseOperation.OperationId = release.OperationId
        INNER JOIN HLAS_Readiness_Checks AS readiness
            ON readiness.ReadinessCheckId = release.ReadinessCheckId
        INNER JOIN HLAS_Governed_Operations AS readinessOperation
            ON readinessOperation.OperationId = readiness.OperationId
        WHERE
            release.ReleaseId = $releaseId
            AND releaseOperation.ProjectId = $projectId
            AND releaseOperation.SeriesId = 'V'
            AND releaseOperation.CompletedUtc IS NOT NULL
            AND releaseOperation.Outcome = 'SUCCESS'
            AND readiness.OverallStatus = 'READY'
            AND readinessOperation.ProjectId = $projectId
            AND readinessOperation.SeriesId = 'V'
            AND readinessOperation.CompletedUtc IS NOT NULL
            AND readinessOperation.Outcome = 'SUCCESS'
            AND
            (
                SELECT COUNT(*)
                FROM HLAS_Readiness_Check_Items AS item
                WHERE item.ReadinessCheckId = readiness.ReadinessCheckId
            ) = 7
            AND NOT EXISTS
            (
                SELECT 1
                FROM HLAS_Readiness_Check_Items AS item
                WHERE
                    item.ReadinessCheckId = readiness.ReadinessCheckId
                    AND item.GateStatus <> 'PASS'
            )
            AND NOT EXISTS
            (
                SELECT 1
                FROM HLAS_Historical_Checkpoints AS checkpoint
                WHERE checkpoint.ReleaseId = release.ReleaseId
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
                    "SAFE-STOP: Release Freeze requires one valid unfrozen Release basis.");
            }

            ReadinessCheckId readinessCheckId =
                new(
                    Guid.Parse(
                        reader.GetString(0)));

            string releaseOperationId =
                reader.GetString(1);

            string evaluatedUtc =
                reader.GetString(2);

            reader.Close();

            using SqliteCommand interveningCommand =
                connection.CreateCommand();

            interveningCommand.CommandText =
                """
        SELECT COUNT(*)
        FROM HLAS_Governed_Operations
        WHERE
            ProjectId = $projectId
            AND StartedUtc > $evaluatedUtc
            AND OperationId <> $releaseOperationId
            AND
        (
            CompletedUtc IS NULL
            OR Outcome = 'SUCCESS'
        )
        """;


            interveningCommand.Parameters.AddWithValue(
    "$projectId",
    projectId.Value.ToString("D"));
            interveningCommand.Parameters.AddWithValue(
                "$evaluatedUtc",
                evaluatedUtc);

            interveningCommand.Parameters.AddWithValue(
                "$releaseOperationId",
                releaseOperationId);

           

            long interveningCount =
                Convert.ToInt64(
                    interveningCommand.ExecuteScalar());

            if (interveningCount != 0)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release basis can no longer be frozen exactly.");
            }

            return readinessCheckId;
        }
    }
}