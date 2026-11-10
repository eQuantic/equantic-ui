namespace eQuantic.UI.Server;

/// <summary>
/// What varies per environment in the Content-Security-Policy the SDK writes, read from
/// <c>EQuantic:ContentSecurityPolicy</c> in configuration and added to in <c>Program.cs</c> with
/// <see cref="UIOptions.UseContentSecurityPolicy"/> (#1267).
/// </summary>
public sealed class ContentSecurityPolicyOptions
{
    /// <summary>The configuration section the options are read from.</summary>
    public const string SectionName = "EQuantic:ContentSecurityPolicy";

    /// <summary>
    /// Whether the browser only reports what the policy would refuse, through
    /// <c>Content-Security-Policy-Report-Only</c>, instead of refusing it: the way to try a policy on a
    /// live app before enforcing it.
    /// </summary>
    public bool ReportOnly { get; set; }

    /// <summary>Where the browser posts a report of each violation, a path of the app or an absolute URL.</summary>
    public string? ReportUri { get; set; }

    /// <summary>
    /// The origins the app's pages connect to besides its own (an API on another domain), each a
    /// <c>scheme://host[:port][/path]</c> whose host may start with <c>*.</c>.
    /// </summary>
    public IList<string> ConnectSources { get; } = new List<string>();

    /// <summary>The origins allowed to frame the app's pages besides its own, written like
    /// <see cref="ConnectSources"/>.</summary>
    public IList<string> FrameAncestors { get; } = new List<string>();
}
