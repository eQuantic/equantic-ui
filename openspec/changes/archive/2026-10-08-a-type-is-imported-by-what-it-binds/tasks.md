# Tasks

## 1. A type reached through its namespace is imported

- [x] 1.1 `NamespaceRemovalStrategy` registers the type the qualified name binds as introduced, which every emitter imports; proved by `TypeImportConformanceTests`, both cases failing on main with both annotations
- [x] 1.2 `IdentifierStrategy` and `ObjectCreationStrategy` write a type reached through a using alias by its own name and register it, its static read and its construction alike; proved by the theory's alias case, failing on main

## 2. Proof and documentation

- [x] 2.1 Run the five suites
- [x] 2.2 One `docs/LEDGER.md` line citing #625
