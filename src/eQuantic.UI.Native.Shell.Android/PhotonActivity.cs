using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using System.Runtime.Versioning;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Vulkan;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Choreographer = Android.Views.Choreographer;
// This assembly's own namespace ends in `Android`, so an inline Android.* path would resolve against
// it. The alias keeps the platform's enum reachable without qualifying it at every use.
using UiMode = Android.Content.Res.UiMode;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The Photon surface as an Android screen. The Activity owns three things and no more — the view,
/// the touches and the clock — exactly as the iOS view controller does, and the tree above it is
/// the same C# on both.
/// <para>
/// The clock is the <see cref="Choreographer"/>, which is the display's own vsync rather than a
/// timer guessing at it, and a frame is only PRESENTED when the host says something changed. A
/// still screen costs a callback that returns immediately, which is what keeps a phone's battery
/// out of this.
/// </para>
/// <para>
/// Presentation goes through VULKAN, straight into the window's own surface — the same engine and
/// the same renderer the Metal backend runs, so the two can only differ in API calls. Where Vulkan
/// is unavailable the NORMATIVE Reference backend draws the same display list in software: correct
/// by definition, slower, and invisible to everything above it.
/// </para>
/// <para>
/// The HOST lives as long as the activity, and the SURFACE does not. Android takes a
/// <c>SurfaceView</c>'s surface away whenever the activity leaves the screen (Home, another app, the
/// screen turning off) and hands a new one back on return. Only what DRAWS goes with it (the
/// swapchain, the window, the backend), so the app comes back on the screen it left, with its
/// state. Building the host again from the app's root there would open the app on its first
/// screen every time the person looked away.
/// </para>
/// <para>
/// The system's BACK reaches <see cref="PhotonHost.Back"/>, which closes what is open: a dialog, a
/// sheet, a drawer, a menu, a phone's detail pane. Only when nothing is open does the system's own
/// back run, and that leaves the app, a field being edited or not.
/// </para>
/// <para>
/// The KEYBOARD opens when an edit begins and closes when it ends, read from the host after each
/// frame through a <see cref="TextInputSession"/>; a field is edited as a document through
/// <see cref="PhotonInputConnection"/>. The tree lays out above the keyboard (the host's
/// <see cref="PhotonHost.ViewInsets"/>) and the field being edited is scrolled into what is left.
/// Keys, from the keyboard or from one attached to the device, reach the host as on a desktop.
/// </para>
/// <para>
/// The LAUNCHER activity is generated into the app's own assembly, not this one. Android would
/// otherwise never load the app: nothing would have referenced it, so the registration that names
/// its program would never run. Subclassing is also what gives an app somewhere to override.
/// </para>
/// </summary>
public class PhotonActivity : Activity, ISurfaceHolderCallback, Choreographer.IFrameCallback
{
    private PhotonSurfaceView? _view;
    private PhotonHost? _host;
    private VulkanBackend? _vulkan;
    private VulkanSwapchain? _swapchain;
    private IntPtr _window;
    private ReferenceBackend? _reference;
    private Bitmap? _bitmap;
    private long _startedAt;
    private BackCallback? _back;
    private bool _backRegistered;
    private bool _backKeyTaken;
    private TextInputSession? _input;
    private InputMethodManager? _keyboard;
    private int _deadAccent;

    /// <summary>Frames actually presented — the self-test's exit evidence, as on every other shell.</summary>
    public int FramesPresented { get; private set; }

    /// <summary>
    /// The app the runner built. Static because Android constructs the Activity itself.
    /// <para>
    /// <c>new</c>, and deliberately: <c>Activity.Application</c> is Android's own application object
    /// and this is Photon's. The two can never be confused by the COMPILER — the types differ, so a
    /// mix-up is a build error rather than a silent wrong value — but a reader of this file sees one
    /// word meaning something other than what the platform means by it, which is why it says so here
    /// rather than leaving a warning for everyone who builds the shell.
    /// </para>
    /// </summary>
    internal static new PhotonApplication? Application { get; set; }

    /// <summary>
    /// The activity on screen. A capability needs one to start anything — a picker, a camera, a
    /// permission prompt — and Android constructs the activity itself, so it says which it is.
    /// </summary>
    internal static PhotonActivity? Current { get; private set; }

