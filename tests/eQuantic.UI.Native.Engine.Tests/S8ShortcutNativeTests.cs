using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S8 on Photon — the DESKTOP app has a real keyboard, so <see cref="Shortcut"/> is not a web
/// affordance: the shell feeds <see cref="PhotonHost.KeyDown"/> and the frame's bindings answer.
/// Being on screen IS the subscription (a closed dialog's Esc simply is not in the frame), and the
/// LAST binding wins a chord — the same LIFO the browser controller uses.
/// </summary>
public class S8ShortcutNativeTests
{
    private static PhotonHost Host(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void KeyDown_FiresTheMatchingChord()
    {
        var opened = 0;
        var host = Host(new Shortcut(
            new Box(new BoxStyle { Width = 60, Height = 30 }),
            KeyChord.Command("k"),
            () => opened++));

        host.KeyDown("k", KeyModifiers.Command).Should().BeTrue("the chord was handled");
        opened.Should().Be(1);
    }

    [Fact]
    public void ModifiersMustMatch_AndKeysAreCaseInsensitive()
    {
        var fired = 0;
        var host = Host(new Shortcut(
            new Box(new BoxStyle { Width = 60, Height = 30 }),
            KeyChord.Command("k"),
            () => fired++));

        host.KeyDown("k").Should().BeFalse("a bare k is not the command chord");
        host.KeyDown("K", KeyModifiers.Command).Should().BeTrue("keys compare case-insensitively");
        fired.Should().Be(1);
    }

    /// <summary>A dialog whose Esc binding exists only while it is OPEN — the shape every real
    /// overlay has (state decides the subtree).</summary>
    private sealed class Dialog : StatefulComponent
    {
        private bool _open = true;

        public int Closed { get; private set; }

        public void Close() => SetState(() => _open = false);

        public override VisualNode Build(ComponentContext context)
        {
            var body = new Box(new BoxStyle { Width = 60, Height = 30 });
            return _open
                ? new Shortcut(body, KeyChord.Escape, () => Closed++)
                : body;
        }
    }

    [Fact]
    public void AnUnmountedBinding_StopsFiring()
    {
        var dialog = new Dialog();
        var host = Host(dialog);

        host.KeyDown("Escape").Should().BeTrue();
        dialog.Closed.Should().Be(1);

        dialog.Close();
        host.RenderFrame(new DisplayListBuilder());

        host.KeyDown("Escape").Should().BeFalse("the binding left with its subtree");
        dialog.Closed.Should().Be(1);
    }

    [Fact]
    public void TheLastBindingWinsAChord()
    {
        var log = new List<string>();
        var stack = new Column(gap: 0);
        stack.Add(new Shortcut(new Box(new BoxStyle { Width = 60, Height = 30 }), KeyChord.Escape,
            () => log.Add("page")));
        stack.Add(new Shortcut(new Box(new BoxStyle { Width = 60, Height = 30 }), KeyChord.Escape,
            () => log.Add("dialog")));

        Host(stack).KeyDown("Escape").Should().BeTrue();

        log.Should().ContainSingle("the topmost — last mounted — binding claims the chord")
            .Which.Should().Be("dialog");
    }

    [Fact]
    public void NoBinding_IsNotHandled()
    {
        Host(new Box(new BoxStyle { Width = 60, Height = 30 })).KeyDown("Escape").Should().BeFalse();
    }
}
