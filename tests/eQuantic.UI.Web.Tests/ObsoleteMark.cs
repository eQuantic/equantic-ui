using System.Globalization;
using System.Text.RegularExpressions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What an <c>[Obsolete]</c> on the contract has to say, at the version it ships in (#323).
/// <para>
/// During a preview it says nothing worth keeping: the preview removes the member now, with its
/// callers. At a stable version it is the only way a member leaves, and it is a promise with a date:
/// a <c>DiagnosticId</c> starting with <c>EQO</c>, so an app can silence that one member and nothing
/// else, a <c>UrlFormat</c> that links what to write instead, and a message that names the major it
/// is removed in, later than the current one.
/// </para>
/// </summary>
internal static class ObsoleteMark
{
    private static readonly Regex RemovedIn = new(@"\bremoved in (?<major>\d+)\.0\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>What keeps <paramref name="obsolete"/> from being right at <paramref name="version"/>,
    /// and nothing when it is.</summary>
    public static IReadOnlyList<string> Problems(string version, ObsoleteAttribute obsolete)
    {
        if (!ReleaseVersion.IsStable(version))
            return ["the preview removes a member now, so delete it and its callers instead of marking it obsolete"];

        var major = ReleaseVersion.Major(version);
        var problems = new List<string>();
        if (obsolete.DiagnosticId is not { } id || !id.StartsWith("EQO", StringComparison.Ordinal))
            problems.Add("its DiagnosticId starts with EQO, so an app can silence this member and nothing else");
        if (string.IsNullOrWhiteSpace(obsolete.UrlFormat))
            problems.Add("its UrlFormat links what to write instead");
        if (obsolete.Message is not { } message
            || RemovedIn.Match(message) is not { Success: true } removal
            || int.Parse(removal.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture) <= major)
            problems.Add($"its message names the major it is removed in, later than {major}, as \"removed in {major + 1}.0\"");
        return problems;
    }
}
