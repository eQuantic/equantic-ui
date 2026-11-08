## ADDED Requirements

### Requirement: An obscured field's text never reaches the semantic tree

A `TextEntry` with `Obscure` SHALL be announced with the dots it draws as its value, one per character
as the eye counts them, and its `SemanticNode` SHALL say it is `Obscured`, so no bridge can hand the
text to a screen reader. Android SHALL mark the node a password, and macOS SHALL give it the
`AXSecureTextField` subrole.

#### Scenario: A password field

- **WHEN** a `TextEntry` holding `hunter2😀` with `Obscure` is laid out
- **THEN** its semantic node's value is eight dots and it says `Obscured`

#### Scenario: A plain field

- **WHEN** a `TextEntry` holding `hunter2` without `Obscure` is laid out
- **THEN** its semantic node's value is `hunter2` and it does not say `Obscured`

### Requirement: A field a listbox hangs from is an editable combobox

A `TextEntry` that is the first text field in the anchor of an `Anchored` whose panel is a `Listbox` SHALL
reach the native tree as an `EditableComboBox`, with the text it holds as its value and `Expanded` saying
whether the list shows, the element the web makes the combobox. A field a menu hangs from, and any other
field of the anchor, SHALL stay a `TextField`.

#### Scenario: A field beside an icon, its list open and then closed

- **WHEN** a row holding an icon and a `TextEntry` labelled "City" holding "Lis" anchors an open listbox,
  and the list is then closed
- **THEN** the field is an `EditableComboBox` named "City" whose value is "Lis" and whose `Expanded` is
  true, and after the close it is false

#### Scenario: Two fields in one anchor

- **WHEN** a column holding a field labelled "City" and one labelled "Country" anchors a listbox
- **THEN** "City" is an `EditableComboBox` and "Country" a `TextField`

### Requirement: The option the keys are on is heard as it changes

The item row of an open menu's or listbox's panel that `Anchored.ActiveIndex` names, counted over the
menu items and options in tree order as the web numbers them, SHALL be a polite live region while it is
the one: its semantic node SHALL say so, and the host SHALL announce its name when it becomes the one,
after the first frame, as a live region's change is announced. A row the keys come back to SHALL be
announced again.

#### Scenario: The arrows walk a field's list and come back

- **WHEN** a field's open list of "Lisbon" and "Porto" has its active option moved from the first to the
  second, back to the first, and to the second again, two seconds apart
- **THEN** the host announces "Porto", "Lisbon" and "Porto", the active option's node is a polite live
  region and the other's is not, and a frame where nothing moved announces nothing

## MODIFIED Requirements

### Requirement: Each platform hears the containers and the combobox in its own words

The bridges SHALL announce a tab bar as `AXTabGroup` and `android.widget.TabWidget`, a radio group as
`AXRadioGroup` and `android.widget.RadioGroup`, a dialog as `AXGroup` with the `AXApplicationDialog`
subrole and `android.app.Dialog`, and an alert dialog as `AXGroup` with the `AXApplicationAlertDialog`
subrole and `android.app.AlertDialog`, the W3C Core-AAM's words for the same ARIA roles. A select-only
combobox SHALL be `AXPopUpButton` and `android.widget.Spinner`, each platform's select-only drop-down,
and an editable combobox SHALL be `AXComboBox` and `android.widget.EditText`, Core-AAM's own pair. On
UIKit the containers and the editable combobox SHALL carry no trait and the select-only combobox the
button trait. The containers SHALL be neither activatable nor adjustable on any platform, and both
comboboxes SHALL be activatable.

#### Scenario: The macOS bridge reads back real components

- **WHEN** the macOS bridge builds the accessibility elements for a page holding a `Tabs`, a labelled
  `RadioGroup`, a `Select` and a destructive `Dialog`, outside a running app
- **THEN** AppKit reports an `AXTabGroup` described as "tab group" before its tabs, an `AXRadioGroup`
  named "Size" described as "radio group" before its radios, an `AXPopUpButton` described as "pop up
  button" that is not expanded, and an `AXGroup` whose subrole is `AXApplicationAlertDialog`

#### Scenario: The web and Photon agree

- **WHEN** a `Tabs`, a `RadioGroup`, a `SegmentedControl`, a `Select` closed and open, a `TimePicker`,
  an open `Menu`, an open `DatePicker`, a `Dialog` and a field anchoring an open listbox are each lowered
  to the DOM and laid out on Photon
- **THEN** the tablist, tab, radiogroup, radio, combobox, option, menuitem, dialog and alertdialog
  elements of the DOM and the matching stops of the tree are the same list of roles, names and states
  in the same order, a combobox that is a text control matching an `EditableComboBox`
