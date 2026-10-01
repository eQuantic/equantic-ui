# Design

## How Flutter answers it

Flutter's semantics tree carries FLAGS rather than one role: a node is checked, selected, in a
mutually exclusive group, a button. Its Android bridge derives the class from them, so a checked
node in a mutually exclusive group becomes `android.widget.RadioButton`, and its tab bar puts the
position into the label through its localizations ("Tab 1 of 3"). `docs/FLUTTER-PARITY.md` §9
records the semantics tree and its bridges as SAME; what differs is that this tree carries the
role explicitly, beneath every target, and the platform words live in one table, `NativeRole`,
instead of in each bridge.

## Decisions

### Five members, appended

`SemanticRole` is a `byte` enum a consumer compiles into its own IL, so the members go at the end
(`EnumValueAbiTests`), 12 to 16. The pin's regeneration also records seven members the code diff
work appended in 0.2.0-preview.59 without a refresh, all shipped values.

### The words come from Core-AAM, and TalkBack decides Android's

For AppKit and Android, the W3C Core Accessibility API Mappings give an answer per ARIA role, and
they are what WebKit and Chrome expose for the web half of the same component, which is the
parity this tree owes. TalkBack names a role from a node's class name: it knows `RadioButton`, and
knows the tab BAR (`TabWidget`) but nothing inside it, so a tab, a menu item and an option have no
class it would name. The toolkits say "tab" through a role description they ship as words; this
framework ships no words for what the platform can say, so those rows give the plain class
Core-AAM gives and let the state speak.

### An option is AXMenuItem on AppKit

Core-AAM sends an option to `AXStaticText`, inside an `AXList`. There is no list node here, and
`AXStaticText` is the sound `NativeRole` exists to stop: a control read as a paragraph, which
`NoRoleButStaticTextIsAnnouncedAsStaticText` forbids. A choice in a popup is what `NSPopUpButton`'s
own items are, and those are `AXMenuItem`, the same word as a menu's action, the way both checks
are `AXCheckBox`: the difference rides the state.

### A destination is its own role, in a button's words

The web says `<button aria-current="page">`, UIKit says "tab" only inside a tab-bar container, and
Material says it through a shipped role description. A `ListItem` is a `Destination` exactly while
it is the current row, so any other word would rename the row the moment it was picked. The ROLE
is its own, so a bridge or a fixture can tell it apart, and where the user is rides `Current`.

### A radio is checked, not picked

Its state rides `Checked`, as the web's `aria-checked` does, which is `AXValue` on AppKit and
`isChecked` on Android, both the radio's own contract. A tab's and an option's ride `Selected`, the
web's `aria-selected`, which Core-AAM maps to `AXSelected`.

### The switch has no default arm

The catch-all is what made the five invisible. Like `NativeRole.Of`, the walk's switch names every
`PressableRole`, so an appended role stops the build with CS8509. CS8524, the unnamed value only a
cast produces, is waived as narrowly.

### A subrole is a column, not a special case

A tab is the only row with one today, and a bridge that knew which roles carry a subrole would be a
second table the compiler cannot see.

## What stays out

- A combobox trigger and a dialog, the handoff's other two `native-roles` members (#501).
- Position in a set ("tab, 2 of 4"): Android's `CollectionItemInfo` and an AppKit list or tab
  group around the items, which this tree has no node for (#502).
- A `Tabs` or a `RadioGroup` is one `Adjustable` stop on Photon, announced as a slider, so the
  tabs and radios inside one are not read one by one; the five roles reach the platforms where a
  pressable stands alone: a menu's items, a select's and a time picker's options, a navigation
  bar's and a rail's destinations, a list's current row (#500).
