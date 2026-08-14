using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using eQuantic.UI.Native.Components;
using AndroidRect = Android.Graphics.Rect;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The TalkBack face of the semantics tree — the Android twin of the iOS and macOS bridges, over the
/// SAME target-neutral tree (<see cref="PhotonHost.Semantics"/>). Photon draws its own pixels, so the
/// platform sees ONE SurfaceView and nothing in it; this answers for a VIRTUAL hierarchy underneath
/// it, which is how Android has always let a custom-drawn view describe what it painted.
/// <para>
/// A virtual child is identified by its INDEX in the tree, and the tree itself is refreshed from the
/// render loop rather than from the queries: assistive tech asks for one node at a time, and walking
/// the whole layout per question would cost a full pass for every word spoken. What the queries see
/// is therefore a snapshot — which is also what keeps ids stable while TalkBack reads down a screen.
/// </para>
/// <para>
/// Identity for ACTING is still the PATH, like every other cross-frame target: the index says which
/// node was asked about, the path says which control that is in the frame happening now.
/// </para>
/// </summary>
internal sealed class PhotonAccessibility : AccessibilityNodeProvider
{
    /// <summary>The host view itself, in the platform's own numbering.</summary>
    private const int Host = HostViewId;

    private readonly View _view;
    private readonly AccessibilityManager? _manager;
    private readonly Func<IReadOnlyList<SemanticNode>> _semantics;
    private readonly Func<string, bool> _activate;
    private readonly Func<string, int, bool> _adjust;

    private IReadOnlyList<SemanticNode> _tree = [];
    private int _focused = Host;
    private int _hovered = Host;

    internal PhotonAccessibility(View view,
        Func<IReadOnlyList<SemanticNode>> semantics,
        Func<string, bool> activate,
        Func<string, int, bool> adjust)
    {
        _view = view;
        _manager = view.Context?.GetSystemService(global::Android.Content.Context.AccessibilityService)
            as AccessibilityManager;
        _semantics = semantics;
        _activate = activate;
        _adjust = adjust;
    }

    /// <summary>
    /// Called once a frame was presented: when the screen's semantics actually changed, the system
    /// is told the subtree is stale and re-reads it. The walk happens only while an assistive
    /// technology is listening — a phone with TalkBack off pays nothing for any of this.
    /// </summary>
    internal void FrameRendered()
    {
        if (_manager?.IsEnabled != true) return;
        var tree = _semantics();
        if (Same(_tree, tree)) return;
        _tree = tree;
        _view.SendAccessibilityEvent(EventTypes.WindowContentChanged);
    }

