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
///
/// <para>
/// BEHAVIOUR remembers nothing; the DIAGNOSTIC remembers for the run. Those are different
/// statements and the distinction is the point: a recovered component is built and drawn exactly
/// like one that never threw, while <see cref="Contained"/> still names it until the run ends.
/// Anything else would let a failure disappear because the next frame happened to be clean, which
/// is the silence the tally exists to remove.
/// </para>
///
/// <para>
/// HOST ONLY, for the same reason and with the same fence as <see cref="FaceResolution"/>: the
/// runtime exports no <c>ComponentBoundary</c>, and an exception list in a test records that
/// decision without enforcing it. eqc routes this whole namespace to <c>@equantic/runtime</c>, so a
/// client component naming this type would compile, emit, and die at hydration on "does not provide
/// an export named".
/// </para>
/// </summary>
[ServerOnly]
public static class ComponentBoundary
{
    private static readonly AsyncLocal<bool> _diagnostics = new();
    private static readonly AsyncLocal<Action<UiComponent, Exception>?> _report = new();
    private static readonly AsyncLocal<List<string>?> _contained = new();

    /// <summary>
    /// How deep the current expansion walk is, in COMPONENTS. Thread-static rather than
    /// <see cref="AsyncLocal{T}"/> like everything else here, and the difference is what each one
    /// measures: those are a render SCOPE a host arms, which may cross an await, while this counts
    /// one synchronous walk that never leaves its thread. Restored in a finally, so two walks
    /// interleaved on one thread cannot see each other's depth either.
    /// </summary>
    [ThreadStatic] private static int _depth;

    /// <summary>
    /// How many components may nest inside one another before the walk is treated as
    /// non-terminating. Not a guess about cycles — a cycle is INFINITE, so it exceeds any bound, and
    /// the only question is whether a real tree could. Sixty-four is several times the deepest thing
    /// anybody composes, and the evidence is that the Studio gallery walk and the layout goldens
    /// render the real pages through this path: a page that exceeded it would be contained, and they
    /// would fail naming it.
    /// </summary>
    private const int MaxDepth = 64;

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

