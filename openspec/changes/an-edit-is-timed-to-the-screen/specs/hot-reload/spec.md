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