    public override AccessibilityNodeInfo? CreateAccessibilityNodeInfo(int virtualViewId)
    {
        if (virtualViewId == Host)
        {
            var host = AccessibilityNodeInfo.Obtain(_view);
            if (host is null) return null;
            _view.OnInitializeAccessibilityNodeInfo(host);
            // The children are declared even before a frame filled the snapshot: an empty screen
            // and an unasked one look the same from here, and the render loop fills it either way.
            for (var index = 0; index < Tree().Count; index++) host.AddChild(_view, index);
            return host;
        }

        var tree = Tree();
        if (virtualViewId < 0 || virtualViewId >= tree.Count) return null;
        var node = tree[virtualViewId];
        var info = AccessibilityNodeInfo.Obtain(_view, virtualViewId);
        if (info is null) return null;

        info.PackageName = _view.Context?.PackageName;
        info.ClassName = ClassOf(node.Role);
        info.SetParent(_view);
        info.SetBoundsInScreen(ScreenBounds(node));
        info.Enabled = !node.Disabled;
        info.VisibleToUser = true;

        // A field's TEXT is what it holds and its HINT is what it is for; everything else says what
        // it is through the description. Setting both on one node would silence the text — the
        // description wins, and a screen reader would read the label instead of the answer.
        if (node.Value is { } value)
        {
            info.Text = value;
            info.HintText = node.Label;
        }
        else
        {
            info.ContentDescription = node.Label;
        }

        // The check's state, in the platform's own words — announced in the USER's language, which
        // is the whole reason this framework ships neither "checked" nor "on". Android has no third
        // state: a mixed check is announced without one rather than claiming to be off.
        info.Checkable = node.Checked is SemanticCheck.On or SemanticCheck.Off;
        info.Checked = node.Checked == SemanticCheck.On;

        info.Focusable = true;
        info.AccessibilityFocused = _focused == virtualViewId;
        info.AddAction(_focused == virtualViewId
            ? global::Android.Views.Accessibility.Action.ClearAccessibilityFocus
            : global::Android.Views.Accessibility.Action.AccessibilityFocus);

        // Only what can actually be ACTED on says so. Marking every node clickable — which a
        // blanket rule does, static text included — makes a screen reader offer "double tap to
        // activate" over a paragraph and do nothing when the user takes it up.
        if (!node.Disabled && Activatable(node.Role))
        {
            info.Clickable = true;
            info.AddAction(global::Android.Views.Accessibility.Action.Click);
            // Disclosure is an ACTION here, not a flag: offering expand is how a node says it is
            // closed on Android, and offering collapse is how it says it is open. Performing either
            // is the same toggle a tap does.
            if (node.Expanded is { } open)
                info.AddAction(open
                    ? global::Android.Views.Accessibility.Action.Collapse
                    : global::Android.Views.Accessibility.Action.Expand);
            // An Adjustable takes the swipe up and down, which is where the platform routes them
            // for a node that has no numeric range to set.
            if (node.Role == SemanticRole.Slider)
            {
                info.AddAction(global::Android.Views.Accessibility.Action.ScrollForward);
                info.AddAction(global::Android.Views.Accessibility.Action.ScrollBackward);
            }
        }

        return info;
    }

    public override bool PerformAction(int virtualViewId, global::Android.Views.Accessibility.Action action,
        Bundle? arguments)
    {
        var tree = Tree();
        if (virtualViewId < 0 || virtualViewId >= tree.Count) return false;
        var path = tree[virtualViewId].Path;

        switch (action)
        {
            case global::Android.Views.Accessibility.Action.AccessibilityFocus:
                if (_focused == virtualViewId) return false;
                _focused = virtualViewId;
                SendVirtual(virtualViewId, EventTypes.ViewAccessibilityFocused);
                return true;

            case global::Android.Views.Accessibility.Action.ClearAccessibilityFocus:
                if (_focused != virtualViewId) return false;
                _focused = Host;
                SendVirtual(virtualViewId, EventTypes.ViewAccessibilityFocusCleared);
                return true;

            // The double tap, and the two gestures that stand for it on a disclosure.
            case global::Android.Views.Accessibility.Action.Click:
            case global::Android.Views.Accessibility.Action.Expand:
            case global::Android.Views.Accessibility.Action.Collapse:
                return _activate(path);

            case global::Android.Views.Accessibility.Action.ScrollForward:
                return _adjust(path, +1);

            case global::Android.Views.Accessibility.Action.ScrollBackward:
                return _adjust(path, -1);

            default:
                return false;
        }
    }

    /// <summary>
    /// Explore by touch: a finger moving over the screen is a QUESTION about what is under it, and
    /// the answer is a hover event naming the virtual child. Without this the surface would only be
    /// reachable by swiping through it in order — the whole point of a touch screen, lost.
    /// </summary>
    internal bool DispatchHover(MotionEvent hover)
    {
        if (_manager?.IsTouchExplorationEnabled != true) return false;
        switch (hover.ActionMasked)
        {
            case MotionEventActions.HoverEnter:
            case MotionEventActions.HoverMove:
                Hover(HitTest(hover.GetX(), hover.GetY()));
                return _hovered != Host;
            case MotionEventActions.HoverExit:
                Hover(Host);
                return true;
            default:
                return false;
        }
    }

