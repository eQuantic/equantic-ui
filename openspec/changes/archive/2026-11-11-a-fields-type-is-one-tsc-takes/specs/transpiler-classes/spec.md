## ADDED Requirements

### Requirement: An array of an enum is typed as the enum crosses

A field, a static's accessors or a local typed as an array of an enum, at any depth, SHALL be annotated with
what the enum crosses as in the browser: a vocabulary enum's `<Enum>Value` union, imported from the runtime,
and an app's own enum's member string, so the runtime's strict `tsc` takes the twin.

#### Scenario: A static array of a vocabulary enum

- **WHEN** a class declares `private static readonly TypeRole[] Roles = [TypeRole.BodyM];`
- **THEN** its twin's accessors are typed `TypeRoleValue[]`, and its module imports `TypeRoleValue` from the runtime

#### Scenario: An array of an app's enum

- **WHEN** a class declares `private static readonly Shade[] Shades = [Shade.Light];`, `Shade` being an enum of the app's
- **THEN** its twin's accessors are typed `string[]`
