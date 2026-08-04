using System.Runtime.InteropServices;
using eQuantic.UI.Native.Shell.Apple;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The window's content view, defined at RUNTIME so it can override one method: the one AppKit
/// calls while it is resizing.
/// <para>
/// A live resize never returns to the app's own loop — AppKit runs its own inside <c>sendEvent:</c>
/// until the button comes up — and it hands out no time there either: a run-loop observer and a
/// common-modes timer were both tried in the real window and neither fired once. What AppKit DOES
/// do is send <c>setFrameSize:</c> to the content view on every step of the drag. Overriding it is
/// the only place the frame can be re-measured while the edge is still moving.
/// </para>
/// <para>
/// One class, one method, one callback into managed code. The callback is a static field rather
/// than an instance variable because there is exactly one window per process on this head, and a
/// GCHandle for a singleton is bookkeeping that buys nothing.
/// </para>
/// </summary>
internal static class PhotonContentView
{
    private delegate void SetFrameSize(IntPtr self, IntPtr selector, CGSize size);
    private delegate bool ReturnsBool(IntPtr self, IntPtr selector);
    private delegate void KeyDown(IntPtr self, IntPtr selector, IntPtr @event);
    private delegate bool PerformKeyEquivalent(IntPtr self, IntPtr selector, IntPtr @event);

    private static SetFrameSize? _override;      // kept alive for the runtime's sake
    private static ReturnsBool? _acceptsFirstResponder;
    private static KeyDown? _keyDown;
    private static PerformKeyEquivalent? _performKeyEquivalent;
    private static IntPtr _superSetFrameSize;
    private static IntPtr _superKeyDown;
    private static Action<float, float>? _onResized;

    /// <summary>What runs for a key press: the key's name in DOM spelling, its modifiers, and the
    /// text the platform composed (empty for a key that produces none). Answering false lets the
    /// event go back to AppKit, which is what makes ⌘Q still quit.</summary>
    internal static Func<string, Primitives.KeyModifiers, string, bool>? OnKey { get; set; }

    /// <summary>What runs after AppKit has applied a new size — the frame drawn from there is
    /// measured against it. Set once the host exists; the view is created before it does.</summary>
    internal static Action<float, float>? OnResized
    {
        get => _onResized;
        set => _onResized = value;
    }

    /// <summary>The class, created once.</summary>
    internal static IntPtr Register()
    {
        if (_cls != IntPtr.Zero) return _cls;

        var selector = sel_registerName("setFrameSize:");
        var super = objc_getClass("NSView");
        _superSetFrameSize = method_getImplementation(class_getInstanceMethod(super, selector));

        _cls = objc_allocateClassPair(super, "EQPhotonContentView", 0);
        _override = OnSetFrameSize;
        // v@:{CGSize=dd} — returns void, takes self, the selector, and a struct of two doubles.
        class_addMethod(_cls, selector, Marshal.GetFunctionPointerForDelegate(_override), "v@:{CGSize=dd}");

        // A view that does not say YES here is never asked for a key press at all — this one line is
        // the difference between a window with a keyboard and a window without one.
        _acceptsFirstResponder = (_, _) => true;
        class_addMethod(_cls, sel_registerName("acceptsFirstResponder"),
            Marshal.GetFunctionPointerForDelegate(_acceptsFirstResponder), "B@:");

        _superKeyDown = method_getImplementation(
            class_getInstanceMethod(super, sel_registerName("keyDown:")));
        _keyDown = OnKeyDown;
        class_addMethod(_cls, sel_registerName("keyDown:"),
            Marshal.GetFunctionPointerForDelegate(_keyDown), "v@:@");

        // Escape's SECOND route. AppKit can deliver it either as an ordinary key press or, when
        // something in the window claims cancel, through the key-equivalent pass that runs first.
        // `keyDown:` above covers the former; this covers the latter, so a dialog closes whichever
        // way the frameworks route it that day.
        _performKeyEquivalent = OnPerformKeyEquivalent;
        class_addMethod(_cls, sel_registerName("performKeyEquivalent:"),
            Marshal.GetFunctionPointerForDelegate(_performKeyEquivalent), "B@:@");

        objc_registerClassPair(_cls);
        return _cls;
    }

