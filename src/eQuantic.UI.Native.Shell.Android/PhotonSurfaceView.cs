using Android.Content;
using Android.Views;
using Android.Views.Accessibility;
using Android.Views.InputMethods;
using eQuantic.UI.Native.Components;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The surface the engine presents into, and the ONE view the platform sees. Everything drawn on it
/// is described through a virtual hierarchy (<see cref="PhotonAccessibility"/>) rather than through
/// child views — the same shape a chart or a map view uses, and the reason a Photon screen can be
/// read at all. It is also the view the keyboard types into: the platform asks the FOCUSED view for
/// an input connection, and this one answers with <see cref="PhotonInputConnection"/> while
/// something is being edited.
/// </summary>
internal sealed class PhotonSurfaceView : SurfaceView
{
    internal PhotonSurfaceView(Context context) : base(context)
    {
        // Without this the platform decides a bare SurfaceView is not worth describing — and since
        // it is the ONLY view in the window, the window ends up with no accessible root at all:
        // uiautomator answers "null root node" and a screen reader finds nothing to read. Saying
        // YES is what puts the virtual hierarchy on the map.
        ImportantForAccessibility = ImportantForAccessibility.Yes;
        // The keyboard asks the focused view for its input connection, so the surface takes focus,
        // from a touch too. Focusable in touch mode makes it focusable as well.
        FocusableInTouchMode = true;
        // A focused view out of touch mode (a key pressed on an attached keyboard) gets the system's
        // focus highlight drawn over it, and this view is the whole window: the app went a shade
        // darker until it was launched again. What has focus inside the tree is Photon's to show.
        DefaultFocusHighlightEnabled = false;
    }

    /// <summary>The semantics bridge, armed once the host exists. Null before that — a view with no
    /// host has nothing to describe.</summary>
    internal PhotonAccessibility? Semantics { get; set; }

    /// <summary>The host and its keyboard session, armed with the host. Null before that: nothing
    /// can be edited yet, so the view asks for no keyboard.</summary>
    internal (PhotonHost Host, TextInputSession Session, InputMethodManager Keyboard)? Editing { get; set; }

    /// <summary>The connection the keyboard was last handed, or null when nothing is being edited.
    /// Only this one edits: one the keyboard has moved on from goes nowhere.</summary>
    internal PhotonInputConnection? Connection { get; private set; }

    /// <summary>Called when the system's insets change: the keyboard rising or falling, a bar
    /// shown or hidden. Nothing about the host has to change for that, so nothing else would draw
    /// the frame that lays out around them.</summary>
    internal System.Action? InsetsChanged { get; set; }

    public override AccessibilityNodeProvider? AccessibilityNodeProvider => Semantics;

    /// <summary>A finger exploring the screen asks what is under it; the bridge answers by naming a
    /// virtual child. Unhandled hovers fall through, so nothing changes when TalkBack is off.</summary>
    protected override bool DispatchHoverEvent(MotionEvent? e) =>
        (e is not null && Semantics?.DispatchHover(e) == true) || base.DispatchHoverEvent(e);

    /// <summary>Whether a keyboard belongs here now: while a field, a code surface or a sheet holds
    /// the caret.</summary>
    public override bool OnCheckIsTextEditor() =>
        Editing?.Session.Target is TextInputTarget.Text or TextInputTarget.Keys;

    /// <summary>
    /// The keyboard starts on this view: a connection for what is being edited, described for the
    /// keyboard, or none when nothing is. The platform asks again whenever the shell restarts it,
    /// which it does each time the edit begins, moves or ends.
    /// </summary>
    public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
    {
        Connection = null;
        if (outAttrs is null || Editing is not { } editing || editing.Session.Target == TextInputTarget.None)
            return null;
        Connection = new PhotonInputConnection(this, editing.Host, editing.Session, editing.Keyboard);
        Connection.Describe(outAttrs);
        return Connection;
    }

    public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
    {
        InsetsChanged?.Invoke();
        return base.OnApplyWindowInsets(insets);
    }
}
