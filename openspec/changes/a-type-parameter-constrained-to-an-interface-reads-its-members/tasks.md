# Tasks

## 1. Which interface gets a twin

- [ ] 1.1 One predicate, `InterfaceTwin.CanBeWritten`, read by the parser, the dependency resolver and the constraint namers: no type parameter and no generic owner, each member an instance property or method with no body, no static member, event, indexer or base interface, one declaration, and not kept on the server. Verify each fence leaves a constrained parameter unconstrained and writes no module.

## 2. The twin

- [ ] 2.1 The parser takes such an interface as a definition, and `TypeScriptEmitter.EmitInterfaceModule` writes `export interface` with each member's twin name and type, `readonly` where the property has no setter, and `export const` of its name; plain JavaScript writes the value alone. Its imports are what its text names.
- [ ] 2.2 The dependency resolver counts the twin as a module, so a module that names it imports it.

## 3. The constraint

- [ ] 3.1 The class's, the component's and the record's constraint namers name an interface with a twin, imported. Verify `Measured<T> where T : IShaped`, reading a property and calling a method on a `T`, is refused by the runtime's `tsc` on main (TS2339) and taken with the fix.
- [ ] 3.2 A conformance case builds a generic class constrained to an interface and a class implementing it, as an app builds them, and reads the members through `T`, with type annotations on and off.

## 4. The record

- [ ] 4.1 Regenerate the library's modules and confirm the runtime's `tsc` takes every twin the library gains.
- [ ] 4.2 Write the ledger entry under `docs/ledger/`, citing #1878.
