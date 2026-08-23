using System.Runtime.InteropServices;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Shell.Apple;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The NSAccessibility face of the semantics tree. Photon draws its own pixels, so AppKit sees ONE
/// view and nothing in it — this bridge answers the view's <c>accessibilityChildren</c> with real
/// elements built from <see cref="PhotonHost.Semantics"/>: role, label, value, frame, and a press
/// action that runs the same handler a tap does. VoiceOver navigates the window through these.
/// <para>
/// Elements are rebuilt on every query — the tree is a per-frame artifact, and assistive tech asks
/// rarely and at its own moments — and identified by PATH like every other cross-frame target. The
/// previous batch is released when a new one is built; AppKit copies what it sends over the wire.
/// </para>
/// <para>
/// Statics, like the content view's own callbacks: one window per process on this head.
/// </para>
/// </summary>
internal static class PhotonAccessibility
{
    /// <summary>The frame's semantics, provided by the window once the host exists.</summary>
    internal static Func<IReadOnlyList<SemanticNode>>? Source { get; set; }

    /// <summary>Runs the control at a path — the AX press action. Answers whether anything ran.</summary>
    internal static Func<string, bool>? OnActivate { get; set; }

    /// <summary>VoiceOver's adjust gesture on a slider-role element: +1 up, −1 down.</summary>
    internal static Func<string, int, bool>? OnAdjust { get; set; }

    private delegate bool PerformPress(IntPtr self, IntPtr selector);

    private static readonly Dictionary<IntPtr, string> Paths = new();
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
        _elementClass = objc_allocateClassPair(objc_getClass("NSAccessibilityElement"), "EQAXElement", 0);
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
        Paths.TryGetValue(self, out var path) && OnActivate?.Invoke(path) == true;

    private static bool OnPerformIncrement(IntPtr self, IntPtr selector) =>
        Paths.TryGetValue(self, out var path) && OnAdjust?.Invoke(path, 1) == true;

    private static bool OnPerformDecrement(IntPtr self, IntPtr selector) =>
        Paths.TryGetValue(self, out var path) && OnAdjust?.Invoke(path, -1) == true;

    /// <summary>Builds the view's accessibility children from the CURRENT frame's semantics.</summary>
    internal static IntPtr BuildChildren(IntPtr view)
    {
        if (Source is null) return IntPtr.Zero;
        var semantics = Source();
        // The view is NOT flipped: AppKit's y grows up from the bottom, the frame's grows down
        // from the top, and an element's parent-space frame has to say so.
        var viewHeight = SendRect(view, Sel("bounds")).Height;

        if (_children != IntPtr.Zero) SendVoid(_children, Sel("release"));
        Paths.Clear();
        _children = Send(Send(objc_getClass("NSMutableArray"), Sel("alloc")), Sel("init"));

        foreach (var node in semantics)
        {
            var element = Send(Send(ElementClass(), Sel("alloc")), Sel("init"));
            SendVoid(element, Sel("setAccessibilityRole:"), NSString(RoleName(node.Role)));
            SendVoid(element, Sel("setAccessibilityLabel:"), NSString(node.Label));
            if (node.Value is { } value)
                SendVoid(element, Sel("setAccessibilityValue:"), NSString(value));
            // A check's value is a NUMBER (0/1/2 — off/on/mixed): AXCheckBox's own contract, and
            // what VoiceOver reads as "checked"/"unchecked"/"mixed" without the label saying it.
            if (node.Checked is { } check)
                SendVoid(element, Sel("setAccessibilityValue:"),
                    Send(objc_getClass("NSNumber"), Sel("numberWithLong:"), (long)check));
            // Disclosure state — VoiceOver reads "expanded"/"collapsed" after the name.
            if (node.Expanded is { } isOpen)
                SendVoid(element, Sel("setAccessibilityExpanded:"), isOpen);
            // The destination the user is ON, or the one of a set that is PICKED — AppKit has one
            // word for both, read after the name.
            if (node.Current || node.Selected == true)
                SendVoid(element, Sel("setAccessibilitySelected:"), true);
            // AppKit is the one platform of the three that takes the LEVEL as well as the fact —
            // AXHeading plus AXDisclosureLevel — so VoiceOver on the Mac can announce "heading
            // level 2" the way it does in a browser.
            if (node.HeadingLevel > 0)
            {
                SendVoid(element, Sel("setAccessibilityRole:"), NSString("AXHeading"));
                SendVoid(element, Sel("setAccessibilityDisclosureLevel:"), (long)node.HeadingLevel);
            }
            SendVoid(element, Sel("setAccessibilityEnabled:"), !node.Disabled);
            SendVoid(element, Sel("setAccessibilityParent:"), view);
            // The path, visible in Accessibility Inspector — the same identity every press,
            // focus and scroll uses, which is what makes an AX report debuggable.
            SendVoid(element, Sel("setAccessibilityIdentifier:"), NSString(node.Path));
            SendVoid(element, Sel("setAccessibilityFrameInParentSpace:"), new CGRect(
                node.Bounds.Left,
                viewHeight - node.Bounds.Top - node.Bounds.Height,
                node.Bounds.Width,
                node.Bounds.Height));
            SendVoid(_children, Sel("addObject:"), element);
            Paths[element] = node.Path;
            // The array holds the element now; the pointer stays valid for the Paths map.
            SendVoid(element, Sel("release"));
        }
        return _children;
    }

    /// <summary>The AX role STRINGS, written literally: the exported constants hold exactly these
    /// values, and dlsym-ing AppKit to read them back buys nothing.</summary>
    private static string RoleName(SemanticRole role) => role switch
    {
        SemanticRole.Button => "AXButton",
        SemanticRole.Link => "AXLink",
        SemanticRole.TextField => "AXTextField",
        SemanticRole.CodeField => "AXTextArea",
        SemanticRole.Slider => "AXSlider",
        SemanticRole.Image => "AXImage",
        // Both checks are AXCheckBox to AppKit — macOS has no switch role; the DISTINCTION lives
        // in SemanticRole for the mobile bridges, which do (UISwitch trait, Switch class).
        SemanticRole.Checkbox or SemanticRole.Switch => "AXCheckBox",
        _ => "AXStaticText",
    };
}
