## MODIFIED Requirements

### Requirement: A stable version removes no member of the contract

When the version in `Directory.Build.props` carries no prerelease label and is not a major's first
release, `x.0.0`, where the major's removals ship, a build of a contract project SHALL fail when any of
its `PublicAPI.Unshipped` files, its own `PublicAPI.Unshipped.txt` or a change's
`PublicAPI.Unshipped.<branch>.txt`, holds a `*REMOVED*` line, naming the line, the file and the two ways
on: mark the member obsolete, or move the version to the next major's preview. A tool or an
experimental project, and any project while the version carries a prerelease label, SHALL build.

#### Scenario: A removal at a stable version

- **WHEN** the version is `1.1.0` and a contract project's `PublicAPI.Unshipped.txt` holds a
  `*REMOVED*` line
- **THEN** the build of that project fails, naming the line

#### Scenario: A removal in a change's file at a stable version

- **WHEN** the version is `1.1.0`, the project's own `PublicAPI.Unshipped.txt` holds no removal, and a
  change's `PublicAPI.Unshipped.<branch>.txt` holds a `*REMOVED*` line
- **THEN** the build of that project fails, naming the line and the change's file

#### Scenario: The same removal during a preview

- **WHEN** the version is `2.0.0-preview.1` and the same line is there
- **THEN** the project builds

#### Scenario: A major's first release

- **WHEN** the version is `2.0.0` and the 2.0 preview's `*REMOVED*` lines still wait for the fold
- **THEN** the project builds

#### Scenario: A tool's removal at a stable version

- **WHEN** the version is `1.1.0` and the compiler's `PublicAPI.Unshipped.txt` holds a `*REMOVED*`
  line
- **THEN** the compiler builds
