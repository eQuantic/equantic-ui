# Spec Delta

## ADDED Requirements

### Requirement: What the SDK hands the build reaches it as it is

The paths and the name the SDK hands the build SHALL reach it as they are, whatever characters they
hold that a file name or an assembly name may hold: a dollar sign, a backtick, a bar, a space, a
quote. No shell SHALL read them on the way, and each program of the build SHALL receive a path as an
argument of its own.

#### Scenario: The list of projects under a directory whose name holds a bar

- **WHEN** the projects the build is handed sit under a directory named `a|b $c`, on a system whose
  paths may hold a bar
- **THEN** each project is read whole, and a type of the library that a page uses is a module of the
  bundle

#### Scenario: An app under a directory whose name holds a dollar sign and quotes

- **WHEN** an app and the class library it references sit under a directory named `app $b 'c'`, and
  the app is built from packages alone
- **THEN** the build succeeds, and the bundle holds the library's types the page uses

#### Scenario: An app under a directory whose name holds a double quote

- **WHEN** an app and the class library it references sit under a directory named `app "e"`, on a
  system whose paths may hold a double quote, and the app is built from packages alone
- **THEN** the build succeeds, and the page runs as it does under any other directory

#### Scenario: An app whose assembly name holds a dollar sign

- **WHEN** an app's project file sets `<AssemblyName>Cash$Flow</AssemblyName>`
- **THEN** the build is handed `Cash$Flow`, and a library's `InternalsVisibleTo("Cash$Flow")` opens
  its internal types to the app
