## MODIFIED Requirements

### Requirement: An event's accessors are called as C# calls them

A subscription to an event, `+=`, SHALL run the event's add accessor and an unsubscription, `-=`, its remove
accessor, as C# compiles them: a field-like event's composes its invocation list, and an event declared with `add`
and `remove` accessors, an explicit interface's included, SHALL run its own, whether the subscription binds the
event through its type or through an interface. An event declared with accessors in a class, a static class, a
record, a struct or a component, a primitive and an abstract base among the components, SHALL run its own, an
explicit interface's and a static one included. An accessor marked `[ServerOnly]` SHALL stay on the server,
written into no twin, and a subscription from client code that calls it SHALL be refused with EQ2010; a
`[ServerAction]` on an accessor is refused where it is declared (EQ2016), in every type, and makes no action.
`+=` and `-=` on a delegate, a field,
a local or a property of one, SHALL compose its invocation list as on an event.

#### Scenario: A handler subscribed through an interface's explicit event

- **WHEN** `Bell` implements `event Action IRinger.Rung { add => _rung += value; remove => _rung -= value; }`,
  a handler adding 100 is subscribed through an `IRinger`, and the bell rings
- **THEN** the handler runs, as in .NET

#### Scenario: A field-like event behind the same interface

- **WHEN** `Chime` implements `IRinger.Rung` as `public event Action? Rung;`, and a handler is subscribed through an `IRinger`
- **THEN** the handler runs when the chime rings

#### Scenario: A class event's own accessors

- **WHEN** an event's `add` keeps its handlers in a list and counts them, two handlers are subscribed and one removed
- **THEN** the posts reach the handlers the list holds, and the count is .NET's

#### Scenario: A record's event with accessors

- **WHEN** `record Signal(int Id)` declares `event Action Fired { add => _fired += value; remove => _fired -= value; }`,
  and the code runs `var count = 0; var s = new Signal(1); Action add = () => count++; s.Fired += add;
  s.Fired += () => count += 10; s.Fire(); s.Fired -= add; s.Fire(); return count;`
- **THEN** it answers 21, as in .NET, where it threw

#### Scenario: A struct's event with accessors

- **WHEN** `struct Pulse` declares `event Action<int> Beat` with the same accessors, two handlers adding `n` and
  `n * 10` are subscribed, the pulse fires 2, the first handler is removed, and it fires 3
- **THEN** it answers 52, as in .NET, where it threw

#### Scenario: A component's event with accessors

- **WHEN** a `StatefulComponent` `Meter` declares `event Action<int> Moved` with the same accessors, and the same
  subscriptions, moves and removal run on a `new Meter()`
- **THEN** it answers 52, as in .NET, where it threw

#### Scenario: An abstract component base's event with accessors

- **WHEN** `abstract class Gauge : StatefulComponent` holds only a protected delegate and `event Action<int> Read`
  with accessors, and the same subscriptions, raises and removal run on a `Dial : Gauge`
- **THEN** it answers 52, as in .NET, where it threw

#### Scenario: A primitive component's event with accessors

- **WHEN** `Lamp : HtmlElement` declares `event Action<int> Lit` with accessors, and the same subscriptions run
- **THEN** it answers 52, as in .NET

#### Scenario: An event's accessor that stays on the server

- **WHEN** a class, a record, a struct and a component each declare an event whose `remove` is `[ServerOnly]` and
  reads the file system, and client code subscribes with `+=`
- **THEN** the build accepts every module, no twin holds the server-only body, and the handlers run as in .NET

#### Scenario: A subscription that calls an accessor kept on the server

- **WHEN** client code writes `s.Fired -= a` where `Fired`'s `remove` is `[ServerOnly]`, on a class, a record, a
  struct or a component
- **THEN** the build refuses it with EQ2010

#### Scenario: A null-conditional subscription to an event with accessors

- **WHEN** a class's, a record's and a component's event declared with accessors is subscribed and unsubscribed
  through `x?.E += h` and `x?.E -= h`, with `x` non-null and with `x` null
- **THEN** a non-null receiver's accessors run as in .NET, and a null receiver subscribes nothing and evaluates the
  handler never

## ADDED Requirements

### Requirement: Every member a component's twin holds is written on every component path

A component's twin SHALL hold the indexers, the statics that initialize in order, and the properties with
a body and the static auto-properties its type declares, whichever kind of component it is: a concrete
component, an abstract base however few members it declares, and a primitive one (deriving from
`HtmlElement`). An indexer SHALL be its twin's `item` and `setItem`, and a static that initializes in order
SHALL start as .NET starts it. A concrete component's twin SHALL keep the members it writes today, in the
same order.

#### Scenario: An abstract component base's indexer

- **WHEN** `abstract class Grid : StatelessComponent` declares only `protected int[] _c = new int[3];` and
  `public int this[int i] { get => _c[i]; set => _c[i] = value * 2; }`, `class Cells : Grid` declares `Build`,
  and the code runs `var c = new Cells(); c[1] = 4; var y = (c[2] = 5); return c[1] * 100 + c[2] + y;`
- **THEN** it answers `815`, as in .NET, where the browser threw "item is not a function"

#### Scenario: A primitive component's indexer

- **WHEN** `class Strip : HtmlElement` declares `public int this[int i] { get => i * 10; set => Last = value + i; }`
  and `public int Last { get; set; }`, and the code runs `var s = new Strip(); s[3] = 4; return s[2] + s.Last;`
- **THEN** it answers `27`, as in .NET, where the browser threw

#### Scenario: An abstract component base's statics that initialize in order

- **WHEN** `abstract class Board : StatelessComponent` declares only
  `protected static readonly int[] Seed = new[] { 1, 2, 3 };`, `class Tiles : Board` declares
  `public int At(int i) => Seed[i];`, and the code runs `return new Tiles().At(1);`
- **THEN** it answers `2`, as in .NET, where the base's twin held no `seed` and the browser threw

#### Scenario: A primitive component's bodied and static properties

- **WHEN** `Strip` also declares `public int Twice => Last * 2;` and `public static int Count { get; set; } = 7;`,
  and the code runs `var s = new Strip { Last = 3 }; return s.Twice + Strip.Count;`
- **THEN** it answers `13`, as in .NET, where the browser answered `NaN`

#### Scenario: A concrete component's twin is unchanged

- **WHEN** the shared components, charts and spreadsheets are transpiled
- **THEN** every pinned module is byte for byte what it was
