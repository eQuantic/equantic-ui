using System;

namespace eQuantic.UI.Core;

/// <summary>
/// The method NEVER crosses to the client: the transpiler omits it from the TypeScript twin
/// entirely, so it may use the whole server surface (HttpClient, EF, file IO) without tripping the
/// client-boundary validation — and no trace of it ships in the bundle.
/// <para>
/// The counterpart of <see cref="ServerActionAttribute"/>, which keeps the method callable from the
/// browser through an RPC stub. Reach for ServerOnly when the SERVER calls the method itself: SSR
/// prefetch (<c>IServerPrefetch.PrefetchAsync</c>), request-time composition, background work.
/// A method the client's own code calls must NOT be ServerOnly — the call would resolve to nothing
/// at runtime; make it a ServerAction instead.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ServerOnlyAttribute : Attribute
{
}
