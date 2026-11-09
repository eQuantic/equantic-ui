using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Writes the Content-Security-Policy on every page the shell serves (#1267), from the app's
/// <see cref="ContentSecurityPolicyBuilder"/> and <see cref="ContentSecurityPolicyOptions"/>.
/// <para>
/// The policy is the strict one: <c>script-src</c> admits the request's nonce and what those scripts
/// load (<c>'strict-dynamic'</c>), so a host list is never needed for scripts; <c>object-src</c> and
/// <c>base-uri</c> are <c>'none'</c>; <c>frame-ancestors</c> and <c>connect-src</c> are the app's own
/// origin plus what the app and its features stated. It is written on pages only, since a policy
/// governs a document, and the nonce is the page's.
/// </para>
/// <para>
/// <c>style-src</c> is not written yet: the runtime creates style elements of its own and the
/// realizer writes style attributes, and #1268 brings both inside a policy before one is written.
/// </para>
/// <para>
/// Under <c>dotnet watch</c> the policy is written as report-only: dotnet watch injects a script of
/// its own into every page, which the SDK did not write and cannot stamp, and enforcing would refuse
/// the reload it exists for. The log says so at start, with the policy it writes.
/// </para>
/// </summary>
internal sealed class ContentSecurityPolicy
{
    private const string Enforcing = "Content-Security-Policy";
    private const string Reporting = "Content-Security-Policy-Report-Only";

    private readonly string _header;
    private readonly string _template;
    private readonly ILogger? _logger;
    private int _upstreamWarned;

    public ContentSecurityPolicy(IOptions<ContentSecurityPolicyOptions> options, ILogger<ContentSecurityPolicy> logger)
        : this(options.Value, Environment.GetEnvironmentVariable("DOTNET_WATCH"), logger)
    {
    }

    internal ContentSecurityPolicy(ContentSecurityPolicyOptions options, string? dotnetWatch, ILogger? logger = null)
    {
        _logger = logger;
        var underWatch = dotnetWatch == "1";
        _header = options.ReportOnly || underWatch ? Reporting : Enforcing;
        _template = Compose(options);
        logger?.LogInformation(
            "{Header}: {Policy}{Why}", _header, _template.Replace(NoncePlaceholder, "<nonce>"),
            underWatch && !options.ReportOnly ? " (report-only under dotnet watch, whose own script no nonce admits)" : "");
    }

    private const string NoncePlaceholder = "{nonce}";

    /// <summary>The header's name and value for a page whose scripts carry <paramref name="nonce"/>.</summary>
    internal (string Name, string Value) For(string nonce) => (_header, _template.Replace(NoncePlaceholder, nonce));

    /// <summary>
    /// Writes the header on a page, unless something before it already wrote one: then the app's own
    /// wins, and the log says once that the SDK's was not written, since a policy without the page's
    /// nonce refuses every script the shell writes.
    /// </summary>
    public void WriteTo(HttpResponse response, string nonce)
    {
        var (name, value) = For(nonce);
        if (!response.Headers.ContainsKey(name))
        {
            response.Headers[name] = value;
            return;
        }
        if (Interlocked.Exchange(ref _upstreamWarned, 1) == 0)
            _logger?.LogWarning(
                "{Header} was already written before the shell, so the SDK's policy is not: unless it admits the page's nonce, it refuses every script the shell writes.",
                name);
    }

    private static string Compose(ContentSecurityPolicyOptions options)
    {
        var directives = new List<string>
        {
            $"script-src 'nonce-{NoncePlaceholder}' 'strict-dynamic'",
            "object-src 'none'",
            "base-uri 'none'",
            Directive("frame-ancestors", options.FrameAncestors),
            Directive("connect-src", options.ConnectSources),
        };
        if (!string.IsNullOrEmpty(options.ReportUri)) directives.Add($"report-uri {options.ReportUri}");
        return string.Join("; ", directives);
    }

    private static string Directive(string name, IEnumerable<string> sources) =>
        string.Join(' ', new[] { name, "'self'" }.Concat(sources.Distinct(StringComparer.OrdinalIgnoreCase)));
}
