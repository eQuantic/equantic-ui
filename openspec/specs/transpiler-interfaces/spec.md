# transpiler-interfaces Specification

## Purpose
What eqc does with a C# interface, which has no JavaScript form: the type erases, and what a class
takes from it has to be written into the class's twin.

## Requirements

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

### Requirement: Two members on one name are refused

eqc SHALL refuse with EQ1007 a class that takes two defaults which lower to one name, from different
interfaces, or a default and an instance member the class declares under that name, since the twin
holds one member per name. It SHALL refuse the same pairs along the class chain, which holds one
member per name too: a member the class declares on the name of a default or helper a base class
takes, unless the member implements that default's interface member for the class; a default the
class takes on the name of a member a base class declares; and a default the class takes on the name
of a default a base class takes for a different interface member.

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

### Requirement: A class takes the SDK's defaults from the runtime

For a default of an interface the runtime provides (the SDK's vocabulary: `IAppTheme`,
`ICodeLanguage`, `ICodeCompletionProvider`), which an app compiles against as metadata, eqc SHALL
write a member that delegates to the runtime's copy of that default, and the runtime SHALL carry a
copy of every default of every such interface. An interface the runtime provides SHALL be a public
interface of one of the assemblies it provides, not any interface that shares their namespaces.

#### Scenario: An app's language relies on the default rules

- **WHEN** an app's `class Words : ICodeLanguage` declares `Name` and `Tokenize` and not `Rules`
- **THEN** its twin has `get rules() { return ICodeLanguage.rules(this); }`, which answers the rules
  whose indent width is 4, as `CodeLanguageRules.Default` does, and the build reports nothing

#### Scenario: Another assembly's interface in the vocabulary's namespace

- **WHEN** a referenced assembly that is not the SDK's declares `namespace eQuantic.UI.Code` and in it
  `interface IForeignLanguage { string Greet() => "hi"; }`, and an app's class implements it
- **THEN** the build fails with EQ1008 for `Greet`, and the twin delegates nothing to the runtime

#### Scenario: The SDK gains a default

- **WHEN** a public interface of the vocabulary gains a member with a body
- **THEN** the test listing the vocabulary's defaults fails until the runtime carries a copy of it

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

### Requirement: An explicit implementation holds its member's name once

An explicit interface implementation SHALL be written under the name of the interface member it
implements, which every call through the interface reaches, a call through a constrained type parameter
included. One whose body hands back what the member holding its name answers (a method of the same
object called with its own parameters passed through as they came, or a property of it read, cast to an
interface or not, and a property's setter writing what its getter reads, through no member a derived
type can override on the way, and handing back the value as it was, neither widened, boxed into a copy
nor cast down, with no `out` or `ref`, no array handed to a `params` parameter and no async body, a
server action's stub or a member kept on the server ending it) SHALL NOT be written, so that
member answers every call through each interface, as they answer alike in .NET, and a method of the
type's own it hands off to SHALL keep its name. A component's explicit implementation beside a method of
its name SHALL be written with it. eqc SHALL refuse with EQ1007, naming both members and the interfaces,
what else its twin would hold on the name of an explicit implementation it writes: another one the type
writes under that name (two interfaces' members of one name, or one interface over two type arguments,
static ones too), a property, an event, a static field or an override the type declares on that name, a
member a base
the source declares holds on it (an abstract method's included), unless that is the base's explicit
implementation of the same interface member, which the type re-implements, and a name the runtime reads
of every object (`equals`, `getHashCode`, `toString`).

#### Scenario: Two interfaces of one name, one handing off to the other

- **WHEN** `class Forwarded : IFoo, IBar` declares `string IFoo.Run() => "foo";` and
  `string IBar.Run() => ((IFoo)this).Run();`, and `IFoo foo = f; IBar bar = f; return foo.Run() + "|" + bar.Run();`
- **THEN** it answers `foo|foo`, as .NET does, where the second `run()` was written over the first and
  called itself until the stack overflowed

#### Scenario: An implicit method that answers one interface beside an explicit one that hands off to it

- **WHEN** `class Mixed : IFoo, IBar` declares `public string Run() => "own";` and
  `string IFoo.Run() => Run();`, and `m.Run() + "|" + foo.Run() + "|" + bar.Run()` runs over it
- **THEN** it answers `own|own|own`, as .NET does, where the build refused `Mixed` with EQ1007

#### Scenario: An explicit property beside the type's own, which it reads

- **WHEN** `class Held : IVal, IOther` declares `public int Value => 5;` and `int IVal.Value => Value;`
- **THEN** `h.Value`, `((IVal)h).Value` and `((IOther)h).Value` answer `5|5|5`, as .NET does, where the
  explicit getter, written last, read itself

#### Scenario: Two interfaces of one name, each with an answer of its own

- **WHEN** `class Both : IFoo, IBar` declares `string IFoo.Run() => "foo";` and `string IBar.Run() => "bar";`
- **THEN** the build fails with EQ1007 at `IBar.Run`, naming `IFoo.Run()` and `IBar.Run()` and the
  hand-off `string IBar.Run() => ((IFoo)this).Run();`, where both calls answered `bar`

#### Scenario: An explicit implementation on the name of a base's method

- **WHEN** `class Derived : Base, IFoo` declares `string IFoo.Run() => "foo";` over
  `class Base { public string Run() => "base"; }`
- **THEN** the build fails with EQ1007, naming `Base.Run()`, where `((Base)d).Run()` answered `foo`

#### Scenario: A re-implementation of a base's explicit implementation

- **WHEN** `class Again : First, IFoo` declares `string IFoo.Run() => "again";` over
  `class First : IFoo { string IFoo.Run() => "first"; }`
- **THEN** the build reports nothing

#### Scenario: A component's method beside an explicit implementation of its name

- **WHEN** a component declares `public string Name() => "own";` and
  `string INamedThing.Name() => "interface";`, and its `Build` calls both
- **THEN** its twin writes `name$1()` and `name()`, where the parser dropped the second it met and the
  twin called the one it dropped
