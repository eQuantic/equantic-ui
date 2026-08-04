using eQuantic.UI.Primitives;
using Foundation;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The whole iOS entry point, so an app is a tree and a theme and nothing else:
/// <code>PhotonApp.Run(args, () => new WalletApp(), PhotonTheme.Instance);</code>
/// No storyboard, no AppDelegate to write, no Info.plist keys beyond what the SDK already emits —
/// the promise the framework makes on web (write the component, not the plumbing) has to hold here
/// or "write once" is only half true.
/// </summary>
public static class PhotonApp
{
    private static Func<VisualNode>? _root;
    private static IAppTheme? _theme;
    private static ThemeMode? _mode;
    private static int _maxFrames;
    private static bool _smoothScroll = true;

    /// <param name="root">
    /// Built AFTER UIKit is up, not before: a tree constructed at process start would measure text
    /// through a font stack that is not loaded yet.
    /// </param>
    /// <param name="forcedMode">Null follows the system's light/dark setting.</param>
    /// <param name="maxFrames">Stops after this many presents — the self-test's exit condition.</param>
    /// <param name="smoothScroll">Whether a scroll glides to where it was sent (see the host).</param>
    public static void Run(string[] args, Func<VisualNode> root, IAppTheme theme,
        ThemeMode? forcedMode = null, int maxFrames = 0, bool smoothScroll = true)
    {
        _root = root;
        _theme = theme;
        _mode = forcedMode;
        _maxFrames = maxFrames;
        _smoothScroll = smoothScroll;
        UIApplication.Main(args, null, typeof(PhotonAppDelegate));
    }

    [Register("PhotonAppDelegate")]
    public sealed class PhotonAppDelegate : UIApplicationDelegate
    {
        public override UIWindow? Window { get; set; }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            Window = new UIWindow(UIScreen.MainScreen.Bounds)
            {
                RootViewController = new PhotonViewController(_root!(), _theme!, _mode)
                {
                    MaxFrames = _maxFrames,
                    SmoothScroll = _smoothScroll,
                },
            };
            Window.MakeKeyAndVisible();
            return true;
        }
    }
}
