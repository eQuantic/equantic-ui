using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The positional reconciler (W6 slice 1): NESTED stateful components keep their state across
/// parent rebuilds — no state hoisting. Identity = tree path + type + key; key changes reset;
/// retained instances adopt fresh config through AdoptConfig.
/// </summary>
public class ReconcilerTests
{
    /// <summary>A stateful child the parent recreates on every Build (the pre-reconciler killer).</summary>
    private sealed class NestedCounter : StatefulComponent
    {
        private int _count;

        public int Count => _count;
        public string Caption { get; init; } = "";

        public void Increment() => SetState(() => _count++);

        public override VisualNode Build(ComponentContext context)
        {
            var row = new Row(gap: Space.S2);
            row.Add(new Text($"{Caption}: {_count}", TypeRole.Caption));
            row.Add(new Button("+", onPressed: Increment));
            return row;
        }

        public override void AdoptConfig(UiComponent next)
        {
            // Caption is CONFIG (the parent owns it); _count is STATE (this instance owns it).
            if (next is NestedCounter fresh)
            {
                // init-only props adopt via the documented pattern: copy through a mutable backing.
                _caption = fresh.Caption;
            }
        }

        private string? _caption;
        public string EffectiveCaption => _caption ?? Caption;
    }

    /// <summary>A stateful parent whose own SetState forces a full rebuild of its subtree.</summary>
    private sealed class Shell : StatefulComponent
    {
        private int _generation;

        public NestedCounter? LastChild { get; private set; }

        public void Bump() => SetState(() => _generation++);

        public string ChildKey { get; set; } = "a";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text($"gen {_generation}", TypeRole.Caption));
            LastChild = new NestedCounter { Caption = $"gen{_generation}", Key = ChildKey };
            column.Add(LastChild);
            return column;
        }
    }

    private static (PhotonHost Host, Shell Shell) CreateHost()
    {
        var shell = new Shell();
        var host = new PhotonHost(shell, PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        return (host, shell);
    }

    private static void Render(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    [Fact]
    public void NestedState_SurvivesParentRebuilds()
    {
        var (host, shell) = CreateHost();

        // Tap the nested + button: the RETAINED child (not the fresh one) receives it via hit regions.
        host.Tap(60, 40);
        Render(host);

        // Parent rebuilds (its own SetState) — the fresh NestedCounter must be replaced by the
        // retained one, so the count SURVIVES without hoisting.
        shell.Bump();
        Render(host);

        host.Tap(60, 40);
        Render(host);

        // The retained instance holds both taps across the parent rebuild.
        var retained = FindRetained(host);
        retained.Count.Should().Be(2, "state lives on the retained instance across parent rebuilds");
        retained.EffectiveCaption.Should().Be("gen1", "config ADOPTS from the fresh instance");
    }

    [Fact]
    public void KeyChange_ResetsTheNestedState()
    {
        var (host, shell) = CreateHost();
        host.Tap(60, 40);
        Render(host);
        FindRetained(host).Count.Should().Be(1);

        shell.ChildKey = "b";
        shell.Bump();
        Render(host);

        FindRetained(host).Count.Should().Be(0, "a changed key is a NEW identity — state resets");
    }

    private static NestedCounter FindRetained(PhotonHost host)
    {
        // The retained child is reachable through the last frame's hit region (the + button lives
        // inside the retained instance's subtree, and Tap dispatch proved which instance is live).
        Render(host);
        var frame = host.RenderFrame(new DisplayListBuilder());
        NestedCounter? found = null;
        void Walk(eQuantic.UI.Native.Framework.LayoutNode node)
        {
            if (node.Source is NestedCounter counter) found = counter;
            foreach (var child in node.Children) Walk(child);
        }
        Walk(frame.Root);
        found.Should().NotBeNull();
        return found!;
    }
}
