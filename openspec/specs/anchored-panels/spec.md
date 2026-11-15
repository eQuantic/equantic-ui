# anchored-panels Specification

## Purpose
What a panel anchored to a control is to assistive technology: the combobox of an anchored listbox is the element the keyboard is in, the trigger or the text control of a field, on both web producers.

## Requirements

### Requirement: The combobox is the element the keyboard is in

An `Anchored` whose panel is a `Listbox` SHALL make its anchor the combobox, with `aria-haspopup`, and while
it is open `aria-controls` naming the panel and `aria-activedescendant` naming the active option. When the
anchor holds a text control, these SHALL be on that text control, which keeps the focus while the arrows
walk the list; otherwise they SHALL be on the anchor's root, the trigger. Both web producers, the server's
and the client's, SHALL agree.

#### Scenario: A field that anchors a listbox

- **WHEN** a box holding a `TextEntry` anchors an open listbox whose active option is the second
- **THEN** the `input` is the combobox, with `aria-controls` naming the listbox and `aria-activedescendant`
  its second option, and nothing else is a combobox

#### Scenario: A Select's trigger

- **WHEN** a `Select` is opened
- **THEN** its trigger is the combobox, as before

### Requirement: A listbox's options may stand in labelled groups

A `Select` given `groups` SHALL draw its ungrouped `options` first and then, for each `OptionGroup`
whose label is not empty, a heading showing the label followed by that group's options. On the web the
open `role="listbox"` SHALL hold, per such group, one `role="group"` element whose `aria-label` is the
label and which contains the heading, hidden from readers since the group's name already says it, and
the group's `role="option"` rows; the server's producer and the
client's SHALL write the same tree. On Photon the menu SHALL draw the same heading over the same rows. A
group whose label is empty or blank SHALL draw no heading and wrap no group, its options read as
ungrouped. An open Select with no options SHALL name no active option.

#### Scenario: A grouped Select opened on the web

- **WHEN** `new Select(["Lisboa"], 0, groups: [new OptionGroup("A seguir", ["Braga", "Faro"])])` is
  opened and lowered by the server's realizer and by the client's
- **THEN** the `role="listbox"` holds the option `Lisboa`, then one `role="group"` with
  `aria-label="A seguir"` holding the text `A seguir` and the options `Braga` and `Faro`, and both
  producers' trees are equal

#### Scenario: A group with no name

- **WHEN** `new Select(groups: [new OptionGroup("", ["Braga", "Faro"])])`, or with a label of blank
  space, is opened on either target
- **THEN** the listbox holds the options `Braga` and `Faro` with no `role="group"` around them and no
  heading above them

### Requirement: The options of every group are one list

The index a `Select`'s `selectedIndex` names and its `onChanged` reports SHALL be the option's position
in reading order: the ungrouped options, then each group's options in turn. While the panel is open,
ArrowDown and ArrowUp SHALL move the highlight over that one list, from the last option of a group to the
first of the next and back, never stopping on a heading, and clamping at the list's ends; Enter SHALL
choose the highlighted option. On the web `aria-activedescendant` SHALL name the highlighted option,
which stands inside its group. Photon SHALL report the same index for the same keys.

#### Scenario: The arrows cross a group on the web

- **WHEN** `new Select(groups: [new OptionGroup("Open", ["Lisboa", "Porto"]), new OptionGroup("Next", ["Braga"])], selectedIndex: 1, onChanged: chosen.Add)`
  is opened in the browser, ArrowDown is pressed and then Enter
- **THEN** after ArrowDown the field's `aria-activedescendant` names the option `Braga`, whose parent is
  the `role="group"` labelled `Next`, and after Enter `chosen` holds `2`

#### Scenario: The arrows cross a group on Photon

- **WHEN** the same Select is opened on Photon, ArrowDown is pressed and then Enter
- **THEN** `chosen` holds `2` and the panel is closed

#### Scenario: The field shows a grouped choice

- **WHEN** `new Select(["Lisboa"], 2, groups: [new OptionGroup("Next", ["Braga", "Faro"])])` is laid out
  on either target
- **THEN** the field shows `Faro`
