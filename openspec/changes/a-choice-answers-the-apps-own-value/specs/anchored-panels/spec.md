## MODIFIED Requirements

### Requirement: A listbox's options may stand in labelled groups

A `Select` given `groups` SHALL draw its ungrouped `options` first and then, for each `OptionGroup<T>`
whose label is not empty, a heading showing the label followed by that group's options. On the web the
open `role="listbox"` SHALL hold, per such group, one `role="group"` element whose `aria-label` is the
label and which contains the heading, hidden from readers since the group's name already says it, and
the group's `role="option"` rows; the server's producer and the
client's SHALL write the same tree. On Photon the menu SHALL draw the same heading over the same rows. A
group whose label is empty or blank SHALL draw no heading and wrap no group, its options read as
ungrouped. An open Select with no options SHALL name no active option.

#### Scenario: A grouped Select opened on the web

- **WHEN** `Select(options: [new("lis", "Lisboa")], value: "lis", groups: [new("A seguir", [new("bra", "Braga"), new("far", "Faro")])])`
  is opened and lowered by the server's realizer and by the client's
- **THEN** the `role="listbox"` holds the option `Lisboa`, then one `role="group"` with
  `aria-label="A seguir"` holding the text `A seguir` and the options `Braga` and `Faro`, and both
  producers' trees are equal

#### Scenario: A group with no name

- **WHEN** `Select(options: [], value: "", groups: [new("", [new("bra", "Braga"), new("far", "Faro")])])`, or
  with a label of blank space, is opened on either target
- **THEN** the listbox holds the options `Braga` and `Faro` with no `role="group"` around them and no
  heading above them

### Requirement: The options of every group are one list

A `Select`'s options SHALL be one list in reading order: the ungrouped options, then each group's
options in turn. The option its `value` selects SHALL be the first of that list whose value equals it,
and `onChanged` SHALL hand the value of the option chosen, wherever it stands. While the panel is open,
ArrowDown and ArrowUp SHALL move the highlight over that one list, from the last option of a group to the
first of the next and back, never stopping on a heading, and clamping at the list's ends; Enter SHALL
choose the highlighted option. On the web `aria-activedescendant` SHALL name the highlighted option,
which stands inside its group. Photon SHALL hand the same value for the same keys.

#### Scenario: The arrows cross a group on the web

- **WHEN** `Select(options: [], value: "por", onChanged: chosen.Add, groups: [new("Open", [new("lis", "Lisboa"), new("por", "Porto")]), new("Next", [new("bra", "Braga")])])`
  is opened in the browser, ArrowDown is pressed and then Enter
- **THEN** after ArrowDown the field's `aria-activedescendant` names the option `Braga`, whose parent is
  the `role="group"` labelled `Next`, and after Enter `chosen` holds `"bra"`

#### Scenario: The arrows cross a group on Photon

- **WHEN** the same Select is opened on Photon, ArrowDown is pressed and then Enter
- **THEN** `chosen` holds `"bra"` and the panel is closed

#### Scenario: The field shows a grouped choice

- **WHEN** `Select(options: [new("lis", "Lisboa")], value: "far", groups: [new("Next", [new("bra", "Braga"), new("far", "Faro")])])`
  is laid out on either target
- **THEN** the field shows `Faro`
