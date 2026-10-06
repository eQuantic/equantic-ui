# Proposal

Closes #591 and #615, sub-issues of #565 (the transpiler's fences hold on every path, continued). It
builds on `a-class-is-built-as-csharp-builds-it`, in the same pull request, which starts a plain
class's state in its constructor as a record's is: that change is what made the shapes of #591
reach a plain class, and what broke the most common shape a C# class has, which this one restores.

## Why

A twin's constructor writes each member's value onto the instance under the member's name. A write
under a name reaches the first accessor of that name along the prototype chain, and an instance's own
value hides every accessor on the prototype, so wherever a member and an accessor share a name the
twin is not the C# type. Measured through the module graph an app's build writes, both sides
executed, on #608's head (87826ea9) and on this branch before this change:

- `private int count; public int Count => count;` threw at `new` on this branch, where #608's head
  answered right: `a-class-is-built-as-csharp-builds-it` declared a class's state for TypeScript only,
  so the constructor's write reached the getter `count` and threw. C# lowers both names to one in the
  twin, and a C# class writes a field, never a property.
- A record's auto-property over a base's computed `virtual` one, or a computed override over a base's
  auto-property, throws at `new` (#591): `TypeError: Attempted to assign to readonly property`.
- A plain class of the same shapes answers the base's value: `new LDerived().Read()`, a computed
  override over an auto-property, answers "base" for "derived".
- An override that declares only a getter loses the setter it inherits: `d.Kind = "x"` over
  `override string Kind => "d"` answers "x|x" for "d|d".
- An override that reads its base's (`get => base.Name + "!"`) reads nothing: "p|z" for "p!|z!".
- A record's or a struct's property that uses `field` runs none of its accessors' bodies (#615):
  `new FRec { X = 5 }` over `set => field = value * 2` holds 5 for 10, and a getter with a body reads
  NaN for 1.

## What Changes

- A plain class's state is declared as class fields with no initializer, which JavaScript defines on
  the instance before the constructor writes them, so the constructor's write lands on the instance's
  own property and never on an accessor or a method of its name. The order the constructor writes them
  in is unchanged.
- A property that a derived type can override or that overrides one, an auto-property declared
  `virtual` or `override`, keeps its value in a store of its own, `$name`, which its accessors on the
  prototype read and write, as a property whose accessors use `field` already does in a class. The
  twin's constructor starts the store, never the property, so no initializer reaches an accessor of
  another level, and every read reaches the override, as a C# property is a method over its backing
  field.
- A record's and a struct's property that uses `field`, or writes one accessor and leaves the other to
  the compiler, keeps its store the same way, its accessors' bodies written over it, as a class's are.
- An override that declares one accessor of a property whose base has both forwards the other to its
  base, as C# inherits it.
- A record compares and hashes a property's store, as .NET compares its backing field, a struct's
  zero zeroes it, and `with` copies it, then writes what the patch names through the setter.
- A twin that keeps a store has `toJSON`, which writes each store under its property's name, read
  through the property, so a server action receives the property by its C# name, as System.Text.Json
  writes it. A class's property that uses `field` was sent under its store's name, `$name`, which the
  server dropped.
- An override auto-property shares its base's store, where C# keeps one per type: `base.Name` in a
  derived type that overrides an auto-property with another reads the override's value. Documented as
  the one difference left.
- No break for an app: its C# compiles to the new form. The public surface of `eQuantic.UI.Compiler`
  gains `Eq.Json` and `ClassBuilder.State`, and `ValueMember` gains the store it lives in (its
  constructor widens, unreleased since #608 widened it). The developer surface does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-classes`: a class's state is written as a field, never through an accessor of its name;
  a property that can be overridden keeps its value in a store of its own; an override declaring one
  accessor forwards the other; a twin with a store writes it in JSON under its property's name.
- `transpiler-records`: a record's or a struct's property that can be overridden, or that uses
  `field`, keeps its value in a store of its own, which its equality compares, its zero zeroes and its
  `with` copies.

## Impact

- eqc: one rule for where a property keeps its value (`PropertyStore`), read by the plain class path
  of `TypeScriptEmitter`, by `RecordTypeEmitter` and by `ValueMembers`; a plain class's state declared
  from the same list its constructor starts; the forwarding accessor; `toJSON` on a twin with a store.
- The runtime: `$eq.json`, a twin's JSON with its stores under their properties' names. Its transpiled
  plain classes regenerate with their state declared as fields, and the code engine's languages keep
  their overridable rules and word sets (`CurlyBraceLanguage.Rules`, each language's `Keywords`) in
  stores under accessors.
- Tests: conformance cases through the module graph for each shape, classes and records, both sides
  executed; a runtime test for `$eq.json`.
- Docs: the wiki's SupportedFeatures page (English and Portuguese) and one docs/LEDGER.md line.
