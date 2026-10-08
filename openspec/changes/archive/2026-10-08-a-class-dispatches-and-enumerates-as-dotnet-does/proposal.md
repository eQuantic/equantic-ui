# Proposal

Closes #563, a sub-issue of #164 (the transpiler's fences hold on every path), and #611 and #612,
sub-issues of #565 (the same, continued). One family: what a class of the
app's IS in the browser, how a call reaches its members, how it is enumerated, and what an exception
class of the app's carries.

## Why

Each of these compiles and runs differently in the browser, measured on main (fc8f0fdc) through the
module graph an app's build writes, both sides executed:

- A method that hides an inherited member (#563), with `new` or with the same signature and no
  `override`, is a method of the same name on its twin, and a method of a derived prototype IS the
  override of its base's in JavaScript. Where the base is the app's, the build refuses it (EQ1007),
  legal C# and all: `class B : A { public new string Name() => "B"; }`, `A a = new B(); a.Name()` is
  "A" in .NET and does not build. Where the base is .NET's, the runtime meets it through the slot it
  asks: over `public new int GetHashCode() => ++Calls;`, `((object)c).GetHashCode()` answers twice
  alike and leaves `Calls` 0 in .NET, and answers two different numbers and counts 2 in the browser.
- A class that implements `IEnumerable<T>` cannot be enumerated (#612): its twin has no
  `[Symbol.iterator]`, which `for…of`, a spread and the runtime's sequence helpers read, and the
  explicit `IEnumerable.GetEnumerator()` is written after the generic one, over it, and calls itself.
  `foreach (var x in bag) t += x;` after `bag.Add(3)` is 30 in .NET, and throws in the browser.
- An exception class of the app's is an `Error` built by its symbol, with no member (#611):
  `new Failure().Code` is 7 in .NET and null in the browser, `new Failure().Describe()` throws, and
  `new Failure().Message` is "" where `: base("failed")` says "failed".

## What Changes

- A method that hides an inherited member holds a name of its own on its twin: the hidden member's
  name, `$` and how many hidden slots of that name it stands over (`name$1`, `name$2`). Its
  declaration is written under it, and every call, method group, base call, null-conditional call,
  initializer's `Add` and `Deconstruct` the model binds to it reaches it by it, so a call bound to the
  hidden member keeps reaching the hidden one, the runtime's own protocol (`getHashCode`, `toString`)
  included. An override fills the slot of the method it overrides, whatever that one is named.
- EQ1007 compares the names each method holds on the twin, so a method that hides one is not refused;
  one that hides one and answers an interface is, since a call through the interface reaches the
  hidden member's name.
- The twin of a type that implements `IEnumerable<T>` (or only `IEnumerable`) carries a
  `[Symbol.iterator]` that calls the method its iteration goes through, the one C#'s `foreach` binds
  (its own public `GetEnumerator()`, or the interface's), and walks what it returns
  (`$eq.linq.iterate`): an enumerator an iterator method filled, or one the app wrote, by its
  `MoveNext` and `Current`, disposed when the walk ends, a class that is its own enumerator included.
  A class, a record and a struct alike. An explicit enumeration member (`GetEnumerator()`, `Current`)
  beside the member that answers its name is not written: they answer alike, and the twin holds one
  name.
- An exception class the app declares on its own is a class: it gets a module and the twin a class
  gets, which extends `$eq.exceptions.Exception` (the browser's `Error`, carrying the .NET types the
  twin's `static $types` says) where its base is .NET's. Its base call hands what a `new` of that
  .NET base hands (#558): `: base(message)` and `: base(message, inner)` reach `Message` and
  `InnerException`, `: base("bad", name)` over `ArgumentException` reads "bad (Parameter 'x')" and its
  `ParamName`, and where no message or a null one is given, the text that base's constructor writes,
  an implicit call included; a message still missing is .NET's default for the class; `Message` is an
  accessor an override replaces; and a construction of a generic class is tagged with its own types
  (`$eq.exceptions.typed`).
- What breaks, in preview: an exception class of the app's is transpiled as any class is. One the
  browser never uses, whose members reach what it cannot run, is marked `[ServerOnly]`, as any such
  class is; and two of its constructors that take as many arguments are refused (EQ1009), as a class's
  are, the serialization constructor obsolete since .NET 8 (SYSLIB0051) beside `(string, Exception)`
  among them. The migration line: delete the serialization constructor, or mark the exception
  `[ServerOnly]` where the browser never sees it. The emitted twin of a hiding method has a name of
  its own, and an app exception's twin is a class: TypeScript written by hand against either would
  move, and nothing in the runtime or the templates is.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-classes`: a method that hides an inherited member holds a name of its own.
- `transpiler-sequences`: a class that implements `IEnumerable<T>` is enumerated as its
  `GetEnumerator()` says.
- `transpiler-exceptions`: an exception class of the app's is a class over the browser's `Error`.

## Impact

- eqc: `TwinMethodName` names every method on its twin and is read by the method lowering, the
  component emitter and every call site that binds a method (`InvocationStrategy`,
  `MemberAccessStrategy`, `IdentifierStrategy`, `ObjectInitializer`, `DeconstructionPattern`) and by
  `OverloadedMethods`; `ToStringStrategy` leaves a hiding `ToString` to the call. `IterableTwin` writes
  the iteration into the class and the record emitters. `PlainClassModule` gives an exception a module,
  `ExceptionTypes` says which exception has a twin, its chain and a generic construction's tag,
  `TypeScriptEmitter` extends the runtime's base and writes `$types`, `TwinConstructor` calls that base
  with what a `new` of the .NET base takes, the text and the parts `ExceptionTypes` reads for both,
  `ObjectCreationStrategy` builds the class, and `TsStandIn` annotates it by its own name.
- The runtime: `$eq.linq.iterate`, `$eq.exceptions.Exception` (its constructor takes the message and
  the parts `create` takes) and `$eq.exceptions.typed`, with specs.
- Public surface of `eQuantic.UI.Compiler`: `Eq.LinqIterate`, `Eq.ExceptionBase` and
  `Eq.ExceptionTyped` are new. The developer surface does not move.
- Tests: conformance cases through the module graph for each issue, both sides executed; the catch
  cases over the app's own exceptions move from the statement harness, which writes no class, to the
  module graph; the compiler's module rule tests count an exception class as a module.
- Docs: docs/DIAGNOSTICS.md for EQ1007, and one docs/LEDGER.md line.
