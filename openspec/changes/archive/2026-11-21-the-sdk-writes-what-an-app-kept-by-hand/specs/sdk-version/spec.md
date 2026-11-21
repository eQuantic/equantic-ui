## ADDED Requirements

### Requirement: An app states the SDK's version once

Each SDK SHALL read its own version from its package and pin the eQuantic.UI packages it references to
it, whether or not the project sets `EQuanticUIVersion`, and SHALL default `EQuanticUIVersion` to it. A
project whose `EQuanticUIVersion` is another version SHALL fail with EQ4010, naming the SDK, its version
and the other one, before a restore collects the packages and before a build. Beside the source tree,
where the SDK has no package, nothing SHALL be compared.

#### Scenario: A project that sets nothing

- **WHEN** a project builds with an SDK package of version 9.9.9-preview.1 and sets no `EQuanticUIVersion`
- **THEN** `EQuanticUIVersion` evaluates to 9.9.9-preview.1

#### Scenario: A second version in the project

- **WHEN** the same project sets `EQuanticUIVersion` to 9.9.8-preview.4, in its body or before the SDK's
  props (as `Directory.Build.props` does)
- **THEN** collecting its packages fails with EQ4010, naming `eQuantic.UI.Sdk 9.9.9-preview.1` and
  9.9.8-preview.4, and a restore fails with that error alone

#### Scenario: The same version stated twice

- **WHEN** the project sets `EQuanticUIVersion` to the SDK's own version
- **THEN** it builds, with no EQ4010
