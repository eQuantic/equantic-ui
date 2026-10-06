## ADDED Requirements

### Requirement: A record's property that can be overridden or uses field keeps a store of its own

A record's or a struct's auto-property declared `virtual` or `override`, and its property with a
backing field and an accessor of its own (one that uses `field`, or that writes one accessor and
leaves the other to the compiler), SHALL keep its value in a store of its own that accessors on the
twin's prototype read and write, the twin's constructor starting the store and never the property, and
an override that declares one accessor of a property whose base has both SHALL forward the other to its
base. The store SHALL be what the record's equality compares and its hash combines, as .NET compares
a backing field, what a struct's zero zeroes, and what `with` copies before it writes the members the
patch names through their setters. A twin that keeps a store SHALL write it in JSON under its
property's name.

#### Scenario: An auto-property over a computed one, and the reverse

- **WHEN** `record RDerived : RBase { public override string Kind { get; } = "derived"; }` over
  `record RBase { public virtual string Kind => "base"; public string Read() => Kind; }`, and
  `record RDerived2 : RBase2 { public override string Kind => "derived"; }` over
  `record RBase2 { public virtual string Kind { get; init; } = "base"; public string Read() => Kind; }`
- **THEN** both build, and `Kind` and `Read()` answer `derived` on each, as in .NET

#### Scenario: A with over a computed override

- **WHEN** `var a = new RDerived2(); var b = a with { Kind = "x" };`
- **THEN** `a == b` is false, `b.Read()` answers `derived` and `a == new RDerived2()` is true, as in .NET

#### Scenario: A property that uses field

- **WHEN** `record FRec { public int X { get; set => field = value * 2; } }` is built with
  `new FRec { X = 5 }` and copied with `with { X = 7 }`, and
  `record GRec { public int X { get => field + 1; set => field = value; } }` is built with `new GRec()`
- **THEN** the first holds `10` and its copy `14`, and prints `FRec { X = 10 }`, and `new GRec().X`
  answers `1`, as in .NET

#### Scenario: A struct's property that uses field

- **WHEN** `struct FSt { public int X { get; set => field = value * 2; } }` is built with
  `new FSt { X = 5 }`, and `FSt z = default;`
- **THEN** the first holds `10` and `z.X` is `0`, as in .NET
