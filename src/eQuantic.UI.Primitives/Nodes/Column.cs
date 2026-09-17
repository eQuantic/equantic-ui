namespace eQuantic.UI.Primitives;

/// <summary>Vertical flex (spec A2). Cross defaults to Stretch (full-width children).</summary>
public sealed class Column : FlexNode
{
    public override string NodeKind => "column";

    /// <summary>The Row constructor's twin — see it for why these are parameters. Cross defaults to
    /// Stretch here, which is a Column's own default and not Row's.</summary>
    public Column(float gap = 0, MainAlign main = MainAlign.Start,
        CrossAlign cross = CrossAlign.Stretch, bool wrap = false, float? runGap = null,
        EdgeInsets? padding = null)
    {
        Gap = gap;
        Main = main;
        Cross = cross;
        Wrap = wrap;
        RunGap = runGap;
        Padding = padding ?? default;
    }

    public override CrossAlign Cross { get; init; } = CrossAlign.Stretch;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
