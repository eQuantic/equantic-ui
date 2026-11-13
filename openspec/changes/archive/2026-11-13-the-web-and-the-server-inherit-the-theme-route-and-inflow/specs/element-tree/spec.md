## ADDED Requirements

### Requirement: The route is read from the tree on every target

A component SHALL read the route through its context from the frame its owner was handed, and the
position SHALL be registered as a reader of the route, apart from the readers of the theme, density and
type scale. An owner handed a frame with another route SHALL build again the positions that read the route
and no other. No code inside a tree SHALL read the route from an ambient channel, and a tree built with no
route SHALL answer an empty one.

#### Scenario: A navigation within one page type

- **WHEN** a page whose header reads the route and whose footer does not is drawn again with another route
- **THEN** the header builds again and the footer does not

#### Scenario: A prefetch reads the request's route

- **WHEN** a component's prefetch resolves `RouteValues` from the services it is handed during a request
  for `/docs/getting-started`
- **THEN** its `slug` parameter is `getting-started`

### Requirement: An inherited value is read the same way in the browser

In the browser, `DependOn<T>()` and `Find<T>()` SHALL find the nearest `T` above the component's position,
and a dependent SHALL build again when that value says it changed, as on .NET.

#### Scenario: An app's inherited value in the browser

- **WHEN** a component in the browser depends on an app's inherited value, and its parent hands the value a
  new number
- **THEN** the component builds again with the new number, and a sibling that did not depend on it does not

### Requirement: A theme is the nearest one above

A component's `context.Theme` SHALL be the theme of the nearest `Theme` above its position, or the frame's
when none is, and reading it SHALL register the component as a dependent of whichever answered.

#### Scenario: A subtree under its own theme

- **WHEN** a component stands under a `Theme` holding another theme than the frame's
- **THEN** it builds with that theme, and a component beside the `Theme` builds with the frame's
