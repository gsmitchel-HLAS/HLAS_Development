using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ProjectAuthorizationStore
    {
        public static void AddAuthorization(
            string projectRoot,
            UserId userId,
            ProjectRole projectRole)
        {
            _ = ProjectPackageReader.Open(projectRoot);

            if (userId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "UserId may not be empty.",
                    nameof(userId));
            }

            string roleValue =
                ValidateProjectRole(projectRole);

            string databasePath = Path.Combine(
                Path.GetFullPath(projectRoot),
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

            using SqliteTransaction transaction =
                connection.BeginTransaction();

            using SqliteCommand existing =
                connection.CreateCommand();

            existing.Transaction = transaction;
            existing.CommandText =
                """
                SELECT COUNT(*)
                FROM HLAS_Project_Authorization
                WHERE UserId = $userId;
                """;

            existing.Parameters.AddWithValue(
                "$userId",
                userId.Value.ToString("D"));

            long count = (long)existing.ExecuteScalar()!;

            if (count != 0)
            {
                throw new InvalidOperationException(
                    "SAFE-STOP: This HLAS identity already has project authorization.");
            }

            using SqliteCommand insert =
                connection.CreateCommand();

            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO HLAS_Project_Authorization
                    (
                        UserId,
                        ProjectRole,
                        AuthorizedUtc
                    )
                VALUES
                    (
                        $userId,
                        $projectRole,
                        $authorizedUtc
                    );
                """;

            insert.Parameters.AddWithValue(
                "$userId",
                userId.Value.ToString("D"));

            insert.Parameters.AddWithValue(
                "$projectRole",
                roleValue);

            insert.Parameters.AddWithValue(
                "$authorizedUtc",
                GovernedTimestamp.CreateNow().Value.ToString("O"));

            insert.ExecuteNonQuery();

            transaction.Commit();
        }

        public static ProjectRole? ResolveRole(
            string projectRoot,
            UserId userId)
        {
            _ = ProjectPackageReader.Open(projectRoot);

            if (userId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "UserId may not be empty.",
                    nameof(userId));
            }

            string databasePath = Path.Combine(
                Path.GetFullPath(projectRoot),
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
                SELECT ProjectRole
                FROM HLAS_Project_Authorization
                WHERE UserId = $userId;
                """;

            command.Parameters.AddWithValue(
                "$userId",
                userId.Value.ToString("D"));

            object? result = command.ExecuteScalar();

            if (result is null)
            {
                return null;
            }

            return result.ToString() switch
            {
                "Technician" => ProjectRole.Technician,
                "Senior" => ProjectRole.Senior,
                "Admin" => ProjectRole.Admin,
                _ => throw new InvalidOperationException(
                    "SAFE-STOP: Stored project authorization role is invalid.")
            };
        }

        private static string ValidateProjectRole(
            ProjectRole projectRole)
        {
            return projectRole.Value switch
            {
                "Technician" => "Technician",
                "Senior" => "Senior",
                "Admin" => "Admin",
                _ => throw new ArgumentException(
                    "ProjectRole is invalid.",
                    nameof(projectRole))
            };
        }
    }
}