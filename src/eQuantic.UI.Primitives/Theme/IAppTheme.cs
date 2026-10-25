namespace eQuantic.UI.Primitives;

/// <summary>
/// The theme every target draws with (spec §09): the web writes it into the page's stylesheet, Photon
/// resolves it once per build pass into plain structs on the primitive tree (zero allocation in
/// steady state, plan D9), and an email inlines it. Every colour is a light/dark
/// <see cref="ColorToken"/>. It says the document as well as the components (the surface, the text,
/// the faces, the selection, the placeholder and the scrollbars), so an app writes no CSS for any of
/// them.
/// </summary>
public interface IAppTheme
{
    // Surfaces & structure
    ColorToken Background { get; }
    ColorToken Surface { get; }
    ColorToken SurfaceSubtle { get; }
    /// <summary>The moving glint of the Skeleton shimmer (spec B16) — translucent white over
    /// SurfaceSubtle in both modes; decorative, no contrast requirement.</summary>
    ColorToken SurfaceHighlight { get; }
    ColorToken Border { get; }
    ColorToken BorderStrong { get; }

    // Text tiers
    ColorToken TextPrimary { get; }
    ColorToken TextSecondary { get; }
    ColorToken TextMuted { get; }
    ColorToken TextInverse { get; }

    // Interaction chrome
    ColorToken FocusRing { get; }
    ColorToken LinkColor { get; }
    ColorToken Scrim { get; }

    // The document's chrome: what a page or a window draws around the components. Each defaults to
    // the value the SDK drew with before it was the theme's, so a theme that sets none draws as it did.

    /// <summary>
    /// The wash behind selected text: the document's selection on the web, and the code editor's on
    /// every target. Defaults to <see cref="FocusRing"/> at 28%, translucent so the text keeps its
    /// colour through it.
    /// </summary>
    ColorToken Selection => FocusRing.WithOpacity(0.28f);

    /// <summary>
    /// The colour selected text takes, or null for its own. A theme whose <see cref="Selection"/> is
    /// opaque names one, so the selected text stays readable on it.
    /// </summary>
    ColorToken? OnSelection => null;

    /// <summary>The placeholder an empty field shows. Defaults to <see cref="TextMuted"/>.</summary>
    ColorToken Placeholder => TextMuted;

    /// <summary>
    /// A scrollbar's thumb: the page's on the web, and a table's on every target. Defaults to
    /// <see cref="TextMuted"/> at 55%.
    /// </summary>
    ColorToken ScrollbarThumb => TextMuted.WithOpacity(0.55f);

    /// <summary>A scrollbar's track. Defaults to <see cref="SurfaceSubtle"/>.</summary>
    ColorToken ScrollbarTrack => SurfaceSubtle;

    /// <summary>
    /// The five sub-tokens for a variant. Filled variants come from the palette; Outline / Ghost / Link
    /// are DERIVED (spec §01): transparent fill + Text/Border tokens, pressed = SurfaceSubtle
    /// (Link: pressed swaps the TEXT to Primary.Pressed — fill stays transparent).
    /// </summary>
    VariantColors Colors(Variant variant);

    /// <summary>The type scale row for a role (spec §02).</summary>
    TypeStyle Type(TypeRole role);

    /// <summary>
    /// The face this theme sets CODE in, or null for the platform's own fixed-pitch face.
    ///
    /// <para>
    /// A second family, and it has to be second rather than a role: <c>TypeStyle.Family</c> is
    /// themeable per ROLE, and <c>Mono</c> is a per-NODE property — a branch name is monospaced and
    /// the word beside it is not, both <c>TypeRole.Caption</c> — so the two never meet. No role in
    /// any shipped scale sets <c>Mono</c>, which makes a <c>style.Mono ? code : text</c> branch
    /// inside <see cref="Type"/> dead code rather than the answer. Reported by the eQuantic Code
    /// IDE, whose handoff names one proportional face and one code face, and whose theme could
    /// carry exactly one of them.
    /// </para>
    ///
    /// <para>
    /// Without this a brand code face has to be named at every call site, which is the work a theme
    /// exists to remove.
    /// </para>
    /// <para>
    /// A family with files ships them: the web serves and declares them, and Photon registers them
    /// (<see cref="FontFamily"/>).
    /// </para>
    /// </summary>
    FontFamily? MonoFamily => null;

