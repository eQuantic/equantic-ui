using eQuantic.UI.Server.HotReload;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The lines the server writes around a rebuild. A person under `dotnet watch` reads them to see a
/// save was taken and how long the rebuild took, and scripts/measure-edit-to-view.cs reads the first
/// one to time the wait after a save (#900): renamed, or written after the build, the measurement
/// would fold the wait into MSBuild, and only this says so.
/// </summary>
[Collection(ProcessConsoleCollection.Name)]
public class HotReloadAnnouncementTests
{
    /// <summary>A server no edit reaches, as one run outside dotnet watch is.</summary>
    private static ServerEdits NoEdits => new(arriveOnSave: false, TimeSpan.Zero);

    [Fact]
    public void ARebuild_SaysWhenItStarts_ThenWhenItEnds()
    {
        using var service = new HotReloadService(Path.GetTempPath(), StandInCompiler.Compiling(), TimeSpan.FromSeconds(30), NoEdits);

        // System's: this namespace also sees the dashboard sample's eQuantic.Console.
        var original = System.Console.Out;
        using var captured = new StringWriter();
        System.Console.SetOut(captured);
        try
        {
            service.Rebuild();
        }
        finally
        {
            System.Console.SetOut(original);
        }

        var lines = captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("[eQuantic.HotReload] a source changed, rebuilding",
            "said before the build runs, so the time to it is the wait after the save and nothing else");
        lines[1].Should().StartWith("[eQuantic.HotReload] rebuilt in ");
    }
}