    private readonly Dictionary<int, TaskCompletionSource<Intent?>> _pending = new();
    private int _nextRequest = 1;

    /// <summary>
    /// Starts something and AWAITS what comes back. Android answers activity results through a
    /// callback on the activity, which is a shape no capability can expose to an app — so it is
    /// bridged here, once, and every capability that starts an intent gets to be an ordinary
    /// awaitable method.
    /// </summary>
    internal Task<Intent?> PickAsync(Intent intent, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<Intent?>();
        int request;
        lock (_pending)
        {
            request = _nextRequest++;
            _pending[request] = completion;
        }

        // Cancelling stops the WAIT, not the system UI: nothing can dismiss the picker from here,
        // and pretending otherwise would leave a caller believing it had.
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        StartActivityForResult(intent, request);
        return completion.Task;
    }

    private readonly Dictionary<int, TaskCompletionSource<bool?>> _pendingPermissions = new();

    /// <summary>
    /// The permission twin of <see cref="PickAsync"/>: Android answers permission requests through
    /// a callback on the activity, and this bridges it once so a capability can simply await.
    /// True when every requested permission was granted, false when one was refused, and null
    /// when the request came back with no answer at all: Android does that when the prompt was
    /// interrupted, and nobody refused anything then.
    /// </summary>
    internal Task<bool?> RequestPermissionsAsync(string[] permissions,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<bool?>();
        int request;
        lock (_pendingPermissions)
        {
            request = _nextRequest++;
            _pendingPermissions[request] = completion;
        }
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        RequestPermissions(permissions, request);
        return completion.Task;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
        global::Android.Content.PM.Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        TaskCompletionSource<bool?>? completion;
        lock (_pendingPermissions)
        {
            if (!_pendingPermissions.Remove(requestCode, out completion)) return;
        }
        completion.TrySetResult(grantResults.Length == 0
            ? null
            : grantResults.All(g => g == global::Android.Content.PM.Permission.Granted));
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        TaskCompletionSource<Intent?>? completion;
        lock (_pending)
        {
            if (!_pending.Remove(requestCode, out completion)) return;
        }
        // Cancelled and "came back with nothing" are the same answer to a caller: no picture.
        completion.TrySetResult(resultCode == Result.Ok ? data : null);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // The activity before this one, when the process had one and Android has not got round to
        // destroying it yet: its tree comes down now, before anything is said to this one's.
        if (!ReferenceEquals(Current, this)) Current?.TakeDown();
        Current = this;

        // What this start came with: a link, or nothing (the launcher's intent carries none). Said
        // here, before the app's tree exists, so the first screen already reads it.
        AndroidDeepLinks.Created(Intent, UrlSchemes, restoring: savedInstanceState is not null);

        // The keyboard is given room rather than drawn over the app. Edge to edge, which Android 15
        // enforces, the window keeps its size and the keyboard arrives as an inset; before that, the
        // system shortens the window. The host reveals the edited field either way.
        Window?.SetSoftInputMode(SoftInput.AdjustResize);

        _view = new PhotonSurfaceView(this);
        _view.Holder!.AddCallback(this);
        _view.InsetsChanged = InsetsChanged;
        SetContentView(_view);
    }

