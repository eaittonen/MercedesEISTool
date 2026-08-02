namespace MercedesEISTool.Tests;

public class AuthenticationConfigurationTests
{
    [Fact]
    public void Program_UsesLongLivedPersistedCookieLifetime()
    {
        var programPath = FindProgramPath();
        var source = File.ReadAllText(programPath);

        Assert.Contains("TimeSpan.FromDays(90)", source);
        Assert.Contains("DateTimeOffset.UtcNow.AddDays(90)", source);
    }

    private static string FindProgramPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "MercedesEISTool.Server", "Program.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate MercedesEISTool.Server/Program.cs from the test output directory.");
    }
}
