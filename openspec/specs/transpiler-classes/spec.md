# transpiler-classes Specification

## Purpose
How eqc builds a plain class in the browser as C# builds it: its twin's constructor reaches every C#
constructor the class declares, starts the class's state in declaration order, before its base's
constructor where C# does, and a construction applies its object initializer once the constructor
returns.

## Requirements

### Requirement: Every constructor a class declares is reached or refused

A plain class's twin SHALL reach each constructor the class declares by the counts of arguments it
takes, its optional parameters counted, as a record's twin does. A constructor that does its own work
(the primary one, or an explicit one that does not chain with `: this(…)`) SHALL bind its parameters,
start the class's instance members, call its base's constructor with its own arguments and run its
body. One that chains with `: this(…)` SHALL evaluate the chain's arguments from the arguments that
arrived, run the constructor it chains to, and then its own body. Every argument of a chain, of a
`: base(…)` and of a base clause SHALL land in its parameter's place and be evaluated in the order it
is written, a base clause's where the primary constructor's parameters are in scope. eqc SHALL refuse
with EQ1009, naming it, a constructor that takes a count of arguments another constructor of the class
takes too, and one that chains to a constructor that chains in turn.

#### Scenario: A constructor that chains to another

- **WHEN** `class Money { public int Cents; public Money() : this(100) { } public Money(int c) { Cents = c; } }`
- **THEN** `new Money().Cents` is `100` and `new Money(5).Cents` is `5`, as in .NET

#### Scenario: A base's constructor with its arguments

- **WHEN** `class D0 : B0 { public D0(int x) : base(x * 2) { } }` over `class B0 { public int X; public B0(int x) { X = x; } }`
- **THEN** `new D0(3).X` is `6`, as in .NET

#### Scenario: A primary constructor

- **WHEN** `class Greeter(string name) { public string Hello() => "hi " + name; }`
- **THEN** `new Greeter("ada").Hello()` is `hi ada`, as in .NET

#### Scenario: A primary constructor with a base clause

- **WHEN** `class Sized(string label) : B0(label.Length) { public string Text => label; }`
- **THEN** `new Sized("abc").X` is `3` and `new Sized("abc").Text` is `abc`, as in .NET

#### Scenario: Two constructors of one count

- **WHEN** `class Pair { public Pair(int a) { } public Pair(string s) { } }`
- **THEN** the build fails with EQ1009, naming `Pair(string s)`

### Requirement: A class's instance members start in declaration order, before its base's constructor

A plain class's instance fields and auto-properties SHALL start as their declarations say, each its
initializer or its type's default, in declaration order, a field's and a property's alike, before the
body of the constructor that does its own work. A derived class's SHALL run before its base's
constructor, as C# runs them: each one that can do anything but read a value SHALL be evaluated
before the base's constructor is called, in declaration order, and assigned once it returns. A
constructor that chains with `: this(…)` SHALL run them once, through the constructor it chains to.

#### Scenario: A derived class's initializers before its base's

- **WHEN** `class Derived : Base0 { int d = Log("derived-init"); public string P { get; } = Log("derived-prop"); }` over
  `class Base0 { int b = Log("base-init"); public Base0() { Log("base-ctor"); } }`
- **THEN** `new Derived()` logs `derived-init derived-prop base-init base-ctor`, as in .NET

#### Scenario: A field's initializer and a property's in declaration order

- **WHEN** `class Plain { public static int N; public int A = ++N; public int B { get; set; } = ++N; }`
- **THEN** `new Plain()` holds `A` `1` and `B` `2`, as in .NET

### Requirement: A class is built, then its object initializer is applied

`new C(…) { … }` for a plain class whose twin eqc writes SHALL call the constructor the call binds,
with its arguments in their parameters' places, and SHALL then apply the object initializer to what
it built, in the order it is written, as a record's is applied. A component's object initializer SHALL
stay its props, and a hand-written twin of the vocabulary SHALL take its config object as before.

#### Scenario: An initializer that reads what the constructor changed

- **WHEN** `class Plain { public static int N; public int A = ++N; public int B { get; set; } = ++N; }`
  and `Plain.N = 0; var c = new Plain { B = Plain.N };`
- **THEN** `c` holds `A` `1` and `B` `2`, and `Plain.N` is `2`, as in .NET

#### Scenario: An initializer's value after the constructor

- **WHEN** `class Ordered { public int X = Log("x-init"); public int Y { get; set; } = Log("y-init"); public Ordered() { Log("ctor"); } }`
  and `new Ordered { Y = Log("value") }`
