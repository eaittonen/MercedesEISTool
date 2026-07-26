using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MercedesEISTool.Server.Services;

public sealed class SqliteDatabasePathResolver
{
    public string Resolve(string connectionString, string? contentRootPath, ILogger logger, Func<string, bool>? canWrite = null)
    {
        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(sqliteBuilder.DataSource))
        {
            throw new InvalidOperationException($"SQLite connection string is missing a DataSource value. Connection string: {connectionString}");
        }

        if (string.Equals(sqliteBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("SQLite database uses an in-memory connection string; skipping filesystem directory validation.");
            return sqliteBuilder.DataSource;
        }

        var basePath = !string.IsNullOrWhiteSpace(contentRootPath)
            ? contentRootPath
            : Directory.GetCurrentDirectory();

        var fullDataSourcePath = Path.GetFullPath(sqliteBuilder.DataSource, basePath);
        var directory = Path.GetDirectoryName(fullDataSourcePath);

        logger.LogInformation("SQLite database path resolved to '{DatabasePath}' from the configured connection string.", fullDataSourcePath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"SQLite database path '{fullDataSourcePath}' does not have a parent directory that can be created or written to.");
        }

        if (TryEnsureWritableDirectory(directory, canWrite, honorCustomValidator: true))
        {
            return fullDataSourcePath;
        }

        var fallbackDirectory = Path.Combine(basePath, "Data");
        var fallbackPath = Path.Combine(fallbackDirectory, Path.GetFileName(fullDataSourcePath));
        logger.LogWarning("Configured SQLite database directory '{ConfiguredDirectory}' is not writable. Falling back to '{FallbackPath}'.", directory, fallbackPath);

        if (!TryEnsureWritableDirectory(fallbackDirectory, canWrite, honorCustomValidator: false))
        {
            throw new InvalidOperationException($"Unable to create or write to SQLite database directory '{fallbackDirectory}' for database '{fallbackPath}'.");
        }

        return Path.GetFullPath(fallbackPath, basePath);
    }

    private static bool TryEnsureWritableDirectory(string directory, Func<string, bool>? canWrite, bool honorCustomValidator)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception)
        {
            return false;
        }

        if (honorCustomValidator && canWrite is not null)
        {
            return canWrite(directory);
        }

        var writeTestPath = Path.Combine(directory, ".mercedes-eis-tool-write-test");
        try
        {
            using var stream = File.Open(writeTestPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.WriteByte(0);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(writeTestPath))
                {
                    File.Delete(writeTestPath);
                }
            }
            catch
            {
                // Best effort cleanup; the main startup failure is already captured above.
            }
        }
    }
}
