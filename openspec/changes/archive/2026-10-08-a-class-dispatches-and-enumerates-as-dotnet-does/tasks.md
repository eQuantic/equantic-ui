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
- [x] 4.5 The constructor only .NET's serialization calls, `(SerializationInfo, StreamingContext)`, is no branch of the twin, for an exception class and a plain class alike. Conformance cases built from Visual Studio's exception template: each of its constructors, a typed catch of it, and a plain class beside a constructor of that shape; failing before the change and green after

## 5. Copilot's first round on #708

- [x] 5.1 The two `GetEnumerator()`s are two members: the explicit one holds the interface member's name and a method beside an explicit implementation of its name one of its own (`TwinMethodName.ExplicitBeside`), EQ1007 refusing one that also answers another interface; `[Symbol.iterator]` walks the interface's, and a `foreach` whose bound `GetEnumerator()` is another method of the app's calls it (`IterableTwin.ForEachSource`). Conformance cases: a `foreach` over the class and the interface's consumers over two `GetEnumerator()`s that differ, a deconstructing `foreach`, a class with only a public `GetEnumerator()`, and a public method beside an explicit implementation of its name; a Compiler test for the refusal; failing on the previous head and green after
- [x] 5.2 A construction of a generic exception class hands its types before its constructor's body runs (`$eq.exceptions.construct`), and an exception type is named as .NET names it at run time. Conformance cases: a generic class that throws itself from its constructor, its default message, and a nested class's; verified by the runtime's specs; failing on the previous head and green after
- [x] 5.3 With no model, a constructor of types named like .NET's serialization stays a branch. A Compiler test, failing on the previous head and green after

## 6. Copilot's second round on #708

- [x] 6.1 A type that implements `IEnumerable<T>` or `IEnumerator<T>` over two types, and a non-generic `GetEnumerator()` or a `Current` the twin holds as one with the member it keeps where it does not hand back that member's answer (`IterableTwin.AnswersAs`), are refused (EQ1007). Compiler tests for each, and for the shapes that hand the answer back; failing on the previous head and green after
- [x] 6.2 An exception type built or tested where a type argument it carries is a type parameter is refused (EQ2013). Compiler tests for a construction in a generic method, a nested class of a generic class and a typed catch in a generic method; failing on the previous head and green after
- [x] 6.3 A call of a serialization constructor the browser would make, a `new`, a base call, a chain or a base clause, is refused (EQ1009), and the template still builds. Compiler tests for each; failing on the previous head and green after
- [x] 6.4 A field of an exception class named like a member its base answers (`message`) keeps a slot of its own since #696 merged. A conformance case

## 7. Over #654, where a nested type is a module of its own

- [x] 7.1 An exception class declared inside another type is a class, its twin named by its owner, and a type declared inside an exception class crosses as its owner does: #654 kept both out while an exception had no module. Conformance cases: a nested exception's members and its typed catch, a record declared in an exception class, and an exception nested in a generic class caught by its construction. A Compiler test that the modules are written, and that an exception nested in a generic class built where its type argument is a type parameter is refused with EQ2013, a nested type of a server-only owner still with EQ2010
- [x] 7.2 A host with no project's compilation, which keeps an exception class out by its base's name, builds one as the runtime's exception: the construction named a twin nothing wrote there. A Compiler test over a snippet compiled on its own

## 8. The real thing

- [x] 8.1 `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, and the Compiler, Server, Web (against this change's wiki branch) and Conformance suites, each through the machine's suite lock, every one green

## 9. Documentation and archive

- [x] 9.1 docs/DIAGNOSTICS.md for EQ1007, EQ1009, EQ2010 and EQ2013, the wiki's SupportedFeatures and Diagnostics pages in English and Portuguese on the wiki branch named like this change's branch, and one docs/LEDGER.md line citing #563, #611 and #612
- [x] 9.2 `./scripts/check-openspec.sh` green, then `openspec archive a-class-dispatches-and-enumerates-as-dotnet-does --yes` before the merge
