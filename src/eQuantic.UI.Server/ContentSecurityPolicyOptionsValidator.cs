using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Refuses, at start, a source the policy could not write: a malformed one would be written into the
/// header, where the browser drops it in silence and refuses the connection it was meant to admit.
/// </summary>
internal sealed partial class ContentSecurityPolicyOptionsValidator : IValidateOptions<ContentSecurityPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, ContentSecurityPolicyOptions options)
    {
        var wrong = options.ConnectSources.Concat(options.FrameAncestors)
            .Where(source => !IsSource(source))
            .ToList();
        if (options.ReportUri is { } report && !Uri.TryCreate(report, UriKind.RelativeOrAbsolute, out _))
            wrong.Add(report);
        return wrong.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{ContentSecurityPolicyOptions.SectionName}: each source is scheme://host[:port][/path], a host "
                + $"may start with *., and a report URI is a path or an absolute URL. Refused: {string.Join(", ", wrong)}");
    }

    /// <summary>Whether <paramref name="source"/> is a host source the policy writes as it is.</summary>
    internal static bool IsSource(string? source) => source is not null && Source().IsMatch(source);

    [GeneratedRegex(@"^(https?|wss?)://(\*\.)?[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*(:\d{1,5})?(/[A-Za-z0-9\-._~%!$&()*+=:@/]*)?$")]
    private static partial Regex Source();
}
