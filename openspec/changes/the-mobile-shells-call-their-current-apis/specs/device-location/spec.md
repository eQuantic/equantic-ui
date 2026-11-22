## ADDED Requirements

### Requirement: Android asks for a location as the framework answers a fine Criteria

From Android 12 the Android shell SHALL ask the fused provider for a location at high accuracy: one update for
`GetCurrentAsync`, and updates at least 10 s and 10 m apart for `Subscribe`. Below Android 12 it SHALL ask
through a `Criteria` of fine accuracy. A reading SHALL end its updates once it has its first location, or when
its wait ends without one.

#### Scenario: One reading on Android 16

- **WHEN** an app with the location permission reads the location once on the Android 16 emulator, with a fix
  set on the emulator
- **THEN** the answer is that fix, and `dumpsys location` shows one fused registration at high accuracy with one
  update, removed once it delivered

#### Scenario: Following on Android 16

- **WHEN** the app follows the location and the emulator's fix moves three times, ten seconds apart
- **THEN** the app receives three locations, the fused registration asks for 10 s and 10 m at high accuracy,
  and disposing the subscription removes it
