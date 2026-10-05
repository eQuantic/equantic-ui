# Proposal

Closes #413, #417, #423, #427, #428, #462 and #546, sub-issues of #164 (the transpiler's fences hold
on every path).

## Why

Each of these compiled, emitted, and ran differently in the browser, measured on main (ca3dadf5)
with both sides executed, most of them with a green build:

- A plain class that declares no member got no module while every module constructing it imported
  one, so the bundle failed (#423). A record that declares only methods, only an indexer or nothing
  got no twin, so `new Animal()` named nothing and every record over it was unextended (#428).
- An instance indexer was written into no twin, so `grid[3]` read a property named "3" (#427).
- A positional record that declares its own property for a parameter listed the member twice, and
  its module did not load (#546).
- The twin's constructor took one argument per member, with each initializer as that parameter's
  default, so an initializer ran only when nothing passed the member: `new Counted { B = 10 }` ran one
  `++N` where C# runs two, a `with` ran them all again, and an explicit constructor's body never ran
  (#413). A nested collection initializer handed the member a new collection instead of adding to the
  one it held (#462).
- Statics were defined where declared, so `static int A = B + 1; static int B = 2;` answered NaN,
  and a class's statics each initialized on its own first read, in no order (#417).

## What Changes

- Every record and struct has a twin, and a plain class has a module whatever it declares; the rule
  is one predicate the parser and the dependency resolver both read.
- A twin's constructor is the C# constructor: it runs every initializer in declaration order (a
  derived record's before its base's constructor), then the body. Each C# constructor is a branch on
  the counts of arguments it takes, one with a body of its own and one that chains with `: this(…)`
  alike; one the twin cannot tell apart is refused with the new error EQ1009.
- A construction calls the constructor the call binds, then applies the object initializer to what
  it built: an assignment, an `Add` per element of a nested collection initializer, an assignment
  into the object a nested object initializer names, and an entry through the type's indexer. A
  `with` copies and runs nothing. A struct's zero runs none of its initializers.
- A record compares its runtime type, its base's members and its own fields, private ones included,
  and prints as .NET's `PrintMembers` does: its base's members first, a computed property included,
  `Name { }` for none. A positional parameter whose property the record declares is one member.
- An instance indexer is the twin's `item` and `setItem`, and every access bound to it calls them, a
  default interface indexer included (no longer EQ1008). A second indexer, or a method on either
  name beside one, is EQ1007.
- A type whose statics can observe one another starts them all at their zero and runs the
  initializers in declaration order, then its static constructor, on first use; a record, a struct,
  a class, a static class and a component alike. A type with a static constructor runs it before its
  first instance and the first use of any static member.
- **BREAKING** (preview), in `eQuantic.UI.Compiler`: `ValueMember` gains `Declaration`,
  `RecordTypeEmitter.CanEmit` takes the type's symbol, `ComponentDependencyResolver` takes the
  project's compilation, and `Eq.TypeInitialization` is new (`PublicAPI.Unshipped.txt`).
  A record's twin no longer takes one argument per member, which an app never sees, since its C#
  constructions compile to the new form: only TypeScript written by hand against a twin moves, and the
  runtime's own specs now pass the C# constructor's arguments and set the rest with `Object.assign`
  (`Object.assign(new CultureOption('en', 'English'), { short: 'EN' })`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-records`: construction, initializer order, `with`, equality, text, a struct's zero,
  constructors, indexers, statics, and which types get a twin or a module.
- `transpiler-interfaces`: a default indexer is written into the twin instead of refused.

## Impact

- **eqc**: `RecordTypeEmitter`, `TypeScriptEmitter`, a new `TypeInitializer`, `MethodLowering`,
  `ObjectCreationStrategy` with a new `ObjectInitializer`, a new `Indexer` used by the element access,
  assignment, compound, step and coalescing strategies, `DefaultValue`, `ArrayCreationStrategy`,
  `ComponentParser`, `ComponentDependencyResolver` with a new `PlainClassModule`, `OverloadedMethods`.
- **Runtime**: the shared library's transpiled modules are regenerated (their twins' constructors,
  statics and text); two specs build a record through `Object.assign`.
- **Diagnostics**: EQ1009 is new; EQ1007 covers an indexer's names; EQ1008 no longer refuses a
  default indexer.
- **Public surface**: `ValueMember`'s shape (`PublicAPI.Unshipped.txt`). The developer surface does
  not move.
