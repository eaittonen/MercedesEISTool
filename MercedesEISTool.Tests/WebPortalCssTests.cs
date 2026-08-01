using System.Text.RegularExpressions;

namespace MercedesEISTool.Tests;

public class WebPortalCssTests
{
    [Fact]
    public void AppCss_UsesStickyTopbarOnDesktopAndStaticTopbarOnMobile()
    {
        var appCssPath = FindAppCssPath();
        var css = File.ReadAllText(appCssPath);

        var topbarRule = Regex.Match(css, @"\.topbar\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(topbarRule.Success, "The .topbar rule should exist in app.css.");
        Assert.Contains("position: sticky", topbarRule.Groups["body"].Value);
        Assert.Contains("top: var(--topbar-offset)", topbarRule.Groups["body"].Value);

        var mobileRule = Regex.Match(css, @"@media\s*\(max-width:\s*720px\)\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(mobileRule.Success, "The mobile media query should exist in app.css.");
        Assert.Contains("position: static", mobileRule.Groups["body"].Value);
        Assert.Contains("top: auto", mobileRule.Groups["body"].Value);
        Assert.DoesNotContain("position: fixed", mobileRule.Groups["body"].Value);

        Assert.Contains("padding-top: var(--safe-top-padding)", css);
        Assert.DoesNotContain("padding-top: 64px", css);
        Assert.DoesNotContain("padding-top: 80px", css);
        Assert.DoesNotContain("padding-top: 4rem", css);
    }

    private static string FindAppCssPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "MercedesEISTool.Server", "wwwroot", "app.css");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate MercedesEISTool.Server/wwwroot/app.css from the test output directory.");
    }
}
