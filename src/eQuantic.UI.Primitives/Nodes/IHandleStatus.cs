namespace eQuantic.UI.Primitives;

/// <summary>
/// The HTTP status this render should answer with — for the page that matched a route but found
/// nothing behind it.
/// <para>
/// A route like <c>/docs/{slug}</c> matches every slug, including the ones that name no document.
/// The page renders "not found" and, without this, the server still answers <b>200 OK</b>: the
/// reader sees the right thing and every machine is told the wrong one. A crawler indexes the empty
/// page, a link checker reports the site healthy, and an uptime probe never notices — the failure is
/// invisible to exactly the things whose job is to notice.
/// </para>
/// <para>
/// Read AFTER <see cref="IServerPrefetch"/>, because "does this exist" is something a page usually
/// learns by loading it. Return 200 when it does; a page that implements this and always says 200
/// behaves exactly as one that does not implement it at all.
/// </para>
/// <example>
/// <code>
/// public sealed class DocPage : StatelessComponent, IServerPrefetch, IHandleStatus
/// {
///     private Doc? _doc;
///
///     [ServerOnly]
///     public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
///         =&gt; _doc = await services.GetRequiredService&lt;IDocs&gt;().FindAsync(Slug, cancellationToken);
///
///     public int StatusCode =&gt; _doc is null ? 404 : 200;
/// }
/// </code>
/// </example>
/// <para>
/// Web-only in effect. A native host has no status code to answer with, so a Photon build ignores
/// this — the page still renders whatever it renders, which is the same tree either way.
/// </para>
/// </summary>
public interface IHandleStatus
{
    /// <summary>The status to answer with. 200 unless the page has a reason.</summary>
    int StatusCode { get; }
}
