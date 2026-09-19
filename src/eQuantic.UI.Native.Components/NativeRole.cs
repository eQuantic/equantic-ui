using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What ONE <see cref="SemanticRole"/> IS to each native accessibility API — every answer a role owes
/// the three bridges, in the one place that is neither macOS, nor iOS, nor Android.
///
/// <para>
/// IT USED TO LIVE IN THE SHELLS, as three switches that each ended in a catch-all:
/// <c>_ =&gt; "AXStaticText"</c>, <c>_ =&gt; "android.widget.TextView"</c>,
/// <c>_ =&gt; UIAccessibilityTrait.StaticText</c>. The default arm and the
/// <see cref="SemanticRole.StaticText"/> ROW were spelled the same, which is what made the hole
/// invisible: a role nobody had mapped was announced as a paragraph, and every suite stayed green
/// because no test project can reference the iOS and Android shells at all (#249). Deleting the
/// <c>Group =&gt; "AXGroup"</c> arm cost nothing; <see cref="SemanticRole.GridCell"/> — a calendar
/// day — had been reaching VoiceOver and TalkBack as static text on all three since the day it was
/// added.
/// </para>
///
/// <para>
/// The catch-all is GONE rather than moved. <see cref="Of"/> is a switch over the enum with no
/// default arm, so appending a role stops the BUILD and names it (CS8509), in an assembly that
/// compiles on every OS instead of only in CI's macOS jobs — the compiler asking the question at the
/// door, which is what <c>IVisualNodeVisitor</c> does for the node vocabulary. The one thing waived
/// is CS8524, the UNNAMED value: only a cast can make one and the semantics walk never does.
/// </para>
/// </summary>
/// <param name="AppKit">
/// The NSAccessibility role, written literally — the exported constants hold exactly these strings,
/// and dlsym-ing AppKit to read them back buys nothing.
/// </param>
/// <param name="Android">
/// The <c>AccessibilityNodeInfo</c> class name. The class name IS Android's role: TalkBack reads
/// "button", "checkbox", "switch" from it, in the user's own language.
/// </param>
/// <param name="UIKit">The trait VoiceOver announces the element by. <see cref="UIKitTrait"/> says
/// why the trait is named here rather than valued.</param>
/// <param name="Activatable">
/// Whether a tap on this DOES something — the roles that carry a handler. Android has to declare
/// <c>ACTION_CLICK</c> up front, so it asks the role; the Apple bridges instead offer the press
/// action to everything and let the host answer whether anything ran.
/// <para>
/// It rides here rather than staying a predicate in the Android shell for the reason the names do:
/// a role appended over there would have silently answered <c>false</c>, which is the same defect
/// one file along — a calendar day that TalkBack cannot activate reads exactly like one that is not
/// meant to be tapped.
/// </para>
/// </param>
/// <param name="Adjustable">
/// Whether the platform's ADJUST gesture — TalkBack's swipe up and down — steps this. A separate
/// question from <see cref="Activatable"/> and not a narrower one: a slider takes the swipes and no
/// tap, which is why asking it as part of the click gate answered nothing at all. The Android bridge
/// did exactly that, and its two <c>ACTION_SCROLL_*</c> lines sat inside
/// <c>if (Activatable(role))</c> with the predicate saying false for the only role that reaches
/// them — so an Adjustable was unreachable to TalkBack while macOS wired increment/decrement and
/// iOS carried the trait.
/// </param>
public readonly record struct NativeRole(
    string AppKit, string Android, UIKitTrait UIKit, bool Activatable, bool Adjustable)
{
    /// <summary>
    /// The row for a role. Every arm is written out, including
    /// <see cref="SemanticRole.StaticText"/>'s: it is a role like any other here, and the moment it
    /// is a fallback as well, a role nobody mapped looks exactly like one somebody did.
    /// </summary>
    // CS8524 is the UNNAMED enum value — `(SemanticRole)99`, which only a cast produces and this
    // tree never casts. CS8509, a NAMED role with no row, stays an ERROR: it is the entire point of
    // this file, so the waiver is this narrow on purpose and not a project-wide property.
#pragma warning disable CS8524
    public static NativeRole Of(SemanticRole role) => role switch
    {
        SemanticRole.StaticText =>
            new("AXStaticText", "android.widget.TextView", UIKitTrait.StaticText,
                Activatable: false, Adjustable: false),

        SemanticRole.Button =>
            new("AXButton", "android.widget.Button", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // Android has no link class. A link is announced by what it DOES, and what it does here is
        // exactly what a button does.
        SemanticRole.Link =>
            new("AXLink", "android.widget.Button", UIKitTrait.Link,
                Activatable: true, Adjustable: false),

        // A text field carries no UIKit trait — UITextField carries none either; what identifies it
        // is that it has a value and takes the keyboard.
        SemanticRole.TextField =>
            new("AXTextField", "android.widget.EditText", UIKitTrait.None,
                Activatable: true, Adjustable: false),

        SemanticRole.CodeField =>
            new("AXTextArea", "android.widget.EditText", UIKitTrait.None,
                Activatable: true, Adjustable: false),

        // The one role that takes the ADJUST gesture and no tap.
        SemanticRole.Slider =>
            new("AXSlider", "android.widget.SeekBar", UIKitTrait.Adjustable,
                Activatable: false, Adjustable: true),

        SemanticRole.Image =>
            new("AXImage", "android.widget.ImageView", UIKitTrait.Image,
                Activatable: false, Adjustable: false),

        // Both checks are AXCheckBox to AppKit — macOS has no switch role; the DISTINCTION lives in
        // SemanticRole for the mobile bridges, which do (UISwitch trait, Switch class). UIKit has
        // neither role: a UISwitch itself reports the button trait and puts its state in the value.
        SemanticRole.Checkbox =>
            new("AXCheckBox", "android.widget.CheckBox", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        SemanticRole.Switch =>
            new("AXCheckBox", "android.widget.Switch", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // One cell of a two-dimensional composite, and the row this table was written to find: it
        // had no arm anywhere, so a calendar day announced as a paragraph you cannot press. AXCell
        // is AppKit's own word for it. Android expresses grid MEMBERSHIP through CollectionItemInfo
        // rather than the class name — which this bridge does not set yet — so the class says the
        // honest half, that the cell is pressed; UIKit has no cell trait either and Apple's own
        // calendars report a button whose picked-ness is the Selected trait, which the bridge
        // already adds from SemanticNode.Selected.
        SemanticRole.GridCell =>
            new("AXCell", "android.widget.Button", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // Something REPORTING how far along it is, which is why it is not a Slider on any of the
        // three: AXProgressIndicator is not AXSlider, a ProgressBar is not a SeekBar, and a bridge
        // that called it a slider would offer gestures that do nothing.
        SemanticRole.ProgressIndicator =>
            new("AXProgressIndicator", "android.widget.ProgressBar", UIKitTrait.UpdatesFrequently,
                Activatable: false, Adjustable: false),

        // The container role (#187): a stop that is READ and then WALKED INTO, which is the whole
        // distinction from every leaf above — announcing one of those consumes what is inside it.
        // Carrying no UIKit trait is the mapping and not a gap: UIKit expresses a group by being an
        // accessibility CONTAINER whose children are the elements, and a trait here would make it a
        // stop that swallows its own.
        SemanticRole.Group =>
            new("AXGroup", "android.view.ViewGroup", UIKitTrait.None,
                Activatable: false, Adjustable: false),
    };
#pragma warning restore CS8524
}
