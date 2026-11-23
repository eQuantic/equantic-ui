# native-platform-apis Specification

## Purpose
Which platform APIs the Photon shells call: the one current from each version they support, and a
build that fails on a call deprecated on a version the shell still supports.

## Requirements

### Requirement: A shell calls the platform API current from its floor up

A Photon shell SHALL call, at each site, the platform API that is current from the version it branches on
upward, and a deprecated one only below the version that deprecated it. CA1422 SHALL fail the build of the iOS
and Android shells, with no `WarningsNotAsErrors` or `NoWarn` lifting it.

#### Scenario: A call deprecated on a supported version

- **WHEN** a shell calls an API that a platform version at or above the shell's floor deprecates, outside a
  guard that keeps it below that version
- **THEN** the shell's build fails with CA1422, naming the call

#### Scenario: The shells as they are

- **WHEN** the iOS and the Android shells build in Release
- **THEN** both build with no warning