    /// <summary>
    /// A link opened while the app runs. The launcher is a single task, so Android hands the
    /// running activity the intent instead of starting another one over it.
    /// <para>
    /// The icon tapped while the app runs arrives here too, as the launcher's own intent, and it is
    /// NOT a start: the tree is alive and the person came back to it, as on an iPhone or a Mac,
    /// where coming to the front tells an app nothing. What the app was started with is still what
    /// it was, for as long as this tree lives. A start is a create, and a create says so.
    /// </para>
    /// </summary>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        AndroidDeepLinks.Relay.Offer(AndroidDeepLinks.LinkOf(intent, UrlSchemes));
    }

    /// <summary>
    /// The URL schemes this app declared on its builder. The launcher the generator writes answers
    /// with them, and a link on any other scheme is not one this app is opened with.
    /// </summary>
    protected virtual IReadOnlyList<string> UrlSchemes => [];

    /// <summary>
    /// The activity is gone for good, and its tree goes with it. Android keeps the process, with
    /// everything the tree subscribed to, and the next activity builds a tree of its own.
    /// </summary>
    protected override void OnDestroy()
    {
        TakeDown();
        base.OnDestroy();
    }

    private void TakeDown()
    {
        _host?.Unmount();
        _host = null;
    }

    /// <summary>
    /// The activity left the screen, and came back onto it. What a capability holds that Android
    /// will not leave with an app nobody sees (the camera) is let go on the first and taken again
    /// on the second.
    /// <para>
    /// STOPPED, not paused. A paused activity may still be in full view: beside another window, or
    /// under a permission prompt or a share sheet. A preview that froze each time one of those
    /// took the focus would be letting the camera go while the person is looking at it.
    /// </para>
    /// </summary>
    internal event Action? Stopped;

    /// <inheritdoc cref="Stopped"/>
    internal event Action? Started;

    /// <summary>The activity is the one in front again. It may never have left the screen, so this
    /// is where a capability that lost its hardware to the window that had the focus asks again.</summary>
    internal event Action? Resumed;

    protected override void OnStart()
    {
        base.OnStart();
        Started?.Invoke();
    }

    protected override void OnStop()
    {
        base.OnStop();
        Stopped?.Invoke();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Resumed?.Invoke();
    }

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        // The window behind the Surface is what Vulkan draws at. Holding it keeps the surface
        // alive for as long as the swapchain does.
        _window = AndroidWindow.FromSurface(holder.Surface!);
        if (_window != IntPtr.Zero && VulkanBackend.IsSupported)
        {
            _vulkan = new VulkanBackend();
        }
        else
        {
            _reference = new ReferenceBackend();
        }

        // Which one drew the frame is not a detail to guess at from a screenshot.
        global::Android.Util.Log.Info("Photon", _vulkan is not null
            ? "presenting through Vulkan"
            : "presenting through the Reference backend (no Vulkan surface)");

        // A surface handed BACK: the app is returning to the screen it left, so the host it left
        // there draws into the new one. Its tree, its state and its focus are exactly as they were.
        if (_host is not null)
        {
            _host.Invalidate();
            Choreographer.Instance!.PostFrameCallback(this);
            return;
        }

        // Android launched us; nobody ran a Main. The app describes itself in a CreateApp the
        // host can find, and is RUN as every other platform's entry point runs it: Run arms what the
        // whole process shares (the capability scope a component in the middle of the tree asks,
        // the UI thread's dispatcher, the report of a contained failure), and its runner hands the
        // app to this activity. Without it, context.GetService answered null on Android and a
        // SetState from another thread was not marshalled.
        if (Application is null) PhotonApplication.Create().Run();
        var app = Application ?? throw new InvalidOperationException(
            "The Android runner did not hand the app to the activity.");

        var metrics = Resources!.DisplayMetrics!;
        var scale = metrics.Density;
        var text = new AndroidTextService();

        // The device's locale, copied onto .NET before the first realize (I18N-PLAN D5/W6):
        // Android's Locale.getDefault() carries language and region in one, so the D13 pair
        // collapses to a single culture here.
        var cultureController = app.Services.GetService(typeof(PhotonCultureController)) as PhotonCultureController;
        cultureController?.Apply(AndroidLocale.Resolve());

        _host = new PhotonHost(app.Root(), app.Options.Theme, Mode(), metrics.WidthPixels / scale,
            metrics.HeightPixels / scale, text)
        {
            RecycleFrames = true,   // production owns its frames (LayoutNodePool)
            RenderScale = scale,
            TextRasterizer = text,
            IconRasterizer = new AndroidIconRasterizer(),
            // A phone rolls too: a scroll that jumps reads as a redraw there just as on a desktop.
            SmoothScroll = app.Options.SmoothScroll,
            // What Ctrl+C, X and V on a keyboard attached to the phone copy, cut and paste through.
            Clipboard = new AndroidClipboard(),
        };
        cultureController?.Attach(_host.Invalidate);

        // The semantics bridge: TalkBack reads the SAME target-neutral tree the Apple shells read,
        // and its gestures reach the same handlers a finger does — by the node's id, like everything
        // else that has to survive the next frame.
        _view!.Semantics = new PhotonAccessibility(_view,
            () => _host?.Semantics() ?? [],
            id => _host?.Activate(id) ?? false,
            (id, direction) => _host?.Adjust(id, direction) ?? false,
            id => _host?.LiveRegionText(id));

        // The keyboard follows the host's edit: the session says when it begins, moves and ends,
        // and the view hands the keyboard a connection to it.
        _keyboard = GetSystemService(InputMethodService)?.JavaCast<InputMethodManager>();
        _input = new TextInputSession(_host);
        if (_keyboard is not null) _view.Editing = (_host, _input, _keyboard);

        Choreographer.Instance!.PostFrameCallback(this);
    }

    /// <summary>The surface's real size arrives here, and again on every rotation.</summary>
    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
    {
        if (_host is null) return;
        var scale = Resources!.DisplayMetrics!.Density;
        _host.Resize(width / scale, height / scale);
        ApplyInsets(scale);

        if (_vulkan is not null)
        {
            if (_swapchain is null) _swapchain = _vulkan.CreateSwapchain(_window, width, height);
            else _swapchain.Resize(width, height);
            return;
        }

        _bitmap?.Dispose();
        _bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
    }

    /// <summary>
    /// The activity left the screen and took its surface with it. What DRAWS goes (the swapchain,
    /// the window, the backend); the host stays, because it holds the app's tree and its state, and
    /// <see cref="SurfaceCreated"/> draws it into the surface handed back.
    /// </summary>
    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        Choreographer.Instance!.RemoveFrameCallback(this);
        _swapchain?.Dispose();
        _swapchain = null;
        _vulkan?.Dispose();
        _vulkan = null;
        if (_window != IntPtr.Zero) AndroidWindow.Release(_window);
        _window = IntPtr.Zero;
        _bitmap?.Dispose();
        _bitmap = null;
        _reference?.Dispose();
        _reference = null;
    }

    /// <summary>
    /// The system's appearance or the display changed while the activity kept running: the manifest
    /// declares these changes as the activity's own to handle, so nothing rebuilds it. Light or dark
    /// follows the system unless the app fixed it, and a new density re-rasters at the new scale AND
    /// lays out at the new size in dp: "Display size" changes the density and not the pixels, so no
    /// surface change arrives to do it.
    /// </summary>
    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (_host is null) return;
        _host.Mode = Mode();
        var scale = Resources!.DisplayMetrics!.Density;
        _host.RenderScale = scale;
        if (_view?.Holder?.SurfaceFrame is { } frame && !frame.IsEmpty)
            _host.Resize(frame.Width() / scale, frame.Height() / scale);
        ApplyInsets(scale);
        _host.Invalidate();
    }

    /// <summary>
    /// The system's own margins — the status bar, the gesture handle, a cutout. A SafeArea node lays
    /// out against what the DEVICE reserves, which is the same contract the iOS shell honours and
    /// the reason the Wallet's trees need no change to sit correctly on either. And the keyboard,
    /// which is not a margin but a cover: the host lays the tree out above it.
    /// </summary>
    private void ApplyInsets(float scale)
    {
        if (_host is null || _view?.RootWindowInsets is not { } insets) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            _host.SafeAreaInsets = new EdgeInsets(
                bars.Left / scale, bars.Top / scale, bars.Right / scale, bars.Bottom / scale);
            _host.ViewInsets = new EdgeInsets(0, 0, 0, KeyboardOverlap(insets) / scale);
            return;
        }

        // API 26-29: the typed accessor does not exist yet. The STABLE insets are the bars alone:
        // the system window's grow by the keyboard, which there shortens the window instead, and
        // read here it would pad the bottom by a keyboard the window already ends above.
        _host.SafeAreaInsets = new EdgeInsets(
            insets.StableInsetLeft / scale, insets.StableInsetTop / scale,
            insets.StableInsetRight / scale, insets.StableInsetBottom / scale);
    }

    /// <summary>
    /// How much of THIS view the keyboard covers, in pixels: from where the view ends to where the
    /// keyboard begins, rather than the keyboard's height. A window the system still shortens for
    /// the keyboard (an app not drawn edge to edge) already ends above it, and the keyboard reaches
    /// the host as that shorter window; read as an inset too, it would count twice.
    /// </summary>
    [SupportedOSPlatform("android30.0")]
    private int KeyboardOverlap(WindowInsets insets)
    {
        if (_view is null || Window?.DecorView is not { } decor) return 0;
        var keyboard = insets.GetInsets(WindowInsets.Type.Ime()).Bottom;
        if (keyboard <= 0) return 0;
        var at = new int[2];
        _view.GetLocationInWindow(at);
        return Math.Max(0, at[1] + _view.Height - (decor.Height - keyboard));
    }

    /// <summary>The insets changed without anything in the host changing: the keyboard rose or
    /// fell. The frame that lays out around them is drawn now.</summary>
    private void InsetsChanged()
    {
        if (_host is null) return;
        var safeArea = _host.SafeAreaInsets;
        ApplyInsets(Resources!.DisplayMetrics!.Density);
        if (_host.SafeAreaInsets != safeArea) _host.Invalidate();
    }

    // ---- The clock ------------------------------------------------------------------------------

    public void DoFrame(long frameTimeNanos)
    {
        Choreographer.Instance!.PostFrameCallback(this);
        if (_host is null || _view?.Holder is not { } holder) return;

        var forced = Application?.Options.MaxFrames > 0;
        if (_startedAt == 0) _startedAt = frameTimeNanos;
        var nowMs = (frameTimeNanos - _startedAt) / 1_000_000f;
        // Dirty, forced, or DUE — the caret's next blink transition (see PhotonHost.IsFrameDue).
        if (!_host.NeedsRender && !_host.IsFrameDue(nowMs) && forced != true) return;

        // Insets can change without a resize — a keyboard, a call banner, a rotation mid-gesture.
        ApplyInsets(Resources!.DisplayMetrics!.Density);

        var builder = new DisplayListBuilder();
        _host.RenderFrame(builder, nowMs);
        var displayList = builder.Build();

        if (_vulkan is not null && _swapchain is not null)
        {
            // False means the window changed shape under us — a fact about the WINDOW, not a
            // failure: rebuild against what it is now and draw the next frame into that.
            if (!_vulkan.Present(displayList, _swapchain))
            {
                _swapchain.Resize(holder.SurfaceFrame!.Width(), holder.SurfaceFrame.Height());
                // Nothing was shown, and rendering marked the host clean: the next tick draws it
                // again, into the rebuilt swapchain, and syncs back after it.
                _host.Invalidate();
                return;
            }
            FramesPresented++;
        }
        else if (_reference is not null && _bitmap is not null)
        {
            using var surface = _reference.CreateSurface(_bitmap.Width, _bitmap.Height);
            _reference.Render(displayList, surface);

            // The engine speaks RGBA; an Android bitmap wants ARGB with the bytes the other way round.
            var rgba = new byte[_bitmap.Width * _bitmap.Height * 4];
            surface.ReadPixelsSrgb(rgba);
            var argb = new int[rgba.Length / 4];
            for (var i = 0; i < argb.Length; i++)
            {
                var p = i * 4;
                argb[i] = (rgba[p + 3] << 24) | (rgba[p] << 16) | (rgba[p + 1] << 8) | rgba[p + 2];
            }
            _bitmap.SetPixels(argb, 0, _bitmap.Width, 0, 0, _bitmap.Width, _bitmap.Height);

            var canvas = holder.LockCanvas();
            if (canvas is null) return;
            canvas.DrawBitmap(_bitmap, 0, 0, null);
            holder.UnlockCanvasAndPost(canvas);
            FramesPresented++;
        }

        // The screen just changed: if that changed what a screen reader would read, tell the system, and
        // what a live region said since the last frame (#1081). Drained whether a reader listens or not:
        // the host keeps what it owes until a shell takes it.
        _view.Semantics?.FrameRendered(_host.TakeAnnouncements());

        // And whether back has anywhere to go now, which Android has to know BEFORE the gesture.
        SyncBack();

        // And whether the keyboard has to open, start over, close or hear a new value.
        SyncKeyboard();

        if (forced == true && FramesPresented >= Application!.Options.MaxFrames)
        {
            ProbeAccessibility();
            var contained = eQuantic.UI.Primitives.ComponentBoundary.Contained;
            var missing = eQuantic.UI.Primitives.FaceResolution.Unresolved;
            var summary = $"frames presented: {FramesPresented}";
            if (contained.Count > 0) summary += $" — CONTAINED: {string.Join(", ", contained)}";
            // AndroidTextService RECORDS a family it could not supply. Recording without surfacing
            // is the silence this exists to end, and this is the one line anyone reads.
            if (missing.Count > 0) summary += $" — FACE NOT FOUND: {string.Join(", ", missing)}";
            Console.WriteLine($"[photon] {summary}");
            // No exit code here on purpose: an Activity is not a process with a status, and
            // Finish() finishes a screen. StrictRender is scoped to the desktop shells, which have
            // one — setting it here would have looked like a gate and been a no-op.

            Choreographer.Instance.RemoveFrameCallback(this);
            Finish();
        }
    }

    /// <summary>
    /// Self-test evidence for the semantics bridge, taken through the PROVIDER the platform itself
    /// queries: what TalkBack would receive if it asked this view right now, and what happens when
    /// it double-taps the first control. Asking the provider rather than the tree is the point —
    /// a walk of the tree would pass whether or not the virtual hierarchy is reachable.
    /// </summary>
    private void ProbeAccessibility()
    {
        if (_view?.AccessibilityNodeProvider is not { } provider) return;
        var host = provider.CreateAccessibilityNodeInfo(
            global::Android.Views.Accessibility.AccessibilityNodeProvider.HostViewId);
        var count = host?.ChildCount ?? 0;
        if (count == 0)
        {
            global::Android.Util.Log.Info("Photon", "accessibility elements: 0");
            return;
        }

        var first = provider.CreateAccessibilityNodeInfo(0);
        global::Android.Util.Log.Info("Photon",
            $"accessibility elements: {count} — first: {first?.ClassName} \"{first?.ContentDescription}\"");

        for (var index = 0; index < count; index++)
        {
            var node = provider.CreateAccessibilityNodeInfo(index);
            if (node?.ClassName != "android.widget.Button") continue;
            var ran = provider.PerformAction(index, global::Android.Views.Accessibility.Action.Click, null);
            global::Android.Util.Log.Info("Photon",
                $"accessibility activate \"{node.ContentDescription}\": {ran}");
            return;
        }
        global::Android.Util.Log.Info("Photon", "accessibility activate: no button on this screen");
    }

    // ---- Back -----------------------------------------------------------------------------------

    /// <summary>
    /// Tells the system whether this app takes back right now, from Android 13 on. Predictive back
    /// starts its animation while the gesture is still under the finger, "leave the app" or "stay",
    /// so the app registers a callback while it has somewhere to go and unregisters it when it has
    /// not. Unregistered, the system's own back runs, which leaves the app with the animation
    /// Android draws for that. Where the platform does not deliver back this way (before 13, or an
    /// app that has not opted into predictive back), the registration is ignored and back arrives as
    /// a key instead: <see cref="DispatchKeyEvent"/>.
    /// </summary>
    private void SyncBack()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        var wanted = _host?.CanGoBack == true;
        if (wanted == _backRegistered) return;
        _back ??= new BackCallback(this);
        if (wanted)
        {
            // OnBackInvokedDispatcher.PRIORITY_DEFAULT: the app's own content, below an IME or a
            // system overlay, which take back first.
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, _back);
        }
        else
        {
            OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(_back);
        }
        _backRegistered = wanted;
    }

    /// <summary>
    /// Every key reaches the host here (<see cref="Key"/>), and back is the one with rules of its own.
    /// <para>
    /// Back, as Android delivers it: the callback above, or this key. Before Android 13 it is always
    /// the key, and from 13 it still is for an app the platform does not treat as opted into
    /// predictive back; when the platform calls the callback, the key never arrives, so the two
    /// cannot both run for one press.
    /// </para>
    /// <para>
    /// The DOWN decides, and the UP follows it. Taken, the activity never starts tracking a back of
    /// its own and cannot finish on the UP; and the UP is taken even when what was open closed in
    /// between, where <see cref="GoBack"/> then does what the system would have. Deciding again on
    /// the UP lost that press: the activity ignores an UP whose DOWN it never saw.
    /// </para>
    /// </summary>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is null) return base.DispatchKeyEvent(e);
        if (e.KeyCode != Keycode.Back) return Key(e) || base.DispatchKeyEvent(e);
        if (e.Action == KeyEventActions.Down && e.RepeatCount == 0) _backKeyTaken = _host?.CanGoBack == true;
        if (!_backKeyTaken) return base.DispatchKeyEvent(e);
        if (e.Action == KeyEventActions.Up)
        {
            _backKeyTaken = false;
            if (!e.IsCanceled) GoBack();
        }
        return true;
    }

    /// <summary>
    /// Back reached the app. The host closes what is open; when it finds nothing (the callback was
    /// still registered from a frame ago), this does what the system would have: the launcher's own
    /// activity sends its task to the background, keeping it alive as Android does from 12 on, and
    /// any other activity finishes.
    /// </summary>
    private void GoBack()
    {
        if (_host?.Back() == true) return;
        SyncBack();
        if (IsTaskRoot) MoveTaskToBack(true);
        else Finish();
    }

    /// <summary>The platform's callback object for back. A Java interface, so a Java object.</summary>
    [SupportedOSPlatform("android33.0")]
    private sealed class BackCallback(PhotonActivity activity)
        : Java.Lang.Object, global::Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => activity.GoBack();
    }

    // ---- The keyboard ---------------------------------------------------------------------------

    /// <summary>
    /// Opens, restarts and closes the keyboard as the host's edit begins, moves and ends, and tells
    /// it a field's new state. Read after each frame, as back is: the host says what is being
    /// edited, the session says what changed, and this does it.
    /// </summary>
    private void SyncKeyboard()
    {
        if (_input is null || _view is null || _keyboard is null) return;
        var change = _input.Poll();
        if (change != TextInputChange.None && PhotonInputTrace.On)
            PhotonInputTrace.Write($"{change}: {_input.Target} {PhotonInputTrace.Value(_input.Value, _input.Obscure)}");
        // An accent a dead key held belongs to the field it was typed in.
        if (change is TextInputChange.Began or TextInputChange.Retargeted or TextInputChange.Ended)
            _deadAccent = 0;
        switch (change)
        {
            case TextInputChange.Began:
                _view.RequestFocus();
                _keyboard.RestartInput(_view);
                ShowKeyboard();
                break;
            case TextInputChange.Retargeted:
                _keyboard.RestartInput(_view);
                ShowKeyboard();
                break;
            case TextInputChange.Ended:
                HideKeyboard();
                // Restarted on nothing: the view now answers that there is nothing to edit.
                _keyboard.RestartInput(_view);
                break;
            case TextInputChange.ValueChanged:
                _view.Connection?.Apply(_input.Value);
                break;
        }
    }

    private void ShowKeyboard()
    {
        if (_view is null) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && _view.WindowInsetsController is { } controller)
            controller.Show(WindowInsets.Type.Ime());
        else
            _keyboard?.ShowSoftInput(_view, 0);
    }

    private void HideKeyboard()
    {
        if (_view is null) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && _view.WindowInsetsController is { } controller)
            controller.Hide(WindowInsets.Type.Ime());
        else
            _keyboard?.HideSoftInputFromWindow(_view.WindowToken, HideSoftInputFlags.None);
    }

    /// <summary>
    /// A key for the host: one the keyboard sent (Enter, Backspace, every key into a code surface)
    /// or one from a keyboard attached to the device. The named key first, as on Windows: only
    /// what nothing claimed becomes text, so Space runs the focused button instead of typing into
    /// whatever field was last touched.
    /// </summary>
    private bool Key(KeyEvent e)
    {
        if (_host is null || e.Action != KeyEventActions.Down) return false;
        var modifiers = AndroidKeys.Modifiers(e);
        if (AndroidKeys.DeadAccent(e, modifiers) is var accent and not 0) return Hold(accent);
        var name = AndroidKeys.NameOf(e);
        var text = AndroidKeys.TypedText(e, modifiers);
        // The key after a dead key: what it types takes the accent, and any other key lets it go.
        // Shift is neither, so the accent waits through it: ´ then Shift+A is Á.
        if (_deadAccent != 0 && (name.Length > 0 || text.Length > 0))
        {
            var held = _deadAccent;
            _deadAccent = 0;
            if (text.Length > 0) return Took(_host.TextInput(AndroidKeys.Composed(held, text)));
            // Backspace takes the accent back, and nothing before it.
            if (name == "Backspace") return true;
        }
        if (name.Length > 0 && _host.KeyDown(name, modifiers)) return Took(true);
        return text.Length > 0 && Took(_host.TextInput(text));
    }

    /// <summary>
    /// A dead key from a keyboard attached to the device. Android hands an app the accent and leaves
    /// combining it to the editor, so the accent waits here for the next key, which
    /// <see cref="Key"/> combines it with, as the dead keys of Windows and macOS combine before a
    /// field sees them. The same dead key again types the accent on its own; another one types the
    /// first accent and waits in its place. With nothing being edited, the key is not the app's.
    /// </summary>
    private bool Hold(int accent)
    {
        if (_host is not { HasTextFocus: true } host)
        {
            _deadAccent = 0;
            return false;
        }
        var held = _deadAccent;
        _deadAccent = held == accent ? 0 : accent;
        return held == 0 || Took(host.TextInput(AndroidKeys.Accent(held)));
    }

    /// <summary>A key the host took may have edited the field: the keyboard's mirror of it takes the
    /// edit before the keyboard's next one reads it.</summary>
    private bool Took(bool handled)
    {
        if (handled) _view?.Connection?.Refresh();
        return handled;
    }

    // ---- Touches --------------------------------------------------------------------------------

    // ONE finger owns the interaction. A second landing mid-gesture would jump the pointer between
    // two positions the host cannot tell apart, and a drag that teleports is worse than a second
    // finger that does nothing.
    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (_host is null || e is null) return false;
        var scale = Resources!.DisplayMetrics!.Density;
        var x = e.GetX() / scale;
        var y = e.GetY() / scale;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _kind = KindOf(e.GetToolType(0));
                _host.PressDown(x, y, kind: _kind);
                return true;
            case MotionEventActions.Move:
                _host.PointerMove(x, y, _kind);
                return true;
            case MotionEventActions.Up:
                _host.PressUp(x, y, _kind);
                // A tap that leaves something being edited brings back a keyboard the person put
                // away: back hides the keyboard and leaves the field being edited, as Android's own
                // fields do, and tapping the field again is how they get it back.
                if (_host.HasTextFocus && _input?.Target is TextInputTarget.Text or TextInputTarget.Keys)
                    ShowKeyboard();
                return true;
            // The system took the gesture away — a scroll view claimed it, a call arrived. Nothing
            // was decided, so nothing is reported and every surface returns to the caller's rest.
            case MotionEventActions.Cancel:
                _host.PointerCancel(_kind);
                return true;
            default:
                return base.OnTouchEvent(e);
        }
    }

    /// <summary>What the gesture in flight is, read from its tool when it lands and kept until it ends.</summary>
    private PointerKind _kind = PointerKind.Touch;

    /// <summary>
    /// A finger, a stylus, or a mouse's button: OnTouchEvent receives all three (a mouse's HOVER alone
    /// arrives as generic motion). A finger pans what it lands on and never hovers (spec S5); a mouse's
    /// press is a press, as it is on the desktop.
    /// </summary>
    private static PointerKind KindOf(MotionEventToolType tool) => tool switch
    {
        MotionEventToolType.Mouse => PointerKind.Mouse,
        MotionEventToolType.Stylus or MotionEventToolType.Eraser => PointerKind.Stylus,
        _ => PointerKind.Touch,
    };

    private ThemeMode Mode()
    {
        if (Application?.Options.Mode is { } forced) return forced;
        var night = Resources!.Configuration!.UiMode & UiMode.NightMask;
        return night == UiMode.NightYes ? ThemeMode.Dark : ThemeMode.Light;
    }
}
