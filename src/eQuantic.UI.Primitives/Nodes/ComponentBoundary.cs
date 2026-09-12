namespace eQuantic.UI.Primitives;

/// <summary>
/// The seam every target expands a component through, and the only place a component's
/// <see cref="UiComponent.Build"/> is allowed to take the rest of the tree down with it.
///
/// <para>
/// A component that throws used to end the whole render: on the server the request became a 500,
/// in the browser the mount threw and the page stayed white, in a window the frame never arrived.
/// One null reference in a card and nothing else on the screen existed. That is not a property of
/// the component model — it is the absence of a boundary, and the boundary belongs HERE, at the one
/// expansion seam, rather than in every host that ever calls one.
/// </para>
///
/// <para>
/// Contained means contained: the failing component's SUBTREE is replaced by a surface that says so,
/// its siblings render, and the app stays interactive. State is not touched and no failure is
/// remembered — a component that stops throwing (a retry, new props, a hot reload) simply builds
/// again on the next pass.
/// </para>
/// </summary>
public static class ComponentBoundary
{
    private static readonly AsyncLocal<bool> _diagnostics = new();
    private static readonly AsyncLocal<Action<UiComponent, Exception>?> _report = new();
    private static readonly AsyncLocal<List<string>?> _contained = new();

    /// <summary>
    /// Whether a DEVELOPER is watching. Armed by the host per render scope — the SSR pipeline from
    /// the environment, the browser boot from <c>__EQ_DEV__</c>, a window from its build — the same
    /// way the atomic style sink is armed around a page render.
    ///
    /// <para>
    /// Off, the panel says only that a section could not be displayed: an exception message is
    /// written for whoever wrote the code, and it can carry an id, a path or a query someone's user
    /// should never read. On, it names the component and quotes the throw.
    /// </para>
    /// </summary>
    public static bool Diagnostics
    {
        get => _diagnostics.Value;
        set => _diagnostics.Value = value;
    }

    /// <summary>
    /// Where a contained failure goes so it is not merely swallowed — the host's logger. A boundary
    /// without this trades a loud crash for a silent one, which is worse: nobody is paged and the
    /// bug lives forever behind a small red box.
    /// </summary>
    public static Action<UiComponent, Exception>? Report
    {
        get => _report.Value;
        set => _report.Value = value;
    }

    /// <summary>
    /// Which components this scope has contained, by type name, first seen first. EMPTY is the
    /// answer a healthy render gives, and that is the whole point of it.
    ///
    /// <para>
    /// <see cref="Report"/> sends a failure to a log, which a human reads. Nothing an automated
    /// check looks at moved: a headless run of an app whose entire title bar threw on every frame
    /// still presented its frames, still exited zero, and reported MORE accessibility elements than
    /// a healthy one — the containment surface has text of its own. Reported by a consumer who had
    /// written "frames presented and exit 0" down as the check that catches a black window, and
    /// found it could not.
    /// </para>
    ///
    /// <para>
    /// Names rather than a count, because the count a run produces is frames × failures and the
    /// actionable fact is WHICH component. A host prints this beside its frame summary, and a
    /// strict one refuses to exit zero while it is non-empty.
    /// </para>
    /// </summary>
    public static IReadOnlyCollection<string> Contained =>
        _contained.Value is { } seen ? seen.ToArray() : [];

    /// <summary>Forgets what was contained — a host arms a render scope with this, as it does the
    /// report sink, so one frame's failures are not read as the next one's.</summary>
    public static void ClearContained() => _contained.Value = null;

    /// <summary>
    /// Expands a component the way every realizer must: its subtree if <see cref="UiComponent.Build"/>
    /// returns, the contained failure surface if it throws.
    /// </summary>
    public static VisualNode BuildContained(this UiComponent component, ComponentContext context)
    {
        try
        {
            return component.Build(context);
        }
        catch (Exception error)
        {
            // DISTINCT, and the reason is the failure mode itself: a component that throws throws on
            // every frame, so a count would report the frame rate and bury the one fact worth
            // having, which is its name.
            var seen = _contained.Value ??= [];
            var name = component.GetType().Name;
            if (!seen.Contains(name)) seen.Add(name);
            Report?.Invoke(component, error);
            return Describe(component, error, context);
        }
    }

    /// <summary>
    /// The surface that takes the failed subtree's place, built from the VOCABULARY — so it is the
    /// same surface on a page and in a window, and it costs no target-specific code to draw.
    /// </summary>
    private static VisualNode Describe(UiComponent component, Exception error, ComponentContext context)
    {
        var tint = context.Theme.Colors(Variant.Destructive);
        var diagnostics = Diagnostics;

        var lines = new Column(gap: Space.S1);
        lines.Add(new Text(
            diagnostics ? $"{component.GetType().Name} failed to render" : "This section could not be displayed.",
            TypeRole.Caption, tint.OnSubtle, maxLines: 2)
        {
            StyleOverride = new TypeStyle(13, 18, FontWeight.SemiBold, 0, 1.3f),
        });

        if (diagnostics)
        {
            lines.Add(new Text($"{error.GetType().Name}: {error.Message}", TypeRole.Caption, tint.OnSubtle, maxLines: 6)
            {
                StyleOverride = new TypeStyle(12, 17, FontWeight.Regular, 0, 1.3f),
                Mono = true,
            });
        }

        var content = new Row(gap: 10) { Cross = CrossAlign.Start };
        content.Add(new Icon(Icons.Error, IconSize.Dense, tint.OnSubtle));
        content.Add(new Flexible(lines));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(14, 12, 14, 12),
            Background = tint.Subtle,
            BorderWidth = 1,
            BorderColor = tint.Base,
            CornerRadius = new CornerRadii(context.Theme.Shape(ShapeScale.Large)),
        }, content);
    }
}
