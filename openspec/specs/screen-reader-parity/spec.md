# screen-reader-parity Specification

## Purpose
A screen reader hears the same component the same way on every target: the same roles, names, states and
order, whether the web realizer lowered it to the DOM or Photon to its accessibility tree. Each requirement
here is a place where the two used to part, and `SemanticsParityTests` holds the rest of the vocabulary to
the same rule against a baseline that may only shrink.

## Requirements

### Requirement: A closed tooltip is out of the reading order

A tooltip that is not showing SHALL be hidden from assistive technology on every target, and SHALL stay
its trigger's description. It SHALL be exposed while it shows, on hover and on keyboard focus.

#### Scenario: A tooltip at rest on the web

- **WHEN** a page holds a `Tooltip` over a button, neither hovered nor focused
- **THEN** a reader walking the page meets the button and not the tooltip's text as a line of its own,
  and hears the tooltip's text as the button's description

#### Scenario: A tooltip on Photon

- **WHEN** the same tooltip is realized on Photon
- **THEN** a reader hears nothing of it until it shows, as on the web

### Requirement: A field's message is announced once and read once

A text field's helper or error line SHALL be a live region on every target, present from the first frame,
so a message that appears in it is announced once. A reader walking the page SHALL meet the message once,
and the field SHALL still be described by it.

#### Scenario: An error appears

- **WHEN** a `TextInput` that held no message is given `error: "Required"`
- **THEN** its message line, a live region on the web and on Photon, announces "Required" once

#### Scenario: A field with a message on the web

- **WHEN** a page holds a `TextInput` with `error: "Required"`
- **THEN** a reader walking the page meets "Required" once, in the message line, and the field is
  described by it

### Requirement: A code surface is read as a field holding its text

A code surface SHALL be read as one field whose value is what its model says a reader hears it hold,
`ICodeSurfaceModel.Reading`, on every target, and the text it draws SHALL NOT be read as lines beside that
field. On the web the field's selection SHALL stand where the model's caret and selection stand. A
spreadsheet's field SHALL hold what the grid says now. Where the page's primary pointer is coarse, the web's
door SHALL hold nothing, because a phone's keyboard edits the text around its caret.

#### Scenario: A code editor's caret moves

- **WHEN** a code editor's caret stands on a line of a long file
- **THEN** the field holds that line and the lines around it, and on the web its selection is the caret

#### Scenario: The name box at rest

- **WHEN** a spreadsheet's name box shows the active cell's address
- **THEN** a reader hears a field holding that address, and no line of text beside it

#### Scenario: An input method composes in a code editor on the web

- **WHEN** an input method's composition is open in the door
- **THEN** nothing moves the door's selection, and after the commit the door holds the reading again

#### Scenario: A code editor under a finger

- **WHEN** a page whose primary pointer is coarse draws a code editor
- **THEN** its door holds no text, and typing reaches the document as it did before

### Requirement: A labelled group is named and then walked into on every target

A `Group` node SHALL be announced as a group named by its `Label`, after which what it holds SHALL be
read in order: on the web a `role="group"` element carrying `aria-label`, on Photon a
`SemanticRole.Group` node with that label, followed by the nodes of its child. A `Group` whose label is
empty SHALL still be a group, with no name, on both targets. It SHALL add no geometry of
its own: its host on the web takes the child's fill and cap, and Photon lays it out as the web sizes that
host. A `Text` marked `ExcludeFromSemantics` SHALL be drawn and never read on both targets, and no part
of a control's derived name, unless a run of it links somewhere, in which case it SHALL be read.

#### Scenario: A grouped Select read on both targets

- **WHEN** `new Select(["Lisboa"], 0, groups: [new OptionGroup("A seguir", ["Braga"])])` is opened and
  read on the web through `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both readings are, after the combobox, `Option "Lisboa"`, `Group "A seguir"` and
  `Option "Braga"`, in that order, the heading read once as the group's name, with `Selected` true on
  `Lisboa` alone

#### Scenario: A group outside a Select

- **WHEN** `Group(Column(children: [Text("Delivery"), Checkbox(…)]), label: "Shipping")` is laid out on
  Photon
- **THEN** its semantics hold a `SemanticRole.Group` node labelled `Shipping`, followed by the static text
  `Delivery` and the checkbox, and the group's bounds are the column's

#### Scenario: A group with no name

- **WHEN** `Group(Column(children: [Text("Delivery")]))` is lowered on the web and laid out on Photon
- **THEN** the web writes a `role="group"` host with no `aria-label`, and Photon announces a
  `SemanticRole.Group` node with no label, followed by the static text `Delivery`

#### Scenario: A control's name leaves out the text a reader passes over

- **WHEN** a `Pressable` holding `Text("Shipping") { ExcludeFromSemantics = true }` and `Text("Free")` is
  read on the web through `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both name the button `Free`
