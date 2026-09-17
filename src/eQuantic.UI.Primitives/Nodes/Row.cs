namespace eQuantic.UI.Primitives;

/// <summary>Horizontal flex (spec A2). Cross defaults to Center. Mirrors automatically in RTL (realizer concern).</summary>
public sealed class Row : FlexNode
{
    public override string NodeKind => "row";

    /// <summary>
    /// Everything an author reaches for on a flex, as PARAMETERS. The properties are init-only, so
    /// before this the only way to set them was an object initializer — which means <c>new</c>, and
    /// <c>new</c> is exactly what the declarative surface removes: <c>Row(gap: …)</c> could pass a
    /// gap and nothing else, so a row that had to centre its content dropped out of the surface
    /// entirely.
    /// <para>
    /// Width, height, background and corner radius are deliberately NOT here — a flex carrying
    /// those is a Box wrapping a flex, and the properties say as much themselves.
    /// </para>
    /// </summary>
    public Row(float gap = 0, MainAlign main = MainAlign.Start,
        CrossAlign cross = CrossAlign.Center, bool wrap = false, float? runGap = null,
        EdgeInsets? padding = null)
    {
        Gap = gap;
        Main = main;
        Cross = cross;
        Wrap = wrap;
        RunGap = runGap;
        Padding = padding ?? default;
    }

    public override CrossAlign Cross { get; init; } = CrossAlign.Center;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
