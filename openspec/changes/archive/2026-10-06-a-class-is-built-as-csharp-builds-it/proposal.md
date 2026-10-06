# Proposal

Closes #571, #582, #583, #587 and #592, sub-issues of #565 (the transpiler's fences hold on every path,
continued). The class and record emission of #608 is what this builds on: a record's and a struct's
twin are built as C# builds them since then, and a plain class is not yet.

## Why

Each of these compiles, emits and runs differently in the browser with a green build, measured on
#608's head (87826ea9) through the module graph an app's build writes, both sides executed:

- A plain class keeps only its widest constructor and calls its base's with no arguments, and a C#
  12 primary constructor on a class is not read at all (#583): `new Money().Cents` over
  `Money() : this(100)` is 100 in .NET and null in the browser, `new D0(3).X` over `: base(x * 2)` is
  6 and null, and `new Greeter("ada").Hello()` is "hi ada" and "hi ".
- A derived class runs its instance initializers after its base's constructor, where C# runs them
  before (#571): .NET logs `derived-init derived-prop base-init base-ctor`, the browser
  `base-init base-ctor derived-prop derived-init`.
- A plain class takes an object initializer as a trailing config object, an argument of its
  constructor, so the values are evaluated before the constructor runs, and a property's initializer
  runs before a field's (#582): `new Plain { B = Plain.N }` holds `1|2|2` in .NET and `2|0|2` in the
  browser.
- An exception built with an object initializer drops it (#587): `new Retry { Attempts = 3 }.Attempts`
  is 3 in .NET and null in the browser.
- A type of the vocabulary's assembly whose twin the runtime transpiles (the spreadsheet's and the
  forms' models) is taken by an app for a hand-written twin, since both share the vocabulary's
  namespace (#592): `new CellRef(1, 2) { Col = 3 }` is `1|3` in .NET and `1|2` in the browser, and
  `default(CellRef)` throws. A plain class built through a config object would make it worse:
  `new SheetController(10, 4) { Changed = … }` would drop its handler.

## What Changes

- A plain class's twin constructor is the C# constructor, by the dispatch a record's twin has: a
  branch per C# constructor on the counts of arguments it takes, a `: this(…)` chain reaching its
  root, a `: base(…)` chain and a base clause passing their arguments to the base's constructor as the
  bound tree binds them, a primary constructor binding its parameters, and EQ1009 for a count two
  constructors share.
- A class's instance fields and properties start as their declarations say, in declaration order, in
  its constructor: a derived class's before its base's constructor, each one that runs code
  evaluated first and assigned once the base's constructor returns.
- `new C(…) { … }` for a class whose twin eqc writes builds the object with the constructor the call
  binds, then applies the object initializer to it, as a record's is (#413). The trailing config
  object stays for a component's props and for the vocabulary's hand-written twins, whose classes
  take one.
- An exception built with an object initializer has it applied once it is built.
- The vocabulary marks the types whose twins the runtime transpiles `[TwinIsTranspiled]`, and every
  decision about a twin eqc writes reads the mark where an app reaches the type as metadata: its
  construction, its zero, its indexer and its type test.
- A primary constructor's parameter a member reads is held on the instance, as a struct's is, and a
  held parameter that lands on the name of another member is refused with EQ1007, as it is for a
  struct.
- **BREAKING** (preview): a plain class's twin no longer takes a trailing config object. TypeScript
  written by hand that built a transpiled class with one sets those members after `new` instead. An
  app never sees it, since its C# compiles to the new form, and nothing in the runtime builds one
  that way. `eQuantic.UI.Primitives` gains `TwinIsTranspiledAttribute`; the public surface of
  `eQuantic.UI.Compiler` and the developer surface do not move.

## Capabilities

### New Capabilities

- `transpiler-classes`: how eqc builds a plain class in the browser, its constructors, the order of
  its initializers, its primary constructor and the object initializer applied to it.

### Modified Capabilities

- `transpiler-exceptions`: an exception built with an object initializer has it applied.

## Impact

- eqc: the constructor a record's twin is built with moves out of `RecordTypeEmitter` into one
  builder written as IR, which the record emitter and the plain class path of `TypeScriptEmitter`
  share; `ObjectCreationStrategy` builds a plain class and then applies its initializer;
  `ExceptionCreationStrategy` applies an exception's; the EQ1007 check reads a class's held parameters.
- The runtime's transpiled classes (the code engine's, the forms', the sheet's, the markdown and
  mermaid parsers') regenerate with the new constructors. No hand-written runtime code changes.
- Tests: conformance cases through the module graph for each issue, both sides executed; the
  transpiled pins of the record twins move by layout only.
- Docs: the wiki's Compiler and SupportedFeatures pages (English and Portuguese), docs/DIAGNOSTICS.md
  for EQ1007 and EQ1009 on a class, and one docs/LEDGER.md line.
