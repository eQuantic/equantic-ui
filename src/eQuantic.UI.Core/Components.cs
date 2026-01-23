using System;

namespace eQuantic.UI.Core;

/// <summary>
/// Base class for stateless components
/// </summary>
public abstract class StatelessComponent : HtmlElement
{
    /// <summary>
    /// Build the component tree
    /// </summary>
    public abstract IComponent Build(RenderContext context);

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        // Stateless components delegate rendering to their Build result
        var context = new RenderContext();
        var component = Build(context);
        return component.Render();
    }
}

/// <summary>
/// Base class for stateful components
/// </summary>
public abstract class StatefulComponent : HtmlElement
{
    private ComponentState? _state;

    /// <summary>
    /// Create the state for this component
    /// </summary>
    public abstract ComponentState CreateState();

    /// <summary>
    /// Get or create the component state
    /// </summary>
    internal ComponentState State => _state ??= InitializeState();

    private ComponentState InitializeState()
    {
        var state = CreateState();
        state.SetComponent(this);
        state.OnInit();
        return state;
    }

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var context = new RenderContext();
        State.Context = context;
        var component = State.Build(context);
        return component.Render();
    }
}

/// <summary>
/// Base class for component state
/// </summary>
public abstract class ComponentState
{
    private StatefulComponent? _component;
    private Action? _onStateChanged;

    /// <summary>
    /// The component this state belongs to
    /// </summary>
    public StatefulComponent Component => _component ?? throw new InvalidOperationException("State not initialized");

    /// <summary>
    /// Render context
    /// </summary>
    public RenderContext Context { get; internal set; } = new();

    internal void SetComponent(StatefulComponent component)
    {
        _component = component;
    }

    /// <summary>
    /// Register a callback for state changes
    /// </summary>
    public void OnStateChanged(Action callback)
    {
        _onStateChanged = callback;
    }

    /// <summary>
    /// Update state and trigger re-render
    /// </summary>
    protected void SetState(Action mutate)
    {
        mutate();
        _onStateChanged?.Invoke();
    }

    /// <summary>
    /// Build the component tree
    /// </summary>
    public abstract IComponent Build(RenderContext context);

    #region Lifecycle

    /// <summary>
    /// Called when the state is first created
    /// </summary>
    public virtual void OnInit() { }

    /// <summary>
    /// Called when the component is mounted to the DOM
    /// </summary>
    public virtual void OnMount() { }

    /// <summary>
    /// Called when the component is updated
    /// </summary>
    public virtual void OnUpdate() { }

    /// <summary>
    /// Called when the component is disposed
    /// </summary>
    public virtual void OnDispose() { }

    #endregion
}

/// <summary>
/// Typed base class for component state
/// </summary>
public abstract class ComponentState<T> : ComponentState where T : StatefulComponent
{
    /// <summary>
    /// The typed component this state belongs to
    /// </summary>
    public new T Component => (T)base.Component;
}
