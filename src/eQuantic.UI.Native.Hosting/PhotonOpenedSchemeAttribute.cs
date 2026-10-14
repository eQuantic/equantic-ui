namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// One URL scheme this app hands to the system, beyond the web's and its own, as the build reads
/// it: the generator writes one per <c>builder.Workspace.Opens…</c> line in <c>Program.cs</c>.
/// <para>
/// A phone answers "is there something for this link" only for the schemes the app's manifest says
/// it may ask about, and a manifest is written at BUILD time. So the line a developer wrote for the
/// policy also has to reach the build, and this is how: the SDK reads these off the compiled
/// assembly and writes the iOS <c>LSApplicationQueriesSchemes</c> and the Android <c>queries</c>
/// from them. Nobody writes one by hand.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PhotonOpenedSchemeAttribute(string scheme) : Attribute
{
    /// <summary>The scheme's NAME, lower-case: <c>mailto</c>, not <c>mailto:</c>.</summary>
    public string Scheme { get; } = scheme;
}
