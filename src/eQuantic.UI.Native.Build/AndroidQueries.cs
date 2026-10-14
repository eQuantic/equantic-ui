using eQuantic.UI.Codegen;

namespace eQuantic.UI.Build;

/// <summary>
/// Writes the part of an Android manifest that says which links the app may ASK the system about.
/// <para>
/// Since Android 11 an app sees only the other apps its manifest lists under <c>queries</c>, so
/// "does something open this link" answers no for every scheme that is not there, whatever is
/// installed. .NET for Android builds the manifest from attributes and has none for this element,
/// so it is written as a fragment the Android build merges over the manifest it generates
/// (<c>AndroidManifestOverlay</c>): the app keeps no manifest of its own for it.
/// </para>
/// <para>
/// The web's two schemes are always there, because every app's policy hands those over, and what
/// the app opened besides follows from its <c>builder.Workspace.Opens…</c> lines. Its OWN schemes
/// are not: an app always sees itself.
/// </para>
/// </summary>
public static class AndroidQueries
{
    /// <summary>What every policy hands over before an app says anything.</summary>
    private static readonly string[] Web = [Uri.UriSchemeHttp, Uri.UriSchemeHttps];

    /// <summary>
    /// Writes the fragment for what this assembly declared. Only when what it says CHANGED: the
    /// build merges the manifest again whenever this file is newer than the merged one.
    /// </summary>
    public static void Write(string assemblyPath, string manifestPath)
    {
        var schemes = Web.Concat(OpenedSchemes.Read(assemblyPath).Except(Web, StringComparer.Ordinal)).ToArray();
        var document = AndroidManifestWriter.Document(manifest => manifest.QueriesForLinks(schemes));
        if (File.Exists(manifestPath) && File.ReadAllText(manifestPath) == document) return;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!);
        File.WriteAllText(manifestPath, document);
    }
}
