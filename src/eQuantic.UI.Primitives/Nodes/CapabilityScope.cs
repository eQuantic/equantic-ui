using System.Threading;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Where a component in the MIDDLE of a tree finds a capability.
/// <para>
/// A page takes what it needs through its constructor, and everything below it had to be handed the
/// same thing by hand: a card that shows a "Copy" button needs an <c>ITextClipboard</c>, so the
/// article above it carried one it never used, and the section above that carried it too. The
/// deeper the component, the longer the thread — and a component that gains a need forces an edit
/// in every ancestor between it and the page.
/// </para>
/// <para>
/// Constructor injection stays the better answer where it fits: it is explicit, it is testable, and
/// a component that states its needs in its signature is a component you can read. This is for the
/// rest — a leaf that needs one capability, three levels down, in a subtree nobody wants to
/// re-plumb.
/// </para>
/// <para>
/// Set per render by whoever owns the surface: the SSR pipeline points it at the REQUEST's
/// container, the client boot at the registered browser capabilities, a Photon host at the shell's.
/// AsyncLocal, like every other per-render ambient here, because SSR renders concurrent requests on
/// shared instances.
/// </para>
/// </summary>
public static class CapabilityScope
{
    private static readonly AsyncLocal<Func<Type, object?>?> Scoped = new();

    /// <summary>
    /// The resolver in force. A <c>Func</c> rather than an <c>IServiceProvider</c> so Primitives
    /// stays free of a container: what it needs is "a type in, a service or null out", and every
    /// host already has something that answers that.
    /// </summary>
    public static Func<Type, object?>? Current
    {
        get => Scoped.Value;
        set => Scoped.Value = value;
    }

    /// <summary>The capability, or null when this target does not have it — or when nothing armed a
    /// resolver, which is the case in a plain unit test.</summary>
    public static T? Resolve<T>() where T : class => Current?.Invoke(typeof(T)) as T;

    /// <summary>
    /// The capability a component said it cannot work without — <c>IClock</c> rather than
    /// <c>IClock?</c> in its constructor. Absent here is a mistake, and this is where saying so is
    /// still cheap: the alternative is a null travelling into the component and failing later,
    /// inside code that never mentions capabilities at all.
    /// <para>
    /// The message names the capability, the component asking, and both ways out — because the
    /// person reading it is usually on the target that does not have it, wondering why the same
    /// screen works elsewhere.
    /// </para>
    /// </summary>
    /// <summary>
    /// Arms ONE capability for as long as the returned handle lives, over whatever is already in
    /// force — a test hands a fake clock, a preview hands a stub clipboard, and everything else
    /// keeps answering the way it did.
    /// <para>
    /// The ceremony it replaces is the reason it exists: <c>Current = type =&gt; type ==
    /// typeof(IClock) ? clock : null</c> answers null to every OTHER capability the component might
    /// ask for, and it has to be undone by hand — a test that forgets leaves the next one resolving
    /// against a fake it never asked for. Nesting composes, and disposing restores exactly what was
    /// there before.
    /// </para>
    /// <example><code>
    /// using var _ = CapabilityScope.With&lt;IClock&gt;(fake);
    /// </code></example>
    /// </summary>
    public static IDisposable With<T>(T capability) where T : class
    {
        var outer = Current;
        Current = type => type == typeof(T) ? capability : outer?.Invoke(type);
        return new Restore(outer);
    }

    /// <summary>Puts back exactly what was in force, once. A `using` that runs twice — a nested
    /// scope disposed by hand and again by its block — must not resurrect an older resolver.</summary>
    private sealed class Restore(Func<Type, object?>? outer) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            Current = outer;
        }
    }

    public static T Require<T>(string component) where T : class =>
        Resolve<T>() ?? throw new InvalidOperationException(
            $"{component} needs {typeof(T).Name}, and this target has none. Register it with the "
            + $"host, or declare the parameter as {typeof(T).Name}? if the component can work "
            + "without it.");
}