    /// <summary>
    /// The key-equivalent pass, which runs BEFORE any key press is delivered. ONLY Escape is
    /// claimed here. Everything else is left alone, because this pass is also how ⌘Q, ⌘W and the
    /// whole menu bar work: claiming a key here takes it away from them.
    /// <para>
    /// A real keyboard closes a dialog with Escape — confirmed in the window. WHICH of the two
    /// routes carried it is not known and does not matter: a synthesised Escape never reaches this
    /// process at all (raw event instrumentation showed a letter arriving and Escape never), so the
    /// two are indistinguishable from the outside, and both are cheap.
    /// </para>
    /// </summary>
    private static bool OnPerformKeyEquivalent(IntPtr self, IntPtr selector, IntPtr @event)
    {
        if (SendUShort(@event, Sel("keyCode")) != EscapeKeyCode) return false;
        return OnKey?.Invoke("Escape", Primitives.KeyModifiers.None, "") == true;
    }

    private const ushort EscapeKeyCode = 53;

    private static void OnKeyDown(IntPtr self, IntPtr selector, IntPtr @event)
    {
        var flags = SendULong(@event, Sel("modifierFlags"));
        var modifiers = Primitives.KeyModifiers.None;
        if ((flags & (1UL << 17)) != 0) modifiers |= Primitives.KeyModifiers.Shift;
        if ((flags & (1UL << 18)) != 0) modifiers |= Primitives.KeyModifiers.Control;
        if ((flags & (1UL << 19)) != 0) modifiers |= Primitives.KeyModifiers.Alt;
        if ((flags & (1UL << 20)) != 0) modifiers |= Primitives.KeyModifiers.Command;

        var text = FromNSString(Send(@event, Sel("characters"))) ?? "";
        var name = NameOf(SendUShort(@event, Sel("keyCode")), text);

        // A key the app claims stops here. Anything else goes back to AppKit — the menu bar's
        // shortcuts, ⌘Q, the window chrome — because swallowing every key would break the app's
        // own frame around it.
        if (OnKey?.Invoke(name, modifiers, TypedText(text, modifiers)) == true) return;
        Marshal.GetDelegateForFunctionPointer<KeyDown>(_superKeyDown)(self, selector, @event);
    }

    /// <summary>
    /// The key's DOM name, taken from the KEY CODE rather than the character it produced. A layout
    /// decides what a key types; it does not decide which key is Return — and a French keyboard's
    /// arrows have to work as arrows.
    /// </summary>
    private static string NameOf(ushort keyCode, string characters) => keyCode switch
    {
        48 => "Tab",
        36 or 76 => "Enter",
        51 => "Backspace",
        117 => "Delete",
        53 => "Escape",
        49 => " ",
        123 => "ArrowLeft",
        124 => "ArrowRight",
        125 => "ArrowDown",
        126 => "ArrowUp",
        115 => "Home",
        119 => "End",
        // Everything else IS what it typed — that is the DOM's rule too, and it is what a chord
        // like ⌘K is written against.
        _ => characters.Length > 0 ? characters : "",
    };

    /// <summary>
    /// The text a key press should INSERT, which is not the same as the text it carries. A command
    /// chord carries a character too (⌘A is "a"), and inserting it would type into the field every
    /// time someone selected all. Control characters are the keys handled by name above.
    /// </summary>
    private static string TypedText(string characters, Primitives.KeyModifiers modifiers)
    {
        if (characters.Length == 0) return "";
        if ((modifiers & (Primitives.KeyModifiers.Command | Primitives.KeyModifiers.Control)) != 0) return "";
        foreach (var character in characters)
            if (char.IsControl(character) || character >= 0xF700) return "";  // arrows and F-keys live up there
        return characters;
    }

    private static IntPtr _cls;

    private static void OnSetFrameSize(IntPtr self, IntPtr selector, CGSize size)
    {
        // Let AppKit do its part first: the view has to BE the new size before anything measures
        // against it.
        var super = Marshal.GetDelegateForFunctionPointer<SetFrameSize>(_superSetFrameSize);
        super(self, selector, size);
        _onResized?.Invoke((float)size.Width, (float)size.Height);
    }
}
