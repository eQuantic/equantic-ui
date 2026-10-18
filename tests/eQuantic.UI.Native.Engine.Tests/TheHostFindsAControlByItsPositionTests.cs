using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The press, the keyboard focus and the hover follow the POSITION of the control they belong to
/// (#976): they stay with it for as long as its position holds it, and never pass to a node that
/// takes its place. They followed its layout path, and a path is a place, not a control: a page that
/// replaced another under the focus handed the focus to whatever button stood where the old one did,
/// and a key that changed on a slot holding one child left the press, the focus and the hover where
/// they were, on the newcomer.
/// </summary>
public class TheHostFindsAControlByItsPositionTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken BaseFill = new(Color.FromRgb(0x11, 0x22, 0x33));
    private static readonly ColorToken HoverFill = new(Color.FromRgb(0xAA, 0xBB, 0xCC));

    private static PhotonHost Open(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static DrawCommand[] Paint(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build().Commands.ToArray();
    }

    private static Pressable Button(string label, List<string> log, string? key = null) =>
        new(new Text(label, TypeRole.BodyM), () => log.Add(label)) { Label = label, Key = key };

    private sealed class FirstPage(List<string> log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Button("first", log);
    }

    private sealed class SecondPage(List<string> log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Button("second", log);
    }

    /// <summary>One page, then a page of another type whose button stands where the first one's did.</summary>
    private sealed class Pages(List<string> log) : StatefulComponent
    {
        private bool _turned;

        public void Turn() => SetState(() => _turned = true);

        public override VisualNode Build(ComponentContext context) =>
            _turned ? new SecondPage(log) : new FirstPage(log);
    }

    [Fact]
    public void APageThatReplacesAnother_DoesNotInheritItsFocus()
    {
        var log = new List<string>();
        var pages = new Pages(log);
        var host = Open(pages);
        host.FocusNext().Should().BeTrue();
        host.ActivateFocused().Should().BeTrue("the first page's button holds the focus");
        log.Should().Equal("first");

        pages.Turn();
        var turned = Paint(host);

        host.FocusedId.Should().BeNull("the focused control left with its page");
        host.ActivateFocused().Should().BeFalse("nothing holds the focus for Enter to press");
        host.KeyDown("Enter");
        log.Should().Equal(["first"], "the second page's button was never focused");
        turned.Length.Should().Be(Paint(Open(new SecondPage([]))).Length, "and it wears no ring");
    }

    /// <summary>A single button whose key changes, at the one slot its component holds.</summary>
    private sealed class Rekeyed(List<string> log) : StatefulComponent
    {
        private string _label = "a";

        public void Replace() => SetState(() => _label = "b");

        public override VisualNode Build(ComponentContext context) => Button(_label, log, key: _label);
    }

    [Fact]
    public void AControlReplacedDuringAPress_RunsNothingWhenThePointerLifts()
    {
        var log = new List<string>();
        var rekeyed = new Rekeyed(log);
        var host = Open(rekeyed);
        var centre = host.LastFrame!.HitRegions.Single().Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        rekeyed.Replace();
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(centre.X, centre.Y);

        log.Should().BeEmpty("the button that was pressed left before the release, and the one in its "
            + "place was never pressed");
    }

    [Fact]
    public void AControlReplacedUnderTheFocus_DoesNotInheritIt()
    {
        var log = new List<string>();
        var rekeyed = new Rekeyed(log);
        var host = Open(rekeyed);
        host.FocusNext().Should().BeTrue();

        rekeyed.Replace();
        host.RenderFrame(new DisplayListBuilder());

        host.ActivateFocused().Should().BeFalse();
        log.Should().BeEmpty();
    }

    /// <summary>
    /// One button instance, kept by the app and returned by two page types: a page replaced by the
    /// other mounts it at a new position, with the very same node. Matching the press by its node
    /// as well as by its position let the release reach the replacement (Copilot's second round).
    /// </summary>
    private sealed class SharedButtonPages(Pressable shared) : StatefulComponent
    {
        private bool _turned;

        public void Turn() => SetState(() => _turned = true);

        public override VisualNode Build(ComponentContext context) =>
            _turned ? new SecondHolder(shared) : new FirstHolder(shared);
    }

    private sealed class FirstHolder(Pressable shared) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => shared;
    }

    private sealed class SecondHolder(Pressable shared) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => shared;
    }

    [Fact]
    public void APress_DoesNotPassToTheSameNodeAtANewPosition()
    {
        var log = new List<string>();
        var pages = new SharedButtonPages(Button("shared", log));
        var host = Open(pages);
        var centre = host.LastFrame!.HitRegions.Single().Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        pages.Turn();
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(centre.X, centre.Y);

        log.Should().BeEmpty("the position the press began on left with its page, and the same node at "
            + "the new page's position was never pressed");
    }

    private static readonly ColorToken PressedFill = new(Color.FromRgb(0xAA, 0x00, 0x00));

    [Fact]
    public void ACancelledPress_LeavesNoPressedFill()
    {
        var host = Open(new Pressable(new Box(new BoxStyle { Width = 60, Height = 30, Background = BaseFill }),
            () => { }) { PressedBackground = PressedFill });
        var paintsPressed = (DrawCommand[] commands) => commands.Any(command =>
            command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == PressedFill.Resolve(ThemeMode.Light));

        host.PressDown(30, 15);
        paintsPressed(Paint(host)).Should().BeTrue("pressed");

        host.PointerCancel(PointerKind.Touch);
        paintsPressed(Paint(host)).Should().BeFalse("the system took the finger, and nothing is pressed any more");
    }

    private static Box HoverBox(string key) => new(new BoxStyle
    {
        Width = 60,
        Height = 30,
        Background = BaseFill,
        Hover = new StyleDiff { Background = HoverFill },
    }) { Key = key };

    /// <summary>A box with a hover style whose key changes, at the one slot its component holds.</summary>
    private sealed class Rehovered : StatefulComponent
    {
        private string _key = "a";

        public void Replace() => SetState(() => _key = "b");

        public override VisualNode Build(ComponentContext context) => HoverBox(_key);
    }

    private static bool HasHoverFill(DrawCommand[] commands) =>
        commands.Any(command => command.Kind == DrawCommandKind.FillRRect
            && command.Paint.Color == HoverFill.Resolve(ThemeMode.Light));

    [Fact]
    public void ABoxAtAnothersPlace_IsNotHoveredUntilThePointerMovesOverIt()
    {
        var rehovered = new Rehovered();
        var host = Open(rehovered);
        host.PointerMove(30, 15);
        HasHoverFill(Paint(host)).Should().BeTrue("the pointer rests on the box");

        rehovered.Replace();
        HasHoverFill(Paint(host)).Should().BeFalse("the box in its place was never under a moving pointer");

        host.PointerMove(31, 15);
        HasHoverFill(Paint(host)).Should().BeTrue("and is hovered once the pointer moves over it");
    }

    /// <summary>A keyed list a row is inserted into, above the others.</summary>
    private sealed class Rows(List<string> log) : StatefulComponent
    {
        private bool _inserted;

        public void Insert() => SetState(() => _inserted = true);

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0);
            if (_inserted) column.Add(Button("new", log, key: "n"));
            column.Add(Button("one", log, key: "1"));
            column.Add(Button("two", log, key: "2"));
            return column;
        }
    }

    [Fact]
    public void AKeyedRow_KeepsItsFocus_WhenARowIsInsertedBeforeIt()
    {
        var log = new List<string>();
        var rows = new Rows(log);
        var host = Open(rows);
        host.FocusNext();
        host.FocusNext();

        rows.Insert();
        host.RenderFrame(new DisplayListBuilder());
        host.ActivateFocused().Should().BeTrue();

        log.Should().Equal(["two"], "the key kept the row's position, and the focus is on the position");
    }
}
