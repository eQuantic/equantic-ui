# hot-reload Specification

## Purpose
A save of a component's C# reaching the page while the app runs: the server watches the app's
sources, runs eqc again and tells the browsers watching it to reload, in Development and under
`dotnet watch`, and the page listens exactly when its server streams.

## Requirements

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
before it builds, each rebuilt exactly as the type it was at every depth, and only into a field the
reloaded page still types the same way. The capture writes the hydration spec of each value's runtime
type beside its JSON, a record and a vocabulary value as their own members, a property's store
included. The reload rebuilds each value with `hydrate`, the server payload's own machinery, and runs
none of a record's accessors. The type the reloaded page gives a place is the hydration spec it
declares there, which decides over an empty or a null initializer, or else what its initializer holds
there. A field the edit removed SHALL be left behind, a field the edit added SHALL keep its
initializer, and a field the reload cannot give back exactly, or into a type the page confirms, SHALL
keep its initializer while the others cross: one holding a number JSON does not write as itself, a
value with a life of its own, a record whose class the page does not hand back or whose saved member
the edited class reaches through a setter, a type the edit changed, or a value the reload has to
build where the page neither declares a type nor initializes a value. The first render's adoption of
the server's payload SHALL NOT write a field the replay decided.

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

- **WHEN** a page holds a `Point`, an `EdgeInsets`, a `ColorToken`, a list of `Point` it declares and a
  record holding a `Rect`, and is reloaded
- **THEN** each comes back an instance of its own class with its values, where every one of them went
  back to its initializer

#### Scenario: Each value as its type at every depth

- **WHEN** a page that declares its fields' types holds a `long[]`, a `Dictionary<int, long>`, a
  dictionary keyed by `decimal`, an anonymous value holding a `long`, and a `long?` and a `DateTime?`
  its initializer left null, and is reloaded
- **THEN** each comes back as the type it was: the longs as longs, where they came back as strings, and
  the dictionary's keys as numbers and its values as longs, found as it found them

#### Scenario: A list of the app's records

- **WHEN** a page holds a list of records of its own and is reloaded
- **THEN** the list keeps its initializer, where its records came back as plain objects without their
  methods: only an initializer names the class of the app's record after a document reload

#### Scenario: A field whose type the edit changed

- **WHEN** the edit changes `_size` from an `int` to a `string`
- **THEN** the reloaded page's `_size` holds the initializer the edit gave it

#### Scenario: A negative zero

- **WHEN** a page holds a negative zero, alone, in a list, in a record or as a dictionary key, and is
  reloaded
- **THEN** the field that holds it keeps its initializer, where it came back as 0 and `1 / x` answered
  the other infinity

#### Scenario: A record that keeps a property's value in a store

- **WHEN** a page holds eqc's `FRec`, whose setter doubles what it is given, set to 5, and `GRec`, whose
  getter adds one, set to 3, and is reloaded
- **THEN** they come back reading 10 and 4, equal to what they were, where `FRec` came back reading 20

#### Scenario: A record whose member the edit gave a setter

- **WHEN** the edit gives a record's member accessors with a setter over a store
- **THEN** the field keeps its initializer and the setter does not run

#### Scenario: A collection whose element type the edit changed

- **WHEN** the edit changes a `long[]`, a `Dictionary<int, long>` and a `long?` left null to text, on a
  page that declares no types
- **THEN** each keeps the initializer the edit gave it, where the `long[]` handed the edited code BigInts,
  and a list of text into a list of text still crosses

#### Scenario: The type a page declares decides

- **WHEN** a page declares a `long[]`, a `DateTime?` and a `Dictionary<int, long>` whose initializers are
  null or empty, and is reloaded once as it was and once after an edit that declares other types there
- **THEN** each comes back as the type it was the first time, and keeps its initializer the second

#### Scenario: The server's payload on the first render

- **WHEN** the replay leaves a field the server's payload lists at its initializer, and the page mounts
- **THEN** the first render does not write the saved value back, and the page's other server members and
  every other component's payload are adopted

#### Scenario: An app's class that declares equals and with

- **WHEN** a page holds an object of an app's class that overrides `Equals` and declares a `With(...)`,
  and is reloaded
- **THEN** the field keeps the object its initializer made: eqc's twin of a record or a struct says it is
  one (`static $record`), and this class's twin, carried as a record before, was rebuilt without its
  constructor

#### Scenario: A list with a hole or an undefined element

