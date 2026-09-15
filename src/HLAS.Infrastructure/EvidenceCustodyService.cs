using System;
using System.IO;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class EvidenceCustodyService
    {
        public const string SourceEvidenceDirectoryName =
            "HLAS_Source_Evidence";

        public static EvidenceCustodyRecord Accept(
            string projectRoot,
            string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "Project root may not be blank.",
                    nameof(projectRoot));
            }

            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                throw new ArgumentException(
                    "Source file path may not be blank.",
                    nameof(sourceFilePath));
            }

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            string fullSourceFilePath =
                Path.GetFullPath(sourceFilePath);

            // Prove the HLAS package is valid and at the
            // currently supported governed database schema.
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

            EvidenceId evidenceId =
                EvidenceId.CreateNew();

            string evidenceIdText =
                evidenceId.Value.ToString("D");

            string relativeCustodyPath =
                Path.Combine(
                    SourceEvidenceDirectoryName,
                    evidenceIdText,
                    originalFileName);

            string sourceEvidenceRootPath =
                Path.Combine(
                    fullProjectRoot,
                    SourceEvidenceDirectoryName);

            string evidenceDirectoryPath =
                Path.Combine(
                    sourceEvidenceRootPath,
                    evidenceIdText);

            string custodyFilePath =
                Path.Combine(
                    evidenceDirectoryPath,
                    originalFileName);

            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

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

                long sourceFileSize;
                string sourceSha256;

                using (FileStream sourceStream = new(
                    fullSourceFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    sourceFileSize =
                        sourceStream.Length;

                    using SHA256 sourceHasher =
                        SHA256.Create();

                    byte[] sourceHash =
                        sourceHasher.ComputeHash(
                            sourceStream);

                    sourceSha256 =
                        Convert.ToHexString(sourceHash)
                            .ToLowerInvariant();

                    sourceStream.Position = 0;

                    using FileStream custodyStream = new(
                        custodyFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);

                    sourceStream.CopyTo(
                        custodyStream);

                    custodyStream.Flush(
                        flushToDisk: true);
                }

                FileInfo custodyFileInfo =
                    new(custodyFilePath);

                long custodyFileSize =
                    custodyFileInfo.Length;

                string custodySha256;

                using (FileStream custodyReadStream =
                    new(
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

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using SqliteConnection connection =
                    new(builder.ToString());

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                using SqliteCommand insertCustodyRecord =
                    connection.CreateCommand();

                insertCustodyRecord.Transaction =
                    transaction;

                insertCustodyRecord.CommandText =
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

                insertCustodyRecord.Parameters.AddWithValue(
                    "$evidenceId",
                    record.EvidenceId.Value.ToString("D"));

                insertCustodyRecord.Parameters.AddWithValue(
                    "$originalFileName",
                    record.OriginalFileName);

                insertCustodyRecord.Parameters.AddWithValue(
                    "$relativeCustodyPath",
                    record.RelativeCustodyPath);

                insertCustodyRecord.Parameters.AddWithValue(
                    "$fileSizeBytes",
                    record.FileSizeBytes);

                insertCustodyRecord.Parameters.AddWithValue(
                    "$sha256Hex",
                    record.Sha256Hex);

                insertCustodyRecord.Parameters.AddWithValue(
                    "$acceptedUtc",
                    record.AcceptedUtc.Value.ToString("O"));

                insertCustodyRecord.ExecuteNonQuery();

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
                            "SAFE-STOP: Evidence custody failed and failed-attempt cleanup could not be completed safely.",
                            new AggregateException(
                                exception,
                                cleanupException));
                    }
                }

                throw;
            }
        }
    }
}