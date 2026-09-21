namespace Jev.Tool.Tests;

public sealed class HostScopedCssLinkTests
{
    [Theory]
    [InlineData("src/Jev.Linux/wwwroot/index.html", "Jev.styles.css")]
    [InlineData("src/Jev.Maui/wwwroot/index.html", "Jev.Maui.styles.css")]
    [InlineData("src/Jev.Wasm/wwwroot/index.html", "Jev.Wasm.styles.css")]
    [InlineData("src/Jev.Web/Components/App.razor", "Jev.Web.styles.css")]
    public void Hosts_link_the_generated_isolation_bundle_instead_of_rcl_styles_css(
        string relativePath,
        string expectedHref)
    {
        var html = File.ReadAllText(RepoFile(relativePath));

        Assert.DoesNotContain("Jev.App.styles.css", html, StringComparison.Ordinal);
        Assert.Contains($"""href="{expectedHref}" """, html, StringComparison.Ordinal);
    }

    private static string RepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Jev.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, relativePath);
    }
}
