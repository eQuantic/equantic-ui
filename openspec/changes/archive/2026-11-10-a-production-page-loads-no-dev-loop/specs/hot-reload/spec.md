## ADDED Requirements

### Requirement: A production page loads no byte of the dev loop

The runtime every page imports SHALL carry no module of the runtime's `src/dev/`. The server SHALL name, in
the page's import map, a runtime that carries the dev loop (the error overlay and the capture and replay of a
page's fields around a hot reload) in Development and wherever it streams rebuilds, and the runtime without
it everywhere else, and SHALL answer the runtime with the loop only where it serves the loop.

#### Scenario: The runtime every page loads

- **WHEN** the map of the runtime the Server embeds as `runtime.js` is read
- **THEN** it names no source under `src/dev/`, where it named the overlay and its helpers

#### Scenario: A production page

- **WHEN** a page is served by an app in Production that does not stream rebuilds
- **THEN** its import map names `/_equantic/runtime.js`, and `/_equantic/runtime.dev.js` answers 404

#### Scenario: A page in Development or under hot reload

- **WHEN** a page is served in Development, or in Production with hot reload on
- **THEN** its import map names `/_equantic/runtime.dev.js`, which the server answers, and the page's hot reload and error overlay run as before