    private void Hover(int virtualViewId)
    {
        if (_hovered == virtualViewId) return;
        var left = _hovered;
        _hovered = virtualViewId;
        // Enter first, then exit — the order the platform's own helper uses, so a reader never has
        // a moment where nothing is under the finger.
        if (virtualViewId != Host) SendVirtual(virtualViewId, EventTypes.ViewHoverEnter);
        if (left != Host) SendVirtual(left, EventTypes.ViewHoverExit);
    }

    /// <summary>The topmost node under a point in the view's own pixels — paint order, so the last
    /// one drawn (a dialog over a page) wins, exactly as it does for a finger.</summary>
    private int HitTest(float x, float y)
    {
        var tree = Tree();
        var density = Density();
        for (var index = tree.Count - 1; index >= 0; index--)
        {
            var bounds = tree[index].Bounds;
            if (x >= bounds.Left * density && x < bounds.Right * density
                && y >= bounds.Top * density && y < bounds.Bottom * density)
                return index;
        }
        return Host;
    }

    private void SendVirtual(int virtualViewId, EventTypes type)
    {
        if (_manager?.IsEnabled != true || _view.Parent is not { } parent) return;
        var tree = Tree();
        if (virtualViewId < 0 || virtualViewId >= tree.Count) return;
        var node = tree[virtualViewId];

        var accessibilityEvent = AccessibilityEvent.Obtain(type);
        if (accessibilityEvent is null) return;
        accessibilityEvent.PackageName = _view.Context?.PackageName;
        accessibilityEvent.ClassName = ClassOf(node.Role);
        accessibilityEvent.SetSource(_view, virtualViewId);
        parent.RequestSendAccessibilityEvent(_view, accessibilityEvent);
    }

    /// <summary>The snapshot the queries answer from, filled on first use when no frame has yet.</summary>
    private IReadOnlyList<SemanticNode> Tree() => _tree.Count > 0 ? _tree : _tree = _semantics();

    private float Density() => _view.Resources?.DisplayMetrics?.Density ?? 1f;

    private AndroidRect ScreenBounds(SemanticNode node)
    {
        var density = Density();
        var origin = new int[2];
        _view.GetLocationOnScreen(origin);
        return new AndroidRect(
            origin[0] + (int)(node.Bounds.Left * density),
            origin[1] + (int)(node.Bounds.Top * density),
            origin[0] + (int)(node.Bounds.Right * density),
            origin[1] + (int)(node.Bounds.Bottom * density));
    }

    /// <summary>Whether a tap on this does anything — the roles that carry a handler. A label and
    /// an image are read, not pressed.</summary>
    private static bool Activatable(SemanticRole role) => role is
        SemanticRole.Button or SemanticRole.Link or SemanticRole.Checkbox or SemanticRole.Switch
        or SemanticRole.TextField or SemanticRole.CodeField;

    /// <summary>
    /// What the platform calls this kind of control. The class name is Android's role: TalkBack
    /// reads "button", "checkbox", "switch" from it, in the user's language.
    /// <para>
    /// Fence: Android has no link class. A link is announced by what it DOES, and what it does here
    /// is exactly what a button does.
    /// </para>
    /// </summary>
    private static string ClassOf(SemanticRole role) => role switch
    {
        SemanticRole.Button => "android.widget.Button",
        SemanticRole.Link => "android.widget.Button",
        SemanticRole.Image => "android.widget.ImageView",
        SemanticRole.Slider => "android.widget.SeekBar",
        SemanticRole.Checkbox => "android.widget.CheckBox",
        SemanticRole.Switch => "android.widget.Switch",
        SemanticRole.TextField or SemanticRole.CodeField => "android.widget.EditText",
        _ => "android.widget.TextView",
    };

    /// <summary>Two trees are the same when every node is — the record struct's own equality, which
    /// covers role, path, bounds, label, value and state.</summary>
    private static bool Same(IReadOnlyList<SemanticNode> a, IReadOnlyList<SemanticNode> b)
    {
        if (a.Count != b.Count) return false;
        for (var index = 0; index < a.Count; index++)
            if (!a[index].Equals(b[index])) return false;
        return true;
    }
}
