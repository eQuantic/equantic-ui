# Proposal

Closes #1475, a Bug under #1174.

## Why

The context a component builds with is `ComponentContext` in C#. The runtime exports an interface for it, `BuildContext`, which every annotation of it is written with, and the class itself, `ComponentContext`, which a construction is written with. eqc imported `BuildContext` for every reference to the type, so a module that constructed a context wrote `new ComponentContext(…)` with nothing to name: `tsc` refused it (`TS2304`) and the first build threw a `ReferenceError`. Nothing in the runtime constructed a context until #1412 made the element engine build one per position, and the engine crosses to the browser in #1405 (E3a-3), on the 1.0 critical path.

## What Changes

- `RuntimeProvidedTypeScanner` asks the bound tree how each name of a runtime-provided type is used. A construction (the name is the type of an `IObjectCreationOperation`, or of an `IInvalidOperation` where the constructor is out of the compilation's reach, as the element engine's internal one is from outside the vocabulary's assembly), a constructor or static method the name binds to, and a static member read or called through the type's name are VALUE uses, and import the type's class: `ComponentContext`. Every other use is an annotation, and imports `BuildContext` as before. A module with both imports both.
- The rule is by symbol and by use, never by name: an app's own class called `BuildContext` stays the app's (#653).
- No public surface and no developer surface moves: the compiler's scanner is internal, and an app writes nothing new.

## Impact

eqc only (`RuntimeProvidedTypeScanner`). The runtime's emission fixtures gain a class that annotates and constructs a context, which the runtime's `tsc` reads.
