## ADDED Requirements

### Requirement: A class's state is written as a field, never through an accessor of its name

A plain class's twin SHALL hold each member of its instance state as the instance's own property,
defined before its constructor writes it, so that the constructor's write never reaches an accessor or
a method of the member's name along the chain, as C# writes a field and never a property. The order
the constructor writes them in SHALL stay the order `a-class-is-built-as-csharp-builds-it` gives it.

#### Scenario: A field beside the property that exposes it

- **WHEN** `class Tally { private int count; public int Count => count; public void Inc() => count++; }`
  is built with `new Tally()` and `Inc()` is called twice
- **THEN** `Count` answers `2`, as in .NET, and the construction does not throw

### Requirement: A property that can be overridden keeps its value in a store of its own

A class's auto-property declared `virtual` or `override`, and its property with a backing field and an
accessor of its own (one that uses `field`, or that writes one accessor and leaves the other to the
compiler), SHALL keep its value in a store of its own that accessors on the twin's prototype read and
write, and the twin's constructor SHALL start the store, never the property: every read of the
property SHALL reach the most derived accessor, and no initializer SHALL reach an accessor that another
type declares. An override that declares one accessor of a property whose base has both SHALL forward
the other to its base. A twin that keeps a store SHALL write it in JSON under its property's name,
read through the property.

#### Scenario: An auto-property over a computed one

- **WHEN** `class KDerived : KBase { public override string Kind { get; } = "derived"; }` over
  `class KBase { public virtual string Kind => "base"; public string Read() => Kind; }`
- **THEN** `new KDerived().Kind` and `new KDerived().Read()` answer `derived`, as in .NET

#### Scenario: A computed override over an auto-property

- **WHEN** `class LDerived : LBase { public override string Kind => "derived"; }` over
  `class LBase { public virtual string Kind { get; set; } = "base"; public string Read() => Kind; }`
- **THEN** `new LDerived().Read()` and `((LBase)new LDerived()).Kind` answer `derived`, as in .NET

#### Scenario: An override that declares only a getter

- **WHEN** `class WDerived : WBase { public override string Kind => "d"; }` over
  `class WBase { public virtual string Kind { get; set; } = "b"; }`, and `d.Kind = "x"` is written
- **THEN** the write reaches the base's setter, and `d.Kind` and `((WBase)d).Kind` answer `d`, as in .NET

#### Scenario: An override that reads its base's

- **WHEN** `class PDerived : PBase { public override string Name { get => base.Name + "!"; set => base.Name = value; } }`
  over `class PBase { public virtual string Name { get; set; } = "p"; }`
- **THEN** `new PDerived().Name` answers `p!`, and after `Name = "z"` it answers `z!`, as in .NET

#### Scenario: The JSON of a twin with a store

- **WHEN** an instance of a twin that keeps `kind` in its store `$kind` beside its own `count` is
  serialized with `JSON.stringify`
- **THEN** the JSON holds `kind`, read through the property, and `count`, and no `$kind`
