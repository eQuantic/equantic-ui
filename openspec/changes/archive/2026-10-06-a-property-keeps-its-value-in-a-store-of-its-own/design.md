# Design

## Context

A twin's constructor starts a type's state in declaration order, its own before its base's
constructor (`TwinConstructor`, shared by records, structs and, since
`a-class-is-built-as-csharp-builds-it`, plain classes). An auto-property's state is written onto the
instance under the property's own name (`this.kind = …`), and declared for TypeScript only
(`declare`), which defines nothing.

In C# a property is a pair of methods, virtual where the property is, and an auto-property's are the
compiler's, over a backing field that belongs to the type declaring it. Its initializer writes that
field, never the property. In JavaScript a write under the property's name is a `[[Set]]`, which
reaches the first accessor of that name along the prototype chain, and the instance's own data
property, once written, wins over every accessor on the prototype. So along a chain where one level
computes a property and another keeps it:

- the derived type's constructor writes over the base's getter, which has no setter, and throws;
- the base's constructor writes over the derived type's getter, and throws;
- where it does not throw, the instance's own property hides the override, and the base answers.

A plain class did not throw before `a-class-is-built-as-csharp-builds-it`: its state was class
fields, which JavaScript DEFINES rather than sets, so the instance's own property hid the accessor
instead, and some shapes answered right by accident. That change declared it for TypeScript only, as
a record's is, and the most common of those shapes, a field beside the property that exposes it
(`int count; int Count => count;`, one name in the twin), threw at `new`. A record refuses that shape
with EQ1007, and a class has never been held to it (#396). A property whose accessors use `field`
already keeps its value apart in a class, in a store named `$name` that its accessors read and write,
and the constructor starts the store. A record's does not: it is held under the property's name, and
its accessors' bodies are not written (#615).

## Goals / Non-Goals

**Goals:**

- Every read of a property that can be overridden reaches the override, and no initializer reaches
  an accessor of another level, for a class and a record alike.
- One rule for where a property keeps its value, read by every path that starts, compares, copies,
  zeroes or serializes it.
- The server receives a twin's properties by their C# names, a store's included.

**Non-Goals:**

- A member that hides another with `new`. JavaScript has one member per name on an object, and which
  of two members a call reaches is decided by its static type in C#.
- A component's properties, which are its props and keep the runtime's contract.
- A base constructor that reads a derived type's member through a virtual one, documented by
  `a-class-is-built-as-csharp-builds-it`.
- A record's own copy constructor (#589).

## Decisions

### A class's state is defined, then written

A plain class declares each member of its state as a class field with no initializer
(`count!: number;`, and `count;` in JavaScript), from the same list its constructor starts. JavaScript
defines the field on the instance as it is built, at the start of its construction or as soon as
`super()` returns, and the constructor then writes its value, in the order it always did. The write
lands on the instance's own property, never on an accessor or a method of its name along the chain,
as C# writes a field and never a property, and as the twin did before
`a-class-is-built-as-csharp-builds-it`. The embedded bun defines a field declared this way when it
bundles an app, as the runtime's own build does (`useDefineForClassFields`), measured on bun 1.3.14.
A derived class's field is defined after `super()` returns, before the constructor writes it, so
nothing the base's constructor wrote is lost that the constructor did not write again anyway.

A record keeps `declare`: EQ1007 refuses two of its members on one name, and the stores below take
care of the chain.

Alternative: `Object.defineProperty` in the constructor for each member. Rejected: a call per member
on every construction, where a class field is what an engine optimizes best.

### Where a property keeps its value

One rule, asked of the declaration (`PropertyStore`):

- an abstract property and a computed one keep nothing;
- a property whose accessors use `field`, and an auto-property declared `virtual` or `override`
  (`sealed override` included), keep a store of their own, `$name`, which accessors on the prototype
  read and write;
- every other auto-property keeps its value under its own name, as it does today.

The syntax answers exactly: a property C# lets a derived type override says `virtual` or `abstract`,
and one that overrides says `override`. An auto-property that says neither cannot be reached by an
override, so it stays a plain member, the cheapest read there is. In the runtime, only the code
engine's languages declare such properties (`CurlyBraceLanguage.Rules`, overridden by each language,
and each language's word sets), and they read the same values through their accessors.

The accessors of a store are the property's own where it writes them (`field`), and the compiler's
otherwise: a getter that returns the store and a setter that writes it, the setter written even for a
property C# declares get-only. C# refuses every write to a get-only property when the C# compiles, so
in the browser the setter is reached only by the runtime: by hydration, which adopts a member through
its setter and skips one that only has a getter, as it skips a computed property, and by `with`'s
copy.

Alternatives considered:

- Define, rather than set, each member the constructor starts (`Object.defineProperty`, or class
  fields again). Rejected: it brings back the old accident. The instance's own property still hides a
  computed override, which then answers the base's value.
- Every auto-property in a store. Rejected: every twin's shape changes, every read goes through an
  accessor and every value crosses the wire through `toJSON`, for no shape that needs it.
- A store per type that declares it (`$name$Type`), as C# keeps a backing field per type. Rejected: a
  twin rebuilt from the server's JSON is built without its constructor and adopts the property once,
  through the most derived setter, so a store per type leaves every base's store undefined. The
  server's hydration contract names a `field` store `$name` too. The one difference left: in a type
  that overrides an auto-property with another, `base.Name` reads the override's value, where C#
  reads the base's own field.

### An override that declares one accessor

C# lets an override declare only the getter, or only the setter, of a property whose base has both,
and inherits the other. A JavaScript accessor is one property with both halves, so a getter alone on
the derived prototype hides the base's setter, and a write throws. An override that declares one
accessor where its overridden property has the other gets a forwarding one: `set kind(value) {
super.kind = value; }`, or `get kind() { return super.kind; }`. The overridden property is asked of the
model, which walks the overrides to the declaration that has the accessor.

### What compares, zeroes and copies a record

A record's equality compares, and its hash combines, each member's store, as .NET's synthesized
`Equals` compares backing fields: a computed override does not change what two records compare. A
struct's zero zeroes the store. `with` copies the instance's own properties, stores included, then
writes what the patch names through the setter, which is what .NET's `with` calls. Its text reads the
properties, as `PrintMembers` does.

### The JSON of a twin with a store

`JSON.stringify` writes an object's own enumerable properties, so a twin with a store wrote `$name`
and not the property, and the server bound nothing to it. A twin that declares a store has
`toJSON() { return $eq.json(this); }`, inherited by the types that derive from it. `$eq.json` copies
the own properties, writes each `$name` under `name`, read through the property, as System.Text.Json
writes a property through its getter, and copies a `$name` with no accessor of that name along the
chain as it is. The `$` is decisive: no C# member's name can begin with one. Each key is defined, not
assigned, as the runtime's other wire objects are, so a member called `__proto__` stays a member.

## Risks / Trade-offs

- [A property in a store reads through an accessor] → only where the property is virtual, overrides
  one or uses `field`; engines inline a getter that returns a property.
- [The setter of a get-only property is reachable from TypeScript written by hand] → only the C#
  compiler's checks stand between an app and that setter, as they stand between it and every other
  member of a twin.
- [`base.Name` over an overridden auto-property] → documented on the wiki's SupportedFeatures page.
