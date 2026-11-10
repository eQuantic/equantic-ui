namespace eQuantic.UI.Server;

/// <summary>
/// The Content-Security-Policy as C# (#1267): the app states what its own code needs, and the SDK
/// adds what its own features need and writes the header.
/// <para>
/// The policy is strict by construction. Scripts are admitted by the request's nonce, which the shell
/// stamps on every script it writes (#1266), and by <c>'strict-dynamic'</c>, which admits what those
/// scripts load. Plugins and a changed base URL are refused, and only the app's own origin may frame
/// a page. Connections go to the app's own origin, to the origins its features need (GTM's, when
/// <c>UseGtm</c> is on) and to the ones stated here. Nothing here takes a policy string: an app that
/// writes <c>'unsafe-inline'</c> has turned the policy off.
/// </para>
/// <para>
/// An integration declares what it needs here whether or not the app turned the policy on, so the
/// policy an app turns on later already admits it.
/// </para>
/// </summary>
public sealed class ContentSecurityPolicyBuilder
{
    private readonly List<string> _connectSources = new();
    private readonly List<string> _frameAncestors = new();

    /// <summary>Whether the app turned the policy on, with <see cref="UIOptions.UseContentSecurityPolicy"/>.</summary>
    internal bool Enabled { get; set; }

    internal bool ReportsOnly { get; private set; }

    internal string? ViolationsEndpoint { get; private set; }

    internal IReadOnlyList<string> ConnectSources => _connectSources;

    internal IReadOnlyList<string> FrameAncestors => _frameAncestors;

    /// <summary>
    /// Lets the app's pages connect to <paramref name="sources"/> besides the app's own origin: a
    /// <c>fetch</c>, an <c>EventSource</c> or a <c>WebSocket</c> to another domain. Each is a
    /// <c>scheme://host[:port][/path]</c>, and a host may start with <c>*.</c>. They add to
    /// <c>EQuantic:ContentSecurityPolicy:ConnectSources</c> in configuration.
    /// </summary>
    public ContentSecurityPolicyBuilder AllowConnectionsTo(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
            if (!_connectSources.Contains(source)) _connectSources.Add(source);
        return this;
    }

    /// <summary>
    /// Lets pages of <paramref name="sources"/> frame the app's pages besides the app's own origin,
    /// written as in <see cref="AllowConnectionsTo"/>. They add to
    /// <c>EQuantic:ContentSecurityPolicy:FrameAncestors</c> in configuration.
    /// </summary>
    public ContentSecurityPolicyBuilder AllowFramingBy(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
            if (!_frameAncestors.Contains(source)) _frameAncestors.Add(source);
        return this;
    }

    /// <summary>
    /// Writes the policy as <c>Content-Security-Policy-Report-Only</c>: the browser reports what it
    /// would refuse and refuses nothing. <c>EQuantic:ContentSecurityPolicy:ReportOnly</c> says the
    /// same per environment.
    /// </summary>
    public ContentSecurityPolicyBuilder ReportOnly()
    {
        ReportsOnly = true;
        return this;
    }

    /// <summary>
    /// Has the browser post a report of each violation to <paramref name="endpoint"/>, a path of the
    /// app or an absolute URL. <c>EQuantic:ContentSecurityPolicy:ReportUri</c> says the same per
    /// environment, and wins.
    /// </summary>
    public ContentSecurityPolicyBuilder ReportViolationsTo(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ViolationsEndpoint = endpoint.OriginalString;
        return this;
    }
}
