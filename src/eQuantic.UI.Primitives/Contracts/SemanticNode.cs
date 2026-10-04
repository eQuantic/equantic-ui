namespace eQuantic.UI.Primitives;

/// <summary>What a node IS to assistive tech — the platform bridges map these onto their own
/// vocabularies (NSAccessibility roles, UIAccessibilityTraits, AccessibilityNodeInfo classes).</summary>
public enum SemanticRole : byte
{
    StaticText,
    Button,
    Link,
    TextField,
    /// <summary>A multiline editable code surface — a text area to a screen reader.</summary>
    CodeField,
    /// <summary>An Adjustable that moves along a range: one stop, and the adjust gesture steps it.
    /// A tab strip and a radio group are Adjustables too, and each has a role of its own
    /// (<see cref="TabBar"/>, <see cref="RadioGroup"/>), because what they hold is a set of
    /// choices a reader walks, not a position.</summary>
    Slider,
    Image,
    /// <summary>A two-or-three-state check: the STATE rides <see cref="SemanticNode.Checked"/>,
    /// never the label — a name that changes when the state does reads as a different control.</summary>
    Checkbox,
    /// <summary>An on/off toggle that acts immediately. Split from Checkbox because the mobile
    /// bridges speak different words for them (UISwitch trait, Switch class), even though macOS
    /// maps both onto AXCheckBox.</summary>
    Switch,


    /// <summary>One cell of a two-dimensional composite — a calendar day (design system C15).
    /// Its picked-ness rides <see cref="SemanticNode.Selected"/>, the same field a tab and a
    /// listbox option use, and never the label.</summary>
    GridCell,

    /// <summary>
    /// Something REPORTING how far along it is — a progress bar, determinate or not. Split from
    /// Slider because the platforms split them: AXProgressIndicator is not AXSlider, and a SeekBar
    /// is not a ProgressBar. The difference a user feels is that this one cannot be moved, and a
    /// bridge that called it a slider would offer gestures that do nothing.
    /// <para>Its value rides <see cref="SemanticNode.Value"/>, and is NULL when indeterminate —
    /// which is a state to announce, not a value that went missing.</para>
    /// <para>
    /// APPENDED, and every future role goes at the end too. These are <c>byte</c> values a consumer
    /// compiles INTO its own IL, so inserting one renumbers every role after it: an app built
    /// against the previous package kept emitting 9 for <c>GridCell</c> and the shells' switches
    /// read 9 as this. The enum's order is an ABI, not a table of contents.
    /// </para>
    /// </summary>
    ProgressIndicator,

    /// <summary>
    /// A LABELLED GROUP THAT KEEPS WALKING — the first container role, and the plain one. A reader
    /// stops on it, says its name, and then goes on into the rows or the fields it holds.
    /// <para>
    /// It exists because every role before it is a LEAF: announcing consumes the subtree, which is
    /// right for a button whose inner text is its name and catastrophic for a navigable grid, whose
    /// every row would vanish. <see cref="Navigable"/> and <see cref="Overlay"/> were both declining
    /// on Photon for exactly that reason while the web honoured them (#187), and a live region could
    /// not have existed at all without it. The containers appended since say WHAT they group —
    /// <see cref="TabBar"/>, <see cref="RadioGroup"/>, and the <see cref="Dialog"/> or
    /// <see cref="AlertDialog"/> an open modal overlay is now (#501) — and keep walking the same
    /// way.
    /// </para>
    /// <para>
    /// The platforms all have it: AXGroup on macOS, <c>android.view.ViewGroup</c> with screen-reader
    /// focusability off, and an accessibility container on UIKit. Appended, by the rule above.
    /// </para>
    /// </summary>
    Group,

    /// <summary>
    /// One choice of an exclusive set. Its state is CHECKED-ness and rides
    /// <see cref="SemanticNode.Checked"/>, as the web's <c>aria-checked</c> says it and as AppKit's
    /// AXValue and Android's <c>isChecked</c> read it, which is not the picked-ness of a tab. UIKit
    /// has no checked radio and says the chosen one with the Selected trait; <c>NativeRole</c>
    /// carries that difference.
    /// <para>
    /// This and the four after it arrived together (#338): until then a radio, a tab, a menu item,
    /// a list option and a navigation destination all reached the bridges as <see cref="Button"/>, because the semantics walk
    /// knew three pressable roles and sent the rest to a catch-all. Appended, by the rule above.
    /// </para>
    /// </summary>
    Radio,

