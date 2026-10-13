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
/// <para>
/// The app runs under the SCENE life cycle. Built with the iOS 27 SDK, an app without it does not
/// launch at all (TN3187), so the window belongs to <see cref="PhotonSceneDelegate"/> and the app
/// delegate keeps only what is the process's. The SDK's <c>PhotonApp.plist</c> declares the scene
/// manifest; the delegate class is named here, in code, where the compiler checks it.
/// </para>
/// </summary>
public static class PhotonApp
{
    private static Func<VisualNode>? _root;
    private static IAppTheme? _theme;
    private static ThemeMode? _mode;
    private static int _maxFrames;
    private static bool _smoothScroll = true;
    private static PhotonViewController? _screen;

    /// <param name="args">The process arguments, as the platform handed them over.</param>
    /// <param name="theme">The app's theme, which every measurement and every paint reads.</param>
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

    /// <summary>
    /// The app's one screen, built the first time a scene asks for it and kept for the life of the
    /// PROCESS. The system may disconnect a scene in the background to reclaim its memory and
    /// connect a new one when the person comes back; a screen that lived and died with the scene
    /// would come back on its first page with its state gone. The process outlived the window under
    /// the old life cycle, and the tree must still outlive it under this one.
    /// </summary>
    private static PhotonViewController Screen() => _screen ??= new PhotonViewController(_root!(), _theme!, _mode)
    {
        MaxFrames = _maxFrames,
        SmoothScroll = _smoothScroll,
    };

    /// <summary>What is the process's and nothing else: which delegate each scene gets.</summary>
    [Register("PhotonAppDelegate")]
    public sealed class PhotonAppDelegate : UIApplicationDelegate
    {
        public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;

        /// <summary>
        /// The app's own scene gets the app's one window. Answered here rather than by a class NAME
        /// in the plist, so the type it names is one the compiler has seen.
        /// <para>
        /// ONLY the app's own role. A display connected by cable or AirPlay asks for a scene of
        /// another role, and handing it this delegate would move the one screen off the phone's
        /// window and onto the display, then leave the phone with none when the display goes. That
        /// scene gets no delegate, so this shell puts no window on it.
        /// </para>
        /// <para>
        /// UNNAMED, measured: a named configuration sends UIKit looking for its dictionary in the
        /// Info.plist, and finding none it logs an error on every launch ("Info.plist contained no
        /// UIScene configuration dictionary") although the scene connects fine.
        /// </para>
        /// </summary>
        public override UISceneConfiguration GetConfiguration(UIApplication application,
            UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        {
            var configuration = new UISceneConfiguration(null, connectingSceneSession.Role);
            if (connectingSceneSession.Role == UIWindowSceneSessionRole.Application)
                configuration.DelegateType = typeof(PhotonSceneDelegate);
            return configuration;
        }
    }

    /// <summary>
    /// The window, and the screen's two moments: the person leaving and coming back. A screen in
    /// the background is not drawn — the system takes the GPU away from it — so its clock stops
    /// there and starts again, with a fresh frame, when the scene returns.
    /// <para>
    /// The URLs a scene is opened WITH arrive here too, in the connection options and in
    /// <c>scene:openURLContexts:</c>, and nowhere else under this life cycle: that is where the
    /// phones' deep links (#777) read them.
    /// </para>
    /// </summary>
    [Register("PhotonSceneDelegate")]
    public sealed class PhotonSceneDelegate : UIWindowSceneDelegate
    {
        public override UIWindow? Window { get; set; }

        public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            if (scene is not UIWindowScene windowScene) return;

            // BEFORE the screen is built, so the first tree already reads what the app was started
            // with. A scene that connects to a screen the process kept is a link arriving in a
            // running app: the tree is alive, and it is told rather than started.
            Opened(connectionOptions.UrlContexts, starting: _screen is null);
            var screen = Screen();
            Window = new UIWindow(windowScene) { RootViewController = screen };
            Window.MakeKeyAndVisible();

            // A scene can connect while the app is in the background, and one connected there never
            // ENTERS the background, so the clock the view just started is paused here. And a scene
            // connected after another left in the background resumes the clock that one paused.
            if (scene.ActivationState == UISceneActivationState.Background) screen.Pause();
            else screen.Resume();
        }

        public override void DidDisconnect(UIScene scene)
        {
            // The window goes with the scene; the screen does not (see Screen()).
            if (Window is not null) Window.RootViewController = null;
            Window = null;
        }

        /// <summary>A link opened while the scene is connected.</summary>
        public override void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts) =>
            Opened(urlContexts, starting: false);

        /// <summary>
        /// Tells the relay. A start says what it came with, a link or nothing, exactly once; every
        /// other URL is one the running app was opened with.
        /// </summary>
        private static void Opened(NSSet<UIOpenUrlContext>? contexts, bool starting)
        {
            var relay = IosDeepLinks.Relay;
            foreach (var context in contexts?.ToArray() ?? [])
            {
                var link = context.Url.AbsoluteString;
                if (starting) relay.StartedWith(link);
                else relay.Offer(link);
                starting = false;
            }
            if (starting) relay.StartedWith(null);
        }

        public override void DidEnterBackground(UIScene scene) => _screen?.Pause();

        public override void WillEnterForeground(UIScene scene) => _screen?.Resume();
    }
}
