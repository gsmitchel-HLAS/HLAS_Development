using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public sealed record EvidenceCustodyRetrievedFile(
        EvidenceCustodyRecord EvidenceRecord,
        string ControlledFilePath);

    public static class EvidenceCustodyRetrievalService
    {
        public static EvidenceCustodyRetrievedFile Retrieve(
            string projectRoot,
            EvidenceId evidenceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            // Prove this is a valid project package at the
            // currently supported governed schema.
            _ = ProjectPackageReader.Open(fullProjectRoot);

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
                    OriginalFileName,
                    RelativeCustodyPath,
                    FileSizeBytes,
                    Sha256Hex,
                    AcceptedUtc
                FROM HLAS_Evidence_Custody
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
                    "SAFE-STOP: Governed EvidenceId was not found.");
            }

            string originalFileName =
                reader.GetString(0);

            string relativeCustodyPath =
                reader.GetString(1);

            long fileSizeBytes =
                reader.GetInt64(2);

            string sha256Hex =
                reader.GetString(3);

            DateTimeOffset acceptedUtcValue =
                DateTimeOffset.ParseExact(
                    reader.GetString(4),
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None);

            GovernedTimestamp acceptedUtc =
                GovernedTimestamp.FromRecorded(
                    acceptedUtcValue);

            EvidenceCustodyRecord record =
                new(
                    evidenceId,
                    originalFileName,
                    relativeCustodyPath,
                    fileSizeBytes,
                    sha256Hex,
                    acceptedUtc);

            string sourceEvidenceRoot =
                Path.GetFullPath(
                    Path.Combine(
                        fullProjectRoot,
                        EvidenceCustodyService.SourceEvidenceDirectoryName));

            string controlledFilePath =
                Path.GetFullPath(
                    Path.Combine(
                        fullProjectRoot,
                        record.RelativeCustodyPath));

            string relativeToEvidenceRoot =
                Path.GetRelativePath(
                    sourceEvidenceRoot,
                    controlledFilePath);

            if (Path.IsPathRooted(relativeToEvidenceRoot) ||
                relativeToEvidenceRoot == ".." ||
                relativeToEvidenceRoot.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) ||
                relativeToEvidenceRoot.StartsWith(
                    ".." + Path.AltDirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Governed custody path escapes HLAS_Source_Evidence.");
            }

            if (!File.Exists(controlledFilePath))
            {
                throw new FileNotFoundException(
                    "SAFE-STOP: Controlled Source Evidence file is missing.",
                    controlledFilePath);
            }

            FileInfo controlledFile =
                new(controlledFilePath);

            if (controlledFile.Length !=
                record.FileSizeBytes)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Controlled Source Evidence size does not match governed custody.");
            }

            string actualSha256;

            using (FileStream stream = new(
                controlledFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                using SHA256 hasher =
                    SHA256.Create();

                byte[] hash =
                    hasher.ComputeHash(stream);

                actualSha256 =
                    Convert
                        .ToHexString(hash)
                        .ToLowerInvariant();
            }

            if (!string.Equals(
                actualSha256,
                record.Sha256Hex,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Controlled Source Evidence SHA-256 does not match governed custody.");
            }

            return new EvidenceCustodyRetrievedFile(
                record,
                controlledFilePath);
        }
    }
}