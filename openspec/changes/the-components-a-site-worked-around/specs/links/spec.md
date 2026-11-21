## ADDED Requirements

### Requirement: A link may open beside the page

A `Link` with `OpensBeside` SHALL write `target="_blank"` and `rel="noopener noreferrer"`, on the server
and after hydration alike, and the router SHALL leave its click to the browser. On Photon, a tap on the
link or its activation by id SHALL hand an absolute `http` or `https` destination to `IUrlLauncher` and
leave the app where it is, and SHALL navigate in place to a destination that is one of the app's routes.
A link without it SHALL write neither attribute.

#### Scenario: The terms beside a consent form

- **WHEN** a page draws `Link("/terms", Text("Terms of use"), opensBeside: true)`
- **THEN** both producers write `target="_blank" rel="noopener noreferrer"` on the anchor

#### Scenario: On Photon

- **WHEN** a link to `https://example.com/terms` that opens beside is tapped or activated
- **THEN** the launcher receives the address and nothing navigates; a link to `/terms` navigates there
