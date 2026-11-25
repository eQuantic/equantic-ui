using System.Runtime.InteropServices;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Native.Shell.Apple;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The NSAccessibility face of the semantics tree. Photon draws its own pixels, so AppKit sees ONE
/// view and nothing in it — this bridge answers the view's <c>accessibilityChildren</c> with real
/// elements built from <see cref="PhotonHost.Semantics"/>: role, label, value, frame, and a press
/// action that runs the same handler a tap does. VoiceOver navigates the window through these.
/// <para>
/// ONE ELEMENT PER NODE, for as long as the node lives (#1111). The tree is read again on every query, and
/// each node's element is found by the id of the node's position, like every other cross-frame target,
/// and told what the node says now. An element is released only when its node leaves the tree. The
/// elements were built anew on every query, and the batch before released: VoiceOver reads the children
/// at its own moments, so the element its cursor stood on died under it (an assistive app reading it got
/// <c>kAXErrorInvalidUIElement</c>), and its navigation stopped at the title bar's buttons, the only
/// elements that lived. Flutter's macOS embedder keeps one native node per semantics node the same way.
/// </para>
/// <para>
/// The view is the GROUP the window lists, and its elements' parent: it was the parent each element
/// named, but not an element itself, so AppKit listed the elements as the window's children while each
/// named a parent nothing listed. The element of the node with the keyboard is the view's focused element,
/// and VoiceOver is told when the keyboard moves (<see cref="FocusMoved"/>).
/// </para>
/// <para>
/// Statics, like the content view's own callbacks: one window per process on this head.
/// </para>
/// </summary>
internal static class PhotonAccessibility
{
    /// <summary>The frame's semantics, provided by the window once the host exists.</summary>
    internal static Func<IReadOnlyList<SemanticNode>>? Source { get; set; }

    /// <summary>Runs the control a semantic node names by its id — the AX press action. Answers
    /// whether anything ran.</summary>
    internal static Func<int, bool>? OnActivate { get; set; }

    /// <summary>VoiceOver's adjust gesture on a slider-role element: +1 up, −1 down.</summary>
    internal static Func<int, int, bool>? OnAdjust { get; set; }

    private delegate bool PerformPress(IntPtr self, IntPtr selector);

    /// <summary>Where the keyboard is, as the id of the node that holds it, or null: the host's.</summary>
    internal static Func<int?>? FocusedId { get; set; }

    /// <summary>The id of the semantic node each native element stands for.</summary>
    private static readonly Dictionary<IntPtr, int> Ids = new();

    /// <summary>Each living node's element, by the node's id, with what it said when last told.</summary>
    private static readonly Dictionary<int, Kept> Elements = new();

    /// <summary>The view the elements belong to: a new view starts with none.</summary>
    private static IntPtr _view;

    /// <summary>The node VoiceOver was last told holds the keyboard, or null.</summary>
    private static int? _reportedFocus;

    /// <summary>Whether the node with the keyboard had no element when VoiceOver was told, so the view was
    /// named instead: the read of the tree that gives the node one tells VoiceOver again.</summary>
    private static bool _focusAwaitsElement;

    /// <summary>A node's element and what the node said the last time it was told.</summary>
    private sealed class Kept(IntPtr element, SemanticNode said)
    {
        public IntPtr Element { get; } = element;
        public SemanticNode Said { get; set; } = said;
    }
    private static PerformPress? _performPress;   // kept alive for the runtime's sake
    private static PerformPress? _performIncrement;
    private static PerformPress? _performDecrement;
    private static IntPtr _elementClass;
    private static IntPtr _children;              // the NSMutableArray we own until the next build

    /// <summary>"EQAXElement": an NSAccessibilityElement that can be PRESSED — the one method the
    /// stock class lacks. Everything else (role, label, frame, parent) is stock setters.</summary>
    private static IntPtr ElementClass()
    {
        if (_elementClass != IntPtr.Zero) return _elementClass;
        // Through AppKit.Class, which loads the framework first, like every class this shell reaches
        // for. objc_getClass alone answers nil wherever no window has loaded AppKit yet, and a pair
        // allocated over nil is a ROOT class that answers no message at all: the suite's first build
        // of these elements outside a running app hung there rather than failing (#338).
        _elementClass = objc_allocateClassPair(AppKit.Class("NSAccessibilityElement"), "EQAXElement", 0);
        _performPress = OnPerformPress;
        class_addMethod(_elementClass, sel_registerName("accessibilityPerformPress"),
            Marshal.GetFunctionPointerForDelegate(_performPress), "B@:");
        // The adjust pair — VoiceOver's swipe up/down on anything with the slider role.
        _performIncrement = OnPerformIncrement;
        class_addMethod(_elementClass, sel_registerName("accessibilityPerformIncrement"),
            Marshal.GetFunctionPointerForDelegate(_performIncrement), "B@:");
        _performDecrement = OnPerformDecrement;
        class_addMethod(_elementClass, sel_registerName("accessibilityPerformDecrement"),
            Marshal.GetFunctionPointerForDelegate(_performDecrement), "B@:");
        objc_registerClassPair(_elementClass);
        return _elementClass;
    }

