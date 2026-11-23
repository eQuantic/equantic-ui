## ADDED Requirements

### Requirement: A canvas that follows a drag keeps a finger's drag

A `Canvas` whose handlers follow a drag, an `OnPointerDown` and an `OnPointerMove`, SHALL write
`touch-action: none` beside `pointer-events: auto`, on both web producers, so a finger's drag reaches its
handlers instead of scrolling the page. A canvas that only hovers (an `OnPointerMove` without a press),
one that only takes a press, and one with no handler SHALL write no `touch-action`, so the page still
scrolls across them.

#### Scenario: A frame editor on a phone

- **WHEN** a page draws a `Canvas` with `OnPointerDown` and `OnPointerMove`
- **THEN** its element carries `touch-action: none`, and the C# and the runtime's lowering write the
  same atomic classes

#### Scenario: A chart's tooltip

- **WHEN** a page draws a `BarChart`, whose canvas listens to `OnPointerMove` and `OnPointerLeave`
- **THEN** its element carries no `touch-action`, and a finger's drag across the chart scrolls the page

#### Scenario: A canvas that only takes a press, or only draws

- **WHEN** a page draws a `Canvas` with only `OnPointerDown`, or with no handler
- **THEN** its element carries no `touch-action`
