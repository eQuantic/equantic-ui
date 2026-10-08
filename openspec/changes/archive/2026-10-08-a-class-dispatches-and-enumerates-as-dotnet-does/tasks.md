# Tasks

## 1. A method that hides one keeps a name of its own (#563)

- [x] 1.1 `TwinMethodName` names a method on its twin from its symbol (an override the slot it fills, a method that hides a member `name$n`), read by the method lowering and by every site that writes a bound method's name: a call, a static call, a call on `this`, a method group, a null-conditional call, an initializer's `Add` and a `Deconstruct`
- [x] 1.2 EQ1007 compares the names the twin holds, and refuses a method that hides one and answers an interface; `ToStringStrategy` leaves a hiding `ToString` to the call. Verified by Compiler tests for both
- [x] 1.3 Conformance cases through the module graph, both sides executed: a call through the base, the hiding type and a base call, an override of a `new virtual`, a method group, a null-conditional call, a hiding without `new`, a static, a method over a field, a generic base, and `GetHashCode` and `ToString`; failing on main and green here

## 2. A class that implements IEnumerable<T> is enumerated (#612)

- [x] 2.1 `IterableTwin` writes `[Symbol.iterator]` into the twin of the type that declares the method its iteration goes through, in the class and the record emitters, and leaves out the explicit non-generic `GetEnumerator()` and `Current` beside the generic ones; the runtime's `$eq.linq.iterate` walks an iterable or an enumerator the app wrote and disposes it. Verified by the runtime's specs
- [x] 2.2 Conformance cases through the module graph: `foreach`, `string.Join`, LINQ, `ToList`, a spread and the non-generic `IEnumerable`; an enumerator the app wrote, under LINQ and disposed by a loop that breaks; a struct's enumerator; an explicit `IEnumerable<T>`; an abstract `GetEnumerator`; a class that implements only `IEnumerable`; a record's sequence; failing on main and green here

## 3. An exception class of the app's is a class (#611)

- [x] 3.1 The module rule gives an exception a module wherever the model sees its chain (a host with no compilation keeps it out by name, 4.4); its twin extends the runtime's `$eq.exceptions.Exception` (or the app's base), writes its chain in `static $types`, calls the base with the bound `message` and `innerException`, and a generic construction is tagged with its own chain; it is annotated by its own name. Verified by the Compiler's module rule tests and the runtime's specs
- [x] 3.2 Conformance cases through the module graph: a field, a property's initializer, a constructor's body, the base's message, an inner exception and none, a method, a derived constructor, an object initializer, a typed catch, a catch of the .NET base, a filter, the default message, an override of `Message`, a generic class and a primary constructor; failing on main and green here
- [x] 3.3 The catch cases over the app's own exceptions run through the module graph, which writes their twins
- [x] 3.4 Once #558 merged, the base call over an exception of .NET's hands what a `new` of that type hands (`ExceptionTypes.FrameworkText` and `ExceptionTypes.Parts`, read by `TwinConstructor`), an implicit call included, and the runtime's base composes `Message` from them. Conformance cases: the text of the base that an implicit and an explicit `base()` reach, a null message, a parameter's name, and a base that takes no message; verified by the runtime's specs

## 4. What the branch's review found

- [x] 4.1 A sequence walks the `GetEnumerator()` C#'s `foreach` binds, its own public one first, and an explicit enumeration member beside the member that answers its name is left out; `$eq.linq.iterate` asks `MoveNext` before a sequence's own iteration. Conformance cases: a public `GetEnumerator()` that returns a struct beside the explicit ones, and a class that is its own enumerator; verified by the runtime's specs
- [x] 4.2 A generic method that hides one over its own type parameter is seen as hiding it. Conformance case
- [x] 4.3 The runtime's exception base answers its members from accessors on its prototype, so a member of the class's own under one of their names answers. Conformance cases: a `Name` of the app's and an override of `ParamName`; verified by the runtime's specs
- [x] 4.4 A host with no compilation keeps an exception class out by its base's name, and builds it as the runtime's exception; verified by the Compiler's module rule tests

## 5. The real thing

- [x] 5.1 `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, and the Compiler, Server, Web (against this change's wiki branch) and Conformance suites, each through the machine's suite lock, every one green

## 6. Documentation and archive

- [x] 6.1 docs/DIAGNOSTICS.md for EQ1007, the wiki's SupportedFeatures page in English and Portuguese on the wiki branch named like this change's branch, and one docs/LEDGER.md line citing #563, #611 and #612
- [x] 6.2 `./scripts/check-openspec.sh` green, then `openspec archive a-class-dispatches-and-enumerates-as-dotnet-does --yes` before the merge
