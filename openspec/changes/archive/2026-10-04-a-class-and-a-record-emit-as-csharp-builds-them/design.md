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

The twin now runs the C# constructor the call binds and writes every member as C# does, in
declaration order, then runs the body. Over a base, the derived initializers that can do anything
are evaluated into locals before `super()`, which JavaScript requires before `this`, and assigned
after: the order C# runs them in, without touching `this` early.

JavaScript has one constructor, so the twin's is a branch per C# constructor on `arguments.length`,
each reached by the RANGE of counts it takes (its optional parameters counted). A constructor that
does its own work (the primary one, or an explicit one that does not chain) is a ROOT: with one
root, its parameters are the twin's own; with several, the twin takes `...$a`, declares the union of
their parameters once, and the branch that takes the call binds them, calls its base's constructor
with its own arguments (JavaScript and TypeScript take a `super()` per branch when the class
initializes no field of its own, which a twin never does) and runs its body. A constructor that
chains with `: this(…)` binds its own parameters in a block that shadows its root's, evaluates the
chain's arguments into temporaries that cross the block, lands them in the root's parameters, and
runs its own body after its root's: no function holds that C#. A parameter the chain leaves out
takes its default, since it would otherwise hold whatever argument arrived in its place. A range
that meets another's cannot be told apart, and a chain to a constructor that chains in turn has no
root to run, so each is refused with EQ1009 rather than reached by the wrong branch.

A root's `return` ends that constructor and nothing after it in C#, where the body of a constructor
chained to it still runs. So a root body that returns early, under an alternate with a body of its
own, runs in a function invoked in place, whose `return` ends it alone: C# allows no `await` and no
`yield` in a constructor, so nothing in it changes meaning there. It is one of the three functions
this change writes by hand, beside the object initializer's and an indexer setter's.

## An object initializer is applied, not passed

A construction calls the constructor the call binds, with its arguments as the bound tree binds them
(`BoundArguments`: each in its parameter's place, evaluated in the order it is written), then applies
the initializer to what it built. The object lives in a temporary the function the C# is written in
declares (#588's `Temporaries`), and the initializer is a sequence over it,
`($n0 = new X(…), $n0.a = f(), $n0.items.push(g()), $n0)`, so every part is evaluated in that
function, each element is applied before the next one's parts are evaluated, and the member an
element adds to is read before its parts, again for each element, as C# runs them. Where no
statement can declare one (a field's or a property's initializer, a constructor's base call),
which C# lets no part await, a function invoked in place holds the same statements. It was an
arrow that took every part as its argument, which evaluated them all before the first element was
applied, then an arrow per element, which still evaluated an element's parts before the getter of
the member it adds to (both found by Copilot's review of #608). Each
element is an `Add` through the
lowering every call to that `Add` has (`push`, `add`, a twin's own method, and a dictionary's through
the dictionary strategy's one spelling, which refuses a key already there once the runtime's `add`
does), an assignment into the member's object, or a write through the indexer. The `Add` is the one the
bound tree binds: an extension's goes to its home, as a call to it does. An element with no
lowering is refused (EQ1004), never dropped. A class keeps its trailing config object for an initializer that only assigns, which
its constructor applies last, so the vocabulary's hand-written twins are untouched.

`with` is a copy onto the prototype and then the patch (`$eq.withPatch`), which runs no constructor,
as .NET's copy runs none; a struct's and a record's the compiler writes in the vocabulary's namespace
go through the twin's own. A struct's zero is `default(S)`: every struct twin carries `$zero()`, built
member by member without the constructor, which `DefaultValue` names for a default, an array's slot, an
OrDefault and `new S()` through the implicit constructor, a generic struct's and a transpiled struct's
of another assembly included, so a zero runs no initializer, no constructor and no static constructor.

## One predicate decides who has a module

The parser decided that a plain class had a module, and the dependency resolver, which scans files
with no semantic model, decided who imports it; they disagreed on a class with no member (#423). The
rule is now one predicate over the syntax that both read, and what it needs from other declarations
is asked of the caller: whether the CHAIN of bases reaches an attribute, an exception or a type that
stays on the server. The parser asks its model (`System.Attribute`, `System.Exception`,
`[ServerOnly]`), and the resolver walks the chain its scan saw, judging by name only a base outside
it. Names alone gave `class Retry : Failure` over `class Failure : Exception` a module extending one
nothing wrote, and none to `class FakeException`, which derives from no exception. A record and a
struct have a twin whatever they declare (#428); a partial declaration that declares nothing is the
exception, since another declaration carries the members.

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
component emitter. A type that declares a static constructor also starts the initialization first in
its instance constructor and in every static method, accessor and operator, since C# runs that
constructor before the first instance and the first use of any static member. It replaces the class emitter's per-static lazy getter, which survived the shared
library's import cycles; the type initializer is lazy too, so it survives them the same way.

## Not here

- A static iterator method of a type with a static constructor starts it when its sequence is first
  read, where C# starts it at the call.
- A plain class's instance members keep their order: an auto-property initializer is a class field and
  a field initializer runs in the constructor, and a derived class's run after its base's constructor.
- A record's text prints each value as JavaScript's template does: `true` where .NET prints `True`.
- An indexer's names are checked within one declaration, not along the class chain.
- A record's own copy constructor is not run by `with` (#589). A hydrated record has none of its
  private state, which its equality reads (#590). An auto-property and a computed property of one name
  along a record's chain meet on one slot (#591). The records the compiler transpiles into the
  vocabulary's namespace are taken for hand-written twins when an app reaches them as metadata (#592).

## The review

The branch was reviewed at max effort before its pull request: eleven finders, a verifier per finding
that could not be measured, and a sweep, every finding a probe ran on both sides of the real conformance
harness. It found twenty-odd defects in the new emission, seven of them regressions against main, and
two streams fixed them in parallel worktrees before the third built on both: the resolver and the static
initializer (a module per declaration of a name, bases by symbol, `[ServerOnly]` records and structs,
the static constructor in a function of its own, statics in declaration order, constant statics as
their value), the indexer and the writes (one place every writer takes, keys by parameter, the
assignment's answer, `^n`, steps of any type, deconstruction, EQ1007 over an indexer's names, the
initializer's `Add` by the bound tree), and the construction (`BoundArguments`, `$zero()` on every
struct, parameters as variables, the record's text from the symbol, a copy constructor no branch, `this =`
in a struct, EQ1007 over two members of one name). What predates the batch is filed, #582 to #587, #589,
#591 and #592, and so is one consequence of it, #590: a hydrated record has none of the private fields
its equality now compares.
