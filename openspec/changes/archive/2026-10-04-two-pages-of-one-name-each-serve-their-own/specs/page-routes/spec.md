## ADDED Requirements

### Requirement: A route renders the page declared for it

A route declared by `[Page]` or by `MapPage<T>` SHALL render, on the server and in the state a client
navigation receives, the page it was declared for, identified by its type, so that two pages with one
simple name in two namespaces each answer at their own route.

#### Scenario: Two pages named Dashboard

- **WHEN** `Admin.Dashboard` is routed at `/same-named/admin` and `Shop.Dashboard` at
  `/same-named/shop`, with SSR on, and each route is requested
- **THEN** each answers with its own page's markup and not the other's
