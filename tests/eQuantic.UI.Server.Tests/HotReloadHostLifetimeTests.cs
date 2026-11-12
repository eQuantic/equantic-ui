using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Hot reload ends with its host, stopped or not (#1375). It was disposed when the app STOPPED
/// (<c>ApplicationStopping</c>), and disposing a host does not stop it, so a host disposed without being
/// stopped, which an integration test does, kept its watchers on the content root, its timers and the
/// compiler it had started, with the root as its working directory: the next save under that root still
/// started a rebuild, and Windows refused to remove the folder (#1366).
/// </summary>
[Collection(ProcessConsoleCollection.Name)]
public class HotReloadHostLifetimeTests
{
    [Fact]
    public async Task AHostDisposedWithoutBeingStopped_RebuildsNothingAfterIt()
    {
        var root = Directory.CreateTempSubdirectory("eq-hot-life-").FullName;
        // System's: this namespace also sees the dashboard sample's eQuantic.Console.
        var original = System.Console.Out;
        using var captured = new StringWriter();
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Production",
                ContentRootPath = root,
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddUI(options =>
            {
                options.ScanAssembly(Assembly.GetExecutingAssembly());
                options.HotReload = true;
            });
            var app = builder.Build();
            app.MapUI();
            await app.StartAsync();

            // Disposed and never stopped.
            await app.DisposeAsync();

            System.Console.SetOut(captured);
            await File.WriteAllTextAsync(Path.Combine(root, "Screen.cs"), "class Screen { }");
            // Well past the wait after a save, when a watcher still alive would have started its rebuild.
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        finally
        {
            System.Console.SetOut(original);
            Directory.Delete(root, recursive: true);
        }

        captured.ToString().Should().NotContain("[eQuantic.HotReload] a source changed, rebuilding",
            "a disposed host watches nothing, stopped or not");
    }
}
