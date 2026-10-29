## ADDED Requirements

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
