using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public sealed record PreChangeFreezeRetrievedFile(
        FrozenStateRecord FrozenState,
        string FrozenFilePath);

    public static class PreChangeFreezeRetrievalService
    {
        public static PreChangeFreezeRetrievedFile Retrieve(
            string projectRoot,
            FreezeId freezeId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                projectRoot);

            if (freezeId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "FreezeId may not be empty.",
                    nameof(freezeId));
            }

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            _ = ProjectPackageReader.Open(
                fullProjectRoot);

            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            FrozenStateRecord record =
                ReadFrozenState(
                    databasePath,
                    freezeId);

            string frozenFilePath =
                Path.GetFullPath(
                    Path.Combine(
                        fullProjectRoot,
                        record.RelativeFreezePath));

            string projectRootPrefix =
                fullProjectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!frozenFilePath.StartsWith(
                projectRootPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Governed Frozen State path leaves the HLAS project root.");
            }

            if (!File.Exists(
                frozenFilePath))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Governed Frozen State file is missing.");
            }

            FileInfo frozenFile =
                new(frozenFilePath);

            string actualSha256 =
                ComputeSha256(
                    frozenFilePath);

            if (frozenFile.Length !=
                    record.FileSizeBytes ||
                !string.Equals(
                    actualSha256,
                    record.Sha256Hex,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Governed Frozen State file failed integrity verification.");
            }

            return new PreChangeFreezeRetrievedFile(
                record,
                frozenFilePath);
        }

        private static FrozenStateRecord ReadFrozenState(
            string databasePath,
            FreezeId freezeId)
        {
            using SqliteConnection connection =
                OpenDatabase(
                    databasePath);

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT
                    OperationId,
                    TargetEvidenceId,
                    FreezeType,
                    RelativeFreezePath,
                    FileSizeBytes,
                    Sha256Hex,
                    FrozenUtc
                FROM HLAS_Frozen_States
                WHERE FreezeId = $freezeId;
                """;

            command.Parameters.AddWithValue(
                "$freezeId",
                freezeId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Governed Frozen State record was not found.");
            }

            OperationId operationId =
                new(
                    Guid.Parse(
                        reader.GetString(0)));

            EvidenceId targetEvidenceId =
                new(
                    Guid.Parse(
                        reader.GetString(1)));

            string freezeType =
                reader.GetString(2);

            string relativeFreezePath =
                reader.GetString(3);

            long fileSizeBytes =
                reader.GetInt64(4);

            string sha256Hex =
                reader.GetString(5);

            DateTimeOffset frozenUtcValue =
                DateTimeOffset.Parse(
                    reader.GetString(6),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);

            GovernedTimestamp frozenUtc =
                GovernedTimestamp.FromRecorded(
                    frozenUtcValue);

            return new FrozenStateRecord(
                freezeId,
                operationId,
                targetEvidenceId,
                freezeType,
                relativeFreezePath,
                fileSizeBytes,
                sha256Hex,
                frozenUtc);
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