    /// <summary>
    /// The face this theme sets TEXT in, or null for the platform's own: the face of every text whose
    /// role and call site name none, and the document's face on the web. The proportional twin of
    /// <see cref="MonoFamily"/>, resolved by the same merge (<c>EffectiveTypeStyle.WithThemeFace</c>).
    /// <para>
    /// A family with files ships them: the web serves and declares them, preloading the upright ones,
    /// and Photon registers them with its text engine (<see cref="FontFamily"/>). A family with none is
    /// the platform's: one the machine does not have falls back on the web, and Photon reports it
    /// unresolved, as it does for a role's face.
    /// </para>
    /// </summary>
    FontFamily? TextFamily => null;

    /// <summary>Elevation level 0–5 (spec §05). Level 0 is none (+1dp Border); dark E1–E2 ALSO require a 1dp border.</summary>
    ShadowSpec Elevation(int level);

    /// <summary>The corner radius (dp) for a shape role — SHAPE is theme-driven (M3 customizes it).</summary>
    float Shape(ShapeScale scale);

    /// <summary>Disabled is not a color pair: a 38% opacity group over the resolved style (spec §01).</summary>
    float DisabledOpacity { get; }

    /// <summary>
    /// The colours a chart draws DATA with — identity, magnitude, polarity and state — as distinct
    /// from the colours the interface is drawn with. DEFAULTED to the validated reference instance
    /// (<see cref="DataPalette.Default"/>) so a theme that never thought about charts still serves
    /// one correctly; a theme with brand ramps overrides this and nothing else. The SDK's tests
    /// audit the DEFAULT (<c>DataPaletteTests</c>); an override is the theme's own and nothing checks
    /// it at run time, so eight hues in a fixed order that clear the colour-vision gates is the
    /// contract it takes on.
    /// </summary>
    DataPalette Data => DataPalette.Default;

    /// <summary>
    /// The spreadsheet's slot (<see cref="SheetTheme"/>), the first of the per-component slots of
    /// <c>docs/ELEMENT-TREE-PLAN.md</c> §3.10 and in their shape: NULL by default, which the spreadsheet
    /// reads as its own colours derived from this theme's foundation, so every theme an app wrote keeps
    /// compiling and draws a sheet correctly. A theme that wants its grid in its own colours answers a
    /// slot naming only those; it crosses to the browser in the theme's JSON as <see cref="Data"/> does.
    /// </summary>
    SheetTheme? Sheet => null;

    /// <summary>
    /// The colour of a code token. DEFAULTED from the palette the theme already has — a design
    /// system that ships one set of variants should not have to invent a second one to show code,
    /// and a theme that wants a real syntax palette overrides this and nothing else.
    /// <para>
    /// The mapping is chosen for READING, not for decoration: comments recede to the muted tier,
    /// strings and numbers take the two calm variants (success, warning), keywords take the primary
    /// accent, and the punctuation that only separates stays quieter than the code it separates.
    /// </para>
    /// </summary>
    ColorToken Code(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => Colors(Variant.Primary).Base,
        CodeTokenKind.Type => Colors(Variant.Info).Base,
        CodeTokenKind.String => Colors(Variant.Success).Base,
        CodeTokenKind.Number => Colors(Variant.Warning).Base,
        CodeTokenKind.Comment => TextMuted,
        CodeTokenKind.Function => Colors(Variant.Tertiary).Base,
        CodeTokenKind.Attribute => Colors(Variant.Warning).OnSubtle,
        CodeTokenKind.Property => Colors(Variant.Info).OnSubtle,
        CodeTokenKind.Constant => Colors(Variant.Destructive).Base,
        CodeTokenKind.Operator => TextSecondary,
        CodeTokenKind.Punctuation => TextSecondary,
        _ => TextPrimary,
    };
}
