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
    /// <summary>An Adjustable: one stop, arrows adjust (slider, segmented control, radio group).</summary>
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
    int HeadingLevel = 0);
