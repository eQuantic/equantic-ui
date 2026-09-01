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
/// <para>
/// On a CLASS it says the same of the whole type: the transpiler emits no module for it. Every
/// top-level static class and every plain class in an app is otherwise mirrored to JavaScript — a
/// Roslyn compilation service, a hosted warm-up, a repository living in the web project failed the
/// build with EQ2004 on their first server-only call, and nothing short of moving them to another
/// assembly could say "this never ships". On a COMPONENT it has no effect — the component path
/// ignores it, because a component always ships. And a class the client's code instantiates or
/// calls must not carry it, for the same reason as the method: the reference would resolve to
/// nothing at runtime.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ServerOnlyAttribute : Attribute
{
}
