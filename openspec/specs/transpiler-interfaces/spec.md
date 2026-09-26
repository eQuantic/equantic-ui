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

#### Scenario: A record with no member of its own

- **WHEN** `record Nobody : IGreet;` and `IGreet` declares only `string Hello() => "hi";`
- **THEN** `((IGreet)new Nobody()).Hello()` answers `hi`

#### Scenario: An optional parameter and a setter

- **WHEN** a default method declares `string Mark(string suffix = "!")`, and a default property
  `int Half { get => Width / 2; set => Width = value * 2; }`
- **THEN** `Mark()` answers `m!`, and setting `Half` to 5 leaves `Width` at 10

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
copy of every default of every such interface.

#### Scenario: An app's language relies on the default rules

- **WHEN** an app's `class Words : ICodeLanguage` declares `Name` and `Tokenize` and not `Rules`
- **THEN** its twin has `get rules() { return ICodeLanguage.rules(this); }`, which answers the rules
  whose indent width is 4, as `CodeLanguageRules.Default` does, and the build reports nothing

#### Scenario: The SDK gains a default

- **WHEN** a public interface of the vocabulary gains a member with a body
- **THEN** the test listing the vocabulary's defaults fails until the runtime carries a copy of it

### Requirement: A default nothing can supply is said

eqc SHALL refuse the class with EQ1008, an error naming the class, the member and the two ways out,
for each default it takes from an interface compiled into a referenced assembly the runtime does not
provide.

#### Scenario: A default uses a static member of its interface

- **WHEN** a default calls `private static string Wrap(string text)` of its interface
- **THEN** the build fails with EQ1008, saying that an interface has no JavaScript form to hold the
  static, and to declare the default in the class

#### Scenario: An interface from another assembly

- **WHEN** a class implements an interface of a referenced assembly and relies on its default
  method `Say()`
- **THEN** the build fails with EQ1008, saying the class relies on that default and to declare `Say`
  in it or keep it out of client code
