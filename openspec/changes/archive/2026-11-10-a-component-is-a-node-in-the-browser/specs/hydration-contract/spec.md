## ADDED Requirements

### Requirement: Every component a page composes is named where it builds

The browser SHALL name every component a page composes, with or without state, where the lowering
builds it, by its type and its ordinal among the components of that type in depth-first order, as the
server's realizer names it. A component SHALL adopt only the entry that names it, on the first load and
on a client navigation alike.

#### Scenario: A page that mixes components with and without state

- **WHEN** a page composes components without state and with state, one inside the other and side by
  side, and the server sent an entry for each
- **THEN** each component adopts its own entry

#### Scenario: A navigation lands

- **WHEN** a client navigation mounts a page whose nested component without state holds a component
  with state, over a page that composed the same types
- **THEN** each component of the new page adopts the entry the new payload names it by, and none shows
  the previous page's
