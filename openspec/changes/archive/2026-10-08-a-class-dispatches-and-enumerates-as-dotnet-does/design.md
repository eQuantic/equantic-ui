# Design

## Context

A class of the app's crosses to the browser as a twin, an ES class eqc writes from its C#: its state on
each instance, its methods on its prototype, its base as `extends`. Three things of what C# says about a
class had no form in that twin:

- C# keeps a method that HIDES an inherited member apart from it: a call through the base type reaches
  the base's member, statically, whether it is virtual or not. A JavaScript class chain has one member
  per name, so the hiding method, written under its own name, was the override of the hidden one. The
  overload check refused it where the base was the app's (EQ1007), and nothing refused it where the
  base was .NET's, whose members the runtime reads by name (`getHashCode`, `toString`, `equals`).
- `foreach`, a spread and every sequence helper of the runtime read a sequence through JavaScript's
  iteration, `[Symbol.iterator]`, which no twin wrote. An iterator method materializes into an array
  (`MethodLowering`), so a class's `GetEnumerator()` already answered something iterable; nothing asked
  it. And the explicit `IEnumerable.GetEnumerator()` was written under the generic one's name, after it.
- An exception class of the app's was kept out of the module rule (`PlainClassModule`) and built by its
  symbol as the runtime's `Error`, carrying its chain of .NET types for a typed `catch` (#561), and
  nothing it declared.

## Goals / Non-Goals

**Goals:**

- Every call the model binds to a method reaches it by the name the twin holds it under, decided once,
  from the symbol, for the declaration and every call site.
- A twin of a sequence iterable as its type's own `GetEnumerator()` says, whatever that returns.
- An exception class of the app's built as a class, with its .NET chain kept for a typed `catch`.

**Non-Goals:**

