using System;
using System.Threading;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// What a component can ask about the render it is in, on the web.
///
/// <para>
/// It used to be a second copy of most of that. A `RouteData` behind its own `AsyncLocal`, a
/// service provider behind another plus a process-wide fallback, and a per-instance service
/// dictionary — all answering questions `RouteValues` and `CapabilityScope` answer for every
/// target, with `ServerRenderingService` arming both sets on every request. The Core dissolution
/// (#83) moved the write-once half down and left this half where it was.
/// </para>
///
/// <para>
/// What is left is the one thing that is genuinely the web's: the LINK POLICY. The route is the one
/// its render was handed, and a capability is read from the vocabulary's scope.
/// </para>
/// </summary>
public class RenderContext
{
    /// <summary>The context of a render drawn for <paramref name="route"/>.</summary>
    /// <param name="route">The route the render is drawn for; <see cref="RouteValues.Empty"/> when none is handed.</param>
    public RenderContext(RouteValues? route = null)
    {
        Route = route ?? RouteValues.Empty;
    }

    /// <summary>
    /// What the ROUTE said — matched parameters and the query string
    /// (<c>context.Route.Param("id")</c>), transpiled to the runtime's <c>context.route</c>.
    /// The route the render was handed, as a write-once component's context reads its frame's (#1395).
    /// </summary>
    public RouteValues Route { get; }

    /// <summary>
    /// A capability, for code that has a context — <c>context.GetService&lt;ITextClipboard&gt;()</c>.
    /// Null when this target does not have it.
    /// <para>
    /// It is <see cref="CapabilityScope"/> and nothing else. The `AsyncLocal` provider, the global
    /// fallback and the per-instance dictionary that used to live here answered the same question:
    /// nothing in the tree ever registered into the dictionary, and nothing ever set the global.
    /// </para>
    /// <para>
    /// ONE accessor, with the SAME signature as <see cref="ComponentContext.GetService{T}"/> — and
    /// that is a contract with the transpiler, not a tidiness choice. `ServiceProviderStrategy`
    /// recognizes `GetService` and `GetRequiredService`; a `TryGetService` beside it fell through to
    /// an ordinary invocation and emitted `context.tryGetService(...)`, which the runtime's
    /// `RenderContext` has never had. And a nullable return is what the twin answers
    /// (`getService(key): T | undefined`), so a version here that THREW would have made the two
    /// sides disagree about an absent capability: SSR fails the request, the client renders on.
    /// Found in review.
    /// </para>
    /// </summary>
    public T? GetService<T>() where T : class => CapabilityScope.Resolve<T>();
}
