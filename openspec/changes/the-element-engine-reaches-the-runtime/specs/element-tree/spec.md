## ADDED Requirements

### Requirement: The engine is written once and reaches the runtime

The element engine (`Element`, `ComponentElement`, `NodeElement`, `BuildOwner` and what they use in
`eQuantic.UI.Primitives`) SHALL be transpiled by eqc into the runtime with no diagnostic, its modules
committed and pinned byte for byte, and SHALL answer every case the C# engine is held to with the same
result when executed in JavaScript. No hand-written TypeScript twin of any of it SHALL exist.

#### Scenario: A position keeps its stateful instance, in the browser too

- **WHEN** an owner mounts a stateful component, its state changes, and the next build hands the same
  position a component of the same type and key with new configuration
- **THEN** the same instance holds the new configuration and the old state, in .NET and in the
  transpiled engine alike

#### Scenario: The engine compiles through eqc

- **WHEN** the engine's sources are transpiled with the other shared models
- **THEN** no error is reported, and the modules match the committed ones

### Requirement: Every node kind has its children's slots

The engine SHALL read the children of every concrete node kind the vocabulary declares. A node whose
kind it does not know SHALL fail with an exception that names the kind, never with an empty answer.

#### Scenario: Every kind is mounted

- **WHEN** one instance of every concrete `VisualNode` in Primitives is mounted through an owner
- **THEN** each is mounted without the unknown-kind exception, apart from the kinds a baseline names
  because no instance can be built for them, and that baseline only shrinks

#### Scenario: A kind is forgotten

- **WHEN** the arm for one node kind is removed
- **THEN** mounting that kind fails with an exception that names it

### Requirement: A component builds through its owner

A component's position SHALL build its component through the owner it belongs to, and the owner SHALL
be handed how a component is built by whoever makes it. On Photon and on the server that is the error
boundary they build through today, with its report, its containment and its depth bound unchanged.

#### Scenario: A build that throws is contained

- **WHEN** a component's `Build` throws inside a tree an owner builds on Photon or on the server
- **THEN** the exception is contained and reported as it was before this change
