namespace eQuantic.UI.Primitives;

/// <summary>
/// What a component's <see cref="UiComponent.Build"/> pass may read: the active theme and the OS
/// Dynamic Type factor. Deliberately MODE-FREE — components author with <see cref="ColorToken"/>s,
/// never resolved colors, so one built tree realizes in light or dark (realizers resolve). Services
/// and route data join here as the shared model grows toward the web `RenderContext`.
/// <para>
/// Where a tree is built with an element per position (Photon, the component tester), each component
/// position has ONE context of its own for as long as it lives (docs/ELEMENT-TREE-PLAN.md, 3.3): it
/// reads the frame's theme, density and type scale from the position's owner, and registers the
/// position as one that builds again when they change; it answers <see cref="InFlow"/> from the tree;
/// and <see cref="DependOn{T}"/> and <see cref="Find{T}"/> find the inherited values above it. A
/// context made with the public constructor is a frame's, with no position: what the web and the
/// server build with until #1395, where those two answer null.
/// </para>
/// </summary>
public sealed class ComponentContext
{
    /// <summary>A frame's context: what its owner hands every component it builds.</summary>
    /// <param name="theme">The theme of the frame.</param>
    /// <param name="typeScale">The OS text scale.</param>
    /// <param name="density">How tight the target wants its controls.</param>
    /// <param name="measureText">The host's text measurer, when it has one.</param>
    /// <param name="route">The route the tree is drawn for; <see cref="RouteValues.Empty"/> when none is handed.</param>
    /// <param name="inFlow">Whether the components built with it were asked to draw in the flow: what a
    /// realizer hands a subtree under an <see cref="Primitives.InFlow"/> that it draws without positions.</param>
    public ComponentContext(IAppTheme theme, float typeScale = 1f,
        Density density = Density.Comfortable, Func<string, TypeStyle, float>? measureText = null,
        RouteValues? route = null, bool inFlow = false)
    {
        _theme = theme;
        _typeScale = typeScale;
        _density = density;
        _measureText = measureText;
        _route = route ?? RouteValues.Empty;
        _inFlow = inFlow;
    }

    /// <summary>The context of a position: its frame's values come from the position's owner.</summary>
    internal ComponentContext(ComponentElement position)
    {
        _position = position;
        _theme = null!;
        _route = RouteValues.Empty;
    }

    private readonly ComponentElement? _position;
    private readonly IAppTheme _theme;
    private readonly float _typeScale;
    private readonly Density _density;
    private readonly Func<string, TypeStyle, float>? _measureText;
    private readonly RouteValues _route;
    private readonly bool _inFlow;

    /// <summary>The frame this position builds in, with the position registered as one that read it.</summary>
    private ComponentContext ReadFrame(ComponentElement position)
    {
        // A position that left the tree (a handler that kept its context) reads, and registers nothing.
        if (position.IsActive) position.Owner.ReadFrame(position);
        return position.Owner.Context;
    }

    public IAppTheme Theme => _position is { } at ? ReadFrame(at).Theme : _theme;

    public float TypeScale => _position is { } at ? ReadFrame(at).TypeScale : _typeScale;

    /// <summary>
    /// What the ROUTE said — <c>context.Route.Param("slug")</c>, <c>context.Route.Query("page")</c>.
    /// Never null; an unmatched name answers null.
    /// <para>
    /// The route of the frame the tree is drawn for, which is why a page does not have to be handed one
    /// and does not have to reach for <c>IHttpContextAccessor</c> to find it. Asking ASP.NET for the
    /// route works and costs the page its write-once-ness: a component that knows the web is a
    /// component that cannot run on Photon.
    /// </para>
    /// <para>
    /// A position reads it from its owner's frame and is registered as one of the route's readers, apart
    /// from the theme's: a frame handed another route builds again what read the route and nothing else,
    /// so a navigation within one page type rebuilds its dependents alone (docs/ELEMENT-TREE-PLAN.md 3.5).
    /// </para>
    /// </summary>
    public RouteValues Route => _position is { } at ? ReadRoute(at) : _route;

    /// <summary>The route of the frame `position` builds in, with the position registered as its reader.</summary>
    private static RouteValues ReadRoute(ComponentElement position)
    {
        if (position.IsActive) position.Owner.ReadRoute(position);
        return position.Owner.Context.Route;
    }

    /// <summary>
    /// Whether this component was asked to draw WHERE IT STANDS rather than over everything — see
    /// <see cref="Primitives.InFlow"/>. Only the overlays answer it; every other component builds
    /// the same either way, which is why it is a question rather than an instruction.
    /// <para>
    /// A position answers from the tree, whether an <see cref="Primitives.InFlow"/> stands above it; a
    /// frame's context answers what its realizer handed it.
    /// </para>
    /// </summary>
    public bool InFlow => _position?.UnderInFlow ?? _inFlow;

