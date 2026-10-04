# Spec Delta

## ADDED Requirements

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
subrole and `android.app.AlertDialog`, the W3C Core-AAM's words for the same ARIA roles. A combobox,
which is select-only in this library, SHALL be `AXPopUpButton` and `android.widget.Spinner`, each
platform's select-only drop-down. On UIKit the containers SHALL carry no trait and the combobox the
button trait. The containers SHALL be neither activatable nor adjustable on any platform, and the
combobox SHALL be activatable.

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
