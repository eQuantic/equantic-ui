## ADDED Requirements

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

## MODIFIED Requirements

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