    /// <summary>
    /// The nearest <typeparamref name="T"/> above this component, and a registration: the component
    /// builds again when that value changes (<see cref="InheritedComponent.UpdateShouldNotify"/>), and a
    /// stateful one hears it first (<see cref="StatefulComponent.OnDependenciesChanged"/>). Null when
    /// none is above it, and on a context with no position (the web and the server until #1395).
    /// </summary>
    public T? DependOn<T>() where T : InheritedComponent =>
        _position?.DependOn(static inherited => inherited is T) as T;

    /// <summary>The nearest <typeparamref name="T"/> above this component, read once: the component does
    /// not build again when it changes. Null as for <see cref="DependOn{T}"/>.</summary>
    public T? Find<T>() where T : InheritedComponent =>
        _position?.Find(static inherited => inherited is T) as T;

    /// <summary>Where the tree this context's position is in sends a destination; null with no position.</summary>
    internal Action<string>? Navigation => _position?.Owner.NavigationRequested;

    /// <summary>
    /// A capability, for a component in the MIDDLE of a tree — <c>context.GetService&lt;ITextClipboard&gt;()</c>.
    /// Null when this target does not have it, which is an answer every capability's caller has to
    /// handle anyway.
    /// <para>
    /// A page takes what it needs through its CONSTRUCTOR, and that stays the better answer where
    /// it fits: explicit, testable, readable in the signature. But everything below the page had to
    /// be handed the same thing by hand — a card with a "Copy" button needs an ITextClipboard, so
    /// the article above it carried one it never used. A component that gains a need forced an edit
    /// in every ancestor between it and the page.
    /// </para>
    /// </summary>
    public T? GetService<T>() where T : class => CapabilityScope.Resolve<T>();

    /// <summary>
    /// How tight this TARGET wants its controls — the same reason a Mac's toolbar is not a
    /// phone's. A component reads it where it reads the theme, and never asks which target it is.
    /// </summary>
    public Density Density => _position is { } at ? ReadFrame(at).Density : _density;

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
    public float MeasureText(string text, TypeStyle style)
    {
        // A position measures with its owner's measurer: the frame's, as every build of it does.
        if (_position is { } at) return at.Owner.Context.MeasureText(text, style);
        return _measureText is null || text.Length == 0 ? 0 : _measureText(text, style);
    }

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
/// nodes — the same authoring shape as the web SDK (`Build(context)`), realized per target. A live
/// tree builds a component at its position (<see cref="ComponentElement"/>) before laying out what it
/// built; realizers draw nothing for the component itself (its built subtree carries the visuals).
/// </summary>
public abstract class UiComponent : VisualNode
{
    /// <summary>All components share one wire kind — realizers expand them via <see cref="Build"/>.</summary>
    public sealed override string NodeKind => "component";

    /// <summary>
    /// SEALED, and the one method in the vocabulary that is. An app's own components are unbounded,
    /// so the visitor answers "a component" once — a subclass that could override this would be
    /// asking to be dispatched as itself, which is the thing that cannot be enumerated. It is also
    /// why eqc never meets an `Accept` in an app's source.
    /// </summary>
    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);

    /// <summary>Produces this component's subtree. Must be PURE over component state + context —
    /// it runs whenever its position builds (a <c>SetState</c> of its own or of an ancestor, a frame
    /// that rebuilds everything, a server's render), and how often that is is the host's to decide.</summary>
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

    /// <summary>
    /// A capability, from anywhere in the component — including where there is no context to ask.
    /// <see cref="ComponentContext.GetService{T}"/> is the same answer for code that has one; this
    /// is for <c>OnMount</c>, <c>OnUnmount</c> and event handlers, which do not.
    /// <para>
    /// Without it, a component that subscribes to a device had to do it inside <c>Build</c> behind
    /// a run-once flag, because Build is where the context arrives — so the subscription lived in
    /// the one method the framework calls repeatedly, and the pairing with <c>OnUnmount</c> read as
    /// an accident. The lifecycle hook is the right place, and this is what it was missing.
    /// </para>
    /// <para>
    /// Null when this target does not have the capability, which every caller has to handle anyway.
    /// Resolves through <see cref="CapabilityScope"/>, armed by whoever owns the surface: the SSR
    /// pipeline points it at the request's container, a Photon host at the shell's, and on the web
    /// the transpiled call reads the browser's own registry.
    /// </para>
    /// </summary>
    protected T? GetService<T>() where T : class => CapabilityScope.Resolve<T>();
}

/// <summary>A component fully described by its constructor inputs — same contract as the web SDK's.</summary>
public abstract class StatelessComponent : UiComponent
{
}