    /// <summary>
    /// Forgets what was contained. A host arms a RUN with this, as it does the report sink — the
    /// scope is the run and deliberately not the frame: a component that threw and then recovered
    /// still threw, and a summary that drops it because the next frame was clean is the silence
    /// this tally exists to remove. What it prevents is a later run being read as carrying an
    /// earlier one's failures, which a long-lived host reaches immediately.
    /// </summary>
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
            return Contain(component, error, context);
        }
    }

    /// <summary>
    /// Expands a component AND realizes what it built — the move every realizer actually makes, and
    /// the one that has to be bounded, because <see cref="BuildContained"/> alone cannot be.
    ///
    /// <para>
    /// A component whose <c>Build</c> returns another component is ordinary composition. One that
    /// returns ITSELF, or two that return each other, is a chain with no subtree at the end of it —
    /// and every realizer expands what Build returned, so the chain walks back into Build forever
    /// and dies on a <see cref="StackOverflowException"/>, which .NET does not let anyone catch. The
    /// request goes with it, or the frame, or the window: precisely what this boundary exists to
    /// prevent, arriving by the one route it did not watch.
    /// </para>
    ///
    /// <para>
    /// Exceeding the bound is a CONTAINED failure, not a throw and not a silent null: the same
    /// surface a throwing component gets, the same entry in <see cref="Contained"/>, the same
    /// <see cref="Report"/>. A host that refuses to exit zero while the tally is non-empty catches a
    /// cycle exactly as it catches a throw, and every realizer inherits that by calling this instead
    /// of expanding by hand.
    /// </para>
    ///
    /// <para>
    /// NOT the email realizer, and that is deliberate rather than an omission: it expands through
    /// <c>Build</c> on purpose, because a component that fails must fail the SEND rather than reach
    /// an inbox dressed as a describe-box. It takes the same bound through <see cref="Enter"/>, and
    /// answers it by THROWING — catchable, so the render fails and the message is not sent, where a
    /// stack overflow took the process with it and sent nothing while reporting nothing.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The caller's state travels as an ARGUMENT rather than a capture, so <c>realize</c> can be a
    /// <c>static</c> lambda and the delegate is cached once instead of allocated per component. It
    /// is the shape .NET's own APIs use for this (<c>ConcurrentDictionary.GetOrAdd</c>,
    /// <c>string.Create</c>), and it is not decoration: Photon measures every component every frame,
    /// and the closure form put the frame 2,786 bytes over the recycled-frame budget, which is how
    /// it was found: <c>PerfHarnessTests.SteadyMotion_WithRecycledFrames_AllocatesFarLess</c> read
    /// 78,562 against a ceiling of 75,776 and failed.
    /// </remarks>
    public static TResult ExpandContained<TState, TResult>(this UiComponent component,
        ComponentContext context, TState state, Func<VisualNode, TState, TResult> realize)
    {
        if (_depth >= MaxDepth)
            return realize(Contain(component, Exceeded(component), context), state);

        _depth++;
        try
        {
            return realize(BuildContained(component, context), state);
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>
    /// One level of the expansion walk, for a realizer that answers the bound ITSELF — and the only
    /// other thing allowed to move <see cref="_depth"/>, so the two answers share one counter rather
    /// than keeping two that could disagree.
    ///
    /// <para>
    /// The EMAIL realizer is why this exists. It expands through <c>Build</c> rather than
    /// <see cref="BuildContained"/> on purpose: a component that fails must fail the send, never
    /// reach an inbox dressed as a describe-box. So containment is the wrong answer at the bound
    /// too, and a throw is the right one — it is catchable, it fails the render, and the message
    /// does not go out. What it replaces is a <see cref="StackOverflowException"/>, which .NET does
    /// not let anyone catch: the sending process died and nothing was reported at all.
    /// </para>
    ///
    /// <para>
    /// A struct, disposed by <c>using</c>, because the scope IS the recursion: the walk enters on
    /// the way down and leaves on the way back up, including when the throw unwinds through it. A
    /// <c>default</c> one decrements nothing, so a value nobody entered cannot corrupt the count.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The walk is already <see cref="MaxDepth"/> components deep. Same sentence
    /// <see cref="ExpandContained"/> puts on its containment surface, because it is the same
    /// observation.
    /// </exception>
    public static Expansion Enter(UiComponent component)
    {
        if (_depth >= MaxDepth) throw Exceeded(component);
        _depth++;
        return new Expansion(true);
    }

    /// <summary>One level of an expansion walk. See <see cref="Enter"/>.</summary>
    public readonly struct Expansion : IDisposable
    {
        private readonly bool _entered;

        internal Expansion(bool entered) => _entered = entered;

        public void Dispose()
        {
            if (_entered) _depth--;
        }
    }

    /// <summary>
    /// What was OBSERVED, not what is suspected: the bound is on depth, so all it knows is that
    /// <see cref="MaxDepth"/> components nested without one of them reaching an ordinary node. That
    /// is a cycle in every case anybody has met, and it is also what a finite tree nested deeper
    /// than this boundary allows would look like — naming only the cycle would hand that developer a
    /// false diagnosis to chase.
    /// <para>
    /// ONE sentence for both answers. A developer who meets the containment panel on a page and the
    /// failed send in a log is meeting the same fact, and two phrasings of it would read as two
    /// problems.
    /// </para>
    /// </summary>
    private static InvalidOperationException Exceeded(UiComponent component) =>
        new($"{component.GetType().Name} was still building components after {MaxDepth} "
            + "expansions, so what it builds was never reached: a component that builds itself, "
            + "a cycle of components, or a tree nested deeper than this boundary allows.");

    /// <summary>
    /// What a contained failure costs, wherever it comes from: the name on the tally, one call to
    /// the host's logger, and the surface that takes the subtree's place. Shared by the two ways a
    /// component can fail to produce one — a throw, and a chain that never ends.
    /// </summary>
    private static VisualNode Contain(UiComponent component, Exception error, ComponentContext context)
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
