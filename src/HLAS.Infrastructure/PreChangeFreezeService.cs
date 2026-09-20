using System;
using System.IO;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class PreChangeFreezeService
    {
        public const string FrozenStatesDirectoryName =
            "HLAS_Frozen_States";

        public static FrozenStateRecord Create(
            string projectRoot,
            OperationId operationId,
            EvidenceId targetEvidenceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                projectRoot);

            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "OperationId may not be empty.",
                    nameof(operationId));
            }

            if (targetEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "TargetEvidenceId may not be empty.",
                    nameof(targetEvidenceId));
            }

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            _ = ProjectPackageReader.Open(
                fullProjectRoot);

            EvidenceCustodyRetrievedFile target =
                EvidenceCustodyRetrievalService.Retrieve(
                    fullProjectRoot,
                    targetEvidenceId);

            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            VerifyOpenOperation(
                databasePath,
                operationId);

            FreezeId freezeId =
                FreezeId.CreateNew();

            string freezeIdText =
                freezeId.Value.ToString("D");

            string relativeFreezePath =
                Path.Combine(
                    FrozenStatesDirectoryName,
                    freezeIdText,
                    target.EvidenceRecord.OriginalFileName);

            string frozenStatesRoot =
                Path.Combine(
                    fullProjectRoot,
                    FrozenStatesDirectoryName);

            string freezeDirectory =
                Path.Combine(
                    frozenStatesRoot,
                    freezeIdText);

            string freezeFilePath =
                Path.Combine(
                    freezeDirectory,
                    target.EvidenceRecord.OriginalFileName);

            bool freezeDirectoryCreated = false;

            try
            {
                Directory.CreateDirectory(
                    frozenStatesRoot);

                if (Directory.Exists(
                    freezeDirectory))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Generated FreezeId folder already exists.");
                }

                Directory.CreateDirectory(
                    freezeDirectory);

                freezeDirectoryCreated = true;

                File.Copy(
                    target.ControlledFilePath,
                    freezeFilePath,
                    overwrite: false);

                FileInfo frozenFile =
                    new(freezeFilePath);

                string frozenSha256 =
                    ComputeSha256(
                        freezeFilePath);

                if (frozenFile.Length !=
                        target.EvidenceRecord.FileSizeBytes ||
                    !string.Equals(
                        frozenSha256,
                        target.EvidenceRecord.Sha256Hex,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Pre-Change Freeze does not match governed Source Evidence.");
                }

                GovernedTimestamp frozenUtc =
                    GovernedTimestamp.CreateNow();

                FrozenStateRecord record =
                    new(
                        freezeId,
                        operationId,
                        targetEvidenceId,
                        FrozenStateRecord.PreChangeFreezeType,
                        relativeFreezePath,
                        frozenFile.Length,
                        frozenSha256,
                        frozenUtc);

                using SqliteConnection connection =
                    OpenDatabase(
                        databasePath);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                using SqliteCommand command =
                    connection.CreateCommand();

                command.Transaction =
                    transaction;

                command.CommandText =
                    """
                    INSERT INTO HLAS_Frozen_States
                        (
                            FreezeId,
                            OperationId,
                            TargetEvidenceId,
                            FreezeType,
                            RelativeFreezePath,
                            FileSizeBytes,
                            Sha256Hex,
                            FrozenUtc
                        )
                    VALUES
                        (
                            $freezeId,
                            $operationId,
                            $targetEvidenceId,
                            $freezeType,
                            $relativeFreezePath,
                            $fileSizeBytes,
                            $sha256Hex,
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
                    "$targetEvidenceId",
                    record.TargetEvidenceId.Value.ToString("D"));

                command.Parameters.AddWithValue(
                    "$freezeType",
                    record.FreezeType);

                command.Parameters.AddWithValue(
                    "$relativeFreezePath",
                    record.RelativeFreezePath);

                command.Parameters.AddWithValue(
                    "$fileSizeBytes",
                    record.FileSizeBytes);

                command.Parameters.AddWithValue(
                    "$sha256Hex",
                    record.Sha256Hex);

                command.Parameters.AddWithValue(
                    "$frozenUtc",
                    record.FrozenUtc.Value.ToString("O"));

                command.ExecuteNonQuery();

                transaction.Commit();

                return record;
            }
            catch
            {
                if (freezeDirectoryCreated &&
                    Directory.Exists(
                        freezeDirectory))
                {
                    Directory.Delete(
                        freezeDirectory,
                        recursive: true);
                }

                throw;
            }
        }

        private static void VerifyOpenOperation(
            string databasePath,
            OperationId operationId)
        {
            using SqliteConnection connection =
                OpenDatabase(
                    databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT COUNT(*)
                FROM HLAS_Governed_Operations
                WHERE
                    OperationId = $operationId
                    AND CompletedUtc IS NULL
                    AND Outcome IS NULL;
                """;

            command.Parameters.AddWithValue(
                "$operationId",
                operationId.Value.ToString("D"));

            long count =
                Convert.ToInt64(
                    command.ExecuteScalar());

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Pre-Change Freeze requires an open governed operation.");
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

        private static SqliteConnection OpenDatabase(
            string databasePath)
        {
            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

            return new SqliteConnection(
                builder.ToString());
        }
    }
}