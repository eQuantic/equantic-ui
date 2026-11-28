## MODIFIED Requirements

### Requirement: The mask engine is an assembly the SDK delivers

The mask engine and its public surface (`MaskFormatter`, `MaskedText`, `InsertKeyMode`, `IMaskedValueParser`,
`MaskedValueParser<TValue>` and `MaskAttribute`) SHALL ship as the assembly and package `eQuantic.UI.Masking`, of
surface class `contract`, whose types the runtime provides, `MaskAttribute` excepted, which stays on the server.
Both SDKs SHALL reference it, by package and, beside a source tree, by project, and SHALL put
`eQuantic.UI.Masking` in scope, so an app's csproj names nothing.

#### Scenario: An app that writes only its SDK

- **WHEN** an app whose csproj is `<Project Sdk="eQuantic.UI.Sdk">` and nothing else builds a page that holds a
  `MaskedText` and a `MaskedValueParser<DateOnly>` and composes a `MaskedInput`
- **THEN** it builds with no `using` and no `PackageReference` of its own, and its module imports them from
  `@equantic/runtime`

#### Scenario: The surface is declared

- **WHEN** a public member is added to `eQuantic.UI.Masking` without a line in its `PublicAPI.Unshipped.txt`
- **THEN** the build fails with RS0016

#### Scenario: The attribute does not cross

- **WHEN** a page's code reads a model whose property carries `[Mask]`
- **THEN** no module of the bundle holds `MaskAttribute`
