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
/// What is left is the one thing that is genuinely the web's: the LINK POLICY. Everything else
/// here reads the vocabulary's ambient rather than a copy of it.
/// </para>
/// </summary>
public class RenderContext
{
    /// <summary>
    /// What the ROUTE said — matched parameters and the query string
    /// (<c>context.Route.Param("id")</c>), transpiled to the runtime's <c>context.route</c>.
    /// It is <see cref="RouteValues.Current"/> itself, never a copy.
    /// </summary>
    public RouteValues Route => RouteValues.Current;

    private static readonly AsyncLocal<Func<string, string>?> _linkPolicy = new();

    /// <summary>
    /// What an in-app destination becomes on the way into an href, for THIS render.
    /// <para>
    /// It exists for one policy — the language prefix — and it lives here rather than in the link
    /// node because the alternative is asking every author to remember it on every link. A site
    /// with two hundred hrefs has two hundred chances to forget, and forgetting is silent: the
    /// link works, it just leaves the language behind.
    /// </para>
    /// <para>
    /// It stays on the WEB rather than moving down with the route and the resolver, because it is
    /// not neutral: the policy is installed by the server's culture routes, and Photon's realizer
    /// reads <c>Link.Destination</c> raw. That is a fence rather than a gap today — nothing arms a
    /// policy on that target — but the day one becomes neutral, the native realizer owes it too.
    /// </para>
    /// <para>
    /// AsyncLocal, like the ambients it sits beside: two requests rendering concurrently in
    /// different languages must not see each other's prefix.
    /// </para>
    /// </summary>
    public static Func<string, string>? LinkPolicy => _linkPolicy.Value;

    /// <inheritdoc cref="LinkPolicy"/>
    public static void SetLinkPolicy(Func<string, string>? policy) => _linkPolicy.Value = policy;

    /// <summary>The destination an href should carry — the policy's answer, or the destination
    /// untouched when no policy is in force. Only APP-INTERNAL rooted paths are offered to it:
    /// <c>//cdn</c>, <c>https://</c>, <c>#anchor</c> and <c>mailto:</c> belong to someone else.
    /// </summary>
    public static string ResolveDestination(string destination) =>
        _linkPolicy.Value is { } policy
        && destination.Length > 0
        && destination[0] == '/'
        && !destination.StartsWith("//", StringComparison.Ordinal)
            ? policy(destination)
            : destination;

    /// <summary>
    /// A capability, for code that has a context — <c>context.GetService&lt;ITextClipboard&gt;()</c>.
    /// Null when this target does not have it.
    /// <para>
    /// It is <see cref="CapabilityScope"/> and nothing else. The `AsyncLocal` provider, the global
    /// fallback and the per-instance dictionary that used to live here answered the same question:
    /// nothing in the tree ever registered into the dictionary, and nothing ever set the global.
    /// </para>
    /// </summary>
    public T? TryGetService<T>() where T : class => CapabilityScope.Resolve<T>();

    /// <summary>The same capability, where its absence is a mistake rather than an answer.</summary>
    public T GetService<T>() where T : class => CapabilityScope.Require<T>(nameof(RenderContext));
}