- A property or a field that hides an inherited member. It is the same mechanism (a name of its own),
  but a property is read and written through far more paths (member reads, assignments, compound
  assignments, initializers, property stores, records, hydration's names), and it is its own change.
- An overload along the chain (`Format(int)` beside a base's `Format(string)`), which hides nothing in
  C#. It stays refused (EQ1007); naming every overload apart is the overload feature itself.
- An enumerator walked by hand (`e.MoveNext()` on what a `GetEnumerator()` returned), and a .NET
  collection's own `GetEnumerator()` (`items.GetEnumerator()`), which has no translation (EQ2004).
- A nested exception class, which has no module of its own, as no nested class has.

## Decisions

### A method's name on its twin is the symbol's, decided in one place

`TwinMethodName` answers the name for a method symbol, and every site that writes a method's name asks
it: the method lowering (classes, records, structs, a component's methods), the call's three paths
(`x.M()`, a bare `M()` on `this`, a static), a method group (a member access and a bare name), a
null-conditional call (rebuilt as an ordinary access), an initializer's `Add`, a `Deconstruct`, and the
overload check. A method that overrides holds the name of the method it overrides, followed to the slot
that is no override. A method that hides a member (C#'s rule, asked of the symbols: the nearest base
that declares a member of its name it inherits, a method of the same signature or any member that is no
method, on the same side of the twin) holds the hidden member's name with `$` and how many hidden slots
of that name it stands over: `name$1`, then `name$2` for one that hides that one. No C# name holds a
`$`, so none of them meets a member the author declares, and the count is unique along one chain by
construction. Only a method this compilation declares takes one: a type the runtime carries is reached
by the names its twin was written with.

Alternatives: the declaring type's name as the suffix (`name$B`). Rejected: two classes of one simple
name in different namespaces can stand in one chain. The depth in the chain as the suffix. Rejected:
it moves when a class is inserted above, and reads as nothing in a stack trace. Renaming the HIDDEN
member instead. Rejected: the hidden one is often .NET's (`GetHashCode`), whose name the runtime reads.

A method that hides one and answers an interface is refused (EQ1007): a call through the interface
reaches the member by the interface's name, which the hidden member holds. Interface dispatch by a name
of its own would be needed to answer it, which no other path has yet.

### A sequence's twin is iterable through its own GetEnumerator()

`IterableTwin` finds the method the type's iteration goes through, the implementation of
`IEnumerable<T>.GetEnumerator()` (or of the non-generic one for a type that implements no
`IEnumerable<T>`), which LINQ, a spread, `string.Join` and a `foreach` over the interface reach in .NET,
and the twin of the type that declares it carries
`[Symbol.iterator]() { return $eq.linq.iterate(this.<its name>()); }`. `iterate` walks what it is handed:
an enumerator the app wrote by its `MoveNext` and `Current`, in a generator whose `finally` disposes it,
so a loop that breaks disposes it as `foreach` does, and anything else iterable (an iterator method's
array, a sequence's own iterator) as it is. `MoveNext` is asked first: a class that is its own enumerator
(`GetEnumerator() => this`) is iterable too, and its own iteration would hand it back without end. A
derived type inherits the member, and its override of the method answers through `this`. How Dart
answers it: an `Iterable` hands out an `Iterator` with `moveNext()` and `current`, .NET's shape, and
dart2js compiles `for-in` to that protocol; eqc writes ES classes and arrays, so the twin speaks
JavaScript's protocol and adapts .NET's enumerator to it.

A `foreach` over the class binds what C# binds, the type's own public `GetEnumerator()` where it has one,
which may walk another sequence than the interface's (measured: a public one yielding 1 beside an explicit
`IEnumerable<int>` one yielding 2 sums 1 in a `foreach` over the class, and 2 in LINQ, `string.Join`, a
spread, `new List<int>(x)` and a `foreach` over the interface). The loop reads that method from the bound
tree (`ForEachStatementInfo.GetEnumeratorMethod`) and, where it is a method of the app's other than the
one the twin's iteration goes through, calls it through `iterate` (`IterableTwin.ForEachSource`); a class
with only a public `GetEnumerator()` is walked the same way. The two methods are two members: the explicit
implementation holds the interface member's name, which every call through the interface reaches, and a
method beside an explicit implementation of its name holds a name of its own
(`TwinMethodName.ExplicitBeside`), for every interface. One that answers another interface too is refused
(EQ1007): a call through that interface would reach the explicit one. Alternative: merge the two into the
public one, which the first draft did, assuming they agree, as .NET's collections keep them. Rejected:
C# does not require it, and every consumer walked the public one (Copilot's review of #708).

The non-generic `IEnumerable.GetEnumerator()` and `IEnumerator.Current` implemented explicitly beside the
generic interface's member are not written: the generic interfaces derive from the non-generic ones, so by
their contract the two answer alike, and written after the generic one the explicit one replaced it and
called itself. Nor is an explicit `Current` beside a public one, the twin holding one property per name.

### An exception class is a class over the runtime's exception base

The module rule no longer keeps an exception out, on the symbol's path and the name's alike. Its twin
extends the twin of the app's exception it derives from, or, where its base is .NET's, the runtime's
`$eq.exceptions.Exception`: an `Error` whose constructor is `System.Exception`'s (the message, the inner
exception) and which tags the instance with the chain `new.target.$types` says, the twin writing its
own chain there from its symbol. `new.target` is the class constructed, so a derived class's exception
carries the derived chain whatever constructor ran.

The base call of a constructor whose base is .NET's hands what a `new` of that .NET type hands the
runtime's `create` (#558, merged while this change was open): the argument bound to `message`, with
the text that constructor writes where none is given or the one given may be null, read from .NET
itself (`ExceptionTypes.FrameworkText`), and the arguments it takes besides, each by its parameter
(`ExceptionTypes.Parts`: a parameter's name, an inner exception, an actual value, a disposed object's
name). An implicit base call reaches the base's parameterless constructor and hands its text the same
way, so `class Closed : InvalidOperationException { }` reads "Operation is not valid due to the current
state of the object.", as `new Closed()` read on main, where the app's exception was built by its
symbol. Every argument is evaluated in the order it is written. One rule for a framework exception's
constructor, whether a `new` or a base call reaches it. Alternative: keep the base call to the message
and the inner exception. Rejected: it answered less than main did once #558 merged, `Exception of type
'App.Closed' was thrown.` for the text above, and `: base("bad", name)` over `ArgumentException` lost
its ` (Parameter 'x')` and its `ParamName`.

`Message` is virtual in .NET, so the base answers it from an accessor on its prototype, which an
override on the twin replaces: a message handed to `Error`'s constructor is the instance's own property,
which would hide every override. The accessor composes it when it is read, as `create` composes it when
it builds, from the message, the parts and the types the instance carries: a missing message is .NET's
default for the class. Every other member the base answers is an accessor too, `InnerException`,
`ParamName` and the rest of the parts, and the `name` the console prints: a member the app's class
declares under one of those names is its own then, an override of the virtual `ParamName` and a `Name`
of the app's included, where an own property of the instance hid it.

A host with no compilation keeps an exception class out by its base's name, as before this change: with
no model, nothing knows the constructors of the base its twin would call, and a module of its own
extended nothing.

### A constructor only .NET's serialization calls is no branch of the twin

Visual Studio's exception template writes `()`, `(string)`, `(string, Exception)` and the protected
`(SerializationInfo, StreamingContext)` of the `ISerializable` pattern. Once an exception class is a
class, the last two take as many arguments, and the twin, which tells its constructors apart by how many
arguments arrive, refused the class (EQ1009). Only .NET's serialization calls that constructor, and the
browser has no `SerializationInfo`, so no `new` there reaches it: it is no branch of the twin, as a
record's copy constructor is not, for an exception class and a plain class alike. The developer who
copied the template builds it as it is written, by the product principle: the SDK knows the platform,
so the developer does not have to. Alternative: refuse it and tell the developer to delete it, which
the first draft of this change did. Rejected: it asked for an edit of what the IDE wrote, for a
constructor nothing in the browser can call.

A generic class's twin knows only its definition (``Failed`1[T]``), and a typed `catch` tells
`Failed<int>` from `Failed<string>`, so a construction of a constructed generic class hands its own chain
to the base before the constructor's body runs (`$eq.exceptions.construct(type, types, ...args)`, which the
base reads off a pending construction of that class): tagged once it returned, which the first draft did,
an exception the constructor threw (`throw this`) missed `catch (Failed<int>)`, and a `Message` it read
named the class (Copilot's review of #708). Alternative: tag every construction at its site and keep no
`$types`. Rejected: a construction through a base call is no site, and the class is what knows what it is.

A type is named as .NET names it at run time, `Type.ToString()` (``App.Failed`1[System.Int32]``,
`App.Outer+Inner`), for the chain a typed `catch` reads and for the message .NET writes where none is
given, `Exception of type '<that name>' was thrown.`: its C# spelling named a type .NET does not.

## Risks / Trade-offs

- A property, a field or an event that hides an inherited member still shares its name with it, and a
  method that is hidden by one shadows it (Non-Goals). Measured: `P p = new Q(); p.Size` over a hiding
  property answers the derived one.
- An exception class of a referenced library, which no twin stands for, is skipped by the twin of an app
  class over it, which extends the runtime's base: its base call hands the message and the inner
  exception by their parameters' names, and no text of its own, which eqc cannot read.
- The default `ToString()` of a class that overrides nothing prints `[object Object]` where .NET prints
  the type's name, which a hiding `ToString` now meets as any class does.
- An exception class is transpiled as any class is, which is the break this change makes: one whose
  members reach what the browser cannot run fails the build as any class does, until it is marked
  `[ServerOnly]`, and two constructors the twin cannot tell apart by their count of arguments are refused
  (EQ1009), as a class's are. Both are said by the build, never in silence.
- A field of the app's class named like a member of the base (`message`) is an own property of the
  instance, which hides the base's accessor, where C# keeps the two apart.
- A collection's own `GetEnumerator()` (`=> items.GetEnumerator()`) has no translation (EQ2004), so a
  sequence that hands out its list's enumerator still fails the build: it yields the items instead.
