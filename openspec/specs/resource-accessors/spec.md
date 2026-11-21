# resource-accessors Specification

## Purpose
The class a build writes for an app's resx catalog, so no Designer is kept by hand, and where eqc finds
the catalog of a class the build wrote.

## Requirements

### Requirement: The build writes a catalog's accessors

For each neutral `.resx` an app's project holds with no `Designer.cs` beside it, the build SHALL write
an accessor class named by the file, in the root namespace followed by the file's folder, with a static
`ResourceManager` whose base name is the manifest name MSBuild gave the resource, a static `Culture`,
and one static string per key. A culture's `.resx` SHALL get no class. A `.resx` with a `Designer.cs`
beside it, or whose project set the `StronglyTyped*` metadata itself, SHALL keep what it has. Both SDKs
SHALL write it.

#### Scenario: A catalog with no Designer

- **WHEN** a project holds `Resources/Strings.resx` with `Hero.Title = One page` and
  `Strings.pt-BR.resx` with `Uma página`, no Designer, and a program reads
  `Probe.Resources.Strings.Hero_Title` in the neutral culture and in pt-BR
- **THEN** it builds and prints `One page`, then `Uma página`

#### Scenario: A catalog with a Designer kept by hand

- **WHEN** the same project keeps a `Resources/Strings.Designer.cs`
- **THEN** that class is the one compiled, and the build writes none

#### Scenario: A project reached through a symbolic link

- **WHEN** the project's folder is reached through a symbolic link and MSBuild names the resource
  without its folder
- **THEN** the class is still `Probe.Resources.Strings`, and its base name is the manifest name, so the
  program prints both cultures

### Requirement: eqc reads a generated catalog where it reads a kept one

On the web SDK the accessor class SHALL land in a mirror of the project's folders inside the compiler's
generated tree, with copies of the project's `.resx` files beside it, refreshed before the resources are
compiled and before eqc runs, so that the catalogs and modules eqc writes are those it writes for a
Designer kept beside its `.resx`. A `.resx` the project names from outside its folders SHALL get no
mirror and no class.

#### Scenario: The dashboard sample without its Designer

- **WHEN** `samples/DefaultUIDashboard` is built without `Resources/Strings.Designer.cs`
- **THEN** `I18nScreen.js` and the `neutral`, `pt-BR`, `es` and `pt` catalogs are byte for byte those
  built with it, and the page switches between its languages in a browser

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
