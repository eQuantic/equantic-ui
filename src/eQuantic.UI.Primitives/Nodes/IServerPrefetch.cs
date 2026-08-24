namespace eQuantic.UI.Primitives;

/// <summary>
/// SERVER DATA for the first render: a page (or any component the page composes) declares the data
/// it needs, the SSR pipeline awaits it BEFORE building the tree, and the values the prefetch stores
/// travel to the browser so hydration sees exactly what the server rendered — the markup carries
/// real numbers for crawlers and the client never flashes an empty state.
/// <para>
/// The implementation is SERVER-ONLY: mark it <c>[ServerOnly]</c> so the transpiler omits it from
/// the client bundle, and it may use the whole server surface (HttpClient, EF, the request's
/// services). Store results in ordinary FIELDS — those are what the hydration payload carries, keyed
/// by field name, into the identical fields of the transpiled twin.
/// </para>
/// <example>
/// <code>
/// public sealed class HomePage : StatelessComponent, IServerPrefetch
/// {
///     private PackageStats _stats = PackageStats.Empty;
///
///     [ServerOnly]
///     public async Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
///         => _stats = await services.GetRequiredService&lt;IPackageStats&gt;().LoadAsync(cancellationToken);
///
///     public override VisualNode Build(ComponentContext context) => new HeroSection(_stats);
/// }
/// </code>
/// </example>
/// <para>
/// EVERY FIELD IS PUBLIC. The payload is written into the served HTML, so a value a prefetch stores
/// is readable by anyone who views the page source — it is not "server data" once it lands in a
/// field. Load what the page DISPLAYS, and nothing else: an access token used to fetch, the
/// connection string behind the query, the internal id you only needed while loading.
/// </para>
/// <para>
/// A secret belongs in neither place. A <c>[ServerAction]</c> runs on the server and may USE one —
/// read a token, open a connection, call an API with it — but its return value is serialized to the
/// browser exactly as a field is, so return the ANSWER and never the secret that produced it. The
/// rule is the same on both sides: what crosses is what the page may show.
/// </para>
/// <para>
/// What does NOT travel is a DEPENDENCY: a field whose type is an interface from outside
/// <c>System</c>, which the client resolves for itself. The <c>System</c> exclusion is deliberate
/// and not a detail — <c>IReadOnlyList&lt;T&gt;</c> is how a component RECEIVES its items, so
/// skipping every interface would delete state rather than protect anything.
/// </para>
/// <para>
/// Everything else that CAN be written travels, including a <c>string</c> nobody meant to publish.
/// A null, a delegate, and a value that fails to serialize are left out — but read that as what it
/// is, a robustness rule so one field cannot empty the payload. It is NOT a protection: never rely
/// on a value being unserializable to keep it off the page.
/// </para>
/// <para>
/// Native fence: Photon hosts render locally, so nothing prefetches for them — a native shell loads
/// its data before constructing the tree (the same data, an explicit call).
/// </para>
/// </summary>
public interface IServerPrefetch
{
    /// <summary>Loads this component's server data. Runs once per request, before the first build.</summary>
    Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken);
}
