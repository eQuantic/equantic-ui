using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A modal layer gives the focus back when it closes (#1811). The focus goes to the control that had it
/// when the layer opened, however the layer closed. When that control is gone, it goes to the control
/// now at its place among the stops beneath the layer: the same control of the next row, or the last
/// one when it was the last. The web's twin is <c>focus-trap.spec.ts</c>, which runs the same rows.
/// Photon recorded no opener at all: the focus stayed on the dialog's button that closed it and left
/// with it, so a keyboard user started over from the top of the page.
/// </summary>
public class ModalFocusReturnTests
{
    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 600, 400)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            SmoothScroll = false,
        };

    private static void Settle(PhotonHost host)
    {
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());
    }

    private static Pressable Button(string label, Action pressed, bool initialFocus = false) =>
        new(new Box(new BoxStyle { Width = SizeValue.Fixed(80), Height = SizeValue.Fixed(24) }), pressed)
        {
            Label = label,
            InitialFocus = initialFocus,
        };

    /// <summary>Rows with an Edit and a Delete each, and the dialog a Delete opens to confirm, its Cancel
    /// marked as the safe action that takes the focus first.</summary>
    private sealed class Rows(params string[] rows) : StatefulComponent
    {
        private readonly List<string> _rows = [.. rows];
        private string? _asking;

        public override VisualNode Build(ComponentContext context)
        {
            var page = new Stack();
            var list = new Column(gap: 4);
            foreach (var row in _rows)
            {
                var line = new Row(gap: 4) { Key = row };
                line.Add(Button($"Edit {row}", () => { }));
                line.Add(Button($"Delete {row}", () => SetState(() => _asking = row)));
                list.Add(line);
            }
            page.Add(list);
            if (_asking is { } asking)
            {
                var actions = new Column(gap: 4);
                actions.Add(Button("Delete", () => SetState(() =>
                {
                    _rows.Remove(asking);
                    _asking = null;
                })));
                actions.Add(Button("Cancel", () => SetState(() => _asking = null), initialFocus: true));
                page.Add(new Overlay(actions) { Label = "Delete the row?" });
            }
            return page;
        }
    }

    /// <summary>The host over <paramref name="rows"/>, with the keyboard on <paramref name="label"/>.</summary>
    private static PhotonHost FocusedOn(string label, params string[] rows)
    {
        var host = Host(new Rows(rows));
        Settle(host);
        for (var i = 0; i < 2 * rows.Length && host.Focused?.Label != label; i++)
        {
            host.FocusNext();
            Settle(host);
        }
        (host.Focused?.Label).Should().Be(label);
        return host;
    }

    /// <summary>Enter on what has the focus, and the frames it asks for.</summary>
    private static void Press(PhotonHost host)
    {
        host.ActivateFocused().Should().BeTrue();
        Settle(host);
    }

    /// <summary>The keyboard moved to the dialog's <paramref name="label"/>, inside the trap.</summary>
    private static void MoveTo(PhotonHost host, string label)
    {
        for (var i = 0; i < 4 && host.Focused?.Label != label; i++)
        {
            host.KeyDown("Tab");
            Settle(host);
        }
        (host.Focused?.Label).Should().Be(label);
    }

    [Fact]
    public void ADialogThatOpens_TakesTheFocus_OnItsMarkedSafeAction()
    {
        var host = FocusedOn("Delete two", "one", "two", "three");

        Press(host);

        (host.Focused?.Label).Should().Be("Cancel", "the dialog takes the focus, on the action marked to take it first");
        host.FocusVisible.Should().BeTrue("the dialog was opened from the keyboard, and the ring stays");
    }

    [Fact]
    public void ADialogThatCloses_GivesTheFocusBackToTheControlThatOpenedIt()
    {
        var host = FocusedOn("Delete two", "one", "two", "three");
        Press(host);
        (host.Focused?.Label).Should().Be("Cancel", "the focus went into the dialog");

        Press(host);

        (host.Focused?.Label).Should().Be("Delete two", "Cancel closed the dialog, and its opener is still there");
    }

    [Fact]
    public void ARowTheDialogDeleted_HandsTheFocusToTheSameControlOfTheNextRow()
    {
        var host = FocusedOn("Delete two", "one", "two", "three");
        Press(host);
        MoveTo(host, "Delete");

        Press(host);

        (host.Focused?.Label).Should().Be("Delete three",
            "the row that opened the dialog is gone, and the control at its place takes the focus");
    }

    [Fact]
    public void TheLastRowDeleted_HandsTheFocusToTheRowBeforeIt()
    {
        var host = FocusedOn("Delete three", "one", "two", "three");
        Press(host);
        MoveTo(host, "Delete");

        Press(host);

        (host.Focused?.Label).Should().Be("Delete two", "nothing stands after the last row, so the last control takes it");
    }

    [Fact]
    public void ADialogOpenedByAPointer_KeepsTheRingOff_OnTheWayInAndOut()
    {
        var host = Host(new Rows("one", "two"));
        Settle(host);
        var delete = host.LastFrame!.HitRegions.Single(region => region.Node.Label == "Delete one").Bounds.Center;
        host.PressDown(delete.X, delete.Y);
        host.PressUp(delete.X, delete.Y);
        Settle(host);

        (host.Focused?.Label).Should().Be("Cancel");
        host.FocusVisible.Should().BeFalse("a pointer opened it, and a ring is for the keyboard");
        var cancel = host.LastFrame!.HitRegions.Single(region => region.Node.Label == "Cancel").Bounds.Center;
        host.PressDown(cancel.X, cancel.Y);
        host.PressUp(cancel.X, cancel.Y);
        Settle(host);

        (host.Focused?.Label).Should().Be("Delete one", "the focus is back on the opener all the same");
        host.FocusVisible.Should().BeFalse();
    }

    /// <summary>A settings dialog over a page, whose Remove opens a confirm over it that removes Remove.</summary>
    private sealed class Nested : StatefulComponent
    {
        private bool _removed;
        private bool _asking;

        public override VisualNode Build(ComponentContext context)
        {
            var page = new Stack();
            page.Add(Button("Behind", () => { }));
            var settings = new Column(gap: 4);
            settings.Add(Keyed("Keep", () => { }));
            if (!_removed) settings.Add(Keyed("Remove", () => SetState(() => _asking = true)));
            settings.Add(Keyed("Close", () => { }));
            page.Add(new Overlay(settings) { Label = "Settings" });
            if (_asking)
            {
                var confirm = new Column(gap: 4);
                confirm.Add(Button("Delete", () => SetState(() =>
                {
                    _removed = true;
                    _asking = false;
                })));
                confirm.Add(Button("Cancel", () => SetState(() => _asking = false)));
                page.Add(new Overlay(confirm) { Label = "Remove it?" });
            }
            return page;
        }

        private static Pressable Keyed(string label, Action pressed) =>
            new(new Box(new BoxStyle { Width = SizeValue.Fixed(80), Height = SizeValue.Fixed(24) }), pressed)
            {
                Label = label,
                Key = label,
            };
    }

    [Fact]
    public void AControlInsideALowerDialog_IsThePlace_WhenTheDialogOverItCloses()
    {
        var host = Host(new Nested());
        Settle(host);
        (host.Focused?.Label).Should().Be("Keep", "the settings dialog took the focus when it opened");
        MoveTo(host, "Remove");
        Press(host);
        (host.Focused?.Label).Should().Be("Delete");

        Press(host);

        (host.Focused?.Label).Should().Be("Close", "inside the dialog still open, never the page behind it");
    }
}
