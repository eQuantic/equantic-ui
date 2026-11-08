## ADDED Requirements

### Requirement: An action is registered under its component's twin

The server SHALL register a component's Server Action under the name of the component's twin and the
action's, which is the id the browser's call sends.

#### Scenario: A nested component's action

- **WHEN** a component `Page` declared inside `Host` has an action `Hits`
- **THEN** the call eqc writes sends `Host$Page/Hits`, and the server answers it

#### Scenario: A page named like a global

- **WHEN** a page `Map` has an action `Pins`
- **THEN** its route serves the module `Map$`, and the server answers `Map$/Pins`
