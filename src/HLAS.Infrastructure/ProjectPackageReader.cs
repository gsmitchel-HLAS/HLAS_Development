using System;
using System.IO;
using System.Text.Json;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProjectPackageReader
    {
        public static ProjectManifest Open(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "Project root may not be blank.",
                    nameof(projectRoot));
            }

            string fullProjectRoot = Path.GetFullPath(projectRoot);

            if (!Directory.Exists(fullProjectRoot))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The HLAS project root does not exist.");
            }

            string manifestPath = Path.Combine(
                fullProjectRoot,
                ProjectPackageCreator.ManifestFileName);

            string databasePath = Path.Combine(
                fullProjectRoot,
                ProjectPackageCreator.DatabaseFileName);

            if (!File.Exists(manifestPath) ||
                !File.Exists(databasePath))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The HLAS project package is incomplete.");
            }

            try
            {
                ProjectManifest manifest =
                    ReadManifest(manifestPath);

                (Guid ProjectId, int DatabaseSchemaVersion)
                    databaseMetadata =
                        ReadDatabaseMetadata(databasePath);

                if (manifest.ProjectId.Value !=
                    databaseMetadata.ProjectId)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Manifest ProjectId does not match database ProjectId.");
                }

                if (databaseMetadata.DatabaseSchemaVersion !=
                    ProjectDatabaseSchema.CurrentDatabaseSchemaVersion)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Database schema version is unsupported.");
                }

                return manifest;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The HLAS project package could not be opened safely.",
                    exception);
            }
        }

        private static ProjectManifest ReadManifest(
            string manifestPath)
        {
            using JsonDocument document =
                JsonDocument.Parse(
                    File.ReadAllText(manifestPath));

            JsonElement root = document.RootElement;

            if (!root.TryGetProperty(
                    "ProjectId",
                    out JsonElement projectIdElement) ||
                projectIdElement.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(
                    projectIdElement.GetString(),
                    out Guid projectIdValue))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Manifest ProjectId is invalid.");
            }

            if (!root.TryGetProperty(
                    "ProjectFormatVersion",
                    out JsonElement versionElement) ||
                !versionElement.TryGetInt32(
                    out int projectFormatVersion))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Manifest ProjectFormatVersion is invalid.");
            }

            if (!root.TryGetProperty(
                    "CreatedUtc",
                    out JsonElement createdUtcElement) ||
                !createdUtcElement.TryGetDateTimeOffset(
                    out DateTimeOffset createdUtcValue))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Manifest CreatedUtc is invalid.");
            }

            return new ProjectManifest(
                new ProjectId(projectIdValue),
                projectFormatVersion,
                GovernedTimestamp.FromRecorded(
                    createdUtcValue));
        }

        private static (
            Guid ProjectId,
            int DatabaseSchemaVersion)
            ReadDatabaseMetadata(string databasePath)
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
                    ProjectId,
                    DatabaseSchemaVersion
                FROM HLAS_Project_Metadata
                WHERE SingletonId = 1;
                """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Database project metadata is missing.");
            }

            string projectIdText = reader.GetString(0);

            if (!Guid.TryParse(
                    projectIdText,
                    out Guid projectId))
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Database ProjectId is invalid.");
            }

            int databaseSchemaVersion =
                reader.GetInt32(1);

            if (databaseSchemaVersion < 1)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: Database schema version is invalid.");
            }

            return (
                projectId,
                databaseSchemaVersion);
        }
    }
}