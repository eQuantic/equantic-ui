## ADDED Requirements

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
