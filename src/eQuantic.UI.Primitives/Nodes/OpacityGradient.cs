namespace eQuantic.UI.Primitives;

/// <summary>
/// The opacity a <see cref="OpacityMask"/> subtree keeps at every point (#1938): a gradient of opacities
/// over the node's bounds, Flutter's <c>ShaderMask</c> with <c>BlendMode.dstIn</c> and the CSS
/// <c>mask-image</c> of a gradient.
/// <para>
/// A CLOSED set: <see cref="LinearOpacityGradient"/> and <see cref="RadialOpacityGradient"/>, and nothing else. A realizer
/// draws them through <see cref="IOpacityGradientVisitor{TState, TResult}"/>, so a kind added here is a build
/// error in every realizer that does not draw it. The set is closed by the <c>private protected</c>
/// members below, which no type outside this assembly can reach.
/// </para>
/// <para>
/// ONE OR MORE <see cref="OpacityStop"/>s, any number, resolved as CSS Images 4 resolves color stops
/// (measured in Chrome and Firefox): <see cref="Resolved"/> is the list every target draws.
/// </para>
/// </summary>
public abstract record OpacityGradient
{
    private readonly OpacityStop[] _stops;
    private readonly OpacityStop[] _resolved;

    private protected OpacityGradient(IReadOnlyList<OpacityStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        _stops = [.. stops];
        if (_stops.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(stops), stops,
                "A gradient needs at least one stop: with none there is no opacity to give any point.");
        foreach (var stop in _stops)
        {
            if (!float.IsFinite(stop.Offset))
                throw new ArgumentOutOfRangeException(nameof(stops), stop.Offset,
                    "A mask's stop sits at a finite offset along its gradient's ray.");
            if (!(stop.Opacity is >= 0 and <= 1))
                throw new ArgumentOutOfRangeException(nameof(stops), stop.Opacity,
                    "A mask's stop keeps an opacity from 0 (nothing shows) to 1 (all of it shows).");
        }
        _resolved = Fixup(_stops);
    }

    /// <summary>The stops as they were written, in order.</summary>
    public IReadOnlyList<OpacityStop> Stops => _stops;

    /// <summary>
    /// The stops after CSS's fix-up, the one list both targets draw: a stop whose offset is below an earlier
    /// one's moves up to the largest earlier offset, so offsets never decrease. Repeated offsets stay (two stops
    /// at one offset are a hard step), and an offset outside 0..1 stays.
    /// </summary>
    public IReadOnlyList<OpacityStop> Resolved => _resolved;

    /// <summary>
    /// The opacity at a position <paramref name="t"/> along the ray, over <see cref="Resolved"/>: piecewise
    /// linear between the stops, the first stop's opacity before it and the last stop's after it. At a hard
    /// step the later stop's opacity holds from the step on, and a single stop is that opacity everywhere. A
    /// position that is not a number is past the end.
    /// <para>
    /// The normative answer Photon composites with. The web writes CSS and leaves the browser to compute it.
    /// </para>
    /// </summary>
    [ServerOnly]
    public float OpacityAt(float t)
    {
        var stops = _resolved;
        if (float.IsNaN(t)) return stops[^1].Opacity;
        // The last stop at or before t: the later of a pair sharing an offset, which is the hard step.
        var at = -1;
        for (var i = 0; i < stops.Length && stops[i].Offset <= t; i++) at = i;
        if (at < 0) return stops[0].Opacity;
        if (at == stops.Length - 1) return stops[^1].Opacity;
        var from = stops[at];
        var to = stops[at + 1];
        return from.Opacity + (to.Opacity - from.Opacity) * ((t - from.Offset) / (to.Offset - from.Offset));
    }

    /// <summary>Hands this mask to the visitor's method for its kind.</summary>
    public TResult Accept<TState, TResult>(IOpacityGradientVisitor<TState, TResult> visitor, TState state) =>
        Dispatch(visitor, state);

    /// <summary>The kind's own call of <see cref="Accept"/>: private protected, so a kind can only be declared
    /// in this assembly.</summary>
    private protected abstract TResult Dispatch<TState, TResult>(IOpacityGradientVisitor<TState, TResult> visitor, TState state);

    /// <summary>Two masks of one kind are equal when their stops are, in order: a list compares by what it holds,
    /// where a record's own equality would compare the reference.</summary>
    public virtual bool Equals(OpacityGradient? other) =>
        other is not null && EqualityContract == other.EqualityContract && _stops.AsSpan().SequenceEqual(other._stops);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EqualityContract);
        foreach (var stop in _stops) hash.Add(stop);
        return hash.ToHashCode();
    }

    private static OpacityStop[] Fixup(OpacityStop[] stops)
    {
        var resolved = new OpacityStop[stops.Length];
        var floor = float.NegativeInfinity;
        for (var i = 0; i < stops.Length; i++)
        {
            floor = MathF.Max(floor, stops[i].Offset);
            resolved[i] = stops[i] with { Offset = floor };
        }
        return resolved;
    }
}
