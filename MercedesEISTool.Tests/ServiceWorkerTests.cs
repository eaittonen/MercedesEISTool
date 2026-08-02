namespace MercedesEISTool.Tests;

public class ServiceWorkerTests
{
    [Fact]
    public void ServiceWorker_UsesUpdatedCacheVersionAndNetworkFirstShellStrategy()
    {
        var serviceWorkerPath = FindServiceWorkerPath();
        var script = File.ReadAllText(serviceWorkerPath);

        Assert.Contains("CACHE_NAME = 'mercedes-eis-toolkit-v3'", script);
        Assert.Contains("event.respondWith(fetch(event.request)", script);
        Assert.Contains("self.skipWaiting()", script);
        Assert.Contains("self.clients.claim()", script);
    }

    private static string FindServiceWorkerPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "MercedesEISTool.Server", "wwwroot", "service-worker.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate MercedesEISTool.Server/wwwroot/service-worker.js from the test output directory.");
    }
}
