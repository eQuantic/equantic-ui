# Spec Delta

## ADDED Requirements

### Requirement: The manifest describes in a workspace the app a build describes

The hydration manifest SHALL read only the declarations the app's compilation holds, so that a
referenced project yields the same manifest and the same diagnostics whether it arrives as metadata,
as a command-line build hands it over, or as another compilation, as the workspace of `dotnet watch`
or of an IDE hands it over.

#### Scenario: A page on another project's base

- **WHEN** a page taking a server value derives from a class declared in a referenced project, and the
  project is passed once as metadata and once as a compilation reference
- **THEN** both runs write the same manifest, which projects the value's `Title`, and neither reports
  CS8785

#### Scenario: A server value handed to another project's method

- **WHEN** a page hands a server value whole to a method declared in a referenced project, passed both
  ways
- **THEN** both runs report the same EQ2114, that the build does not have the method's source
