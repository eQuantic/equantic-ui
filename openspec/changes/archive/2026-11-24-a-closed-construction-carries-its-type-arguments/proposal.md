## Why

#1863, a Bug under #1587, found by #1563. A closed construction of a generic the runtime library twins reached the browser without its type arguments when an app wrote it. `ObjectCreationStrategy.ClosureOf` marked a construction only when its generic type was declared in source, and an app reads the framework from metadata. So `new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(…)` crossed unmarked, though the build knew `HoldThenDrag`, and the browser took two factories over different types for one runtime type: a detector whose factory changed type kept its old recognizer, where Photon and Flutter make a new one.

## What Changes

- A closed construction of a generic class, record or struct the runtime library twins (a type of a runtime-provided namespace, or one marked `[RuntimeProvided]`) is marked with its type arguments wherever it is written, as one the source declares is. What reads the marks reads it too: a runtime type's comparison, `typeof` of the closed type, a generic method's answer and a struct's zero.
- A generic the runtime maps to JavaScript's own types (the BCL's collections) stays unmarked, and so does a construction whose type arguments are known only at run time (#755).

A developer writes nothing. Neither the public surface nor the developer surface moves. The part reached is eqc's object creation and what shares its closure.

## Capabilities

### New Capabilities

### Modified Capabilities

- `transpiler-classes`: a runtime library's generic, built by an app, keeps its type arguments.

## Impact

- `src/eQuantic.UI.Compiler/CodeGen/Strategies/Expressions/ObjectCreationStrategy.cs`.
- `tests/eQuantic.UI.Compiler.Tests/RuntimeGenericClosureTests.cs` (new).
