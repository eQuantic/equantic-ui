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
