## ADDED Requirements

### Requirement: An input shape takes a press where its path is painted

A `VectorShape` with `OnPressed` SHALL call it when a press lands inside its path by its own fill rule and is released on the same shape, on the web and on Photon. A point inside the shape's bounding box and outside its path SHALL NOT press it, whatever is drawn after it in the same drawing.

#### Scenario: Two triangles that split a square

- **WHEN** a 200dp drawing holds two input triangles split along the square's diagonal, the lower one drawn second, and a press lands at (40, 40)
- **THEN** the upper triangle's `OnPressed` runs, and the lower one's does not, though its box holds the point

#### Scenario: A hole by the fill rule

- **WHEN** an input shape is a square with a square inside it, both drawn in the same direction, and a press lands in the inner square
- **THEN** it presses the shape when the shape fills nonzero, and does not when `EvenOdd` is set

#### Scenario: A release on the neighbour

- **WHEN** a press begins on one input shape and is released on another shape of the same drawing
- **THEN** neither shape's `OnPressed` runs

### Requirement: An input shape is a keyboard stop that Enter and Space press

Each input shape SHALL be a stop of the focus order, in the order of the drawing's shapes, and Enter or Space while it has the focus SHALL call its `OnPressed`. The keyboard's focus SHALL stroke the shape's outline in the theme's focus colour, 2dp wide at any scale.

#### Scenario: Tab, then Enter, on the web

- **WHEN** the focus is on the first input shape of a map and Tab is pressed, then Enter
- **THEN** the second shape has the focus, its `OnPressed` runs, and its outline is stroked in the focus colour

#### Scenario: Space does not scroll

- **WHEN** an input shape has the focus on the web and Space is pressed
- **THEN** its `OnPressed` runs and the page does not scroll

### Requirement: An input shape is announced as a button by its name

Each input shape SHALL be announced as a button named by its `Label`, with `Selected` as its state: `aria-pressed` on the web, the selected state of its semantics node on Photon. A reader's activation of that node SHALL run the shape the keyboard stops on. A drawing that holds input shapes SHALL NOT be hidden from assistive tech, labelled or not.

#### Scenario: A map read aloud

- **WHEN** a map's three regions are input shapes labelled "Matosinhos", "Maia" and "Porto", and "Porto" is selected
- **THEN** the web's accessibility tree holds three buttons with those names, "Porto" pressed, inside a group named by the drawing's label

#### Scenario: A reader's activation on Photon

- **WHEN** a reader activates the semantics node of the second input shape
- **THEN** its `OnPressed` runs, and the node's id is the id of that shape's focus stop

### Requirement: An input shape's hover is laid over its paint

While the pointer is over an input shape where a press would land, the members of its `Hover` that a path has SHALL apply: `Background` as its fill, `BorderColor` and `BorderWidth` as its stroke, `Opacity` as its opacity. A `Hover` that sets any other member SHALL be refused when the shape is built.

#### Scenario: A region that lightens

- **WHEN** an input shape declares `Hover = new StyleDiff { Background = token }` and the pointer moves over it
- **THEN** the web fills it with the token under its `:hover` rule, and Photon follows it as the hovered shape and fills it with the token

#### Scenario: A hover a path cannot draw

- **WHEN** a shape is built with `Hover = new StyleDiff { Elevation = 2 }`
- **THEN** building it throws, naming the members a shape's hover can change
