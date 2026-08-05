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
        Density density = Density.Comfortable)
    {
        Theme = theme;
        TypeScale = typeScale;
        Density = density;
    }

    public IAppTheme Theme { get; }
    public float TypeScale { get; }

    /// <summary>
    /// How tight this TARGET wants its controls — the same reason a Mac's toolbar is not a
    /// phone's. A component reads it where it reads the theme, and never asks which target it is.
    /// </summary>
    public Density Density { get; }
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

    protected void SetState(Action mutate)
    {
        mutate();
        StateInvalidated?.Invoke();
    }
}