    private static bool OnPerformPress(IntPtr self, IntPtr selector) =>
        Ids.TryGetValue(self, out var id) && OnActivate?.Invoke(id) == true;

    private static bool OnPerformIncrement(IntPtr self, IntPtr selector) =>
        Ids.TryGetValue(self, out var id) && OnAdjust?.Invoke(id, 1) == true;

    private static bool OnPerformDecrement(IntPtr self, IntPtr selector) =>
        Ids.TryGetValue(self, out var id) && OnAdjust?.Invoke(id, -1) == true;

    /// <summary>
    /// Builds the view's accessibility children from the CURRENT frame's semantics: each node's element,
    /// the one it had at the last build when it had one, told what the node says now.
    /// </summary>
    internal static IntPtr BuildChildren(IntPtr view)
    {
        if (Source is null) return IntPtr.Zero;
        var semantics = Source();
        // The view is NOT flipped: AppKit's y grows up from the bottom, the frame's grows down
        // from the top, and an element's parent-space frame has to say so.
        var viewHeight = SendRect(view, Sel("bounds")).Height;
        if (view != _view)
        {
            Forget();
            _view = view;
        }

        var previous = _children;
        _children = Send(Send(AppKit.Class("NSMutableArray"), Sel("alloc")), Sel("init"));
        // Once per build rather than per value: AppKit.Class loads the framework before it looks, and
        // a screen asks for a number up to four times a node, on every query VoiceOver makes.
        var number = AppKit.Class("NSNumber");
        var living = new HashSet<int>();

        foreach (var node in semantics)
        {
            // Two nodes never share a position; if they ever did, the second is told on an element of its
            // own for this build, and the first keeps the one VoiceOver may be on.
            if (!living.Add(node.Id))
            {
                var stray = Made(node);
                Tell(stray, node, null, view, viewHeight, number);
                SendVoid(_children, Sel("addObject:"), stray);
                SendVoid(stray, Sel("release"));
                continue;
            }
            IntPtr element;
            SemanticNode? said = null;
            if (Elements.TryGetValue(node.Id, out var kept) && SameKind(kept.Said, node))
            {
                element = kept.Element;
                said = kept.Said;
                kept.Said = node;
            }
            else
            {
                // A position that now holds another kind of node is another element: VoiceOver should meet
                // it as new rather than as the old one with a changed role.
                if (kept is not null) Release(node.Id, kept);
                element = Made(node);
                Elements[node.Id] = new Kept(element, node);
                Ids[element] = node.Id;
            }
            Tell(element, node, said, view, viewHeight, number);
            SendVoid(_children, Sel("addObject:"), element);
        }

        foreach (var gone in Elements.Where(entry => !living.Contains(entry.Key)).ToList())
            Release(gone.Key, gone.Value);
        // The array before this one is released only now that every element still living is in the new one.
        if (previous != IntPtr.Zero) SendVoid(previous, Sel("release"));
        // The node with the keyboard has its element now: VoiceOver, told the view, is told the element. Only
        // while that node still holds the keyboard: once it moved, the next frame tells VoiceOver where to.
        if (_focusAwaitsElement && _reportedFocus is { } waiting)
        {
            if (FocusedId?.Invoke() != waiting)
                _focusAwaitsElement = false;
            else if (Elements.TryGetValue(waiting, out var arrived))
            {
                _focusAwaitsElement = false;
                AppKitNotifications.Post(arrived.Element, "NSAccessibilityFocusedUIElementChangedNotification");
            }
        }
        return _children;
    }

    /// <summary>
    /// The view's focused element: the element of the node that holds the keyboard, or the view itself
    /// when no node does. AppKit asks the first responder, which is the view, and the window's focused
    /// element was the window: VoiceOver never said where a press or Tab had moved the keyboard (#1111).
    /// </summary>
    internal static IntPtr FocusedElement(IntPtr view)
    {
        if (FocusedId?.Invoke() is not { } id) return view;
        // Read the tree first, every time: the element kept from an earlier read may say what its node said
        // then, or stand for a kind of node the position no longer holds.
        BuildChildren(view);
        return Elements.TryGetValue(id, out var kept) ? kept.Element : view;
    }

