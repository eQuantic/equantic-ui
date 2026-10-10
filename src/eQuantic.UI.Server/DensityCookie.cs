using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// The density a request renders at (#623). The server cannot see the pointer, so the browser tells
/// it: the runtime reads <c>(pointer: fine)</c> at boot and leaves its answer in this SESSION cookie,
/// and every later request of the session renders at that density from its first byte. Without one
/// the server renders <see cref="Density.Comfortable"/>, the touch density, and the client hydrates
/// at what the server rendered before it switches the whole page at once.
/// <para>
/// A session cookie, written by the runtime and holding no identity: a display fact about the
/// device, gone when the browser closes. The runtime's <c>DENSITY_COOKIE</c> names the same cookie.
/// </para>
/// </summary>
internal static class DensityCookie
{
    public const string Name = "eq-density";

    /// <summary>The density this request renders at. An unrecognised value is user-supplied text,
    /// and is ignored rather than trusted.</summary>
    public static Density Resolve(HttpContext? context) =>
        context?.Request.Cookies[Name] == "compact" ? Density.Compact : Density.Comfortable;

    /// <summary>The density as the runtime names it.</summary>
    public static string NameOf(Density density) => density == Density.Compact ? "compact" : "comfortable";
}
