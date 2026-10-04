# Design

## How Dart does it

docs/FLUTTER-PARITY.md has no row for a transpiler's semantics, so the question goes to Dart, which
Flutter's web target compiles to JavaScript.

- A Dart constructor evaluates its fields' initializers, then its initializer list, then calls the
  super constructor, then runs the bodies from the base down: a derived class's initializers run
  before its base's constructor, as C#'s do. The constructor is the one place a member starts, and a
  call site never repeats what it starts as.
- A Dart cascade (`Box()..items.add(1)`) is applied to the object the constructor returned, which is
  what a C# object initializer is.
- Dart has no overloading: a second way to build a type is a named or a factory constructor, which is
  what EQ1009 recommends (`static Coded FromText(string text)`).
- dart2js lowers `operator []` and `operator []=` to methods, as an indexer lowers here.
- Dart initializes each static field lazily on its own first read, which is what the class emitter's
  lazy getter did. C# initializes a TYPE: every static at its zero, then the initializers in
  declaration order, once. That is the semantics to keep, so the mechanism is C#'s, not Dart's.

## The twin's constructor is the C# constructor

The twin took one argument per member, with each initializer as that parameter's default. JavaScript
evaluates a default only for an argument that is missing, so every rule that depends on an
initializer running (#413) failed: an object initializer skipped the initializer of the member it set,
a `with` rebuilt the record and ran them all again, and an explicit constructor had nowhere to go.

The twin now takes the parameters of the C# constructor it is (the primary one, or the one explicit
constructor that runs a body of its own) and writes every member as C# does, in declaration order,
then runs the body. Over a base, the derived initializers that can do anything are evaluated into
locals before `super()`, which JavaScript requires before `this`, and assigned after: the order C#
runs them in, without touching `this` early.

JavaScript has one constructor, so another constructor that chains with `: this(…)` is a branch on
`arguments.length`. Each is reached by the RANGE of counts it takes (its optional parameters
counted), and a range that meets another's cannot be told apart, so it is refused with EQ1009 rather
than reached by the wrong branch. A parameter the chain leaves out takes its default, since it would
otherwise hold whatever argument arrived in its place. A second constructor with a body of its own is
refused too: there is no constructor for it to be.

## An object initializer is applied, not passed

A construction calls the constructor the call binds, with the arguments in its parameters' order,
then applies the initializer to what it built. Assignments alone go through `Object.assign`, which
evaluates the values after the constructor and calls a setter where the twin has one. Anything else
(a nested collection initializer, a nested object initializer, an entry) is written as statements over
the object, in an arrow that returns it: each element is an `Add` through the lowering that `Add` has
everywhere (`push`, `add`, the dictionary's `set`, a twin's own method), an assignment into the
member's object, or a write through the indexer. An element with no lowering is refused (EQ1004),
never dropped. A class keeps its trailing config object for an initializer that only assigns, which
its constructor applies last, so the vocabulary's hand-written twins are untouched.

`with` is a copy onto the prototype and then the patch (`$eq.withPatch`), which runs no constructor,
as .NET's copy runs none. A struct's zero is `default(S)`: when the twin's constructor does more than
zero it, the twin carries `$zero()`, built without the constructor, which `DefaultValue` names for a
default, an array's slot and an OrDefault.

## One predicate decides who has a module

The parser decided that a plain class had a module, and the dependency resolver, which scans files
with no semantic model, decided who imports it; they disagreed on a class with no member (#423). The
rule is now one predicate over the syntax that both read, and the one fact it needs from another file,
whether a base stays on the server, is asked of the caller. A record and a struct have a twin
whatever they declare (#428); a partial declaration that declares nothing is the exception, since
another declaration carries the members.

## The state, the text and the equality are three lists

A record's state (what the constructor writes, what `equals` compares, what `with` copies) is every
instance field, a private one included, and a positional parameter only when the record makes a
property of it: a parameter whose name the body declares is the body's member (#546), and one that
names a property of its base is the base's. Its text is a different list, .NET's `PrintMembers`: the
base's first, then the positional properties, then the public fields and readable public properties
in declaration order, computed ones included. Equality compares the runtime type (the root's
`o.constructor === this.constructor`, reached through `super.equals` from a derived record), as
`EqualityContract` does.

## An indexer is two methods

An instance indexer is `item(keys)` and `setItem(keys, value)`, the extension indexer's `item` being
the precedent. The setter answers the value it wrote, so a call site uses it as the assignment's value,
and a setter that returns early runs in an arrow of its own. Every access the bound tree binds to such
an indexer calls them; a compound, a step and a coalescing assignment go through the same
single-evaluation templates the dictionary's entries use. The two names are the indexer's, so EQ1007
refuses a second indexer or a method on either name in the same type.

## A type initializer

A type whose statics can observe one another (any initializer that is not a constant, or a static
constructor) holds every static in a `$slots` object that `$init()` builds the first time one of them
is read or written: zeros first, then the initializers in declaration order, then the static
constructor's body. Each static is an accessor pair over its slot, so a read during the initialization
sees what C# sees, and a cycle between two types reads the zero C# reads. A type whose initializers are
constants keeps plain fields. One mechanism serves the record emitter, the class emitter and the
component emitter. It replaces the class emitter's per-static lazy getter, which survived the shared
library's import cycles; the type initializer is lazy too, so it survives them the same way.

## Not here

- The initialization starts on a read or a write of a static. A static method that touches none of
  them, or an instance constructed, does not start it, where C# runs a static constructor on either;
  only a static constructor's effect outside its own type can tell.
- A plain class's instance members keep their order: an auto-property initializer is a class field and
  a field initializer runs in the constructor, and a derived class's run after its base's constructor.
- A record's text prints each value as JavaScript's template does: `true` where .NET prints `True`.
- An indexer's names are checked within one declaration, not along the class chain.
