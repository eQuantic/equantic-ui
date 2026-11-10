## ADDED Requirements

### Requirement: A bridge's subtree reads one theme

A `VisualNodeComponent` given a theme SHALL lower its whole subtree in that theme and that type scale
in the browser, a stateless component included, as the server does, and SHALL leave the ambient ones as
it found them.

#### Scenario: A stateless component under a themed bridge

- **WHEN** `new VisualNodeComponent(new Checkbox(false), custom)` renders in the browser, `custom.borderStrong` being green
- **THEN** the checkbox's box is drawn with that green, and the ambient theme is the same after the render
