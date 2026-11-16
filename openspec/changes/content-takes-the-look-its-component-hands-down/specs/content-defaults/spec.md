## ADDED Requirements

### Requirement: A text that names no look takes its box's

A `Text` that names no role, no line limit or no alignment SHALL be drawn with the ones the nearest enclosing box
states in `BoxStyle.Content`, on the web (server and browser), on Photon and in email. Where no box states one it SHALL
be drawn in `BodyL`, with no line limit, at the start, as before. A text that takes the box's role SHALL also take the
box's `Type`; a text that names its own role SHALL keep that role's type whole.

#### Scenario: A label inside a box that states the button's look

- **WHEN** a `Box` with `Content = new ContentStyle { Role = TypeRole.Label, MaxLines = 1 }` holds `Text("Save")`
- **THEN** the web writes the text as `eq-type-label` cut to one line, and Photon measures it in the `Label` style on
  one line

#### Scenario: A text that names its own role

- **WHEN** the same box, with `Type = TypeStyle.OfSize(15, FontWeight.SemiBold)`, holds `Text("Note", TypeRole.Caption)`
- **THEN** the text is drawn in the `Caption` role's own style, not the box's

#### Scenario: Nothing stated

- **WHEN** a `Text("Body")` stands in no box that states a `Content`
- **THEN** it is drawn in `BodyL` with no line limit, as every pinned output records

### Requirement: An inner box lays its look over the outer one

A box's `Content` SHALL be laid over the `Content` in effect around it member by member: what the inner box names wins,
and what it leaves null is the outer box's.

#### Scenario: A row inside a button

- **WHEN** an outer box states `Role = Label` and an inner box states only `MaxLines = 1`
- **THEN** a `Text` inside the inner box is drawn in `Label` on one line

### Requirement: An icon that names no size takes its box's

An `Icon` that names no size SHALL take the nearest box's `Content.IconSize`, and 24 where none is stated.

#### Scenario: An icon in a button's row

- **WHEN** a box states `IconSize = 18` and holds `Icon(Icons.Plus)`
- **THEN** the icon is 18 by 18 on every target

### Requirement: A string is a node

A `string` SHALL convert implicitly to a `VisualNode`, the `Text` of that string naming nothing, wherever a node is
taken, and the conversion SHALL cross to the browser as the runtime twin's `VisualNode.fromString`.

#### Scenario: A string among a column's children

- **WHEN** a component builds `Column(children: ["Title"])` inside a box that states `Role = Label`
- **THEN** the column holds a `Text` "Title" drawn in `Label`, on the server, in the browser and on Photon

### Requirement: A component lowered again alone keeps its look

When the browser lowers one component again alone, a `Text` inside it SHALL be drawn with the look in effect where the
component stands, as the whole page's lowering drew it.

#### Scenario: A counter inside a box that states a role

- **WHEN** a stateful component inside a box stating `Role = Label` changes its state and the browser lowers it alone
- **THEN** its `Text` is still written as `eq-type-label`
