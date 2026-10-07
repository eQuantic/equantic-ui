# Spec Delta

## ADDED Requirements

### Requirement: An app run under dotnet watch rebuilds its modules on a save

An app whose `UIOptions.HotReload` is unset SHALL rebuild its modules on a save of its C# and reload
the browsers watching it when it runs in the Development environment, and SHALL do the same when it
runs under `dotnet watch`, which sets `DOTNET_WATCH` to 1 on the app it runs, whatever its
environment. An app that sets `HotReload` SHALL be rebuilt exactly as it says.

#### Scenario: dotnet watch without a launch profile

- **WHEN** the dashboard sample runs under `dotnet watch --no-launch-profile`, so in Production, and
  the text of a component's `.cs` is edited and saved
- **THEN** eqc runs again, the page reloads, and it shows the new text

#### Scenario: The decision

- **WHEN** `HotReload` is unset, the environment is Production and `DOTNET_WATCH` is `1`
- **THEN** the app rebuilds on a save; with `DOTNET_WATCH` absent it does not; with `HotReload = false`
  in Development and `DOTNET_WATCH` at `1` it does not; with `HotReload = true` in Production it does

### Requirement: A page listens for rebuilds exactly when its server streams them

The server SHALL tell the page in its configuration (`hotReload` in `window.__EQ_CONFIG`) whether it
streams rebuilds, from the same decision that maps the stream, and the page SHALL open the stream and
replay its state after a reload only when told so, whatever the environment says.

#### Scenario: The environment and the decision disagree

- **WHEN** an app in Development sets `HotReload = false`
- **THEN** its page carries `hotReload: false` and opens no stream, and the server maps none
- **WHEN** an app in Production sets `HotReload = true`
- **THEN** its page carries `hotReload: true` and opens `/_equantic/hmr`, which the server streams

#### Scenario: The modules' cache

- **WHEN** an app streams rebuilds
- **THEN** its modules are served `no-cache`, since a rebuild rewrites them under the same URL; any
  other app serves them `immutable`

### Requirement: The app's C# source maps are served in Development alone

The stage-one source maps at `/_equantic/src-map/{name}`, which carry the app's C#, SHALL be served in
the Development environment, where the error overlay that reads them installs, and nowhere else,
whatever the hot reload decision.

#### Scenario: dotnet watch in another environment

- **WHEN** an app in Production streams rebuilds
- **THEN** `/_equantic/src-map/Probe.ts.map` is not served

#### Scenario: Development with hot reload off

- **WHEN** an app in Development sets `HotReload = false`
- **THEN** the map is served, so the overlay's second hop finds it

### Requirement: Stopping the app stops its rebuild and its streams

When the app stops, the server SHALL stop a rebuild still running and end every rebuild stream it
holds open.

#### Scenario: SIGTERM to the app alone, mid-rebuild

- **WHEN** the dashboard sample, with a page holding the stream open, receives SIGTERM while a rebuild
  runs, as `dotnet watch` stops an app it restarts
- **THEN** no rebuild is left running, and the app exits in about three seconds, where before the
  rebuild went on writing and the app took fourteen
