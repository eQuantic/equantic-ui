## MODIFIED Requirements

### Requirement: A combobox's field and a dialog are announced as what they are

The pressable a listbox panel hangs from SHALL reach the native tree as a `ComboBox`, with `Expanded`
saying whether its list is open, the rule the web applies to the anchor's root. It SHALL be named by its
author's label, and by nothing without one, and what its field shows SHALL be its value, as the web reads a
select-only combobox, which is never named from its content. A menu's trigger SHALL
stay a button. An open modal layer SHALL reach the tree as a `Dialog` named by its label, or as an
`AlertDialog` when it interrupts, as the web's `role="dialog"` and `role="alertdialog"` do. The layer
Photon opens for an anchored panel SHALL be a dialog only when its panel is one, and no stop at all for
a menu, a listbox or a tooltip.

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
- **THEN** the tree holds one `Dialog`, followed by the calendar's days, and the opener stays a button

#### Scenario: A labelled combobox

- **WHEN** an anchored listbox hangs from a pressable labelled "Country" whose field shows "Brazil"
- **THEN** the field is a `ComboBox` named "Country" whose value is "Brazil", on Photon as on the web
