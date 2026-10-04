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
/// <param name="AppKitSubrole">
/// The second word AppKit needs for a role it names with two, or null for the many it names with
/// one. A tab is the reason it exists: AppKit has no tab role, and what NSTabView's own tabs report
/// is a radio button whose subrole is <c>AXTabButton</c> — the pair VoiceOver reads as "tab". The
/// two dialogs are the others: a group whose subrole says <c>AXApplicationDialog</c> or
/// <c>AXApplicationAlertDialog</c> (#501). It is a column rather than a special case in the bridge
/// for the reason the names are: a bridge that knew which roles carry a subrole would be a second
/// table, and the first one the compiler could not see.
/// </param>
/// <param name="UIKitCheckAsSelected">
/// Whether UIKit hears a CHECK as the Selected trait rather than as the element's value. A toggle's
/// state is its value there ("1" or "0", a UISwitch's own contract); a radio is one choice of a set,
/// which UIKit says the way its segmented controls say which segment is chosen. AppKit and Android
/// read a radio's check as they read a checkbox's, AXValue and isChecked, so the difference is
/// UIKit's alone, and a row rather than a role the iOS bridge would have to know (#338).
/// </param>
public readonly record struct NativeRole(
    string AppKit, string Android, UIKitTrait UIKit, bool Activatable, bool Adjustable,
    string? AppKitSubrole = null, bool UIKitCheckAsSelected = false)
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

        // THE FIVE PRESSABLE ROLES that reached every bridge as Button until #338. AppKit's and
        // Android's words below are the ones the W3C's Core-AAM gives for the same ARIA role, which
        // is what WebKit and Chrome expose for the web half of the same component; where this table
        // departs from it, the row says why. UIKit has none of the five: Apple's own segmented
        // controls, tab bars and menus report the button trait, and what is picked or current carries
        // the Selected trait, which the bridge adds from SemanticNode.Selected and Current — and from a
        // radio's check, which its row asks for.

        // One choice of an exclusive set. AXRadioButton reads its state from AXValue, the 0/1 a check
        // carries too, and RadioButton is the one class TalkBack names "radio button" from. UIKit
        // hears the chosen one as Selected, the way a segmented control's segment says it, and not
        // as a toggle's "1" or "0", which beside the button trait would be read out as a number.
        SemanticRole.Radio =>
            new("AXRadioButton", "android.widget.RadioButton", UIKitTrait.Button,
                Activatable: true, Adjustable: false, UIKitCheckAsSelected: true),

        // One tab: a radio button whose SUBROLE says it is a tab, which is what NSTabView's own tabs
        // report and what VoiceOver reads as "tab". Android has no class for a single tab: TalkBack
        // knows the tab BAR (TabWidget) and nothing inside it, and the toolkits say "tab" through a
        // role description they ship as words — which this framework does not ship. So the class is
        // the plain View Core-AAM gives, which claims nothing, and the state says the rest.
        SemanticRole.Tab =>
            new("AXRadioButton", "android.view.View", UIKitTrait.Button,
                Activatable: true, Adjustable: false, AppKitSubrole: "AXTabButton"),

        // One action in a menu. MenuItem is not a View on Android, and that is Core-AAM's answer on
        // purpose: TalkBack names nothing from it, which is how Android's own menus sound — the
        // item's words, then the activate hint — rather than "button".
        SemanticRole.MenuItem =>
            new("AXMenuItem", "android.view.MenuItem", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // One choice in a list of them. The ONE departure from Core-AAM, whose AppKit answer is
        // AXStaticText: that is the sound this table exists to stop (a control read as a paragraph),
        // and the list WebKit hangs it from (AXList) is no node here. A choice in a popup is what
        // NSPopUpButton's own items are, and those are AXMenuItem — the same AppKit word as a menu's
        // action, the way both checks are AXCheckBox: the difference rides the state, picked or not.
        SemanticRole.Option =>
            new("AXMenuItem", "android.view.View", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // One place a navigation bar, a rail or a list leads to, in a BUTTON's words on all three,
        // and on purpose. The web says the same, `<button aria-current="page">`, and no platform has
        // a word of its own for it: UIKit says "tab" only inside a container with the tab-bar trait,
        // and Material and Compose say it through a role description they ship as words. A
        // ListItem settles it: it is a Destination exactly while it is the current row, so any other
        // word would rename the row the moment it was picked. The ROLE is still its own, which is
        // what lets a bridge or a fixture tell it apart, and where the user is rides Current.
        SemanticRole.Destination =>
            new("AXButton", "android.widget.Button", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // THE FOUR CONTAINERS AND THE TRIGGER that reached every bridge as something else until #500
        // and #501. AppKit's and Android's words are again the W3C Core-AAM's for the same ARIA role
        // (tablist, radiogroup, dialog, alertdialog), what WebKit and Chrome expose for the web half;
        // the combobox is the one departure, argued in its row. What AppKit itself calls each of
        // them was read back from an NSAccessibilityElement rather than assumed
        // (AppKitAccessibilityTests): "tab group", "radio group", "pop up button", and "group" for
        // both dialogs, whose subrole is the part that says dialog.
        //
        // The containers are pressed by nothing and adjusted by nothing on any platform. They are
        // read and then WALKED INTO, like a Group: each tab, radio and control inside is a stop of
        // its own and carries its own action. A tab strip's keyboard (one Tab stop, arrows move the
        // pick) is a keyboard's convenience, and its reader's twin is the tabs themselves, which is
        // how NSTabView, Android's RadioGroup and Flutter's tab bar are read as well.

        // The bar a set of tabs sits in. AXTabGroup is NSTabView's own role, and TabWidget is the
        // class TalkBack names "tab bar" (Role.java, ROLE_TAB_BAR). Chrome maps a tablist to a
        // plain ViewGroup instead, because TabWidget makes TalkBack drop a CollectionInfo's "2 of
        // 4"; this tree carries no position yet (#502), so the bar's own word wins today, and #502
        // is where that trade is made again. UIKit's word, the tabBar trait, is reserved for a
        // CONTAINER that is not itself an element (Apple: "return false for
        // isAccessibilityElement"), which a flat bridge does not build, so the row is a group's: no
        // trait, and the name.
        SemanticRole.TabBar =>
            new("AXTabGroup", "android.widget.TabWidget", UIKitTrait.None,
                Activatable: false, Adjustable: false),

        // A set of radios of which one is chosen. AXRadioGroup is AppKit's own role for one, and
        // RadioGroup is Android's own class, which TalkBack reads as a plain group: Role.java has no
        // radio-group role yet, and expects a radio button's ANCESTOR to be one, which a flat bridge
        // does not build either (#502). UIKit has no word for it.
        SemanticRole.RadioGroup =>
            new("AXRadioGroup", "android.widget.RadioGroup", UIKitTrait.None,
                Activatable: false, Adjustable: false),

        // A field that shows one choice and opens the list of the others. The ONE departure from
        // Core-AAM in this group, whose words for a combobox (AXComboBox, android.widget.EditText)
        // are an EDITABLE one's: NSComboBox on the Mac, and on Android a field TalkBack calls an
        // "edit box" and offers to type into. Every combobox this library builds is SELECT-ONLY, and a
        // select-only field is each platform's drop-down. On the Mac that is NSPopUpButton, whose
        // AXPopUpButton is also Core-AAM's word for a button with aria-haspopup and the pair to an
        // option's AXMenuItem, NSPopUpButton's own items (#338). On Android it is Spinner, which
        // TalkBack names "drop-down list" and which is the class Chrome gives the web half of this
        // very component (a combobox that is not a text field). UIKit has no trait for it: a menu
        // button reports the button trait, and whether the list is open rides the expanded status
        // the bridge already sets. An editable combobox, a text field with the list under it, would
        // take Core-AAM's own pair, and nothing in the library builds one.
        SemanticRole.ComboBox =>
            new("AXPopUpButton", "android.widget.Spinner", UIKitTrait.Button,
                Activatable: true, Adjustable: false),

        // A layer that holds the screen until it is answered: AXGroup with the AXApplicationDialog
        // subrole, the pair WebKit and Chrome expose for role="dialog". AppKit's own description of
        // that pair is "group", because the description a browser gives it is the browser's own; the
        // SUBROLE is what marks the element a dialog to VoiceOver and the Accessibility Inspector,
        // and a description of our own would be words this framework does not ship. TalkBack reads
        // android.app.Dialog as a dialog (Role.java, ROLE_DIALOG). UIKit has no dialog trait: a
        // modal layer says so with accessibilityViewIsModal on a CONTAINER, which a flat bridge
        // cannot set, so the row is a group's and the name is the dialog's title.
        SemanticRole.Dialog =>
            new("AXGroup", "android.app.Dialog", UIKitTrait.None,
                Activatable: false, Adjustable: false, AppKitSubrole: "AXApplicationDialog"),

        // A dialog that interrupts, the destructive confirm: Core-AAM's AXApplicationAlertDialog
        // subrole, and android.app.AlertDialog, the class TalkBack reads as an alert dialog
        // (ROLE_ALERT_DIALOG). Everything else is the dialog's row.
        SemanticRole.AlertDialog =>
            new("AXGroup", "android.app.AlertDialog", UIKitTrait.None,
                Activatable: false, Adjustable: false, AppKitSubrole: "AXApplicationAlertDialog"),
    };
#pragma warning restore CS8524
}