- **WHEN** a page holds a list with an undefined element (a vocabulary struct's `default`), a list
  `Array.Resize` grew, or a tuple holding an undefined, and is reloaded
- **THEN** each keeps its initializer, where JSON wrote the hole and the undefined as null

### Requirement: The time from a save to the screen is measured on both samples

The repository SHALL measure, on the dashboard sample and on the Photon desktop sample, the time from
a save of a component's `.cs` file, with the app running under `dotnet watch`, to the screen drawing
the change. Each edit SHALL be repeated enough to report a distribution, the first edit after the app
starts apart from the ones after it, and each part of the path SHALL be timed on its own.

#### Scenario: The web

- **WHEN** the measuring tool edits a component's text on disk while the dashboard sample runs under
  `dotnet watch` with a browser on the page that shows it
- **THEN** it reports when the browser drew the new text, and how long the wait after the save,
  MSBuild, eqc, bun, the reload and the replay each took

#### Scenario: The desktop

- **WHEN** the measuring tool edits a component's text on disk while the Photon desktop sample runs
  under `dotnet watch`
- **THEN** it reports when the window drew the frame with the new text, and how long the delta took
  to compile and apply and the frame took to draw

### Requirement: The desktop sample reaches the screen in under a second

The time from a save to the frame that draws it, measured on the Photon desktop sample under
`dotnet watch`, SHALL be held to a budget of a second on its warm median, and the measurement SHALL
fail when the median is over the budget, naming the part and the value.

#### Scenario: Within the bar

- **WHEN** the measuring tool times six edits of the desktop sample with the repository's budget
- **THEN** the warm median of the total is under 1000ms and the run passes

#### Scenario: Over a budget

- **WHEN** the same edits are held to a budget below their median
- **THEN** the run fails, saying the warm median and the budget it is over

### Requirement: The web sample is held to a ceiling that only comes down

The time from a save to the frame that draws it, measured on the dashboard sample under `dotnet
watch`, SHALL be held to a ceiling on its warm median, set from the first CI run that measured it with
room for the runner's spread. The measurement SHALL fail over the ceiling, and SHALL say when the
median falls under half of a ceiling that is above the bar, so the change that made it faster lowers
it in the same pull request.

#### Scenario: Over the ceiling

- **WHEN** the measuring tool times four edits of the web sample with the repository's budget, and
  their warm median is over the ceiling
- **THEN** the run fails, saying the warm median and the ceiling it is over

#### Scenario: Far under the ceiling

- **WHEN** the warm median is under half of a ceiling above the bar of a second
- **THEN** the run passes and says the ceiling can be lowered

### Requirement: An app that publishes trimmed reloads an edit in place

A Photon app that sets `PublishTrimmed` or `PublishAot` SHALL keep .NET's startup hook on for a Debug
build that is not a publish, which is what `dotnet watch` builds, so an edit is applied in the running
app rather than by restarting it. A publish SHALL leave the hook as trimming decides.

#### Scenario: The build dotnet watch runs

- **WHEN** a native SDK consumer with `PublishTrimmed` is evaluated for Debug
- **THEN** `StartupHookSupport` is `true`

#### Scenario: A publish

- **WHEN** the same consumer is evaluated with `_IsPublishing` set, or for Release
- **THEN** `StartupHookSupport` is not set to `true` by the SDK

#### Scenario: The desktop sample under dotnet watch

- **WHEN** the desktop sample, which publishes trimmed, runs under `dotnet watch` and a `Build` body
  is edited
- **THEN** dotnet watch applies the edit to the running app instead of restarting it, and the window
  draws the frame with the edit

### Requirement: A save is rebuilt after the editor's burst, and none is dropped

The server SHALL start a rebuild once the app's sources have been quiet for 50ms, each change of the
same save restarting that wait. A save that arrives while a rebuild runs SHALL be rebuilt when that
rebuild ends, by one more rebuild however many saves arrived.

#### Scenario: Saves during a rebuild

- **WHEN** two saves arrive while a rebuild runs
- **THEN** one more rebuild runs when it ends, so the rebuilds number two, where the server used to
  run one and keep the browser on the earlier edit

#### Scenario: The wait after a save

- **WHEN** `scripts/measure-edit-to-view.cs web` times an edit of the dashboard sample
- **THEN** its "wait" part, from the save to the server starting its rebuild, is under 100ms

### Requirement: A save reloads the page once, into markup that holds the edit

The rebuild stream SHALL be the page's one source of reloads: while it runs, the page SHALL keep
dotnet watch's refresh script from connecting. A rebuild SHALL announce itself once the edit of the
save it answers has reached the server, when dotnet watch runs the server, and SHALL say whether the
server's markup holds the edit. The page SHALL hydrate markup that holds the edit and replay its
fields after hydrating it, and SHALL draw over markup that does not.

#### Scenario: One reload per save

- **WHEN** `scripts/measure-edit-to-view.cs web` times an edit of the dashboard sample
- **THEN** each save reloads the page once, the reloaded document hydrates the server's markup, and it
  raises no hydration warning, or the run fails

#### Scenario: The edit reaches the server after the rebuild

- **WHEN** a rebuild ends before the edit of its save has reached the server, and dotnet watch runs it
- **THEN** the rebuild announces itself once the edit arrives, and says the page hydrates

#### Scenario: No edit reaches the server

- **WHEN** a rebuild ends and no edit reaches the server within five seconds of the save, or the server
  runs outside dotnet watch
- **THEN** the rebuild says the page mounts, and the page draws over the server's markup

### Requirement: The stream says which run of the server a page talks to, and why a rebuild failed

Every connection to the rebuild stream SHALL open by naming the run of the server, and a page that
reconnects to another run SHALL reload. A rebuild that fails SHALL send its errors, and the page SHALL
show them over itself without reloading.

#### Scenario: The server restarts

- **WHEN** a page's stream reconnects to a run of the server with another name
- **THEN** the page reloads once

#### Scenario: A static file of the app changes

- **WHEN** a file under the app's `wwwroot` changes, outside eqc's output
- **THEN** the stream says the page hydrates, and the page reloads once

#### Scenario: A save that does not compile

- **WHEN** a rebuild fails
- **THEN** the page shows MSBuild's error lines in the development overlay and keeps running the code
  it has

### Requirement: A save is compiled by an eqc the server keeps running

The server SHALL ask the project's MSBuild how to start eqc once for a run, as the app starts, and
SHALL keep that eqc running, asking it to compile on each save. No MSBuild SHALL run for a save. The
server SHALL own the process: eqc SHALL end when its standard input does, a stop or a compile past
its limit SHALL kill it, and the next save SHALL start it again.

#### Scenario: A save

- **WHEN** `scripts/measure-edit-to-view.cs web` times an edit of the dashboard sample
- **THEN** its "msbuild" part is a few milliseconds, and eqc's part pays no process start

#### Scenario: eqc died

- **WHEN** the running eqc exits between two saves
- **THEN** the next save starts it again, with what MSBuild said at the start of the run, and compiles

#### Scenario: A compile past its limit, or a stop

- **WHEN** a compile takes longer than its limit, or the app stops while one runs
- **THEN** eqc is killed, and the compile answers that it did not run

#### Scenario: A broken edit, then its fix

- **WHEN** a save does not compile, and the next one does
- **THEN** the first sends its errors to the development overlay, and the second reloads the page with
  the fix, with no restart of the app or of eqc

### Requirement: Hot reload ends with its host, stopped or not

The server SHALL end hot reload, its watchers, its timers and the compiler it started, when the app stops
and when the host's services are disposed, whichever comes first, and ending it a second time SHALL do
nothing more.

#### Scenario: A host disposed without being stopped

- **WHEN** an app with hot reload on is started and its host disposed without being stopped, and a source under its content root is then written
- **THEN** no rebuild starts, where the watcher still running started one

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

### Requirement: A save is announced once

The server SHALL rebuild and announce only for the saves made since the last rebuild took its saves, so a
second call for a save the last rebuild answered SHALL rebuild nothing and send the page nothing. Every
rebuild that ends SHALL say on the server's console whether the page hydrates or mounts, and for a mount,
when the save was written and when the last edit reached the server.

#### Scenario: A second call for an answered save

- **WHEN** a save is rebuilt and announced, and the debounce asks for a rebuild again with nothing saved since
- **THEN** the page hears one announcement, where it heard a second one, which said mount

#### Scenario: An event of a path already gone

- **WHEN** a save's file is written, its edit reaches the server, and the save's events include one for a path gone by the time the server reads it
- **THEN** the event dates nothing and opens no rebuild, whether it comes before the source's event or after the save's rebuild, and the page hears one hydrate, where the gone path dated the save at its arrival and the page heard mount

#### Scenario: A page that mounts

- **WHEN** a rebuild tells the page to mount
- **THEN** the server's console says when the save was written and when the last edit reached it, or that no save brings it its edit

### Requirement: Asking MSBuild how to start eqc never holds the loop

The MSBuild the server asks how to start eqc SHALL leave no worker node or server behind holding its
streams, and what it wrote SHALL be read within what is left of the time the server gives it, so a save
that asks answers instead of waiting on a stream nothing will close.

#### Scenario: An MSBuild that exits and leaves its output held

- **WHEN** the MSBuild the server asks exits, and something it started goes on holding its output
- **THEN** the asking ends within its limit, and the save answers that eqc could not be started
