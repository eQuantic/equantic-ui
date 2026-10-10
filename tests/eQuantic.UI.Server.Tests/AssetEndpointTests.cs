using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The SDK's asset routes serve the files under <c>wwwroot/_equantic</c> and nothing else (#673). They
/// serve anyone, so a sign-in page under a fallback policy comes alive, and their <c>{name}</c> is the
/// request's text: on Windows an encoded backslash decodes into a separator, so a name like
/// <c>..\..\secret</c> combined into a path outside the directory (found by Copilot on #686).
/// </summary>
public class AssetEndpointTests
{
    private static readonly string AssetDirectory =
        Path.Combine(Path.GetTempPath(), "eq-assets", "wwwroot", "_equantic");

    /// <summary>Separators of both kinds on every OS: the one that combines on Windows and the one
    /// that combines everywhere, so the rule is the same wherever the app runs.</summary>
    [Theory]
    [InlineData(@"..\..\secret")]
    [InlineData("../../secret")]
    [InlineData(@"pages\secret")]
    [InlineData("/etc/secret")]
    [InlineData("")]
    public void ANameThatIsNotAFileName_ResolvesToNothing(string name)
    {
        AssetPaths.Resolve(AssetDirectory, name, ".js").Should().BeNull();
    }

    [Fact]
    public void AFileName_ResolvesInsideTheDirectory()
    {
        AssetPaths.Resolve(AssetDirectory, "NotFoundScreen-3kd9", ".js")
            .Should().Be(Path.Combine(Path.GetFullPath(AssetDirectory), "NotFoundScreen-3kd9.js"));
    }

    /// <summary>Over the real pipeline, with real files two levels above the asset directory, where
    /// <c>..\..\secret.js</c> lands on Windows. On a Unix host the backslash is a file name character,
    /// so this one is decided on the Windows runner.</summary>
    [Fact]
    public async Task ATraversingName_ServesNothingOutsideTheDirectory()
    {
        var root = Directory.CreateTempSubdirectory("eq-assets-").FullName;
        var assets = Directory.CreateDirectory(Path.Combine(root, "wwwroot", "_equantic")).FullName;
        File.WriteAllText(Path.Combine(root, "secret.js"), "const secret = 'OUTSIDE-THE-ASSETS';");
        File.WriteAllText(Path.Combine(root, "secret.js.map"), "{\"secret\":\"OUTSIDE-THE-ASSETS\"}");
        File.WriteAllText(Path.Combine(assets, "Probe.js"), "export const probe = 'INSIDE';");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = root,
            WebRootPath = Path.Combine(root, "wwwroot"),
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        var server = app.GetTestServer();

        // The path set as the server reads it, past any client's own normalization of the URL.
        async Task<(int Status, string Body)> Get(string path)
        {
            var context = await server.SendAsync(request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = path;
            });
            using var reader = new StreamReader(context.Response.Body);
            return (context.Response.StatusCode, await reader.ReadToEndAsync());
        }

        (await Get("/_equantic/Probe.js")).Body.Should().Contain("INSIDE", "a file name still serves");
        foreach (var path in new[] { @"/_equantic/..\..\secret.js", @"/_equantic/..\..\secret.js.map" })
        {
            var (status, body) = await Get(path);
            status.Should().Be(StatusCodes.Status404NotFound, path);
            body.Should().NotContain("OUTSIDE-THE-ASSETS", path);
        }
    }
}
