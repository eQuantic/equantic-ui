# native-accessibility Specification

## Purpose
How Photon's semantics tree reaches the platforms' screen readers: what role each node is, the
words VoiceOver on macOS and iOS and TalkBack on Android hear for it, and where its state rides,
so a component is announced on every target as the web announces it.

## Requirements

### Requirement: Every pressable role reaches the native tree as its own role

Every `PressableRole` SHALL reach the native semantics tree as a `SemanticRole` of its own, with its
state where the web puts it: a radio's check as `Checked` (the web's `aria-checked`), a tab's and an
option's selection as `Selected` (`aria-selected`), a destination's place as `Current`
(`aria-current`), and nothing for a menu item. A role appended to `PressableRole` SHALL fail the
build until the walk names it.

#### Scenario: A picked tab

- **WHEN** a `Pressable` with `Role = PressableRole.Tab` and `Selected = true` is laid out on Photon
- **THEN** its semantic node's role is `SemanticRole.Tab` and its `Selected` is true, where the
  node's role was `Button`

#### Scenario: A chosen radio

- **WHEN** a `Pressable` with `Role = PressableRole.Radio` and `Selected = true` is laid out
- **THEN** its role is `SemanticRole.Radio`, its `Checked` is `On`, and its `Selected` is null

#### Scenario: The current destination

- **WHEN** a `Pressable` with `Role = PressableRole.Destination` and `Selected = true` is laid out
- **THEN** its role is `SemanticRole.Destination` and its `Current` is true

### Requirement: Each platform hears a pressable role in its own words

The bridges SHALL announce each pressable role with the platform's own role, AppKit's and Android's
taken from the W3C Core-AAM for the same ARIA role unless the row says why not: a radio as
`AXRadioButton` and `android.widget.RadioButton`; a tab as `AXRadioButton` with the `AXTabButton`
subrole and `android.view.View`; a menu item as `AXMenuItem` and `android.view.MenuItem`; an option
as `AXMenuItem` and `android.view.View`; a destination as `AXButton` and `android.widget.Button`. On
UIKit each SHALL carry the button trait, with the Selected trait for what is picked or current, and
for a chosen radio, whose check UIKit SHALL hear as Selected rather than as a toggle's value. No
pressable role SHALL be announced as static text on any platform.

#### Scenario: A tab on macOS

- **WHEN** the macOS bridge builds the accessibility element for a `SemanticRole.Tab` node, outside a
  running app
- **THEN** AppKit reads `AXRadioButton` as the element's role and `AXTabButton` as its subrole

#### Scenario: A radio on Android

- **WHEN** the Android bridge describes a `SemanticRole.Radio` node
- **THEN** its class name is `android.widget.RadioButton`, which TalkBack names a radio button, and
  it is checkable and checked as the radio is

#### Scenario: A chosen radio on iOS

- **WHEN** the iOS bridge describes a `SemanticRole.Radio` node whose check is `On`
- **THEN** `NativeRole` asks UIKit for the Selected trait and no value, so VoiceOver says the radio is
  selected rather than reading a "1"

### Requirement: A tab strip and a radio group are containers a reader walks into

A tab strip and a radio group SHALL reach the native semantics tree as a container of their own role,
`TabBar` and `RadioGroup`, carrying the label the author gave the group, with no value and no range,
followed by each of their tabs and radios as a stop of its own that says whether it is picked or
checked, in the order the control draws them. The container SHALL stay the control's one keyboard
stop, whose arrows still step the pick, and each tab and radio SHALL run the handler a tap runs when a
reader activates it. A slider SHALL stay one stop that carries its value.

#### Scenario: A tab strip

- **WHEN** a `Tabs` of "Overview", "Activity" and "Settings" with "Activity" picked is laid out on
  Photon
- **THEN** the tree holds a `TabBar`, then a `Tab` "Overview" not selected, a `Tab` "Activity"
  selected and a `Tab` "Settings" not selected, where it held one unnamed `Slider`

#### Scenario: A labelled radio group

- **WHEN** a `RadioGroup` labelled "Shipping" with "Express" chosen is laid out
- **THEN** the tree holds a `RadioGroup` named "Shipping", then each option as a `Radio` whose check is
  `On` for "Express" and `Off` for the others

#### Scenario: A reader activates a tab

- **WHEN** a reader activates the third tab of a strip
- **THEN** the strip's select handler runs with index 2, and the strip remains the only keyboard stop

### Requirement: A combobox's field and a dialog are announced as what they are

