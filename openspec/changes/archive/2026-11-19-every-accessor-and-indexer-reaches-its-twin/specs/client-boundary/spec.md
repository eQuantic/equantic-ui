## ADDED Requirements

### Requirement: A subscription through a contract a server-only accessor fulfils is refused where it is written

Client code whose `+=` or `-=` binds to an interface's event, or to an event that can be overridden, SHALL
fail the build with EQ2010 at that site, naming the accessor, when an `add` or `remove` marked
`[ServerOnly]` of a type that crosses implements or overrides the accessor the subscription calls, and the
subscription's receiver can be that type, by the rule a call through a contract follows. An explicit
implementation's accessor SHALL count as an implicit one's does. A subscription whose accessor no such
type keeps on the server, the other accessor of the same event included, and one whose receiver no such
type can be SHALL build.

#### Scenario: A subscription through an interface

- **WHEN** `record Signal(int Id) : IHasFired` declares `event Action Fired` with a `[ServerOnly] add`, or
  `class Hidden : IHasFired` declares `event Action IHasFired.Fired` with one, and client code writes
  `public void Take(IHasFired s, Action a) => s.Fired += a;`
- **THEN** the build fails with EQ2010 at the subscription, where .NET answered `1|10` and the browser `0|0`

#### Scenario: A subscription through a virtual event

- **WHEN** `class Derived : Base` overrides `Base`'s `virtual event Action Fired` with a `[ServerOnly] add`, and
  client code writes `public void Take(Base b, Action a) => b.Fired += a;`
- **THEN** the build fails with EQ2010 at the subscription, where .NET answered `1|100` and the browser ran the
  base's accessor and answered `1|1`

#### Scenario: A subscription no server-only accessor answers

- **WHEN** client code writes `s.Fired -= a` on an `IHasFired`, whose `remove` crosses on every type, or
  `s.Fired += a` on a `Sibling : Base` that overrides nothing
- **THEN** the build succeeds

### Requirement: A server mark on an accessor is refused where it is declared

eqc SHALL fail the build with EQ2016 where the accessor is declared, naming the type, the member and the
accessor, for `[ServerAction]` on any accessor, of an event, a property or an indexer, in any type it
writes; and for `[ServerOnly]` on an accessor of a property or an indexer. The mark SHALL be read from
the accessor's symbol, so an alias marks it too. `[ServerOnly]` on an event's `add` or `remove` SHALL keep
the accessor on the server, as before, and SHALL NOT be refused.

#### Scenario: A Server Action on an accessor

- **WHEN** a component declares `public event Action Fired { [ServerAction] add => _f += value; remove => _f -= value; }`
  or `public string Label { [ServerAction] get => "x"; }`, or a class declares an event whose `add` is
  `[ServerAction]`
- **THEN** the build fails with EQ2016 at the declaring file, naming `Fired` or `Label`, where it built, the
  component's `add` was left out of its twin in silence, its getter crossed, and the class's `add` crossed

#### Scenario: A server-only accessor of a property or an indexer

- **WHEN** `record Card(int Id)` declares `public string Note { [ServerOnly] get => "kept on the server"; }`, or
  `class Ledger` declares `public int this[int i] { [ServerOnly] get => i + 42; }`, and client code reads it
- **THEN** the build fails with EQ2016 at the declaring file, naming the member, where the getter crossed into
  the twin with its body

#### Scenario: A server-only event accessor

- **WHEN** an event's `remove` is `[ServerOnly]` and client code subscribes with `+=`
- **THEN** the build succeeds, and no twin holds the `remove`'s body
