# Spec Delta

## ADDED Requirements

### Requirement: A served page is built at the browser's density and hydrates at it

The server SHALL build a page at the density the browser's pointer asks for, as the browser reported
it, and SHALL say in the page's configuration which density it used. Hydration SHALL lower at that
density, and when the browser's own differs SHALL switch the whole page to it at once.

#### Scenario: The first request of a session under a mouse

- **WHEN** a page is requested with no density cookie and hydrated under a fine pointer
- **THEN** it is served Comfortable, hydration adopts it as served, the whole page then switches to
  Compact together, and the session cookie says compact

#### Scenario: Every later request

- **WHEN** the same session requests a page again
- **THEN** the server builds it Compact, the configuration says so, and nothing switches
