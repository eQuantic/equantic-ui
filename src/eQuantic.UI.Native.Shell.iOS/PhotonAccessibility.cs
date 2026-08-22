using CoreGraphics;
using eQuantic.UI.Native.Components;
using Foundation;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The UIAccessibility face of the semantics tree — the iOS twin of the macOS bridge, reading the
/// SAME target-neutral tree (<see cref="PhotonHost.Semantics"/>). Photon draws its own pixels, so
/// UIKit sees ONE view with nothing inside it; this makes that view an accessibility CONTAINER
/// whose elements are built from the tree, in reading order, and whose gestures reach the very
/// handlers a finger does.
/// <para>
/// Elements are rebuilt when the tree CHANGES rather than on every query: VoiceOver asks for the
/// element list while it walks it, and a fresh batch mid-walk would move the cursor under the
/// user's finger. Identity is the PATH, like every other cross-frame target (press, focus,
/// scroll) — the tree is a per-frame artifact and a reference into it is stale by the time
/// assistive tech acts on it.
/// </para>
/// </summary>
internal sealed class PhotonAccessibility
{
    private readonly UIView _view;
    private readonly Func<IReadOnlyList<SemanticNode>> _semantics;
    private readonly Func<string, bool> _activate;
    private readonly Func<string, int, bool> _adjust;

    private IReadOnlyList<SemanticNode> _tree = [];
    private NSObject[] _elements = [];

    internal PhotonAccessibility(UIView view,
        Func<IReadOnlyList<SemanticNode>> semantics,
        Func<string, bool> activate,
        Func<string, int, bool> adjust)
    {
        _view = view;
        _semantics = semantics;
        _activate = activate;
        _adjust = adjust;
    }

    /// <summary>The view's accessibility children — what VoiceOver navigates the screen through.</summary>
    internal NSObject[] Elements()
    {
        Refresh();
        return _elements;
    }

