using System;

namespace eQuantic.UI.Primitives;

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
/// <para>
/// On a STRUCT it says the same, and the compiler calls the whole rule the HOST-ONLY fence — which
/// is wider than this attribute's name. A value type in the vocabulary that only a RASTERIZER
/// consumes (<c>Matrix2D</c>, <c>RRect</c>) is host-only in exactly this sense: eqc routes the
/// namespace to the runtime by NAMESPACE, so naming one from a page compiles, emits an import, and
/// dies at hydration. Marking it moves that to the build.
/// </para>
/// <para>
/// On an OPERATOR it fences the operator alone, which is a shape worth stating because the failure
/// is silent rather than loud: JavaScript cannot overload one, so <c>a + b</c> on two framework
/// values emits JavaScript's own <c>+</c>. A twin that IS a primitive (<c>SizeValue</c>,
/// <c>Index</c>) passes the value through and the answer is right; a twin that is an OBJECT
/// concatenates it into a string, or answers NaN, in a page that compiled and shipped.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ServerOnlyAttribute : Attribute
{
}
