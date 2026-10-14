using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What this app hands to the SYSTEM, stated where every other app fact is stated — on the builder,
/// in <c>Program.cs</c>, in C#.
/// <para>
/// There is one setting here and it is a door: which URL schemes
/// <see cref="IUrlLauncher.OpenUrlAsync"/> hands over. The web is open already, and so is every scheme this app declared it answers to with
/// <c>builder.Bundle.UrlScheme(…)</c> — the SDK knows those, so they are not said twice. Everything
/// else is a decision, and it is made here:
/// </para>
/// <code>
/// builder.Workspace.OpensMail();                          // a "Contact support" button
/// builder.Workspace.Opens("x-apple.systempreferences");   // a settings pane, on a Mac
/// </code>
/// <para>
/// Two readers. At run time the policy is composed when the app is built and registered as the
/// <see cref="OpenUrlPolicy"/> every realization consults; an app that registers its own policy
/// before <c>Build()</c> has decided, and this one steps aside. At BUILD time the generator reads
/// the same lines, because a phone answers <see cref="IUrlLauncher.CanOpen"/> only for the schemes
/// its manifest lists: the SDK writes them there, so a scheme opened here is a constant (EQ3007).
/// </para>
/// </summary>
public sealed class PhotonWorkspaceBuilder
{
    private OpenUrlPolicy _policy = OpenUrlPolicy.Web;

    /// <summary>The policy as declared so far: the web, plus what was opened here. The app's own
    /// schemes join it when the app is built.</summary>
    public OpenUrlPolicy Declared => _policy;

    /// <summary><c>mailto:</c> links — a "Contact support" button, a "share by e-mail" action.</summary>
    public PhotonWorkspaceBuilder OpensMail() => Opens(OpenedSchemeRule.Mail);

    /// <summary><c>tel:</c> links. A phone dials; a desktop hands them to whatever claims them.</summary>
    public PhotonWorkspaceBuilder OpensPhone() => Opens(OpenedSchemeRule.Phone);

    /// <summary><c>sms:</c> links — the platform's messages app.</summary>
    public PhotonWorkspaceBuilder OpensMessages() => Opens(OpenedSchemeRule.Messages);

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
