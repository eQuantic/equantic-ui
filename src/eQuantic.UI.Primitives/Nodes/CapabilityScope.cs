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
}
