## MODIFIED Requirements

### Requirement: A class keeps the default interface members it relies on

eqc SHALL write every default interface member a class takes without declaring it into the class's
twin, as the class's own member, whichever emitter writes the class: a plain class, a record, a
struct or a component. The implementation SHALL be the one C# picks for the class. A member whose
implementation a base class takes too SHALL be left to the base class's twin, and one the base class
answers with a less specific default SHALL be written into the class's twin, over the base's.

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

### Requirement: A default nothing can supply is said

eqc SHALL refuse the class with EQ1008, an error naming the class, the member and the two ways out,
for each default it takes from an interface compiled into a referenced assembly the runtime does not
provide, and for a default that uses a static member of its interface. A default indexer SHALL be
written into the twin as the class's own indexer is, and not refused.

#### Scenario: A default uses a static member of its interface

- **WHEN** a default calls `private static string Wrap(string text)` of its interface
- **THEN** the build fails with EQ1008, saying that an interface has no JavaScript form to hold the
  static, and to declare the default in the class

#### Scenario: A default indexer

- **WHEN** `interface IIndexed { int this[int i] => i * 2; }` and a class relies on it
- **THEN** the build reports nothing, and the class's twin carries the indexer, which answers as in
  .NET

#### Scenario: An interface from another assembly

- **WHEN** a class implements an interface of a referenced assembly and relies on its default
  method `Say()`
- **THEN** the build fails with EQ1008, saying the class relies on that default and to declare `Say`
  in it or keep it out of client code
