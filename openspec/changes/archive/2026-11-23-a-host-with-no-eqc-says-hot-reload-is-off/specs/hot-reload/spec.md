## ADDED Requirements

### Requirement: A host that is not an eQuantic.UI app says once that hot reload is off

A server whose project is not built with eQuantic.UI.Sdk, as a host serving the pages of a library it references
is, SHALL say once, in its own words, that hot reload is off for it, with no MSBuild error. It SHALL ask MSBuild no
more, rebuild nothing on a save and send the page nothing. Whether the project is built with the SDK SHALL be asked
only when MSBuild refuses the SDK's target, so an app pays nothing for the question.

#### Scenario: A host serving a library's pages

- **WHEN** an ordinary ASP.NET Core project serving eQuantic.UI pages from a library it references starts in
  Development
- **THEN** its log says once that hot reload is off for this host, and holds no `MSB4057` and no "Build failed"

#### Scenario: A save in such a host

- **WHEN** a C# file of that host is saved
- **THEN** nothing is rebuilt, nothing is written to the log and the page receives nothing

#### Scenario: An eQuantic.UI app

- **WHEN** an app built with eQuantic.UI.Sdk starts in Development
- **THEN** MSBuild is asked once, as before, and the app's eqc starts
