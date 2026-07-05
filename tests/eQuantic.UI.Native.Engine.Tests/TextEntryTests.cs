using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The text-entry primitive and its B9/B10 components on Photon (spec fence: caret/selection/IME
/// land at M4 — the spec fixes the VISUAL contract now so forms don't re-layout later). The entry
/// measures ONE line of its role filling the available width; the value renders through the W4
/// placeholder-bar convention (TextPrimary), an empty value shows the placeholder (TextMuted).
/// </summary>
public class TextEntryTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static (RealizeResult Frame, DrawCommand[] Commands) Render(VisualNode root, float w = 260, float h = 200)
    {
        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(root, w, h, Theme, ThemeMode.Light, builder);
        return (frame, builder.Build().Commands.ToArray());
    }

    [Fact]
    public void TextEntry_MeasuresOneRoleLine_FillingTheAvailableWidth()
    {
        var (frame, _) = Render(new TextEntry("ana@equantic"));

        var line = Theme.Type(TypeRole.BodyL).LineHeight;
        frame.Root.Bounds.Width.Should().Be(260, "the editable area fills the field");
        frame.Root.Bounds.Height.Should().Be(line, "one line of the entry's role (BodyL)");
    }

    [Fact]
    public void TextEntry_RendersValueBar_OrMutedPlaceholderBar()
    {
        var (_, withValue) = Render(new TextEntry("ana@equantic"));
        var (_, empty) = Render(new TextEntry("") { Placeholder = "you@company.com" });

        var valueBar = withValue.Single(c => c.Kind == DrawCommandKind.FillRRect);
        var placeholderBar = empty.Single(c => c.Kind == DrawCommandKind.FillRRect);

        valueBar.Paint.Color.Should().Be(
            Theme.TextPrimary.Resolve(ThemeMode.Light).WithOpacity(0.30f),
            "a typed value reads as primary text");
        placeholderBar.Paint.Color.Should().Be(
            Theme.TextMuted.Resolve(ThemeMode.Light).WithOpacity(0.30f),
            "the placeholder reads as muted text (spec: TextMuted, never the label)");
    }

    [Fact]
    public void TextInput_Container_FollowsTheB9Frame()
    {
        var (frame, commands) = Render(new TextInput("", label: "Email", placeholder: "you@company.com"));

        // Column(5): [ Column(6): [label, container], caption ] — container = the bordered stroke.
        var stroke = commands.Single(c => c.Kind == DrawCommandKind.StrokeRRect);
        stroke.StrokeWidth.Should().Be(1);
        stroke.Paint.Color.Should().Be(Theme.BorderStrong.Resolve(ThemeMode.Light));

        // Label (Label role) + reserved caption line always present: total height is stable.
        var labelLine = Theme.Type(TypeRole.Label).LineHeight;
        var captionLine = Theme.Type(TypeRole.Caption).LineHeight;
        frame.Root.Bounds.Height.Should().Be(labelLine + 6 + 48 + 5 + captionLine,
            "label + 6dp + Large 48dp container + 5dp + the ALWAYS-reserved helper line");
    }

    [Fact]
    public void TextInput_ErrorState_SwapsToDestructive_WithoutShiftingLayout()
    {
        var (okFrame, _) = Render(new TextInput("", label: "Email", helper: "We never share it."));
        var (errFrame, errCommands) = Render(new TextInput("", label: "Email", error: "Enter a valid email address."));

        errFrame.Root.Bounds.Height.Should().Be(okFrame.Root.Bounds.Height,
            "the helper line is always reserved — error swaps never shift the form (spec B9)");

        var stroke = errCommands.Single(c => c.Kind == DrawCommandKind.StrokeRRect);
        stroke.Paint.Color.Should().Be(Theme.Colors(Variant.Destructive).Base.Resolve(ThemeMode.Light));
    }

    [Fact]
    public void TextInput_SmallSize_Asserts()
    {
        var act = () => new TextInput("", size: SizeVariant.Small);
        act.Should().Throw<ArgumentOutOfRangeException>("text + padding can't fit 32dp (spec B9)");
    }

    [Fact]
    public void SearchField_IsTheBorderlessPill_WithClearOnlyWhenNonEmpty()
    {
        var (_, empty) = Render(new SearchField(""));
        var (emptyFrame, _) = Render(new SearchField(""));
        var (frame, withQuery) = Render(new SearchField("rio"));

        empty.Should().NotContain(c => c.Kind == DrawCommandKind.StrokeRRect, "no border (E0)");
        emptyFrame.HitRegions.Should().BeEmpty("no clear button while empty");
        frame.HitRegions.Should().HaveCount(1, "the clear button registers its 48dp hit");

        var pill = withQuery.First(c => c.Kind == DrawCommandKind.FillRRect);
        pill.Shape.Rect.Height.Should().Be(40);
        pill.Shape.Radii.TopLeft.Should().Be(20, "Radius.Full normalizes to height/2 on a 40dp pill");
        pill.Paint.Color.Should().Be(Theme.SurfaceSubtle.Resolve(ThemeMode.Light));
    }

    [Fact]
    public void SearchField_ClearTap_RoutesAnEmptyQueryThroughOnChanged()
    {
        string? changed = null;
        var host = new PhotonHost(new SearchField("rio", value => changed = value),
            Theme, ThemeMode.Light, 260, 40);
        var frame = host.RenderFrame(new DisplayListBuilder());

        var clear = frame.HitRegions.Single();
        host.Tap(clear.Bounds.X + clear.Bounds.Width / 2, clear.Bounds.Y + clear.Bounds.Height / 2);

        changed.Should().Be("", "clear works through the controlled model — the app owns the query");
    }

    [Theory]
    [InlineData(ThemeMode.Light, "textinput-light")]
    [InlineData(ThemeMode.Dark, "textinput-dark")]
    public void TextInputForms_Golden(ThemeMode mode, string golden)
    {
        var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S2) };
        column.Add(new TextInput("ana@equantic", label: "Email", leading: Icons.Mail,
            helper: "We never share it."));
        column.Add(new TextInput("", label: "Company", placeholder: "eQuantic",
            error: "Required."));
        column.Add(new SearchField("rio"));

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(280, 260);
        var host = new PhotonHost(column, PhotonTheme.Instance, mode, 280, 260);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
