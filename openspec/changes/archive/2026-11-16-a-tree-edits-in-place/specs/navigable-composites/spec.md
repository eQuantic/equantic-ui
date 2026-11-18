## ADDED Requirements

### Requirement: A key typed into a field inside a composite is the field's

A key typed into a text field inside a `Navigable` (an arrow, Home, End, a letter) SHALL reach the field alone: the
composite SHALL neither move nor search on it, on the web as on Photon. A press inside such a field SHALL put the
caret where it pressed and leave the keyboard in the field.

#### Scenario: Typing in a tree's rename field

- **WHEN** a text field inside a tree has the keyboard and the person types `a` and presses ArrowLeft
- **THEN** the field holds the `a` with its caret moved left, and the tree's active item did not move

### Requirement: A composite takes the keyboard back on request

A `Navigable` whose `FocusVersion` is higher than the one it was last built with SHALL take the keyboard to its
one stop, on the web as on Photon. Its first build SHALL only record the version, so a composite mounted with any
version takes nothing.

#### Scenario: A field inside the tree closes by Enter

- **WHEN** a field inside a tree has the keyboard, and the tree is built again without the field and with its
  `FocusVersion` one higher
- **THEN** the tree's one stop has the keyboard
