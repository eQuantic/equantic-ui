using System;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Marks a method as a Server Action that executes on the server.
/// The method will be invocable from the client via the Server Actions bridge.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ServerActionAttribute : Attribute
{
    /// <summary>
    /// Optional custom action name. Defaults to method name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// How long the browser waits for the action, in seconds: past it, the call is abandoned and the
    /// page's <c>await</c> throws. Unset, or 0, keeps the runtime's 30 seconds. A slow but bounded action
    /// (a code checked with an identity provider, a report that takes a minute) states its own; work with
    /// no bound returns at once and sends its result later, over a <see cref="ServerTopic{T}"/>.
    /// <para>
    /// The build reads it and refuses a value below 0 or above 600 (EQ2017). Nothing on the server limits
    /// an action, but a reverse proxy in front of the app may end a request sooner (many end one at 60 or
    /// 100 seconds), so a value above the proxy's own does not hold.
    /// </para>
    /// </summary>
    public int TimeoutSeconds { get; set; }
}
