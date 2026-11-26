## MODIFIED Requirements

### Requirement: A composite takes the keyboard back on request

A `Navigable` wrapped in a `Focus` whose node is asked with `RequestFocus()` SHALL take the keyboard to its one
stop after the next write, on the web as on Photon. A composite mounted with a node nobody asked SHALL take
nothing. The composite's own version counter is gone: the request is the node's.

#### Scenario: A field inside the tree closes by Enter

- **WHEN** a field inside a tree has the keyboard, and the tree is built again without the field after its
  handler called `RequestFocus()` on the node of the `Focus` around the tree's `Navigable`
- **THEN** the tree's one stop has the keyboard

#### Scenario: A tree mounted

- **WHEN** a tree is built for the first time, its `Focus` holding a node nobody asked
- **THEN** the keyboard stays where it was
