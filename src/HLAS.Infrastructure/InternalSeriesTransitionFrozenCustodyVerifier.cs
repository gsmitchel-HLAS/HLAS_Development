using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    internal static class InternalSeriesTransitionFrozenCustodyVerifier
    {
        internal static void Verify(
            string projectRoot,
            InternalSeriesTransitionBasisReference basis,
            ReleaseFreezeManifest manifest)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
            ArgumentNullException.ThrowIfNull(basis);
            ArgumentNullException.ThrowIfNull(manifest);

            if (manifest.Evidence is null)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Released evidence list is missing.");
            }

            string databasePath = Path.Combine(
                Path.GetFullPath(projectRoot),
                PreChangeFreezeService.FrozenStatesDirectoryName,
                basis.ReleaseFreezeId.Value.ToString("D"),
                ProjectPackageCreator.DatabaseFileName);

            if (!File.Exists(databasePath))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Frozen database is missing.");
            }

            Dictionary<string, ReleaseFreezeManifestEvidenceEntry> entries =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (ReleaseFreezeManifestEvidenceEntry entry
                in manifest.Evidence)
            {
                if (entry is null ||
                    string.IsNullOrWhiteSpace(entry.EvidenceId) ||
                    !entries.TryAdd(entry.EvidenceId, entry))
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Released evidence identities are invalid.");
                }
            }

            try
            {
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
                        EvidenceId,
                        RelativeCustodyPath,
                        FileSizeBytes,
                        Sha256Hex
                    FROM HLAS_Evidence_Custody;
                    """;

                using SqliteDataReader reader =
                    command.ExecuteReader();

                while (reader.Read())
                {
                    string evidenceId = reader.GetString(0);

                    if (!entries.Remove(
                            evidenceId,
                            out ReleaseFreezeManifestEvidenceEntry? entry) ||
                        entry is null ||
                        !string.Equals(
                            entry.RelativeCustodyPath,
                            reader.GetString(1),
                            StringComparison.Ordinal) ||
                        entry.FileSizeBytes != reader.GetInt64(2) ||
                        !string.Equals(
                            entry.Sha256Hex,
                            reader.GetString(3),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "SAFE-STOP: Frozen custody and release manifest disagree.");
                    }
                }
            }
            catch (SqliteException exception)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Frozen custody database could not be verified.",
                    exception);
            }

            if (entries.Count != 0)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Release manifest contains evidence absent from the frozen database.");
            }
        }
    }
}