## ADDED Requirements

### Requirement: A button, a chip or a selection control takes the developer's content as nodes

`Button`, `FormSubmit` and `Chip` SHALL take what they show as a `VisualNode` named `child`, and `Checkbox` and
`RadioGroup` SHALL take theirs as a `VisualNode` named `title`. `Chip` SHALL take an `avatar`, a node drawn before
its child, in whose place a selected filter chip SHALL draw its check. Each component SHALL hand its content the look
it drew its text in (the role or type style, the line limit, the colour, and for a button or a chip the icon size),
so a `string` or a bare `Text` SHALL be drawn as the component's own text was, and a node that names its own look
SHALL keep it.

#### Scenario: A string child draws as before

- **WHEN** a `Button` is given `"Save"` as its child
- **THEN** the words are drawn in the size table's label style, on one line, in the variant's colour, as the label
  string was, on the web and on Photon

#### Scenario: A child with a badge

- **WHEN** a `Button` is given `Row(children: ["Inbox", Badge(count: 3)])` as its child
- **THEN** the button draws the row in its label's look, and the badge keeps its own

#### Scenario: A chip with an avatar

- **WHEN** an unselected filter chip is given `avatar: Avatar("AS")`, and then is selected
- **THEN** it draws the avatar before its child, and once selected the check in the avatar's place, at the same
  width

### Requirement: A control is named by its content

A `Button`, a `FormSubmit`, a pressable `Chip` and a `Checkbox` SHALL be named by the words of their content, as
both targets name a control that states no label, so a child that is a component of the developer's own SHALL still
name the control. A `RadioGroup`'s title SHALL name the group through the part that names the nearest nameable.

#### Scenario: A child that is a component

- **WHEN** a `Button` is given a child that is an app's own component drawing the words "Pay now"
- **THEN** a screen reader on the button hears "Pay now", on the web and on Photon

#### Scenario: A group named by its heading

- **WHEN** a `RadioGroup` is given `title: "Shipping"`
- **THEN** a screen reader on the group hears "Shipping", on the web and on Photon
