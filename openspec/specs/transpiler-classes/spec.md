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
