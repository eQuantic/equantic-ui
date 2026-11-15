## MODIFIED Requirements

### Requirement: A class keeps the default interface members it relies on

eqc SHALL write every default interface member a class takes without declaring it into the class's
twin, as the class's own member, whichever emitter writes the class: a plain class, a record, a
struct or a component. A default property, indexer, method and event SHALL each be written, an
event's `add` and `remove` as the class's own event's accessors are, so a subscription through the
interface runs the default's accessor. The implementation SHALL be the one C# picks for the class. A
member whose implementation a base class takes too SHALL be left to the base class's twin, and one the
base class answers with a less specific default SHALL be written into the class's twin, over the base's.

#### Scenario: A default property and a default method

- **WHEN** `record Circle(double R) : IShape` declares `Name` and `Area()`, and `IShape` declares
  `int Sides => 0;` and `string Describe() => Name + ":" + Area() + ":" + Sides;`
- **THEN** `((IShape)new Circle(2)).Describe()` answers `circle:12:0` in the browser, as in .NET

#### Scenario: A member the class declares wins

- **WHEN** `record Square(double S) : IShape` declares `int Sides => 4;`
- **THEN** `((IShape)new Square(3)).Describe()` answers `square:9:4`, and the twin has one `sides`

#### Scenario: A derived interface overrides a base interface's default

- **WHEN** `interface ILabelled : IShape` declares `string IShape.Describe() => "[" + Name + "]";`
  and `record Tag(string Text) : ILabelled`
- **THEN** `((IShape)new Tag("x")).Describe()` answers `[x]`

#### Scenario: A derived class takes a more specific default than its base

- **WHEN** `interface ILoudSpeaker : ISpeaker` declares `string ISpeaker.Speak() => "LOUD";`,
  `record SoftSpeaker : ISpeaker` takes `ISpeaker`'s default `Speak()`, and
  `record LoudSpeaker : SoftSpeaker, ILoudSpeaker`
- **THEN** `((ISpeaker)new LoudSpeaker()).Speak()` answers `LOUD`, and
  `((ISpeaker)new SoftSpeaker()).Speak()` answers `soft`

#### Scenario: A default calls a private member of its interface

- **WHEN** `IGreeter.Greet()` calls `private string Wrap(string text)` of the same interface
- **THEN** `((IGreeter)new Greeter("ana")).Greet()` answers `<hi ana>`

#### Scenario: A base record named without arguments

- **WHEN** `record GreetBase : IGreet` declares `int N`, and `record GreetDerived : GreetBase;`
- **THEN** `((IGreet)new GreetDerived()).Hello()` answers `hi`, and `new GreetDerived() is GreetBase`
  is true

#### Scenario: A record with no member of its own

- **WHEN** `record Nobody : IGreet;` and `IGreet` declares only `string Hello() => "hi";`
- **THEN** `((IGreet)new Nobody()).Hello()` answers `hi`

#### Scenario: An optional parameter and a setter

- **WHEN** a default method declares `string Mark(string suffix = "!")`, and a default property
  `int Half { get => Width / 2; set => Width = value * 2; }`
- **THEN** `Mark()` answers `m!`, and setting `Half` to 5 leaves `Width` at 10

#### Scenario: A default indexer

- **WHEN** `interface IIndexed { int this[int i] => i * 3; }` and `record Tripler : IIndexed;`, or
  `interface IShelf { string this[int i] => "shelf " + i; }` and `class Shelf : IShelf { }`
- **THEN** `((IIndexed)new Tripler())[3]` answers `9` and `((IShelf)new Shelf())[4]` answers
  `shelf 4`, as in .NET

#### Scenario: A default event

- **WHEN** `interface ITicker` declares `List<Action<int>> Handlers { get; }`,
  `event Action<int> Ticked { add { Handlers.Add(value); } remove { Handlers.Remove(value); } }` and
  `void Tick(int n)`, which calls each handler; a class, a record, a struct, a component and a class
  derived from a class each take it; and the code runs `var sum = 0; ITicker t = …; Action<int> add = n
  => sum += n; t.Ticked += add; t.Ticked += n => sum += n * 10; t.Tick(2); t.Ticked -= add; t.Tick(3);
  return sum + "|" + t.Handlers.Count;`
- **THEN** each answers `52|1`, as in .NET, where the browser answered `0|0`

#### Scenario: A derived interface's default event wins

- **WHEN** `interface ILoud : ITicker` declares `event Action<int> ITicker.Ticked` whose `add` wraps the
  handler to multiply by 100 and whose `remove` clears the list, and `class Siren : ILoud` runs the same code
- **THEN** it answers `2200|0`, as in .NET

### Requirement: Two members on one name are refused

eqc SHALL refuse with EQ1007 a class that takes two defaults which lower to one name, from different
interfaces, or a default and an instance member the class declares under that name, since the twin
holds one member per name. It SHALL refuse the same pairs along the class chain, which holds one
member per name too: a member the class declares on the name of a default or helper a base class
takes, unless the member implements that default's interface member for the class; a default the
class takes on the name of a member a base class declares; and a default the class takes on the name
of a default a base class takes for a different interface member. A default event SHALL take the two
names its accessors are written under, and not its own: a member on the event's name is no clash, and
two default events on one name are.

#### Scenario: Two interfaces, one name

- **WHEN** `IAlpha` and `IBeta` each declare `string Mark()` with a body, and `class Doubled : IAlpha, IBeta`
- **THEN** the build fails with EQ1007, naming both defaults

#### Scenario: An explicit implementation beside another interface's default

- **WHEN** `class Twice : IOne, ITwo` declares `string IOne.M() => "one";` and `ITwo` declares
  `string M() => "two";`
- **THEN** the build fails with EQ1007, naming the explicit implementation and the default

#### Scenario: A derived member on the name of its base's default

- **WHEN** `class ChainBase : IChained` takes the default `string Mark()`, and
  `class ChainDerived : ChainBase` declares `public int Mark;`
- **THEN** the build fails with EQ1007, naming the field and the default its base takes

#### Scenario: A derived member that re-implements the interface

- **WHEN** `record MarkedBase : IMarked` takes the default `string Mark()`, and
  `record Remarked : MarkedBase, IMarked` declares `public string Mark() => "r";`
- **THEN** the build reports nothing, `((IMarked)new Remarked()).Mark()` answers `r`, and
  `((IMarked)new MarkedBase()).Mark()` answers `m`

#### Scenario: A method on a default event's name

- **WHEN** `interface IChime` declares a default `event Action Rang` with accessors, and
  `class Ringing : IChime` declares `public string Rang() => "r";`, which C# compiles
- **THEN** the build reports nothing, and the twin holds `rang()` beside the event's add and remove

#### Scenario: Two default events on one name

- **WHEN** `IA` and `IB` each declare a default `event Action E` with accessors, and `class Both : IA, IB`,
  which C# compiles, taking both
- **THEN** the build fails with EQ1007, naming both defaults
