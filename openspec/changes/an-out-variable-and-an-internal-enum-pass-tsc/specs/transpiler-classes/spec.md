## MODIFIED Requirements

### Requirement: A type named like an enum's union is its own

A vocabulary enum's union SHALL be named, in every module eqc writes, with a character C# cannot spell
(`$DensityValue` for `Density`), so a type of any name an app or the vocabulary declares beside the enum
SHALL be that type, imported from its own module, and never taken for the union. Every top-level non-flags enum
of the vocabulary's assemblies SHALL have its union, an internal one included, since a twin imports the union of
whatever enum its C# names.

#### Scenario: An app's record named like the union

- **WHEN** an app declares `record DensityValue(int Rows)` and a component whose constructor takes a
  `Density` and a `DensityValue`
- **THEN** the component's module imports `DensityValue` from the app's module and `$DensityValue` from
  the runtime, and it builds and runs as .NET runs it, with type annotations on and off

#### Scenario: An internal enum has its union

- **WHEN** an assembly holds an internal top-level enum and a public class with a nested enum
- **THEN** the generator's list of unions holds the internal enum and not the nested one
