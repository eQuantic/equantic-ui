using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// A box's CSS background, every layer above its colour (#1937): the gradient, the glow and the
/// patterns, in CSS's order, where the FIRST layer is on top. So the patterns are written from the
/// last to the first, under the glow and the gradient, the reverse of the order Photon paints them
/// in, and a box stacks its layers the same on both targets. The runtime's twin is
/// <c>backgroundLayers</c> in lowering.ts.
/// <para>
/// Each list is written only when one of its entries is not CSS's default, and then in full, padded
/// with the default for every other layer, because CSS repeats a short list from its start. The size
/// list keeps the rule it always had: written whenever the box has a pattern.
/// </para>
/// </summary>
internal sealed class BackgroundLayers
{
    private readonly List<BackgroundLayer> _layers = [];
    private readonly bool _patterned;

    public BackgroundLayers(in BoxStyle style, IAppTheme theme)
    {
        if (style.Gradient is { } gradient) _layers.Add(new BackgroundLayer(TokenCss.Gradient(gradient)));
        if (style.Glow is { } glow) _layers.Add(new BackgroundLayer(TokenCss.Glow(glow)));
        if (style.Patterns is not { Count: > 0 } patterns) return;
        _patterned = true;
        var painter = new PatternLayers(theme, Variants);
        for (var index = patterns.Count - 1; index >= 0; index--)
            _layers.AddRange(patterns[index].Accept(painter, index));
    }

    /// <summary>The per-element inputs of a layer whose image differs by mode or contrast: one custom
    /// property per variant, which stays inline on the element (<see cref="HtmlStyle.CustomProperties"/>).</summary>
    public Dictionary<string, string> Variants { get; } = new(StringComparer.Ordinal);

    public string? Images => _layers.Count == 0 ? null : string.Join(", ", _layers.Select(layer => layer.Image));

    public string? Sizes => _patterned ? string.Join(", ", _layers.Select(layer => layer.Size)) : null;

    public string? Repeats => List(layer => layer.Repeat, BackgroundLayer.DefaultRepeat);

    public string? Positions => List(layer => layer.Position, BackgroundLayer.DefaultPosition);

    public string? Origins => List(layer => layer.Origin, BackgroundLayer.DefaultOrigin);

    public string? Clips => List(layer => layer.Clip, BackgroundLayer.DefaultClip);

    public string? Attachments => List(layer => layer.Attachment, BackgroundLayer.DefaultAttachment);

    /// <summary>Every list this background writes, as the declarations a state's diff carries.</summary>
    public IEnumerable<(string Property, string Value)> Declarations()
    {
        if (Images is { } images) yield return ("background-image", images);
        if (Sizes is { } sizes) yield return ("background-size", sizes);
        if (Repeats is { } repeats) yield return ("background-repeat", repeats);
        if (Positions is { } positions) yield return ("background-position", positions);
        if (Origins is { } origins) yield return ("background-origin", origins);
        if (Clips is { } clips)
        {
            yield return ("-webkit-background-clip", clips);
            yield return ("background-clip", clips);
        }
        if (Attachments is { } attachments) yield return ("background-attachment", attachments);
    }

    /// <summary>Writes every list and the variants into <paramref name="style"/>.</summary>
    public void WriteInto(HtmlStyle style)
    {
        style.BackgroundImage = Images;
        style.BackgroundSize = Sizes;
        style.BackgroundRepeat = Repeats;
        style.BackgroundPosition = Positions;
        style.BackgroundOrigin = Origins;
        style.BackgroundClip = Clips;
        style.BackgroundAttachment = Attachments;
        if (Variants.Count == 0) return;
        style.CustomProperties ??= new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in Variants) style.CustomProperties[name] = value;
    }

    private string? List(Func<BackgroundLayer, string> entry, string standard) =>
        _layers.Any(layer => entry(layer) != standard) ? string.Join(", ", _layers.Select(entry)) : null;
}
