using Microsoft.Data.Sqlite;
using System.IO;

namespace HLAS.Infrastructure
{
    public static class DevelopmentalSessionCleanup
    {
        public static void Delete(
            string sessionRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                sessionRoot);

            SqliteConnection.ClearAllPools();

            if (Directory.Exists(sessionRoot))
            {
                Directory.Delete(
                    sessionRoot,
                    recursive: true);
            }
        }
    }
}