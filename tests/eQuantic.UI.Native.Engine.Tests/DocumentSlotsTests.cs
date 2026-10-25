using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon draws the document's slots from the theme (#1038): a field's placeholder and its text face,
/// a table's scrollbars and the code editor's selection. Under a theme that sets none of them it
/// draws exactly what it drew before (the goldens hold that); under one that sets them, it draws
/// them.
/// </summary>
public class DocumentSlotsTests
{
    private static readonly ColorToken Forest = new(new Color(0x1B, 0x5E, 0x4B, 255));
    private static readonly ColorToken Gold = new(new Color(0xC9, 0xA9, 0x6E, 255));
    private static readonly ColorToken Cream = new(new Color(0xFA, 0xF7, 0xF2, 255));

    private static readonly IAppTheme Brand = new SlottedTheme(PhotonTheme.Instance)
    {
        Text = new("Atkinson Hyperlegible Next"),
        Wash = Forest.WithOpacity(0.4f),
        Hint = Gold,
        Thumb = Forest,
        Track = Cream,
    };

    [Fact]
    public void AFieldsPlaceholder_IsTheThemesPlaceholder()
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(new TextEntry("") { Placeholder = "you@company.com" }, 260, 200, Brand, ThemeMode.Light, builder);

        builder.Build().Commands.ToArray().Single(c => c.Kind == DrawCommandKind.FillRRect).Paint.Color
            .Should().Be(Gold.Resolve(ThemeMode.Light).WithOpacity(0.30f),
                "the headless stand-in bar is drawn in the colour the glyphs would be");
    }

    /// <summary>A field's text goes through the merge a Text goes through, so the theme's face
    /// reaches the field a person types into as it reaches every label.</summary>
    [Fact]
    public void AFieldsText_IsSetInTheThemesTextFace()
    {
        new TextEntry("ana").Resolve(Brand).Family.Should().Be(new FontFamily("Atkinson Hyperlegible Next"));
        new TextEntry("ana").Resolve(PhotonTheme.Instance).Family.Should().BeNull("Photon keeps the platform's own face");
    }

    [Fact]
    public void ATablesScrollbars_AreTheThemesScrollbarColours()
    {
        var paints = TableScrollbarPaints(Brand);

        paints.Should().Contain(Forest.Resolve(ThemeMode.Light), "the thumb").And.Contain(Cream.Resolve(ThemeMode.Light), "the track");
        TableScrollbarPaints(PhotonTheme.Instance).Should()
            .Contain(PhotonTheme.Instance.TextMuted.Resolve(ThemeMode.Light).WithOpacity(0.55f), "the default thumb is what Photon drew")
            .And.Contain(PhotonTheme.Instance.SurfaceSubtle.Resolve(ThemeMode.Light), "and so is the track");
    }

    [Fact]
    public void TheCodeEditorsSelection_IsTheThemesSelection()
    {
        var editor = new CodeEditor("one two", "csharp") { ShowLineNumbers = false, MatchBrackets = false };
        editor.Editor.Selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 7));
        var host = new PhotonHost(editor, Brand, ThemeMode.Light, 500, 400) { TextRasterizer = new FixedWidthRasterizer() };

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);

        builder.Build().Commands.ToArray()
            .Should().Contain(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Forest.WithOpacity(0.4f).Resolve(ThemeMode.Light));
    }

    /// <summary>
    /// A theme that draws its selection opaque names the colour selected text takes, and the editor's
    /// selected glyphs take it, or they would vanish into the band. The rest keep their colours, and a
    /// theme that names none recolours nothing.
    /// </summary>
    [Fact]
    public void TheCodeEditorsSelectedText_TakesTheThemesOnSelection()
    {
        var magenta = new ColorToken(new Color(255, 0, 255, 255));
        var opaque = new SlottedTheme(PhotonTheme.Instance) { Wash = Forest, SelectedText = magenta };

        SelectedRuns(opaque, magenta).Should().Equal("two");
        SelectedRuns(PhotonTheme.Instance, magenta).Should().BeEmpty();
    }

    private static List<string> SelectedRuns(IAppTheme theme, ColorToken ink)
    {
        var editor = new CodeEditor("one two", "csharp") { ShowLineNumbers = false, MatchBrackets = false };
        editor.Editor.Selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 7));
        var host = new PhotonHost(editor, theme, ThemeMode.Light, 500, 400) { TextRasterizer = new FixedWidthRasterizer() };
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);

        var runs = new List<string>();
        void Walk(LayoutNode node)
        {
            if (node.Source is Text text && text.Color == ink) runs.Add(text.Content);
            foreach (var child in node.Children) Walk(child);
        }
        Walk(host.LastFrame!.Root);
        return runs;
    }

    private static Color[] TableScrollbarPaints(IAppTheme theme)
    {
        var table = new TableView(new UniformTableSpans(1_000, 28), new UniformTableSpans(100, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM));
        var host = new PhotonHost(table, theme, ThemeMode.Light, 400, 300) { SmoothScroll = false, ReducedMotion = true };
        for (var frame = 0; frame < 4; frame++) host.RenderFrame(new DisplayListBuilder(), frame * 16);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 64);
        return builder.Build().Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.FillRRect).Select(c => c.Paint.Color).ToArray();
    }

    /// <summary>Photon's theme with the document's slots it is given.</summary>
    private sealed class SlottedTheme(IAppTheme inner) : IAppTheme
    {
        public FontFamily? Text { get; init; }
        public ColorToken? Wash { get; init; }
        public ColorToken? SelectedText { get; init; }
        public ColorToken? Hint { get; init; }
        public ColorToken? Thumb { get; init; }
        public ColorToken? Track { get; init; }

        public FontFamily? TextFamily => Text;
        public ColorToken Selection => Wash ?? inner.Selection;
        public ColorToken? OnSelection => SelectedText ?? inner.OnSelection;
        public ColorToken Placeholder => Hint ?? inner.Placeholder;
        public ColorToken ScrollbarThumb => Thumb ?? inner.ScrollbarThumb;
        public ColorToken ScrollbarTrack => Track ?? inner.ScrollbarTrack;

        public ColorToken Background => inner.Background;
        public ColorToken Surface => inner.Surface;
        public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
        public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
        public ColorToken Border => inner.Border;
        public ColorToken BorderStrong => inner.BorderStrong;
        public ColorToken TextPrimary => inner.TextPrimary;
        public ColorToken TextSecondary => inner.TextSecondary;
        public ColorToken TextMuted => inner.TextMuted;
        public ColorToken TextInverse => inner.TextInverse;
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