- **THEN** it logs `x-init y-init ctor value`, as in .NET

### Requirement: A class's held parameter has a name of its own

A primary constructor's parameter that a member of a plain class reads outside an initializer SHALL
be held on each instance, as a struct's is, and eqc SHALL refuse with EQ1007, naming both, a held
parameter whose name lands on the name of another member of the class.

#### Scenario: A held parameter beside a property of its name

- **WHEN** `class Greeter(string name) { public string Name => name.ToUpper(); }`
- **THEN** the build fails with EQ1007, naming `Greeter(name)` and `Name`

#### Scenario: A parameter read only by an initializer

- **WHEN** `class Point(int x) { public int X { get; } = x; }`
- **THEN** the build succeeds and `new Point(3).X` is `3`, as in .NET

### Requirement: A transpiled vocabulary type is built as an app's type is

A type of the vocabulary's assembly whose twin the runtime transpiles from its C# (marked
`[TwinIsTranspiled]`) SHALL be built by an app as the app's own types are: the constructor the call
binds, then the object initializer applied to what it built. Its zero, its indexer and its type test
SHALL be those of a twin eqc writes.

#### Scenario: A record struct and a record built with an initializer

- **WHEN** `new CellRef(1, 2) { Col = 3 }` and `new FieldError("name", "required") { Message = "too short" }`
- **THEN** the cell is `1|3` and the error's message is `too short`, as in .NET

#### Scenario: A class built with an initializer

- **WHEN** `new SheetController(10, 4) { Changed = edit => { } }` and `new SheetEdit { At = 3, Count = 2 }`
- **THEN** the controller holds its handler and the edit is `3|2`, as in .NET

#### Scenario: A record struct's zero

- **WHEN** `CellRef c = default;`
- **THEN** `c` is `0|0`, as in .NET

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

### Requirement: A field a case apart from a member keeps its own slot

A plain class's instance field whose twin name is another member's, a property, a method, an event or another field of its type or its bases, SHALL live in a slot of its own, its name with a `$` after it, and every read and write of the field SHALL reach that slot, a call of a delegate field, a read of a field called `Count` and a pattern's included: a property subpattern SHALL read the member the model binds it to, each one along an extended path, and a positional pattern over a `Deconstruct` the app wrote SHALL call it and read the parts it hands back, never the members its outs are named after. The other member SHALL keep its name. A class with such a field SHALL be written to JSON as System.Text.Json writes it: the property, read through its getter, and never the field.

#### Scenario: A setter beside its field

- **WHEN** `int value;` stands beside `int Value { get => value; set => this.value = value * 2; }`, and `Value` is set to 3
- **THEN** `Value` reads 6, as in .NET

#### Scenario: A method beside a field

- **WHEN** `int size = 3;` stands beside `int Size() => size * 2`, and `Size()` is called
- **THEN** it answers 6, as in .NET

#### Scenario: A pattern naming the field

- **WHEN** `int value = 1;` stands beside `int Value => value * 10;`, and `this is { value: 1 }` is tested
- **THEN** it answers true, as in .NET, and so does a switch arm or a case label naming the field, `this is { value: var v }` binds 1, and `this is { next.value: 1 }` reads each field on its path in its slot

#### Scenario: A positional pattern through a Deconstruct the app wrote

- **WHEN** `Point` holds the fields `x` and `y` beside `int X => x * 10` and `int Y => y * 10`, its `Deconstruct(out int x, out int y)` hands back the fields, and `new Point(1, 2) is (1, 2)` is tested
- **THEN** it answers true, as in .NET: the pattern calls the `Deconstruct`, an extension's as well, and reads the parts it hands back

#### Scenario: A method forwarding to its delegate field

- **WHEN** `Func<int, bool> validate = n => n > 2;` stands beside `bool Validate(int n) => validate(n)`, and `Validate(3)` is called
- **THEN** it calls the delegate and answers true, as in .NET, and so does a call of the field through `this`, on another instance and through `?.`

#### Scenario: A field called Count

- **WHEN** `int Count = 2;` stands beside `int count() => Count * 3`, and `t.Count + t.count()` is read
- **THEN** it answers 8, as in .NET: the field is read in its slot, not as a collection's count

#### Scenario: The JSON of a class with a moved field

- **WHEN** an instance whose field moved is written to JSON
- **THEN** the property is written under its name, read through its getter, and the field's slot is not written
