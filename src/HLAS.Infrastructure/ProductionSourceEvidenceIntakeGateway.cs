using System;
using System.IO;
using System.Security.Cryptography;

using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProductionSourceEvidenceIntakeGateway
    {
        private const string ProductionSourceClass =
            "PRODUCTION";

    public static EvidenceCustodyRecord Accept(
        string projectRoot,
            string sourceFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            string fullSourceFilePath =
                Path.GetFullPath(sourceFilePath);

            _ = ProjectPackageReader.Open(fullProjectRoot);

            if (!File.Exists(fullSourceFilePath))
            {
                throw new FileNotFoundException(
                    "SAFE-STOP: The selected source file does not exist.",
                    fullSourceFilePath);
            }

            string originalFileName =
                Path.GetFileName(fullSourceFilePath);

            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The selected source filename is invalid.");
            }

            long sourceFileSize;
            string sourceSha256;

            using (FileStream sourceReadStream = new(
                fullSourceFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                sourceFileSize =
                    sourceReadStream.Length;

                using SHA256 sourceHasher =
                    SHA256.Create();

                byte[] sourceHash =
                    sourceHasher.ComputeHash(
                        sourceReadStream);

                sourceSha256 =
                    Convert.ToHexString(sourceHash)
                        .ToLowerInvariant();
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

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            SafeStopIfDuplicate(
                connection,
                sourceFileSize,
                sourceSha256);

            EvidenceId evidenceId =
                EvidenceId.CreateNew();

            string evidenceIdText =
                evidenceId.Value.ToString("D");

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

                if (Directory.Exists(evidenceDirectoryPath))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: The generated EvidenceId custody folder already exists.");
                }

                Directory.CreateDirectory(
                    evidenceDirectoryPath);

                evidenceDirectoryCreated = true;

                File.Copy(
                    fullSourceFilePath,
                    custodyFilePath,
                    overwrite: false);

                FileInfo custodyFileInfo =
                    new(custodyFilePath);

                long custodyFileSize =
                    custodyFileInfo.Length;

                string custodySha256;

                using (FileStream custodyReadStream = new(
                    custodyFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    using SHA256 custodyHasher =
                        SHA256.Create();

                    byte[] custodyHash =
                        custodyHasher.ComputeHash(
                            custodyReadStream);

                    custodySha256 =
                        Convert.ToHexString(custodyHash)
                            .ToLowerInvariant();
                }

                if (sourceFileSize != custodyFileSize ||
                    !string.Equals(
                        sourceSha256,
                        custodySha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: The controlled custody copy does not match the selected original.");
                }

                GovernedTimestamp acceptedUtc =
                    GovernedTimestamp.CreateNow();

                EvidenceCustodyRecord record = new(
                    evidenceId,
                    originalFileName,
                    relativeCustodyPath,
                    custodyFileSize,
                    custodySha256,
                    acceptedUtc);

                GovernedTimestamp catalogedUtc =
                    GovernedTimestamp.CreateNow();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertCustodyRecord(
                    connection,
                    transaction,
                    record);

                InsertCatalogRecord(
                    connection,
                    transaction,
                    record.EvidenceId,
                    catalogedUtc);

                transaction.Commit();

                return record;
            }
            catch (Exception exception)
            {
                if (evidenceDirectoryCreated &&
                    Directory.Exists(evidenceDirectoryPath))
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
                            "SAFE-STOP: Production Source Evidence intake failed and failed-attempt cleanup could not be completed safely.",
                            new AggregateException(
                                exception,
                                cleanupException));
                    }
                }

                throw;
            }
        }

        private static void SafeStopIfDuplicate(
            SqliteConnection connection,
            long sourceFileSize,
            string sourceSha256)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT COUNT(*)
                FROM HLAS_Source_Evidence_Catalog AS catalog
                INNER JOIN HLAS_Evidence_Custody AS custody
                    ON custody.EvidenceId = catalog.EvidenceId
                WHERE
                    catalog.SourceClass = $sourceClass
                    AND custody.FileSizeBytes = $fileSizeBytes
                    AND lower(custody.Sha256Hex) = lower($sha256Hex);
                """;

            command.Parameters.AddWithValue(
                "$sourceClass",
                ProductionSourceClass);

            command.Parameters.AddWithValue(
                "$fileSizeBytes",
                sourceFileSize);

            command.Parameters.AddWithValue(
                "$sha256Hex",
                sourceSha256);

            long duplicateCount =
                Convert.ToInt64(
                    command.ExecuteScalar());

            if (duplicateCount > 0)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: This Production Source Evidence is already in governed custody.");
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
                ProductionSourceClass);

            command.Parameters.AddWithValue(
                "$catalogedUtc",
                catalogedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }
    }
}