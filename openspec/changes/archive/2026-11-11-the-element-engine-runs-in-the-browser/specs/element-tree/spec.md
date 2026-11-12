## ADDED Requirements

### Requirement: The engine runs in the browser as it runs on .NET

The element engine SHALL be transpiled into the runtime, its modules committed and pinned byte for byte,
exported from the served runtime and accepted by the runtime's type check, and no hand-written
TypeScript twin of any of it SHALL exist. Driven by the same components through the same steps, the
transpiled engine SHALL answer every case it is held to as the C# engine does: what a position keeps,
what a frame builds and in which order, and what mounts and unmounts.

#### Scenario: A position keeps its stateful instance in the browser too

- **WHEN** an owner of the transpiled engine mounts a stateful component, its state changes, and the
  next build hands the same position a component of the same type and key with new configuration
- **THEN** the same instance holds the new configuration and the old state, as on .NET

#### Scenario: One instance at two positions

- **WHEN** one stateful instance is placed at two positions and they leave one after the other
- **THEN** it unmounts once, when the last leaves, on both engines

#### Scenario: An app member takes a name the engine calls

- **WHEN** an app's stateful component declares a method named `EnterPosition` or `LeavePosition`
- **THEN** the build refuses it with EQ2011
