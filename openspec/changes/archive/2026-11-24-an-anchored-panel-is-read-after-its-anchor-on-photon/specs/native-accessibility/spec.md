## ADDED Requirements

### Requirement: An anchored panel is read right after its anchor

On Photon, the stops of an open anchored panel (a menu, a select's list, an editable combobox's list, a date
picker's calendar) SHALL follow its anchor's stops, before whatever follows the anchored node on the page, as the
web lowers the panel inside the anchor's markup and as Flutter attaches an `OverlayPortal`'s child to the portal's
semantics. An anchored node the walk does not reach SHALL have its panel read after the page, as before.

#### Scenario: An open menu in the middle of a page

- **WHEN** a text "Before", a menu triggered by "More" holding "Rename" and "Delete" and open, and a text "After"
  are laid out on Photon
- **THEN** the reader hears "Before", the "More" button, the "Rename" and "Delete" menu items, then "After"

#### Scenario: A dialog panel

- **WHEN** a dismissible anchored dialog panel holding "July 2026" hangs from a "Pick" button between "Before" and
  "After", open
- **THEN** the reader hears "Before", "Pick", the "Dismiss" button, a `Dialog`, "July 2026", then "After"

#### Scenario: The web and Photon agree on an open date picker

- **WHEN** a `DatePicker` with its calendar open is lowered to the DOM and laid out on Photon
- **THEN** both read the field, the opener, the dismiss button, the dialog, the calendar and then the field's
  message line, in that order

## MODIFIED Requirements

### Requirement: A combobox's field and a dialog are announced as what they are

The pressable a listbox panel hangs from SHALL reach the native tree as a `ComboBox`, with `Expanded`
saying whether its list is open, the rule the web applies to the anchor's root. It SHALL be named by its
author's label, and by nothing without one, and what its field shows SHALL be its value, as the web reads a
select-only combobox, which is never named from its content. A menu's trigger SHALL
stay a button. An open modal layer SHALL reach the tree as a `Dialog` named by its label, or as an
`AlertDialog` when it interrupts, as the web's `role="dialog"` and `role="alertdialog"` do. An anchored
panel SHALL be a dialog only when it is one, and its stop SHALL be the panel itself, after the scrim's
dismiss button, as the web's `role="dialog"` is the panel and its scrim stands outside it. A menu's, a
listbox's or a tooltip's panel SHALL be no stop at all.

#### Scenario: A select, closed and then open

- **WHEN** a `Select` showing "Lisbon" is laid out, and its field is then activated
- **THEN** the field is a `ComboBox` with no name whose value is "Lisbon" and whose `Expanded` is false, and after the press it
  is true and is followed by the dismissing scrim and the `Option`s, with no dialog or group between
  them

#### Scenario: A destructive confirm

- **WHEN** a `Dialog` titled "Delete card?" whose confirming action is destructive is laid out
- **THEN** its layer is an `AlertDialog` named "Delete card?", followed by its title, body and actions

#### Scenario: A date picker's calendar

- **WHEN** a `DatePicker`'s calendar is opened
- **THEN** the opener stays a button, followed by the scrim's dismiss button, one `Dialog`, and the calendar's
  days

#### Scenario: A labelled combobox

- **WHEN** an anchored listbox hangs from a pressable labelled "Country" whose field shows "Brazil"
- **THEN** the field is a `ComboBox` named "Country" whose value is "Brazil", on Photon as on the web