/// <summary>
/// A component with internal state, mutated through <see cref="SetState"/>. State lives as fields on
/// the instance, and the instance is RETAINED across the parent's rebuilds by position (same type,
/// same key) — so a counter keeps counting while the page around it re-renders.
/// <para>
/// THE THING TO KNOW BEFORE YOUR FIRST PARAMETERIZED CONSTRUCTOR: the parent's <c>Build</c> still
/// runs <c>new YourComponent(args)</c> every time, but that fresh instance is NOT the one that
/// renders. The retained one does — and it learns about the fresh arguments only through
/// <see cref="UiComponent.AdoptConfig"/>, whose default copies nothing. Leave it alone and the
/// retained instance shows its FIRST configuration forever: a mood set at frame one, a root folder
/// that never changes when you navigate. Override <c>AdoptConfig</c> to copy the constructor/init
/// props across; state fields stay yours. Three components in one consumer app shipped this bug
/// before the rule was written here, and ten of the SDK's own until a test handed each of them a
/// second configuration (#1142).
/// </para>
/// <para>
/// An <c>init</c> accessor cannot be written from <c>AdoptConfig</c>. Back such a setting with a field
/// the override writes, or keep the fresh instance whole (<c>_config = fresh</c>) and read every
/// setting from it: the shape of Flutter's <c>State.widget</c>, which carries a setting added later
/// without a line of its own.
/// </para>
/// </summary>
public abstract class StatefulComponent : UiComponent
{
    private bool _mounted;

    /// <summary>
    /// The position this instance holds in a LIVE tree, set by the element that retains it and
    /// cleared when the last position it holds leaves. Null for an instance no host is drawing: a
    /// fresh one a parent's build made and the element discarded for its retained twin, one laid out
    /// once, one a server renders.
    /// </summary>
    internal ComponentElement? MountedAt;

    /// <summary>
    /// The other positions this one instance holds at the same time: a parent that keeps an instance
    /// and places it twice. Every one of them shows it, so every one builds on a <c>SetState</c>, and
    /// the instance stays mounted until the last of them leaves. Null in the common case.
    /// </summary>
    private List<ComponentElement>? _alsoMountedAt;

    /// <summary>Takes a position in a live tree; true when it is the instance's first, which is when
    /// it mounts.</summary>
    internal bool EnterPosition(ComponentElement at)
    {
        if (MountedAt is null)
        {
            MountedAt = at;
            return true;
        }

        (_alsoMountedAt ??= new List<ComponentElement>()).Add(at);
        return false;
    }

    /// <summary>Leaves a position; true when it was the last the instance held, which is when it
    /// unmounts.</summary>
    internal bool LeavePosition(ComponentElement at)
    {
        if (!ReferenceEquals(MountedAt, at))
        {
            _alsoMountedAt?.Remove(at);
            return false;
        }

        if (_alsoMountedAt is { Count: > 0 } others)
        {
            MountedAt = others[^1];
            others.RemoveAt(others.Count - 1);
            return false;
        }

        MountedAt = null;
        return true;
    }

    /// <summary>Every position showing this instance builds again.</summary>
    private void MarkPositions()
    {
        MountedAt?.MarkNeedsBuild();
        if (_alsoMountedAt is not { } others) return;
        foreach (var at in others) at.MarkNeedsBuild();
    }

    protected void SetState(Action mutate)
    {
        // OFF the UI thread, the whole mutation is posted rather than run: the fields this touches
        // are read by the render thread while it builds, so mutating them here is a data race whose
        // symptom is a frame drawn from half-old state — no exception, no stack, once a week. The
        // check is the dispatcher's, not a thread-id comparison of ours, because only the host knows
        // which thread draws. Where nothing needs marshalling (the browser has one thread; a request
        // is rendered on the thread that asked) no dispatcher is armed and this runs inline, exactly
        // as it always did.
        if (UiDispatcher.Current is { IsOnUiThread: false } dispatcher)
        {
            dispatcher.Post(() =>
            {
                mutate();
                MarkPositions();
            });
            return;
        }

        mutate();
        // The position builds again, and only it: its owner asks the host for the frame that shows it.
        MarkPositions();
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
    /// Runs before this instance builds again because a value it read through
    /// <see cref="ComponentContext.DependOn{T}"/> changed: Flutter's <c>didChangeDependencies</c>. The
    /// place to redo what was derived from that value (a formatter for the new culture, a request for
    /// the new route) once, rather than in every build.
    /// </summary>
    protected virtual void OnDependenciesChanged()
    {
    }

    internal void NotifyDependenciesChanged() => OnDependenciesChanged();

    /// <summary>
    /// Runs when this instance's position LEAVES the tree — unsubscribe here whatever
    /// <see cref="OnMount"/> subscribed to. Without it every mount is a leak, so the pair ships
    /// together.
    /// </summary>
    protected virtual void OnUnmount()
    {
    }

    /// <summary>
    /// Called by the tree's OWNER when the frame that built this instance's position ends, never by
    /// app code. Idempotent: an instance already in the tree does not mount twice.
    /// </summary>
    internal void NotifyMounted()
    {
        if (_mounted) return;
        _mounted = true;
        OnMount();
    }

    /// <summary>The pair of <see cref="NotifyMounted"/> — also idempotent, and a no-op for an
    /// instance that never mounted (a fresh one the reconciler discarded in favour of a retained
    /// twin never entered the tree, so it never left it either).</summary>
    internal void NotifyUnmounted()
    {
        if (!_mounted) return;
        _mounted = false;
        OnUnmount();
    }
}
