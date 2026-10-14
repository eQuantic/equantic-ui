using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// <c>UIApplication</c>, handed a URL: <c>openURL:options:completionHandler:</c> routes it to
/// whatever claims its scheme and says whether it opened in a callback, and <c>canOpenURL:</c>
/// asks without acting.
/// <para>
/// The answer is the system's own and it arrives LATER, which is why the contract is asynchronous.
/// The call that answered at once was retired, and an app linked against a current SDK is told no
/// by it without the URL being looked at.
/// </para>
/// <para>
/// <c>canOpenURL:</c> answers only for the schemes the app's <c>Info.plist</c> lists under
/// <c>LSApplicationQueriesSchemes</c>, and says no to every other one whatever is installed. The
/// build writes that list from the same <c>builder.Workspace.Opens…</c> lines the policy is built
/// from, so the two agree and nobody writes the key.
/// </para>
/// <para>
/// Both are UIKit's and belong to the main thread. A click handler is already there. A caller that
/// is not is taken there, as the file dialogs are.
/// </para>
/// </summary>
internal sealed class IosUrlLauncher(OpenUrlPolicy policy, ILogger<IosUrlLauncher> logger) : UrlLauncher(policy, logger)
{
    /// <inheritdoc />
    protected override ValueTask<bool> Hand(Uri url)
    {
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            if (NSUrl.FromString(url.AbsoluteUri) is not { } native)
            {
                opened.TrySetResult(false);
                return;
            }
            UIApplication.SharedApplication.OpenUrl(native, new UIApplicationOpenUrlOptions(),
                took => opened.TrySetResult(took));
        });
        return new(opened.Task);
    }

    /// <inheritdoc />
    protected override bool Claimed(Uri url)
    {
        var claimed = false;
        // Synchronous, and inline when this already is the main thread.
        UIApplication.SharedApplication.InvokeOnMainThread(() =>
            claimed = NSUrl.FromString(url.AbsoluteUri) is { } native
                && UIApplication.SharedApplication.CanOpenUrl(native));
        return claimed;
    }
}
