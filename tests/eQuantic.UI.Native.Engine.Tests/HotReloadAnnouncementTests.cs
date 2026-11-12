using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The line a running app writes on the frame that draws an edit. A person under `dotnet watch` reads
/// it to see how long an edit took to reach the window, and scripts/measure-edit-to-view.cs reads it
/// to time the desktop's part of edit-to-view (#900): renamed or written twice, the measurement would
/// time the wrong thing or nothing at all, and only this says so. The edit says when it ARRIVES too,
/// with the windows it woke, which is how the measurement tells an edit that never reached the window
/// from one that reached it and was never drawn (#1473).
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

        var said = captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        said.Should().HaveCount(2, "the edit says it arrived, its frame says it drew it, and the frame before the edit and the frame after its own say nothing");
        said[0].Should().MatchRegex(@"^\[eQuantic\.HotReload\] an edit changed 0 types and woke \d+ windows? to draw it$",
            "the edit arrives before any frame draws it");
        said[0].Should().NotContain("woke 0 windows", "the host this test made is live, so the edit woke it");
        said[1].Should().MatchRegex(@"^\[eQuantic\.HotReload\] drew the edit \d+ms after it was applied$");
    }
}
