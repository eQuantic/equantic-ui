namespace eQuantic.UI.Primitives;

/// <summary>
/// What a component's <see cref="UiComponent.Build"/> pass may read: the active theme and the OS
/// Dynamic Type factor. Deliberately MODE-FREE — components author with <see cref="ColorToken"/>s,
/// never resolved colors, so one built tree realizes in light or dark (realizers resolve). Services
/// and route data join here as the shared model grows toward the web `RenderContext`.
/// </summary>
public sealed class ComponentContext
{
    public ComponentContext(IAppTheme theme, float typeScale = 1f,
        Density density = Density.Comfortable, Func<string, TypeStyle, float>? measureText = null)
    {
        Theme = theme;
        TypeScale = typeScale;
        Density = density;
        _measureText = measureText;
    }

    private readonly Func<string, TypeStyle, float>? _measureText;

    public IAppTheme Theme { get; }
    public float TypeScale { get; }

    /// <summary>
    /// What the ROUTE said — <c>context.Route.Param("slug")</c>, <c>context.Route.Query("page")</c>.
    /// Never null; an unmatched name answers null.
    /// <para>
    /// Reads the ambient per-request route, which is why a page does not have to be handed one and
    /// does not have to reach for <c>IHttpContextAccessor</c> to find it. Asking ASP.NET for the
    /// route works and costs the page its write-once-ness: a component that knows the web is a
    /// component that cannot run on Photon.
    /// </para>
    /// </summary>
    public RouteValues Route => RouteValues.Current;

    /// <summary>
    /// Whether this component was asked to draw WHERE IT STANDS rather than over everything — see
    /// <see cref="Primitives.InFlow"/>. Only the overlays answer it; every other component builds
    /// the same either way, which is why it is a question rather than an instruction.
    /// </summary>
    public bool InFlow => Primitives.InFlow.Current;

    /// <summary>
    /// How tight this TARGET wants its controls — the same reason a Mac's toolbar is not a
    /// phone's. A component reads it where it reads the theme, and never asks which target it is.
    /// </summary>
    public Density Density { get; }

    /// <summary>
    /// How WIDE this text would be in this style, in dp.
    /// <para>
    /// Layout measures text; a COMPONENT normally does not have to, and should not want to. The
    /// exception is a control whose geometry IS text geometry — a code editor mapping a click to a
    /// line and column, placing a caret between two characters, drawing a selection band across
    /// part of a line. Without this seam that control cannot be a component at all: it has to be a
    /// node with its own support in both realizers, written twice.
    /// </para>
    /// <para>
    /// Answers 0 when no measurer is present (a headless render, a test that never draws) — a
    /// caller that has to place something exactly should ask once and reuse the number, which for
    /// a monospaced face is all it ever needs: one advance times a column.
    /// </para>
    /// </summary>
    public float MeasureText(string text, TypeStyle style) =>
        _measureText is null || text.Length == 0 ? 0 : _measureText(text, style);

    /// <summary>
    /// The advance of ONE character in a monospaced style — the number a code editor turns every
    /// column into. Measured over a run and divided, because a single glyph's advance can round
    /// differently than the run it sits in.
    /// </summary>
    public float MonoAdvance(TypeStyle style)
    {
        const string sample = "0000000000";
        var width = MeasureText(sample, style with { Mono = true });
        return width > 0 ? width / sample.Length : 0;
    }
}

/// <summary>
/// Base of the SHARED component model (docs/SHARED-COMPONENTS-PLAN.md): a component IS a
/// <see cref="VisualNode"/> (it composes into any tree) whose <see cref="Build"/> expands to more
/// nodes — the same authoring shape as the web SDK (`Build(context)`), realized per target. Layout
/// engines expand components inline during measurement; realizers draw nothing for the component
/// itself (its built subtree carries the visuals).
/// </summary>
public abstract class UiComponent : VisualNode
{
    /// <summary>All components share one wire kind — realizers expand them via <see cref="Build"/>.</summary>
    public sealed override string NodeKind => "component";

    /// <summary>Produces this component's subtree. Must be PURE over component state + context —
    /// it may run more than once per frame (measurement) and on every invalidation.</summary>
    public abstract VisualNode Build(ComponentContext context);

    /// <summary>
    /// Positional-reconciler hook: when this RETAINED instance is matched against a freshly built
    /// one at the same tree position (same type + key), copy the fresh CONFIGURATION (constructor/
    /// init props) from <paramref name="next"/> onto this instance — state fields stay untouched.
    /// The default keeps the existing config (correct for components whose props never change
    /// between parent builds). Explicit by design: no reflection, AOT-safe, and the component
    /// author decides what is config vs state.
    /// </summary>
    public virtual void AdoptConfig(UiComponent next)
    {
    }
}

/// <summary>A component fully described by its constructor inputs — same contract as the web SDK's.</summary>
public abstract class StatelessComponent : UiComponent
{
}

/// <summary>
/// A component with internal state mutated through <see cref="SetState"/> — the web SDK's contract.
/// v1 caveat (pre-reconciler): state lives on the component INSTANCE, so it persists where the
/// instance persists — the retained root a host holds, or children a parent retains in fields.
/// Instance-per-build children lose state; positional state retention arrives with the reconciler
/// (plan W6), and parity with the web's `CreateState`/`ComponentState` split is resolved at the
/// Core unification.
/// </summary>
public abstract class StatefulComponent : UiComponent
{
    /// <summary>Raised after every <see cref="SetState"/> — hosts subscribe to schedule a rebuild.</summary>
    public event Action? StateInvalidated;

    private bool _mounted;

    protected void SetState(Action mutate)
    {
        mutate();
        StateInvalidated?.Invoke();
    }

    /// <summary>
    /// Runs ONCE, when this instance has entered a live tree — read stored state, subscribe to a
    /// device, start the request whose answer the next build will show.
    /// <para>
    /// A constructor is the wrong place for all three: the reconciler builds fresh instances every
    /// pass and keeps the RETAINED one, so anything a constructor starts is started again for an
    /// instance that is then thrown away. This runs on the instance that stays.
    /// </para>
    /// <para>
    /// It does NOT mean "the pixels exist" — Photon has no DOM to read geometry from, so a hook
    /// promising it could not be write-once. It means: this component is in the tree, its build is
    /// about to be shown, and a <c>SetState</c> from here schedules the next pass rather than
    /// fighting the current one.
    /// </para>
    /// </summary>
    protected virtual void OnMount()
    {
    }

    /// <summary>
    /// Runs when this instance's position LEAVES the tree — unsubscribe here whatever
    /// <see cref="OnMount"/> subscribed to. Without it every mount is a leak, so the pair ships
    /// together.
    /// </summary>
    protected virtual void OnUnmount()
    {
    }

    /// <summary>
    /// Called by the HOST (the instance store, or the surface for a root), never by app code. Idem-
    /// potent: an instance already in the tree does not mount twice.
    /// </summary>
    public void NotifyMounted()
    {
        if (_mounted) return;
        _mounted = true;
        OnMount();
    }

    /// <summary>The pair of <see cref="NotifyMounted"/> — also idempotent, and a no-op for an
    /// instance that never mounted (a fresh one the reconciler discarded in favour of a retained
    /// twin never entered the tree, so it never left it either).</summary>
    public void NotifyUnmounted()
    {
        if (!_mounted) return;
        _mounted = false;
        OnUnmount();
    }
}
