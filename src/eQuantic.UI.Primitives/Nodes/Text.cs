namespace eQuantic.UI.Primitives;

/// <summary>
/// A shaped paragraph (spec A8). Role-driven — free-form font sizes don't exist in the component API:
/// a <see cref="TypeRole"/> resolves size/weight/line-height/tracking and the Dynamic Type cap (§02).
/// Width fills the available line box and hugs when unconstrained; height = line count × line height;
/// truncation is shaping-time ellipsis honoring <see cref="MaxLines"/>.
/// </summary>
public sealed class Text : VisualNode
{
    public override string NodeKind => "text";

    /// <summary>
    /// Alignment, face and the style ESCAPE HATCH as parameters, for the same reason the flex
    /// containers take their layout: these are init-only, so setting one meant an object
    /// initializer, which means <c>new</c> — and a declarative author has neither.
    /// <para>
    /// <paramref name="styleOverride"/> is the answer to a scale that does not have the rung you
    /// need. The rungs are the right default and a design that reaches past them everywhere has
    /// stopped having a scale, but a closed scale with no way out is a scale that eventually gets
    /// worked around by nesting a raw HtmlElement — which is worse, because it only works on one
    /// target.
    /// </para>
    /// </summary>
    public Text(string content, TypeRole role = TypeRole.BodyL, ColorToken? color = null,
        int maxLines = 0, TextAlignment align = TextAlignment.Start, bool mono = false,
        bool tabular = false, TypeStyle? styleOverride = null, int headingLevel = 0)
    {
        Content = content;
        Role = role;
        Color = color;
        MaxLines = maxLines;
        Align = align;
        Mono = mono;
        Tabular = tabular;
        StyleOverride = styleOverride;
        HeadingLevel = headingLevel;
    }

    public string Content { get; init; }
    public TypeRole Role { get; init; }

    /// <summary><c>null</c> resolves to the theme's TextPrimary.</summary>
    public ColorToken? Color { get; init; }

    /// <summary>0 = unlimited lines.</summary>
    public int MaxLines { get; init; }

    /// <summary>
    /// Where this text sits in the document's OUTLINE — 1 to 6, or 0 for text that is not a
    /// heading (design system A9).
    /// <para>
    /// Deliberately independent of <see cref="Role"/>, which is the type scale. A section title
    /// can be the second level of the outline and visually smaller than a number above it, and a
    /// jumbo figure can be the largest thing on the page and no heading at all. Tying the two
    /// would make every layout decision a semantic one.
    /// </para>
    /// <para>
    /// It is not decoration on any target. The web emits the real <c>h1</c>–<c>h6</c>, which is
    /// what lets a screen reader jump by heading and a crawler read the page's shape; the native
    /// bridges report the platform's heading trait for the same navigation. Before this the whole
    /// framework emitted no heading element anywhere, so every document was one flat run of spans.
    /// </para>
    /// </summary>
    public int HeadingLevel
    {
        get => _headingLevel;
        // On the ACCESSOR, not the constructor parameter, so an object initializer cannot walk
        // past it: `new Text("x") { HeadingLevel = 7 }` is the door a parameter check leaves open,
        // and it would reach a realizer as an `h7` that no browser has.
        init => _headingLevel = value is >= 0 and <= 6 ? value
            : throw new ArgumentOutOfRangeException(nameof(HeadingLevel), value,
                "A heading level is 1 to 6 — HTML has six, and every other target's outline is "
                + "read from the same number. 0 is text that is not a heading.");
    }

    private readonly int _headingLevel;

    /// <summary>
    /// LINE alignment inside the paragraph's own box (CSS <c>text-align</c>): a centered display
    /// headline must center its WRAPPED lines too — container alignment only places the block.
    /// Photon fence: the native shaper positions lines start-aligned for now (single-line display
    /// text is unaffected — the box hugs).
    /// </summary>
    public TextAlignment Align { get; init; } = TextAlignment.Start;

    /// <summary>
    /// Monospace rendering (code snippets, versions, tabular figures — the design's
    /// <c>font-mono</c>). Web = the platform mono stack; Photon fence: the native rasterizer keeps
    /// the system face until <c>ITextRasterizer</c> grows a family parameter.
    /// NOTE: <see cref="Content"/> may contain <c>\n</c> — both targets honor it as a hard line
    /// break (web renders with <c>white-space: pre-line</c>; CoreText breaks paragraphs natively).
    /// </summary>
    public bool Mono { get; init; }

    /// <summary>
    /// The SLANTED cut of the face (CSS <c>font-style: italic</c>): a defined term, a citation, a
    /// caption under a figure. A face swap like <see cref="Mono"/>, and it lands in the same place
    /// — the <see cref="TypeStyle"/> the measurer and the rasterizer both read — so the box a
    /// slanted paragraph is measured into is the box it draws into.
    /// <para>
    /// On a run it is <see cref="TextRun.Italic"/>; here it slants the whole paragraph.
    /// </para>
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// TABULAR figures: every digit takes the same advance, so a number that changes in place does
    /// not make its row jiggle (a Stepper counting up, a table column, a live metric). The bundled
    /// face ships the <c>tnum</c> feature — this is the design system's own figure style, NOT a
    /// face swap like <see cref="Mono"/>. Photon fence: the native rasterizer keeps proportional
    /// figures until <c>ITextRasterizer</c> grows a font-features parameter.
    /// </summary>
    public bool Tabular { get; init; }

    /// <summary>
    /// RICH runs (spec A8's inline emphasis): when set, the paragraph renders these runs in order
    /// instead of <see cref="Content"/> — a highlighted figure inside a sentence
    /// (<c>"… ecosystem with **627k+** downloads …"</c>) without splitting the paragraph into
    /// blocks. Wrapping stays paragraph-level. Photon fence: the native shaper draws the
    /// concatenated text in the base style until multi-run shaping lands; keep Content as the
    /// accessible/plain twin of the runs.
    /// </summary>
    public IReadOnlyList<TextRun>? Spans { get; init; }

    /// <summary>The paragraph as PLAIN text: <see cref="Content"/>, or the runs joined — what the
    /// native shaper draws (multi-run fence) and what accessibility reads.</summary>
    public string PlainContent => Spans is { Count: > 0 } spans
        ? string.Concat(spans.Select(run => run.Content))
        : Content;

    /// <summary>
    /// SYSTEM COMPONENTS ONLY: overrides the role's <see cref="TypeStyle"/> with an exact style from a
    /// design-system table (e.g. the Button label sizes 13/15/16/17 per spec A12). App code uses roles —
    /// free-form font sizes remain outside the component API (spec A8).
    /// </summary>
    public TypeStyle? StyleOverride { get; init; }

    /// <summary>
    /// Fills the glyphs with a 2-stop gradient instead of <see cref="Color"/> (the display-headline
    /// treatment). Native tints the glyph COVERAGE with the gradient paint — the same
    /// <c>Paint.ColorAt</c> the SDF path uses; web lowers to <c>background-clip: text</c>. When set,
    /// <see cref="Color"/> is ignored.
    /// </summary>
    public LinearGradient? Gradient { get; init; }

    /// <summary>Spec S6: animates COLOR changes to this paragraph (the design's ubiquitous
    /// <c>transition-colors</c> on nav labels and links — a state swap recolors the text and the
    /// change should glide, not flip). <c>null</c> = snap.</summary>
    public TransitionSpec? Transition { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
