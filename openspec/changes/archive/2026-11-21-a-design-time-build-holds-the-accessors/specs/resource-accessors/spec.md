## ADDED Requirements

### Requirement: A compilation loaded without a full build holds the accessors

A build that runs `Compile` alone, the design-time build `dotnet watch` and an editor load a project's
compilation from, SHALL write the accessor classes of the catalogs the build writes them for and
compile them, in both SDKs, so that a hot reload can apply an edit to a project whose catalog has no
`Designer.cs`.

#### Scenario: A design-time build of Compile alone

- **WHEN** a project holds `Resources/Strings.resx` with no Designer, and a design-time build runs
  `Compile` alone with `DesignTimeBuild=true`, `SkipCompilerExecution=true` and
  `ProvideCommandLineArgs=true`
- **THEN** its `Compile` items hold the accessor class's file

#### Scenario: A save under dotnet watch in the dashboard sample

- **WHEN** `samples/DefaultUIDashboard`, which keeps no Designer, runs under `dotnet watch` and a screen's
  `.cs` file is saved
- **THEN** dotnet watch applies the edit, and the rebuild's page hydrates the server's markup that holds it
