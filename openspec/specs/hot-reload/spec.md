# hot-reload Specification

## Purpose
A save of a component's C# reaching the page while the app runs: the server watches the app's
sources, runs eqc again and tells the browsers watching it to reload, in Development and under
`dotnet watch`, and the page listens exactly when its server streams.

## Requirements

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

#### Scenario: A rebuild past its limit

- **WHEN** a rebuild runs past the time it is allowed
- **THEN** it is stopped, and no process of it is left, where releasing it ended nothing

#### Scenario: A stream that opens as the app stops

- **WHEN** a page's stream registers after the app began stopping
- **THEN** it ends at once instead of holding the shutdown

#### Scenario: A rebuild that is starting as the app stops

- **WHEN** the app begins stopping while a rebuild's process is still starting
- **THEN** stopping waits for the process to exist, stops it, and returns only once the rebuild has
  ended, since the host waits for nothing after it

### Requirement: A page keeps its fields across a hot reload

A hot reload SHALL hand the reloaded page the fields it held before the save that are data, the ones its C# declares,
before it builds, each rebuilt as the type it was at every depth: the capture writes the hydration spec
of each value's runtime type beside its JSON, and the reload rebuilds it with `hydrate`, the server
payload's own machinery. A field the edit removed SHALL be left behind, a field the edit added SHALL
keep its initializer, and a field the reload cannot give back as it was SHALL keep its initializer while
the others cross: one holding a number JSON writes as null, a value with a life of its own, a record of
the app's where the reloaded page's initializer holds none of its class, or a type the edit changed.

#### Scenario: A counter across a save

- **WHEN** the dashboard sample's counter page is at `Count: 3` and an edit to its `.cs` is saved
- **THEN** the page reloads with the edit and shows `Count: 3`, and the next press shows `Count: 4`

#### Scenario: A field the edit removed or added

- **WHEN** the edit removes `_label` and adds `_title`
- **THEN** the reloaded page holds no `_label`, and `_title` has its initializer

#### Scenario: A controller beside the data

- **WHEN** a page holds a record, a `long` and a controller, and is reloaded
- **THEN** the record comes back as a record and the `long` as a `long`, and the controller, whose JSON
  is not it, keeps the one its initializer made

#### Scenario: A number JSON writes as null

- **WHEN** a page holds NaN or an infinity, alone, in a list or in a record, and is reloaded
- **THEN** the field that holds it keeps its initializer, where it came back null, and the page's other
  fields cross

#### Scenario: A controller inside a dictionary

- **WHEN** a page's dictionary holds a controller, or the page holds a sorted dictionary, and is reloaded
- **THEN** the dictionary keeps the one its initializer made: a controller's JSON is not it, and a sorted
  dictionary's order is a comparer the capture cannot name

#### Scenario: The vocabulary's value types

- **WHEN** a page holds a `Point`, an `EdgeInsets`, a `ColorToken`, a list of `Point` and a record holding
  a `Rect`, and is reloaded
- **THEN** each comes back an instance of its own class with its values, where every one of them went
  back to its initializer

#### Scenario: Each value as its type at every depth

- **WHEN** a page holds a `long[]`, a `Dictionary<int, long>`, a dictionary keyed by `decimal`, an
  anonymous value holding a `long`, and a `long?` and a `DateTime?` its initializer left null, and is
  reloaded
- **THEN** each comes back as the type it was: the longs as longs, where they came back as strings, and
  the dictionary's keys as numbers and its values as longs, found as it found them

#### Scenario: A list of the app's records

- **WHEN** a page holds a list of records of its own and is reloaded
- **THEN** the list keeps its initializer, where its records came back as plain objects without their
  methods: only an initializer names the class of the app's record after a document reload

#### Scenario: A field whose type the edit changed

- **WHEN** the edit changes `_size` from an `int` to a `string`
- **THEN** the reloaded page's `_size` holds the initializer the edit gave it
