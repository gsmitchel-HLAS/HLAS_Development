using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProjectPackageCreator
    {
        public const string ManifestFileName = "HLAS_Project.json";
        public const string DatabaseFileName = "HLAS_Project.db";

        public static ProjectManifest CreateNew(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "Project root may not be blank.",
                    nameof(projectRoot));
            }

            string fullProjectRoot = Path.GetFullPath(projectRoot);

            bool rootAlreadyExisted = Directory.Exists(fullProjectRoot);

            if (rootAlreadyExisted &&
                Directory.EnumerateFileSystemEntries(fullProjectRoot).Any())
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: The selected project root is not empty.");
            }

            if (!rootAlreadyExisted)
            {
                Directory.CreateDirectory(fullProjectRoot);
            }

            string manifestPath =
                Path.Combine(fullProjectRoot, ManifestFileName);

            string databasePath =
                Path.Combine(fullProjectRoot, DatabaseFileName);

            bool manifestCreated = false;
            bool databaseCreated = false;

            try
            {
                ProjectManifest manifest = ProjectManifest.CreateNew();

                using (FileStream reservation = new(
                    databasePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                }

                databaseCreated = true;

                SqliteConnectionStringBuilder connectionBuilder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite
                };

                using (SqliteConnection connection =
                    new(connectionBuilder.ToString()))
                {
                    connection.Open();

                    using SqliteTransaction transaction =
                        connection.BeginTransaction();

                    ProjectDatabaseSchema.InitializeNewDatabase(
      connection,
      transaction,
      manifest.ProjectId);

                    transaction.Commit();
                }

                object manifestPayload = new
                {
                    ProjectId = manifest.ProjectId.Value,
                    ProjectFormatVersion =
                        manifest.ProjectFormatVersion,
                    CreatedUtc = manifest.CreatedUtc.Value
                };

                JsonSerializerOptions jsonOptions = new()
                {
                    WriteIndented = true
                };

                using (FileStream manifestStream = new(
                    manifestPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    manifestCreated = true;

                    JsonSerializer.Serialize(
                        manifestStream,
                        manifestPayload,
                        jsonOptions);
                }

                return manifest;
            }
            catch
            {
                if (manifestCreated && File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                }

                if (databaseCreated && File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }

                throw;
            }
        }
    }
}