    /// <summary>One tab of a set. Its picked-ness rides <see cref="SemanticNode.Selected"/>, as the
    /// web's <c>aria-selected</c> says it.</summary>
    Tab,

    /// <summary>One action in a menu: it is run, never picked, so it carries no state.</summary>
    MenuItem,

    /// <summary>One choice in a list of them, a select's or a time picker's. Its picked-ness rides
    /// <see cref="SemanticNode.Selected"/>, as the web's <c>aria-selected</c> says it.</summary>
    Option,

    /// <summary>
    /// One place a navigation bar, a rail or a list leads to. Where the user IS rides
    /// <see cref="SemanticNode.Current"/>, as the web's <c>aria-current</c> says it, and the
    /// platforms name it as they name a button — see <c>NativeRole</c> for why.
    /// </summary>
    Destination,

    /// <summary>
    /// The bar a set of tabs sits in — a <c>Tabs</c> strip. A CONTAINER, read and then walked into:
    /// each <see cref="Tab"/> inside it is a stop of its own and says whether it is the picked one,
    /// as the web's <c>tablist</c> of <c>tab</c>s does. Flutter's word for it is the same,
    /// <c>SemanticsRole.tabBar</c>.
    /// <para>
    /// This and the four after it arrived together (#500, #501). Until then a tab strip and a radio
    /// group reached every bridge as one unnamed slider whose tabs and radios were never read, a
    /// combobox's trigger as a button, and a dialog as a plain <see cref="Group"/>. Appended, by the
    /// rule above.
    /// </para>
    /// </summary>
    TabBar,

    /// <summary>A set of <see cref="Radio"/>s of which one is chosen — a <c>RadioGroup</c> or a
    /// <c>SegmentedControl</c>. A container like <see cref="TabBar"/>: each radio inside it is a stop
    /// of its own and carries its check, as the web's <c>radiogroup</c> of <c>radio</c>s does.</summary>
    RadioGroup,

    /// <summary>
    /// A field that shows one choice and opens the list of the others — a <c>Select</c>'s or a
    /// <c>TimePicker</c>'s trigger, the pressable a listbox panel hangs from. Whether the list is open
    /// rides <see cref="SemanticNode.Expanded"/>, as the web's <c>aria-expanded</c> says it, and the
    /// choices in it are <see cref="Option"/>s. Every one the library builds is SELECT-ONLY, which is
    /// why the platforms hear it as their own drop-down rather than as an editable combo box; see
    /// <c>NativeRole</c>.
    /// </summary>
    ComboBox,

    /// <summary>
    /// A layer that holds the screen until it is answered: an open modal <c>Overlay</c>, or a panel
    /// the focus moves into, such as a date picker's calendar. A container whose name is the layer's
    /// label, and what it holds follows it, as the web's <c>role="dialog"</c> does.
    /// </summary>
    Dialog,

    /// <summary>A <see cref="Dialog"/> that interrupts, such as the destructive confirm, as the web's
    /// <c>role="alertdialog"</c> does. Its own role because the platforms have their own words for
    /// it.</summary>
    AlertDialog,
}

/// <summary>A check's state, in ARIA's own three words. Mixed exists for checkboxes and nothing
/// else — the "select all" over a partly-selected set.</summary>
public enum SemanticCheck : byte
{
    Off = 0,
    On = 1,
    Mixed = 2,
}

