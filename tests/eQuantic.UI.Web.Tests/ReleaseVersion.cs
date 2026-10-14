using System.Globalization;
using System.Reflection;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The version a build carries, and whether the compatibility promise holds at it (#323).
/// <para>
/// The preview's rule (CLAUDE.md, "No member survives to keep an old shape alive") holds while the
/// version carries a prerelease label. The day it does not, at the 1.0 cut, the stable rule holds by
/// itself: the guards that read this switch with the version, so nothing has to be remembered on the
/// day. The version is the one <c>Directory.Build.props</c> gave the build, read from the assembly
/// the guard scans, so a checkout needs no history and no tag to know it.
/// </para>
/// </summary>
internal static class ReleaseVersion
{
    /// <summary>The version <paramref name="assembly"/> was built at, without its build metadata.</summary>
    public static string Of(Assembly assembly) =>
        (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty)
            .Split('+')[0];

    /// <summary>A version with no prerelease label is a stable release, and the promise holds from it.</summary>
    public static bool IsStable(string version) =>
        version.Length > 0 && !version.Split('+')[0].Contains('-', StringComparison.Ordinal);

    public static int Major(string version) =>
        int.Parse(version.Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture);
}
