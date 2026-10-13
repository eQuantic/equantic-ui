namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Which lines of the developer surface a stable version keeps (#323).
/// <para>
/// A line that leaves <c>developer-surface.baseline.txt</c> is a setting an app may still write and
/// that now means nothing: a property its csproj sets, a section its appsettings binds, a parameter
/// its <c>dotnet new</c> line passes. During a preview that is a break like any other, regenerated and
/// written in the notes. At a stable version it is the contract's, and it stays until a major. The
/// native SDK's properties, the native template's parameters and the <c>Photon</c> section are the
/// native track's, experimental in 1.0 (#324), and free to go.
/// </para>
/// </summary>
internal static class DeveloperSurfaceRule
{
    private static readonly string[] NativeTrack =
        ["msbuild eQuantic.UI.Sdk.Native ", "template equantic-native ", "config Photon"];

    public static bool IsNativeTrack(string line) =>
        NativeTrack.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>The lines among <paramref name="gone"/> that <paramref name="version"/> may not
    /// lose: every one that is the contract's at a stable version, and none during a preview.</summary>
    public static IReadOnlyList<string> Refused(string version, IEnumerable<string> gone) =>
        ReleaseVersion.IsStable(version) ? gone.Where(line => !IsNativeTrack(line)).ToList() : [];
}
