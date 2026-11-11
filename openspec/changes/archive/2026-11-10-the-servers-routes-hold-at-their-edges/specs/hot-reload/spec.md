## MODIFIED Requirements

### Requirement: An app run under dotnet watch rebuilds its modules on a save

An app whose `UIOptions.HotReload` is unset SHALL rebuild its modules on a save of its C# and reload
the browsers watching it when it runs in the Development environment, and SHALL do the same when it
runs under `dotnet watch`, which sets `DOTNET_WATCH` to 1 on the app it runs, in any environment but
Production, which streams rebuilds only when the app sets `HotReload = true`. An app that sets
`HotReload` SHALL be rebuilt exactly as it says.

#### Scenario: dotnet watch without a launch profile

- **WHEN** the dashboard sample runs under `dotnet watch --no-launch-profile`, so in Production, with
  `HotReload` unset
- **THEN** the server maps no relay and serves its modules `immutable`; with `HotReload = true` it streams
  rebuilds as in Development

#### Scenario: dotnet watch in a development environment

- **WHEN** the dashboard sample runs under `dotnet watch` with `ASPNETCORE_ENVIRONMENT=Development`, and
  the text of a component's `.cs` is edited and saved
- **THEN** eqc runs again, the page reloads, and it shows the new text

#### Scenario: The decision

- **WHEN** `HotReload` is unset, the environment is Staging and `DOTNET_WATCH` is `1`
- **THEN** the app rebuilds on a save; in Production with `DOTNET_WATCH` at `1` it does not; with
  `DOTNET_WATCH` absent it does not; with `HotReload = false`
  in Development and `DOTNET_WATCH` at `1` it does not; with `HotReload = true` in Production it does