    /// <summary>
    /// Tells VoiceOver the keyboard moved, once a frame drew, when it did: AppKit's focused-element
    /// notification, posted on the element that holds it now. Only once something has read the tree, so a
    /// person with no assistive app pays nothing for it.
    /// </summary>
    internal static void FocusMoved(IntPtr view)
    {
        if (_view == IntPtr.Zero || view != _view) return;
        var id = FocusedId?.Invoke();
        if (id == _reportedFocus) return;
        _reportedFocus = id;
        _focusAwaitsElement = false;
        var target = FocusedElement(view);
        // A keyboard on a node the tree does not hold yet is told ONCE, on the view, and told again when a
        // read of the tree gives the node its element (BuildChildren). Recorded as unreported instead, it was
        // told again on every frame, each time reading the whole tree, for as long as the node had none.
        _focusAwaitsElement = id is not null && target == view;
        AppKitNotifications.Post(target, "NSAccessibilityFocusedUIElementChangedNotification");
    }

    /// <summary>A node's element, made with its role: a role AppKit names with two words gets its subrole.</summary>
    private static IntPtr Made(SemanticNode node)
    {
        var element = Send(Send(ElementClass(), Sel("alloc")), Sel("init"));
        var native = NativeRole.Of(node.Role);
        // A heading is AppKit's AXHeading whatever the vocabulary's role, which takes the level beside it.
        SendVoid(element, Sel("setAccessibilityRole:"), NSString(node.HeadingLevel > 0 ? "AXHeading" : native.AppKit));
        // The second word, for a role AppKit names with two: a tab is AXRadioButton until its
        // subrole says AXTabButton, which is what VoiceOver reads as "tab" (#338).
        if (native.AppKitSubrole is { } subrole)
            SendVoid(element, Sel("setAccessibilitySubrole:"), NSString(subrole));
        return element;
    }

    /// <summary>
    /// Whether a node is the same kind of node an element was made for: its role, whether it is a heading,
    /// which is a role of its own on the Mac, and whether it has the attributes an element cannot drop once
    /// told (an open-or-closed state, a cell's place, a grid's size, an outline row's level). Told NO, AppKit
    /// keeps an expanded state, and VoiceOver would read "collapsed" on a button that no longer opens
    /// anything, so a node that gains or loses one is a new element.
    /// </summary>
    private static bool SameKind(SemanticNode before, SemanticNode now) =>
        before.Role == now.Role
        && before.HeadingLevel > 0 == now.HeadingLevel > 0
        && before.Expanded is not null == now.Expanded is not null
        && before.Place is not null == now.Place is not null
        && before.Extent is not null == now.Extent is not null
        && before.TreePlace is not null == now.TreePlace is not null;

