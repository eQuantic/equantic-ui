## MODIFIED Requirements

### Requirement: An array of an enum is typed as the enum crosses

A field, a static's accessors or a local typed as an array of an enum, at any depth, SHALL be annotated with
what the enum crosses as in the browser: a vocabulary enum's `$<Enum>Value` union, imported from the runtime,
and an app's own enum's member string, so the runtime's strict `tsc` takes the twin.

#### Scenario: A static array of a vocabulary enum

- **WHEN** a class declares `private static readonly TypeRole[] Roles = [TypeRole.BodyM];`
- **THEN** its twin's accessors are typed `$TypeRoleValue[]`, and its module imports `$TypeRoleValue` from the runtime

#### Scenario: An array of an app's enum

- **WHEN** a class declares `private static readonly Shade[] Shades = [Shade.Light];`, `Shade` being an enum of the app's
- **THEN** its twin's accessors are typed `string[]`

## ADDED Requirements

### Requirement: A type named like an enum's union is its own

A vocabulary enum's union SHALL be named, in every module eqc writes, with a character C# cannot spell
(`$DensityValue` for `Density`), so a type of any name an app or the vocabulary declares beside the enum
SHALL be that type, imported from its own module, and never taken for the union.

#### Scenario: An app's record named like the union

- **WHEN** an app declares `record DensityValue(int Rows)` and a component whose constructor takes a
  `Density` and a `DensityValue`
- **THEN** the component's module imports `DensityValue` from the app's module and `$DensityValue` from
  the runtime, and it builds and runs as .NET runs it, with type annotations on and off
