using Microsoft.Extensions.Logging;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public class SqliteDatabasePathResolverTests
{
    [Fact]
    public void Resolve_UsesFallbackPath_WhenConfiguredDirectoryIsNotWritable()
    {
        var resolver = new SqliteDatabasePathResolver();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        var logger = loggerFactory.CreateLogger("tests");

        var contentRoot = Path.Combine(Path.GetTempPath(), "mercedes-eis-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        try
        {
            var resolvedPath = resolver.Resolve(
                "Data Source=/unwritable/mercedes-eis-auth.db",
                contentRoot,
                logger,
                _ => false);

            Assert.Equal(Path.Combine(contentRoot, "Data", "mercedes-eis-auth.db"), resolvedPath);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }
}
