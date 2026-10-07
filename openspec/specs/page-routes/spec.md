# page-routes Specification

## Purpose
Which page a route serves: the one its `[Page]` attribute or its `MapPage<T>` declaration names, on
the server and in what a client navigation receives.

## Requirements

### Requirement: A route renders the page declared for it

A route declared by `[Page]` or by `MapPage<T>` SHALL render, on the server and in the state a client
navigation receives, the page it was declared for, identified by its type, so that two pages with one
simple name in two namespaces each answer at their own route.

#### Scenario: Two pages named Dashboard

- **WHEN** `Admin.Dashboard` is routed at `/same-named/admin` and `Shop.Dashboard` at
  `/same-named/shop`, with SSR on, and each route is requested
- **THEN** each answers with its own page's markup and not the other's

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
