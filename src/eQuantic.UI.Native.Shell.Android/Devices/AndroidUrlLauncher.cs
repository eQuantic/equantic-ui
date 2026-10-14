using Android.Content;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// A VIEW intent, which is how Android is handed a URL: the system resolves it to whatever claims
/// the scheme (a browser, the mail app, the dialer, a map) and starts it.
/// <para>
/// <c>startActivity</c> answers before it returns: it throws when nothing claims the intent, and
/// that is this contract's ordinary false.
/// </para>
/// <para>
/// The QUESTION needs the manifest. Since Android 11 an app sees only the other apps its manifest
/// says it may ask about, so resolving an intent answers "nothing" for every scheme the manifest
/// does not list under <c>queries</c>, whatever is installed. The build writes that list from the
/// same <c>builder.Workspace.Opens…</c> lines the policy is built from, with the web's two schemes,
/// so <see cref="UrlLauncher.CanOpen"/> tells the truth and nobody writes the element.
/// </para>
/// </summary>
internal sealed class AndroidUrlLauncher(OpenUrlPolicy policy, ILogger<AndroidUrlLauncher> logger)
    : UrlLauncher(policy, logger)
{
    /// <inheritdoc />
    protected override ValueTask<bool> Hand(Uri url)
    {
        if (PhotonActivity.Current is not { } activity) return new(false);
        try
        {
            activity.StartActivity(View(url));
            return new(true);
        }
        catch (ActivityNotFoundException)
        {
            return new(false);
        }
    }

    /// <inheritdoc />
    protected override bool Claimed(Uri url) =>
        PhotonActivity.Current is { PackageManager: { } packages }
        && View(url).ResolveActivity(packages) is not null;

    private static Intent View(Uri url) =>
        new(Intent.ActionView, global::Android.Net.Uri.Parse(url.AbsoluteUri));
}
