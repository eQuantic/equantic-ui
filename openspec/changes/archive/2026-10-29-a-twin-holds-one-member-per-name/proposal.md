# Proposal

Closes #1146, a Bug under #849 ("The transpiler's fences hold on every path, part three").

## Why

eqc names every member of a type's twin by its camelCased name, and a JavaScript class holds one
member per name. EQ1007 refused two METHODS that lower to one name, and a record's or a struct's state
beside a member of its name. Every other pair that lands on one key of the twin passed the build, and
the browser kept one of them: a static field beside a static property or method of its name (the field
takes the accessor's place when the class is defined, and `Seq.Next()` is "not a function"), a
component's field beside its property (the instance field hides the getter, and `Label => label.ToUpper()`
answers `x`), a property beside a method a case apart, an event beside either, an extension block's
property or indexer beside a method, two `operator +` of one type, a conversion beside `FromInt`, and
members on names the twin writes itself (a record's `With(int)`, a class's `ToString(string)`, a
`Constructor()` that leaves a module no browser parses). TypeScript refuses most of them, but only where
a twin is type-checked, which is the runtime's own: an app's module is bundled unchecked.

## What Changes

For a developer, a type whose twin cannot hold two of its members apart fails the build with EQ1007,
naming both members and what to rename, instead of a page that misbehaves in the browser:

- **Every pair of members of one type on one key is refused**, the same lowered name and the same
  static-ness, whatever kind each member is: a field, a constant, a property, an event, a method, an
  operator, a conversion, an extension block's property, indexer or method, and a primary constructor's
  parameter the twin holds. On a class, a record, a struct, a static class, a component and a static
  class nested in a component.
- **What the twin holds as one member stands**: the two accessors of a property, a plain class's field
  beside a member of its name (it moves to `name$`, #396), a positional parameter its record redeclares
  (#546), a method that hides a member and holds a name of its own (#563), a typed `Equals` (#947).
- **A member on a name the twin holds itself is refused**: a class's `constructor` and static
  `prototype`; a record's or a struct's `with`; and `equals`, `getHashCode`, `toString` and `toJSON`,
  which the runtime and JavaScript ask wherever .NET calls `Equals(object)`, `GetHashCode()`,
  `ToString()`, and never a `ToJSON`. An override of `Equals(object)`, `GetHashCode()` or `ToString()`
  stands: the twin writes it in place of its own.
- **The twin writes such an override in place of its own, and nothing else in its place.** A struct's
  `Equals(object)` override was written beside the twin's `equals`; a record's static `ToString(int)`
  suppressed the record's synthesized text, which the twin then wrote nowhere.

Parts reached: eqc only (`OverloadedMethods`, a new `MembersOnOneName` pass, `RecordTypeEmitter`, and
`TwinOwnMembers`, the predicate both read). Neither the public surface nor the developer surface moves.
EQ1007 keeps its code and widens its meaning (docs/DIAGNOSTICS.md and the wiki's Diagnostics page in
every language).

Not in this change: explicit interface implementations (#1139), moving a component's member aside
instead of refusing it (#662), and the same pairs along a class chain (a derived `size()` beside a
base's `Size`).

A break only for an app that held two members on one key, which never worked in the browser: the
migration line is the message's, rename one of them.