    /// <summary>Tells <paramref name="element"/> what <paramref name="node"/> says now, and takes away what it
    /// said before (<paramref name="said"/>) and no longer does.</summary>
    private static void Tell(IntPtr element, SemanticNode node, SemanticNode? said, IntPtr view, double viewHeight,
        IntPtr number)
    {
        SendVoid(element, Sel("setAccessibilityLabel:"), NSString(node.Label));
        // What it says now, and nothing it said before: a value it no longer has is taken away (nil),
        // as are bounds a range no longer gives.
        SendVoid(element, Sel("setAccessibilityValue:"), node.Value is { } value ? NSString(value) : IntPtr.Zero);
        if (node.Range is null && said?.Range is not null)
        {
            SendVoid(element, Sel("setAccessibilityMinValue:"), IntPtr.Zero);
            SendVoid(element, Sel("setAccessibilityMaxValue:"), IntPtr.Zero);
        }
        // THE NUMBERS BEHIND THE WORDS (#243). VoiceOver reads AXValue as the announcement and
        // uses AXMinValue/AXMaxValue to place it — "45%", or "45 of 100" on a control whose
        // range is not a ratio. Before the semantic snapshot carried numbers, this bridge had
        // only the formatted string and could offer neither bound, so every progress bar and
        // slider was announced against AppKit's assumptions rather than its own range.
        //
        // AFTER the string value above, deliberately: a caller's WORDS win over the number
        // (that is what aria-valuetext means on the web and what `Spoken` decides here), so the
        // bounds are added beside the announcement rather than over it.
        if (node.Range is { } range)
        {
            SendVoid(element, Sel("setAccessibilityMinValue:"),
                Send(number, Sel("numberWithDouble:"), (double)range.Min));
            SendVoid(element, Sel("setAccessibilityMaxValue:"),
                Send(number, Sel("numberWithDouble:"), (double)range.Max));
            // The value as a NUMBER only when nothing spoke for it — otherwise the words above
            // are the announcement and replacing them with "0.45" is the loss this exists to
            // prevent.
            //
            // AGAINST THE NUMBER, not against null. `SemanticsVisitor` sends `Spoken`, which
            // falls back to `range.Number` when a caller gave no words — so `Value` is never
            // null for a node that has a range, and a null check here was dead code that left
            // AXValue an NSString of "0.45" on every ordinary bar. Comparing is exact rather
            // than a heuristic: both strings come from the same formatter, so they are equal
            // precisely when nothing replaced the number.
            if (node.Value is null || node.Value == range.Number)
                SendVoid(element, Sel("setAccessibilityValue:"),
                    Send(number, Sel("numberWithDouble:"), (double)range.Now));
        }
        // A check's value is a NUMBER (0/1/2 — off/on/mixed): AXCheckBox's own contract, and
        // what VoiceOver reads as "checked"/"unchecked"/"mixed" without the label saying it.
        if (node.Checked is { } check)
            SendVoid(element, Sel("setAccessibilityValue:"),
                Send(number, Sel("numberWithLong:"), (long)check));
        // Disclosure state — VoiceOver reads "expanded"/"collapsed" after the name. Told only to a node
        // that has one: a button that never expands must not read "collapsed", and a node that stops having
        // one is a new element (SameKind).
        if (node.Expanded is { } isOpen)
            SendVoid(element, Sel("setAccessibilityExpanded:"), isOpen);
        // The destination the user is ON, or the one of a set that is PICKED — AppKit has one
        // word for both, read after the name.
        if (node.Current || node.Selected == true)
            SendVoid(element, Sel("setAccessibilitySelected:"), true);
        else if (said is { } before && (before.Current || before.Selected == true))
            SendVoid(element, Sel("setAccessibilitySelected:"), false);
        // WHERE A CELL STANDS in its table or grid, and how large the grid is (#1081): AppKit's own
        // AXRowIndexRange and AXColumnIndexRange on a cell, AXRowCount and AXColumnCount on the table.
        // The tree is flat, so VoiceOver's table commands have no rows to walk yet (#1105); the element
        // still says the truth about itself.
        if (node.Place is { } place)
        {
            SendVoid(element, Sel("setAccessibilityRowIndexRange:"), new NSRange((nuint)place.Row, (nuint)place.Rows));
            SendVoid(element, Sel("setAccessibilityColumnIndexRange:"), new NSRange((nuint)place.Column, (nuint)place.Columns));
        }
        if (node.Extent is { } extent)
        {
            SendVoid(element, Sel("setAccessibilityRowCount:"), (ulong)extent.Rows);
            SendVoid(element, Sel("setAccessibilityColumnCount:"), (ulong)extent.Columns);
        }
        // AppKit is the one platform of the three that takes the LEVEL as well as the fact —
        // AXHeading plus AXDisclosureLevel — so VoiceOver on the Mac can announce "heading
        // level 2" the way it does in a browser.
        if (node.HeadingLevel > 0)
        {
            // An INTEGER register: ObjC has no SendVoid for a long, and a long converts implicitly to
            // a double, whose overload put the level in a float register AppKit never reads, so every
            // heading reached VoiceOver with whatever the integer register held.
            SendVoid(element, Sel("setAccessibilityDisclosureLevel:"), (ulong)node.HeadingLevel);
        }
        // An outline row's LEVEL (#1529): AXDisclosureLevel counts from zero where the vocabulary's
        // TreePlace counts from one, as UI Automation's and ARIA's do. The integer register, for
        // the reason the heading's level above gives. Its index among its siblings has no AppKit
        // attribute of its own; VoiceOver counts an outline's rows itself.
        if (node.TreePlace is { } treePlace)
            SendVoid(element, Sel("setAccessibilityDisclosureLevel:"), (ulong)Math.Max(0, treePlace.Level - 1));
        SendVoid(element, Sel("setAccessibilityEnabled:"), !node.Disabled);
        SendVoid(element, Sel("setAccessibilityParent:"), view);
        // The id, visible in Accessibility Inspector — the same identity every press, focus and
        // scroll uses, its position's, which is what makes an AX report debuggable.
        SendVoid(element, Sel("setAccessibilityIdentifier:"),
            NSString(node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        SendVoid(element, Sel("setAccessibilityFrameInParentSpace:"), new CGRect(
            node.Bounds.Left,
            viewHeight - node.Bounds.Top - node.Bounds.Height,
            node.Bounds.Width,
            node.Bounds.Height));
    }

    /// <summary>Lets go of a node's element: its node left the tree, or holds another kind of node now.</summary>
    private static void Release(int id, Kept kept)
    {
        Elements.Remove(id);
        Ids.Remove(kept.Element);
        // A focused node's new element is news, though the keyboard did not move.
        if (_reportedFocus == id) _reportedFocus = null;
        SendVoid(kept.Element, Sel("release"));
    }

    /// <summary>Lets go of every element: the view they belonged to is not the one asking.</summary>
    private static void Forget()
    {
        foreach (var entry in Elements.ToList()) Release(entry.Key, entry.Value);
        _reportedFocus = null;
        _focusAwaitsElement = false;
    }
}
