# Spec Delta

## ADDED Requirements

### Requirement: What the SDK hands the build reaches it as it is

The paths and the name the SDK hands the build SHALL reach it as they are, whatever characters they
hold that a file name or an assembly name may hold: a dollar sign, a backtick, a bar, a space, a
quote. No shell SHALL read them on the way.

#### Scenario: A solution under a directory whose name holds a bar

- **WHEN** an app and the class library it references sit under a directory named `a|b $c`, on a
  system whose paths may hold a bar
- **THEN** the build succeeds, and a type of the library that a page uses is a module of the bundle

#### Scenario: An app whose assembly name holds a dollar sign

- **WHEN** an app's project file sets `<AssemblyName>Cash$Flow</AssemblyName>`
- **THEN** the build is handed `Cash$Flow`, and a library's `InternalsVisibleTo("Cash$Flow")` opens
  its internal types to the app
