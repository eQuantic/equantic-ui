using System.Security;

namespace eQuantic.UI.Codegen;

/// <summary>
/// An Android manifest, as much of one as the SDK writes: a fragment the Android build merges over
/// the manifest it generates from the app's attributes. Some of what a manifest says has no
/// attribute to say it with, and this is where that goes instead of a file a developer keeps.
/// </summary>
public sealed class AndroidManifestWriter : CodeWriter
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

    /// <summary>The document: prologue, the root element, and whatever the caller puts in it.</summary>
    public static string Document(Action<AndroidManifestWriter> body)
    {
        var writer = new AndroidManifestWriter();
        writer.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        using (writer.BeginScope($"<manifest xmlns:android=\"{AndroidNamespace}\">", "</manifest>")) body(writer);
        return writer.ToString();
    }

    /// <summary>
    /// The other apps this one may ask the system about, by the links they open: one VIEW intent
    /// per scheme. Since Android 11 an app sees only what it lists here, and resolving an intent
    /// for any other scheme answers "nothing", whatever is installed.
    /// </summary>
    public AndroidManifestWriter QueriesForLinks(params string[] schemes)
    {
        using (BeginScope("<queries>", "</queries>"))
        {
            foreach (var scheme in schemes)
            {
                using (BeginScope("<intent>", "</intent>"))
                {
                    AppendLine("<action android:name=\"android.intent.action.VIEW\" />");
                    AppendLine($"<data android:scheme=\"{SecurityElement.Escape(scheme)}\" />");
                }
            }
        }
        return this;
    }
}
