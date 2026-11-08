## 1. The twin's name

- [x] 1.1 Move the reserved words and the emitted globals into `src/Shared/JsNames.cs`, linked beside `TwinName.cs`, and make `TwinName.OfNested` give a top-level name the `$` a local takes.
- [x] 1.2 Apply it to a type whose twin eqc writes (`TwinTypeName`), and widen `NestedTwinName` to `RenamedTwin`, which every reference asks.
- [x] 1.3 Write the registered name at every site that introduces a twin: `NamespaceRemovalStrategy`, the extension homes, the alias construction, and a local that starts null.
- [x] 1.4 Register a component's action under its twin in `ServerActionRegistry`.

## 2. What a module imports

- [x] 2.1 Ask `PlainClassModule.Is` of the symbol whether the class is a resource class, and record a Designer in the parser before that rule turns it away.
- [x] 2.2 Read the emitted text outside its strings in the component path's import test.

## 3. Tests

- [x] 3.1 `ReservedTypeNameConformanceTests`: a type named `function`, `@class`, `Math`, `Symbol`, `Number` and a component named `Set`, at every kind of reference, both sides executed.
- [x] 3.2 `AClassNamedLikeADotNetTypeTests`: one file calling the solution's `Math` and .NET's, through the real build of a solution; the existing cases name `Math$`.
- [x] 3.3 `NestedTwinNameTests`, `TwinTypeNameTests` and the action registry's tests: the names, the imports, the page served and the action registered.
- [x] 3.4 `ReferencedProjectStringsAndWireTests`, `ResourceAccessorTests` and `TypeImportScopeTests`: no `./Strings`, and no import of a name only a string holds.

## 4. Documentation

- [x] 4.1 A ledger entry under `docs/ledger/`.
