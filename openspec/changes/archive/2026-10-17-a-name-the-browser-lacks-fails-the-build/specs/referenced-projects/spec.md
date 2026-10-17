## MODIFIED Requirements

### Requirement: A referenced project's name is imported only where a symbol names its type

A module SHALL import a referenced project's type only where a symbol of the module's own code is
that type. A name bound to another type, or to none, SHALL NOT import a referenced project's module
of that name.

#### Scenario: A package's type of the same name

- **WHEN** a page reads `Status.Label`, a static of a class of a package, and a referenced project
  declares a class named `Status` that nothing reaches
- **THEN** the build fails with EQ2004 naming the package's `Vendor.Status.Label`, never the referenced
  project's, and the bundle holds no module of the referenced project's
