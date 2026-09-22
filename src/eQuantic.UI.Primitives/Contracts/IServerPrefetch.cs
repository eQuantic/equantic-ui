namespace eQuantic.UI.Primitives;

/// <summary>
/// SERVER DATA for the first render: a page — or any component the page composes — declares the
/// data it needs, the SSR pipeline awaits it BEFORE drawing the tree, and the values the prefetch
/// stores travel to the browser so hydration sees exactly what the server rendered — the markup
/// carries real numbers for crawlers and the client never flashes an empty state.
/// <para>
/// ANY COMPONENT, and that sentence used to be a promise the pipeline did not keep. Only the root of
/// the route was asked and only its fields travelled, so a header composed into every route drew the
/// right value during SSR and blanked the moment hydration rebuilt it — `curl` returned perfect
/// HTML and only a browser showed the loss. The traversal below is what makes the sentence true.
/// </para>
/// <para>
/// HOW IT REACHES YOU: the drawing is the discovery — it names every component it expands — and the
/// prefetches it found are then awaited ONE AT A TIME, because they are all handed the request's
/// service provider and a scoped dependency (an EF DbContext, the ordinary case) is not safe for two
/// of them at once. The drawing runs again with the values restored, and repeats while it keeps
/// finding components it has not asked: a page that loads a list and then composes one component per
/// row creates prefetchers that did not exist when the first round looked.
/// </para>
/// <para>
/// The payload names each component <c>Type#ordinal</c> in that expansion order, and the client
/// counts the same way as it builds, so each component is handed back its own fields. A tree that
/// somehow differs between the two sides leaves a component with its DEFAULTS rather than another
/// component's data: the type in the key has to match before anything is written — by simple name,
/// so two components called <c>Row</c> from different namespaces share that refusal as well as
/// their ordinal. A component the
/// DATA replaced — your page loads, and composes a different row at the same position — is
/// recognised as a different component and asked for its own data rather than handed the previous
/// one's, as long as what distinguishes the two is a field this can compare: a string, a number, an
/// enum, any value type. Two rows told apart only by an object you pass them cannot be distinguished
/// here, so give such a component something comparable if its position is data-driven.
/// </para>
/// <para>
/// The implementation is SERVER-ONLY: mark it <c>[ServerOnly]</c> so the transpiler omits it from
/// the client bundle, and it may use the whole server surface (HttpClient, EF, the request's
/// services). Store results in ordinary FIELDS — those are what the hydration payload carries, by
/// field name within the component's entry, into the identical fields of the transpiled twin. A
/// field declared on a BASE class travels too, private ones included.
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
