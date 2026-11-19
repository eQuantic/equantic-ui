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

### Requirement: A tree and its items are read with their level and place on every target

A `Navigable` whose role is `Tree` SHALL be announced as a tree named by its `Label`, after which its
items SHALL be read in order. A `Pressable` whose role is `TreeItem` SHALL be announced as a tree item,
with its `Expanded` and `Selected` states and, when it carries a `TreePlace`, its level, its position in
its set and the set's size: on the web a `role="treeitem"` element (never a `<button>`) with
`aria-level`, `aria-posinset`, `aria-setsize`, `aria-expanded` and `aria-selected`, inside a
`role="tree"` host whose items are not wrapped in rows; on Photon a `SemanticRole.TreeItem` node carrying
the same `TreePlace`, after a `SemanticRole.Tree` node. On macOS the item SHALL be an outline row whose
disclosure level is its level less one.

#### Scenario: A two-level tree read on both targets

- **WHEN** a `Navigable` with role `Tree` labelled `Files` holds a tree item `src` (level 1, 1 of 1,
  expanded) and a tree item `App.cs` (level 2, 1 of 1, selected), and is read on the web through
  `ComponentTester` and on Photon through `PhotonHost.Semantics()`
- **THEN** both readings are `Tree "Files"`, `TreeItem "src"` (level 1, 1 of 1, expanded) and
  `TreeItem "App.cs"` (level 2, 1 of 1, selected), in that order

#### Scenario: The server and the browser write the same tree

- **WHEN** the same tree is lowered by the server's realizer and by the browser's twin
- **THEN** both write a `role="tree"` host with `tabindex="0"`, `aria-activedescendant` naming the
  active item's id, and `div` tree items carrying the same ids and attributes

### Requirement: A merged subtree is heard as one stop on every target

A `MergeSemantics` node SHALL be heard as ONE stop. When a control is among the stops its subtree would make,
the stop SHALL be the first control, with that control's role, value and states, and activating it SHALL press
that control on every target; a later control SHALL stay a stop of its own, after the merged one. With no
control the stop SHALL be a group, and nothing inside it SHALL be a stop of its own. It SHALL add no geometry of
its own.

#### Scenario: A switch beside its label

- **WHEN** `MergeSemantics(Row[Text("Wi-Fi"), Switch(true, onChanged)])` is read on Photon through
  `PhotonHost.Semantics()` and on the web through `ComponentTester`
- **THEN** both readings are one stop, `Switch "Wi-Fi"`, checked, and activating it on Photon or tapping it on
  the web calls `onChanged`

#### Scenario: Read content with no control

- **WHEN** `MergeSemantics(Row[Icon(Check, label: "Paid"), Text("Total"), Text("42 EUR")])` is read on both
  targets
- **THEN** both readings are one stop, `Group "Paid Total 42 EUR"`

#### Scenario: Two controls

- **WHEN** `MergeSemantics(Row[Text("Volume"), Slider(0.5), Button("Mute")])` is read on both targets
- **THEN** both readings are `Slider "Volume"` and then `Button "Mute"`

### Requirement: A merged stop is named by what its subtree says

A merged stop's name SHALL join, in reading order, the names of the stops its subtree would make, the merged
control's own in its place and a later control's left out. What a reader passes over, a `Text` marked
`ExcludeFromSemantics` or an `ExcludeSemantics` subtree, SHALL be no part of it. On the web the name SHALL be
computed by the browser from the parts the merge hides, so it follows their content.

#### Scenario: The merged name leaves out what a reader passes over

- **WHEN** `MergeSemantics(Row[ExcludeSemantics(Text("JD")), Text("Initials") { ExcludeFromSemantics = true },
  Text("Jane Doe")])` is read on both targets
- **THEN** both readings are one stop, `Group "Jane Doe"`

### Requirement: An excluded subtree is heard by no one on every target

An `ExcludeSemantics` node whose `Excluding` is true SHALL leave its whole subtree out of what a reader hears on
every target, controls included, and SHALL change nothing a person does with it: a control inside SHALL still
take a press and a Tab stop. With `Excluding` false its subtree SHALL be read as if the node were not there, and
the node SHALL stay in the tree. It SHALL add no geometry of its own.

#### Scenario: Decoration beside what is read

- **WHEN** `Row[ExcludeSemantics(Row[Icon(label: "Done"), Text("Decoration")]), Text("Shown")]` is read on
  both targets
- **THEN** both readings are `StaticText "Shown"` alone, and with `Excluding = false` they are `Image "Done"`,
  `StaticText "Decoration"` and `StaticText "Shown"`

#### Scenario: A control inside an exclusion

- **WHEN** `ExcludeSemantics(Pressable(Text("Hidden"), onPressed))` is laid out on Photon
- **THEN** its semantics are empty, the pressable is still a focus stop, and activating it calls `onPressed`