The pressable a listbox panel hangs from SHALL reach the native tree as a `ComboBox`, with `Expanded`
saying whether its list is open, the rule the web applies to the anchor's root. A menu's trigger SHALL
stay a button. An open modal layer SHALL reach the tree as a `Dialog` named by its label, or as an
`AlertDialog` when it interrupts, as the web's `role="dialog"` and `role="alertdialog"` do. The layer
Photon opens for an anchored panel SHALL be a dialog only when its panel is one, and no stop at all for
a menu, a listbox or a tooltip.

#### Scenario: A select, closed and then open

- **WHEN** a `Select` showing "Lisbon" is laid out, and its field is then activated
- **THEN** the field is a `ComboBox` named "Lisbon" whose `Expanded` is false, and after the press it
  is true and is followed by the dismissing scrim and the `Option`s, with no dialog or group between
  them

#### Scenario: A destructive confirm

- **WHEN** a `Dialog` titled "Delete card?" whose confirming action is destructive is laid out
- **THEN** its layer is an `AlertDialog` named "Delete card?", followed by its title, body and actions

#### Scenario: A date picker's calendar

- **WHEN** a `DatePicker`'s calendar is opened
- **THEN** the tree holds one `Dialog`, followed by the calendar's days, and the opener stays a button

### Requirement: Each platform hears the containers and the combobox in its own words

The bridges SHALL announce a tab bar as `AXTabGroup` and `android.widget.TabWidget`, a radio group as
`AXRadioGroup` and `android.widget.RadioGroup`, a dialog as `AXGroup` with the `AXApplicationDialog`
subrole and `android.app.Dialog`, and an alert dialog as `AXGroup` with the `AXApplicationAlertDialog`
subrole and `android.app.AlertDialog`, the W3C Core-AAM's words for the same ARIA roles. A select-only
combobox SHALL be `AXPopUpButton` and `android.widget.Spinner`, each platform's select-only drop-down, and
an editable combobox SHALL be `AXComboBox` and `android.widget.EditText`, Core-AAM's pair for a text field
with a list under it. On UIKit the containers and the editable combobox SHALL carry no trait and the
select-only combobox the button trait. The containers SHALL be neither activatable nor adjustable on any
platform, and both comboboxes SHALL be activatable.

#### Scenario: The macOS bridge reads back real components

- **WHEN** the macOS bridge builds the accessibility elements for a page holding a `Tabs`, a labelled
  `RadioGroup`, a `Select` and a destructive `Dialog`, outside a running app
- **THEN** AppKit reports an `AXTabGroup` described as "tab group" before its tabs, an `AXRadioGroup`
  named "Size" described as "radio group" before its radios, an `AXPopUpButton` described as "pop up
  button" that is not expanded, and an `AXGroup` whose subrole is `AXApplicationAlertDialog`

#### Scenario: The web and Photon agree

- **WHEN** a `Tabs`, a `RadioGroup`, a `SegmentedControl`, a `Select` closed and open, a `TimePicker`,
  an open `Menu`, an open `DatePicker` and a `Dialog` are each lowered to the DOM and laid out on
  Photon
- **THEN** the tablist, tab, radiogroup, radio, combobox, option, menuitem, dialog and alertdialog
  elements of the DOM and the matching stops of the tree are the same list of roles, names and states
  in the same order

#### Scenario: The macOS bridge reads back an editable combobox

- **WHEN** the macOS bridge builds the accessibility elements for a `TextEntry` holding "al", labelled "Go to",
  that an open listbox hangs from, outside a running app
- **THEN** AppKit reports an `AXComboBox` named "Go to", described as "combo box", whose value is "al" and which
  is expanded

### Requirement: Tables, grids, cells and headers in each platform's words

A table and a grid SHALL reach the native tree as roles of their own, and so SHALL a cell, a grid cell,
the header of a column and the header of a row, each in the words its platform uses: on macOS a table
and a grid are `AXTable` and their cells and headers `AXCell`, with the cell's row and column index
ranges set; on Android a table and a grid are `android.widget.GridView` and a cell carries its row and
column; on iOS a header carries the header trait and a grid cell the button trait, as a calendar day
does. A table, a grid and a table's cell SHALL be neither activatable nor adjustable, and a grid cell
and a header SHALL be activatable, since activating one selects it. A new role SHALL fail the build
until every platform names it. The tree stays flat in this change: a cell is not nested inside its row
or its table.

#### Scenario: A grid on macOS

- **WHEN** a grid viewport holds the cell of row 3 and column 2 and is realized in the macOS shell
- **THEN** AppKit reads an element whose role is `AXTable`, and one whose role is `AXCell` with the row
  index range starting at 3 and the column index range starting at 2

#### Scenario: Headers on iOS

- **WHEN** a grid's row 0 holds the headers of its columns and is realized in the iOS shell
- **THEN** each header's element carries the header trait

