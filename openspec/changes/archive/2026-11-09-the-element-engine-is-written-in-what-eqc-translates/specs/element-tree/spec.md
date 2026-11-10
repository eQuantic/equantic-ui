## ADDED Requirements

### Requirement: The engine is written in what eqc translates

The element engine (`Element`, `ComponentElement`, `NodeElement`, `BuildOwner`, `ChildSlots` and
`NodeSlots` in `eQuantic.UI.Primitives`) SHALL go through eqc with its `[ServerOnly]` marks lifted with
no error and no warning, and SHALL yield a module for every one of its files. A construct eqc cannot
translate, written into the engine, SHALL fail the build's tests naming its file.

#### Scenario: The engine compiles through eqc

- **WHEN** the engine's sources, their `[ServerOnly]` marks removed, are transpiled by eqc
- **THEN** every file yields a module, and no diagnostic is reported

#### Scenario: A construct eqc refuses

- **WHEN** a call eqc has no translation for is written into one of the engine's files
- **THEN** the test fails, naming the file and eqc's diagnostic

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

A component's position SHALL build its component through the owner it belongs to, at the depth of
components the tree reads, and the owner SHALL be handed how a component is built by whoever makes
it. On Photon and on the server that is the error boundary they build through, with its report, its
containment and its depth bound unchanged.

#### Scenario: The owner's build is the one used

- **WHEN** an owner made with a build of its own mounts a page that holds two components
- **THEN** that build is asked for the page at depth 0 and for each component at depth 1, and nothing
  else builds them

#### Scenario: A build that throws is contained

- **WHEN** a component's `Build` throws inside a tree an owner from `ComponentBoundary` builds
- **THEN** the exception is contained and reported as it was before this change