/// <summary>
/// One element a screen reader can land on, in READING ORDER (tree order — the same order Tab
/// walks). Identified by PATH, like every press, focus and scroll target: the tree is rebuilt every
/// frame, so a reference is stale by the time assistive tech acts on it, and the path is what stays.
/// <para>
/// HOST ONLY, for now, and the fence is the thing that will come off. A node like this is what a
/// WALK produces for a platform bridge to read; no page constructs one, because on the web the
/// realizer writes ARIA inline instead. The day the web realizer produces these — the second half
/// of FLUTTER-PARITY's `SemanticsNode` row — this attribute goes and the runtime owes a twin.
/// </para>
/// <para>
/// <see cref="SemanticRole"/> and <see cref="SemanticCheck"/> are NOT fenced: they are enums, they
/// cross as string literals, and a component naming one costs nothing.
/// </para>
/// </summary>
/// <param name="Role">What KIND of thing this is, which decides how a bridge announces it.</param>
/// <param name="Path">Where it sits in the tree — the identity a focus or an action names.</param>
/// <param name="Bounds">Where it landed, for the bridge's own hit map.</param>
/// <param name="Label">What assistive tech announces, already resolved to the app's culture.</param>
/// <param name="Value">Its current value, for roles that carry one.</param>
/// <param name="Disabled">Whether it refuses interaction, announced as such rather than hidden.</param>
/// <param name="Checked">
/// The two-or-three-state answer to a question the control asks — a checkbox, a switch, a radio.
/// Null where the role has no such question, so a plain button never announces a state it does
/// not have.
/// </param>
/// <param name="Expanded">
/// Whether a disclosure is open, for the roles that can be. Null where nothing expands — which is
/// what tells a bridge to offer no expand/collapse action at all.
/// </param>
/// <param name="Current">
/// This is the destination the user is ON — the web's <c>aria-current="page"</c>. Each bridge
/// reports it with the nearest thing its platform has, which on both mobiles is the SELECTED trait;
/// a bar whose active stop is only a tint colour is a bar a screen-reader user walks blind.
/// </param>
/// <param name="Selected">
/// This one of a set is PICKED — a tab, a listbox option, a calendar day (the web's
/// <c>aria-selected</c>). Distinct from <see cref="Checked"/>, which is a two-or-three-state
/// answer to a question, and from <see cref="Current"/>, which says where you ARE; every
/// bridge reports it with its platform's selected state. Null where the role has no notion of
/// being picked, so a plain button never announces "not selected".
/// <para>Design system §10 REQUEST, opened by C15: before this, a Tab and an Option reached
/// the native tree as plain Buttons and their selection was paint only.</para>
/// </param>
/// <param name="HeadingLevel">
/// Where this text sits in the document's OUTLINE — 1 to 6, 0 for anything that is not a
/// heading (design system A9). Every platform has the same navigation built on it: VoiceOver's
/// rotor and TalkBack's heading swipe jump between them, which is how a screen-reader user
/// skims a long page instead of reading it end to end.
/// <para>A TRAIT rather than a role, because a heading is still static text — the bridges add
/// the platform's header trait on top of what they already report.</para>
/// </param>
/// <param name="Range">
/// The NUMBERS behind <paramref name="Value"/>, for the roles that have them — a progress
/// indicator, a slider. Null for everything else, and null for an INDETERMINATE progress bar,
/// which is a state rather than a missing number.
/// <para>
/// It exists because <paramref name="Value"/> is a <c>string</c>: the numbers were formatted away
/// before any bridge saw them, so NO bridge could populate a platform range — Android's
/// <c>AccessibilityNodeInfo.RangeInfo</c>, or <c>AXValue</c>/<c>AXMinValue</c>/<c>AXMaxValue</c> on
/// macOS. The web was the only target that got the real trio, because it is the only one that reads
/// the NODE rather than this snapshot (#243).
/// </para>
/// <para>
/// APPENDED, like every parameter before it, and for the reason the roles are: a consumer compiles
/// against this shape. It is <see cref="ServerOnlyAttribute"/> so the blast radius is internal, but
/// inserting rather than appending would still renumber nothing and break every positional call
/// site at once — which is what the seven-argument repairs this repository removed were trying to
/// avoid, the expensive way.
/// </para>
/// </param>
/// <param name="Live">
/// Set when this node is a LIVE REGION: the platform watches it and announces a change inside it
/// wherever the user happens to be, without moving focus. Null for everything else, which is almost
/// everything — a region that interrupts when nothing happened is worse than one that never speaks.
/// <para>Only <see cref="SemanticRole.Group"/> carries it, because announcing a change means
/// re-reading what is INSIDE, and every other role here consumes its subtree.</para>
/// <para>It is the DATA an announcement needs, and on Photon nothing posts one yet: the semantics
/// tree is a per-frame snapshot with nothing to compare against, so the frame-to-frame diff is its
/// own slice. The fence is written where the behaviour is — <c>SemanticsVisitor.Visit(LiveRegion)</c>.</para>
/// </param>
[ServerOnly]
public readonly record struct SemanticNode(
    SemanticRole Role,
    string Path,
    Rect Bounds,
    string Label,
    string? Value,
    bool Disabled,
    SemanticCheck? Checked = null,
    bool? Expanded = null,
    bool Current = false,
    bool? Selected = null,
    int HeadingLevel = 0,
    LiveRegionUrgency? Live = null,
    RangeValue? Range = null);