    /// <summary>
    /// Called once a frame was presented: when the screen's semantics actually changed, VoiceOver
    /// is told, otherwise it keeps announcing the layout it read on arrival. The check runs only
    /// while an assistive technology is on — a phone with VoiceOver off pays nothing for this.
    /// <para>
    /// The announcement is a LAYOUT change, not a screen change: a Photon frame is a repaint of the
    /// same screen, and the screen-change notification would throw the cursor back to the top on
    /// every state flip.
    /// </para>
    /// </summary>
    internal void FrameRendered()
    {
        if (!UIAccessibility.IsVoiceOverRunning) return;
        if (Refresh())
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, null);
    }

    /// <summary>Rebuilds the elements when the tree differs. Answers whether it did.</summary>
    private bool Refresh()
    {
        var tree = _semantics();
        if (Same(_tree, tree)) return false;
        _tree = tree;
        _elements = Build(tree);
        return true;
    }

    private NSObject[] Build(IReadOnlyList<SemanticNode> tree)
    {
        var elements = new NSObject[tree.Count];
        for (var index = 0; index < tree.Count; index++)
        {
            var node = tree[index];
            elements[index] = new PhotonAccessibilityElement(_view, node, _activate, _adjust)
            {
                AccessibilityLabel = node.Label,
                AccessibilityValue = ValueOf(node),
                // The binding types this as the raw NSUInteger the selector takes; the enum is
                // still where the meaning is written.
                AccessibilityTraits = (ulong)TraitsOf(node),
                // The tree speaks the view's coordinates and UIKit wants the screen's. Converting
                // through the view is also what keeps the frame honest when the window is not the
                // whole screen — a split view, a Slide Over, a Stage Manager pane.
                AccessibilityFrame = UIAccessibility.ConvertFrameToScreenCoordinates(
                    new CGRect(node.Bounds.Left, node.Bounds.Top, node.Bounds.Width, node.Bounds.Height),
                    _view),
                // Visible in the Accessibility Inspector: the same identity every press, focus and
                // scroll uses, which is what makes an accessibility report debuggable.
                AccessibilityIdentifier = node.Path,
            };
        }
        return elements;
    }

    /// <summary>
    /// What VoiceOver says the element IS. A check does not get the Selected trait: its state is
    /// its VALUE here (see <see cref="ValueOf"/>), and setting both would announce the state twice.
    /// </summary>
    private static UIAccessibilityTrait TraitsOf(SemanticNode node)
    {
        var traits = node.Role switch
        {
            SemanticRole.Button => UIAccessibilityTrait.Button,
            SemanticRole.Link => UIAccessibilityTrait.Link,
            SemanticRole.Image => UIAccessibilityTrait.Image,
            SemanticRole.Slider => UIAccessibilityTrait.Adjustable,
            // UIKit has no checkbox role and no switch role: a UISwitch itself reports the button
            // trait and puts its state in the value, which is exactly what these do.
            SemanticRole.Checkbox or SemanticRole.Switch => UIAccessibilityTrait.Button,
            // A text field is an element with no trait at all — UITextField carries none either;
            // what identifies it is that it has a value and takes the keyboard.
            SemanticRole.TextField or SemanticRole.CodeField => UIAccessibilityTrait.None,
            _ => UIAccessibilityTrait.StaticText,
        };
        if (node.Disabled) traits |= UIAccessibilityTrait.NotEnabled;
        // The destination the user is ON, or the one of a set that is PICKED (a tab, an option, a
        // calendar day) — UIKit has one trait for both. A check never gets it (its state is its
        // VALUE), which is why the flag is separate from Checked rather than another value of it.
        if (node.Current || node.Selected == true) traits |= UIAccessibilityTrait.Selected;
        return traits;
    }

    /// <summary>
    /// What VoiceOver reads after the name. A field's value is its text; a check's is "1" or "0" —
    /// UIKit's OWN contract for a toggle, which is why the announcement comes out as "on"/"off" in
    /// the user's language without this framework shipping either word.
    /// <para>
    /// Fence: iOS has no third state. A mixed checkbox announces its name and role and stops,
    /// rather than claiming to be on or off.
    /// </para>
    /// </summary>
    private static string? ValueOf(SemanticNode node) => node.Value ?? node.Checked switch
    {
        SemanticCheck.On => "1",
        SemanticCheck.Off => "0",
        _ => null,
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

/// <summary>
/// One element VoiceOver can land on — and ACT on: the double-tap runs the same handler a tap does,
/// and the swipe up/down on an Adjustable drives the same OnAdjust the desktop's arrow keys do.
/// Both resolve by path against the CURRENT frame, so an element read a second ago still acts on
/// the control that is there now.
/// </summary>
internal sealed class PhotonAccessibilityElement : UIAccessibilityElement
{
    private readonly string _path;
    private readonly bool? _expanded;
    private readonly Func<string, bool> _activate;
    private readonly Func<string, int, bool> _adjust;

    internal PhotonAccessibilityElement(NSObject container, SemanticNode node,
        Func<string, bool> activate, Func<string, int, bool> adjust)
        : base(container)
    {
        _path = node.Path;
        _expanded = node.Expanded;
        _activate = activate;
        _adjust = adjust;
    }

    /// <summary>
    /// Whether this element OPENS something, and whether it currently is open — iOS 17's own
    /// vocabulary, the twin of the web's aria-expanded and of AppKit's accessibilityExpanded. A
    /// control that discloses nothing answers Unsupported, which is a different answer from
    /// "closed" and the reason the node carries a nullable.
    /// <para>
    /// An older system simply never asks: the selector is the whole feature, so there is nothing
    /// to guard by version.
    /// </para>
    /// </summary>
    [Export("accessibilityExpandedStatus")]
    public UIAccessibilityExpandedStatus ExpandedStatus() => _expanded switch
    {
        true => UIAccessibilityExpandedStatus.Expanded,
        false => UIAccessibilityExpandedStatus.Collapsed,
        null => UIAccessibilityExpandedStatus.Unsupported,
    };

    // The three gestures arrive as selectors on an informal protocol — UIKit has no managed
    // virtual to override here, so they are exported by name, which is what the runtime dispatches
    // on anyway.
    [Export("accessibilityActivate")]
    public bool Activate() => _activate(_path);

    [Export("accessibilityIncrement")]
    public void Increment() => _adjust(_path, +1);

    [Export("accessibilityDecrement")]
    public void Decrement() => _adjust(_path, -1);
}
