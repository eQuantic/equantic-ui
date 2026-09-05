using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What this app hands to the SYSTEM through <see cref="IWorkspace"/>, stated where every other app
/// fact is stated — on the builder, in <c>Program.cs</c>, in C#.
/// <para>
/// There is one setting here and it is a door: which URL schemes <see cref="IWorkspace.OpenUrl"/>
/// hands over. The web is open already, and so is every scheme this app declared it answers to with
/// <c>builder.Bundle.UrlScheme(…)</c> — the SDK knows those, so they are not said twice. Everything
/// else is a decision, and it is made here:
/// </para>
/// <code>
/// builder.Workspace.OpensMail();                          // a "Contact support" button
/// builder.Workspace.Opens("x-apple.systempreferences");   // a settings pane, on a Mac
/// </code>
/// <para>
/// Nothing here reaches a manifest, so no generator reads it: the policy is composed when the app
/// is built and registered as the <see cref="OpenUrlPolicy"/> every realization consults. An app
/// that registers its own policy before <c>Build()</c> has decided, and this one steps aside.
/// </para>
/// </summary>
public sealed class PhotonWorkspaceBuilder
{
    private OpenUrlPolicy _policy = OpenUrlPolicy.Web;

    /// <summary>The policy as declared so far: the web, plus what was opened here. The app's own
    /// schemes join it when the app is built.</summary>
    public OpenUrlPolicy Declared => _policy;

    /// <summary><c>mailto:</c> links — a "Contact support" button, a "share by e-mail" action.</summary>
    public PhotonWorkspaceBuilder OpensMail() => Opens(Uri.UriSchemeMailto);

    /// <summary><c>tel:</c> links. A phone dials; a desktop hands them to whatever claims them.</summary>
    public PhotonWorkspaceBuilder OpensPhone() => Opens("tel");

    /// <summary><c>sms:</c> links — the platform's messages app.</summary>
    public PhotonWorkspaceBuilder OpensMessages() => Opens("sms");

    /// <summary>
    /// The general form: another app's scheme, or one of the system's. The scheme is a NAME
    /// (<c>slack</c>, <c>vscode</c>, <c>x-apple.systempreferences</c>), and .NET's own constants fit
    /// (<c>Uri.UriSchemeMailto</c>). <c>file</c> is refused: a file has typed doors —
    /// <see cref="IWorkspace.OpenFile"/>, <see cref="IWorkspace.Reveal"/> — that check what a URL
    /// would skip.
    /// </summary>
    public PhotonWorkspaceBuilder Opens(string scheme)
    {
        _policy = _policy.Allowing(scheme);
        return this;
    }
}
