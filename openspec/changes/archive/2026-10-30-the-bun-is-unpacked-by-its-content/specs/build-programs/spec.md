## ADDED Requirements

### Requirement: The embedded bun runs from the user's cache, unpacked by its content

The SDK SHALL run the embedded bun from the user's cache, unpacked from the zip its runtime package
carries into a folder named by the SHA-256 of that zip, and SHALL write nothing beside the zip. The
folder SHALL appear whole and executable, in one rename. The source tree's own builds and the
conformance harness SHALL run the bun the same way, so the bun that runs is always the one its zip
holds.

#### Scenario: A package cache nobody may write

- **WHEN** a web app is restored into a package cache, the cache is made read-only, and the app is built
- **THEN** the build succeeds, the cache holds the same files as before, and the bun it ran is outside it

#### Scenario: A zip that moves to another version

- **WHEN** a zip is unpacked, and a zip with other bytes then takes its place
- **THEN** the program unpacked from the second zip is the one that runs, in a folder of its own

#### Scenario: Two builds unpacking one zip at once

- **WHEN** eight unpacks of the same zip start together on an empty cache
- **THEN** all of them answer the same program, one of them unpacked it, and no partial folder is left

#### Scenario: Nothing else unpacks the bun

- **WHEN** the build files of the source tree and the conformance harness are read
- **THEN** none of them unzips beside the zip, and each runs the bun through the one owner