### Requirement: A reader's activation of a cell lands where a tap would

A reader's activation of a grid cell or of a header that has no handler of its own SHALL be a primary
press and release at the centre of its bounds, delivered as a tap there would be, so that the surface
under it answers as it answers a tap.

#### Scenario: A cell under a surface

- **WHEN** a reader activates a grid cell drawn inside a surface that handles the pointer
- **THEN** the surface receives a press and a release at the cell's centre, and nothing else is pressed

### Requirement: A live region is heard on every shell

A live region whose text changes SHALL be announced by the macOS, iOS and Android shells alike, with its
urgency: macOS posts an announcement request, iOS posts the platform's announcement, and Android marks
the region's node live and reports that its content changed. A region that changes within a second of
its last announcement SHALL be announced when that second ends with its text at that time, once,
however many times it changed in between, and not at all if its text is back to what was last said.

#### Scenario: A change inside the quiet window

- **WHEN** a live region announces `B3, 1` at 0 milliseconds, says `C3, 2` at 300 and `D3, 3` at 600,
  and nothing after
- **THEN** `D3, 3` is announced on the first frame at or after 1 000 milliseconds, and `C3, 2` is never
  announced

#### Scenario: Back to what was said

- **WHEN** a live region announces `B3, 1` at 0 milliseconds, says `C3, 2` at 300 and `B3, 1` at 600
- **THEN** nothing more is announced

#### Scenario: Android

- **WHEN** a polite live region's text changes in the Android shell
- **THEN** its node is a polite live region and the shell reports a change of that node's content

### Requirement: A mixed check on Android is announced without a state

The Android shell SHALL make a checked or unchecked check checkable, with its state, and SHALL leave a mixed
check uncheckable, so TalkBack announces it with no state rather than as off. From Android 16 the node SHALL
carry its state as `CheckedState`, a mixed check as partial.

#### Scenario: TalkBack on Android 16

- **WHEN** TalkBack focuses three checks, on, off and mixed, on the Android 16 emulator
- **THEN** it reads "checked", "not checked", and the mixed one with its name and role and no state

### Requirement: Android's nodes come from the constructors current from API 30

The Android shell SHALL create its accessibility nodes, range information and events with the constructors
Android added in API 30, and with the `Obtain` factories only below API 30.

#### Scenario: TalkBack walks the screen

- **WHEN** TalkBack's next-item key walks a screen from its first node on the Android 16 emulator
- **THEN** it reads every node in tree order with the same words as before this change

### Requirement: A text field a listbox hangs from is an editable combobox

A `TextEntry` that is an `Anchored` node's anchor, through wrappers that hold only it, when the panel is a
`Listbox`, SHALL reach the native tree as an `EditableComboBox`, named by its label or by its placeholder without
one, whose value is the text it holds and whose `Expanded` says whether its list is open, as the web's text control
under that anchor carries `role="combobox"`. A field under a menu, or with no anchored panel, SHALL stay a
`TextField`. The option the keyboard is on, the item row `Anchored.ActiveIndex` counts to in tree order, SHALL be
announced politely from the field each time it changes, the option marked selected having no part in it. A list
that is closed or has no active row SHALL say nothing, so the option it shows is announced again when it opens or
the keyboard comes back to it. On the host's first frame nothing SHALL be announced.

#### Scenario: The go-to field, open and closed

- **WHEN** a `TextEntry` holding "al", labelled "Go to", anchors an open listbox of three options
- **THEN** it is an `EditableComboBox` named "Go to" whose value is "al" and whose `Expanded` is true, followed by
  the three `Option`s, and with the list closed its `Expanded` is false

#### Scenario: A field under a menu, and a field alone

- **WHEN** the same field anchors a menu, or anchors nothing
- **THEN** it is a `TextField`

#### Scenario: The keyboard walks the list

- **WHEN** the active row of the field's open list moves from "Alpha" to "Beta", and later to "Gamma"
- **THEN** the host announces "Beta" and then "Gamma", politely, each from the field's node, and announced nothing
  on its first frame

#### Scenario: The list opens again

- **WHEN** the list closes and opens again with the keyboard on "Alpha", or its active row goes to none and comes
  back to "Alpha"
- **THEN** nothing is announced while it is closed or has no active row, and "Alpha" is announced when it comes back

#### Scenario: The picked option is not the active one

- **WHEN** "Alpha" is the option marked selected and the active row moves to "Gamma"
- **THEN** the host announces "Gamma"

#### Scenario: The web and Photon agree

- **WHEN** the field with its list open, and with it closed, is lowered to the DOM and laid out on Photon
- **THEN** the DOM's combobox input and options and the tree's stops are the same list of roles, names, values and
  states in the same order
