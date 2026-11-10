using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The line a running app writes on the frame that draws an edit. A person under `dotnet watch` reads
/// it to see how long an edit took to reach the window, and scripts/measure-edit-to-view.cs reads it
/// to time the desktop's part of edit-to-view (#900): renamed or written twice, the measurement would
/// time the wrong thing or nothing at all, and only this says so.
/// </summary>
[Collection(ProcessConsoleCollection.Name)]
public class HotReloadAnnouncementTests
{
    private sealed class Label : Primitives.StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("Edited", TypeRole.Title);
    }

    [Fact]
    public void TheFrameThatDrawsAnEdit_SaysSo_Once()
    {
        var host = new PhotonHost(new Label(), PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());

        var original = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            host.RenderFrame(new DisplayListBuilder());
            // The runtime's own sequence for an applied edit.
            PhotonHotReload.ClearCache(null);
            PhotonHotReload.UpdateApplication(null);
            host.RenderFrame(new DisplayListBuilder());
            host.RenderFrame(new DisplayListBuilder());
        }
        finally
        {
            Console.SetOut(original);
        }

        captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Should().ContainSingle("the frame before the edit and the frame after its own say nothing")
            .Which.Should().MatchRegex(@"^\[eQuantic\.HotReload\] drew the edit \d+ms after it was applied$");
    }
}
