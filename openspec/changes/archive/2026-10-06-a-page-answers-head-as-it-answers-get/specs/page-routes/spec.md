## ADDED Requirements

### Requirement: A page answers HEAD as it answers GET

A page route, declared by `[Page]` or by `MapPage<T>`, and the modules the server serves under
`/_equantic/` SHALL answer a HEAD request with the status and the headers they answer a GET with,
and SHALL send no body. A HEAD for a path nothing serves SHALL be answered as the GET is, by the
fallback.

#### Scenario: A page route

- **WHEN** a HEAD request asks for a route a `[Page]` declares, on Kestrel
- **THEN** it is answered 200 with the GET's content type and no body

#### Scenario: A route MapPage declares

- **WHEN** a HEAD request asks for a route `MapPage<T>` declares
- **THEN** it is answered 200 with the GET's content type and no body

#### Scenario: A module

- **WHEN** a HEAD request asks for `/_equantic/runtime.js`, or for an app's module under `/_equantic/`
- **THEN** it is answered 200 with the GET's content type and cache headers, and no body

#### Scenario: No page

- **WHEN** a HEAD request asks for a path no page declares
- **THEN** it is answered 404
