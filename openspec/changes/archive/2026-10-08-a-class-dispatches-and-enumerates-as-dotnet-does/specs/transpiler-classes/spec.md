## ADDED Requirements

### Requirement: A method that hides an inherited member holds a name of its own

A method that hides an inherited member, declared with `new` or with the same signature and no
`override`, SHALL hold a name of its own on its type's twin, and every call, method group, base call
and null-conditional call bound to it SHALL reach it by that name, so that a call bound to the hidden
member reaches the hidden one, as .NET's does: through the base type, the base's method answers, and
through the hiding type, the hiding one. An override of a method that hides one SHALL answer for that
method. A member of .NET's that the runtime reads by its name (`GetHashCode`, `ToString`, `Equals`)
SHALL be answered where the runtime reads it by the member, never by a method of the app's that hides
it. eqc SHALL refuse with EQ1007 a method that hides an inherited member and answers an interface's
member, which a call through the interface reaches by the hidden member's name.

#### Scenario: A call through the base

- **WHEN** `class A { public virtual string Name() => "A"; }`,
  `class B : A { public new string Name() => "B"; }` and `A a = new B(); return a.Name() + new B().Name();`
- **THEN** it answers `AB`, as .NET does, where the build refused `B` with EQ1007

#### Scenario: An override of a virtual method that hides one

- **WHEN** `class D : B { public new virtual string Name() => "D"; }`,
  `class E : D { public override string Name() => "E" + base.Name(); }` and
  `var e = new E(); A a = e; B b = e; D d = e; return a.Name() + "|" + b.Name() + "|" + d.Name();`
- **THEN** it answers `A|B|ED`, as .NET does

#### Scenario: A method that hides GetHashCode

- **WHEN** `class Counter { public int Calls; public new int GetHashCode() => ++Calls; }` and
  `var c = new Counter(); object o = c; var h1 = o.GetHashCode(); var h2 = o.GetHashCode(); return (h1 == h2) + "|" + c.Calls;`
- **THEN** it answers `True|0`, object's identity hash, as .NET does, where the hiding method answered
  `False|2`

#### Scenario: A method that hides one and answers an interface

- **WHEN** `class Hiding : Base, INamed { public new string Name() => "hiding"; }` over
  `class Base { public string Name() => "base"; }` and `interface INamed { string Name(); }`
- **THEN** eqc refuses `Hiding` with EQ1007, naming the method and the interface

#### Scenario: A generic method that hides one over its own type parameter

- **WHEN** `class Gen { public virtual string F<T>(List<T> x) => "base"; }`,
  `class GenHiding : Gen { public new string F<U>(List<U> x) => "hiding"; }` and
  `Gen g = new GenHiding(); return g.F(new List<int>()) + new GenHiding().F(new List<int>());`
- **THEN** it answers `basehiding`, as .NET does, where the build refused `GenHiding` with EQ1007

## MODIFIED Requirements

### Requirement: Every constructor a class declares is reached or refused

A plain class's twin SHALL reach each constructor the class declares by the counts of arguments it
takes, its optional parameters counted, as a record's twin does, save the constructor only .NET's
serialization calls, `(SerializationInfo, StreamingContext)`, which no `new` in the browser reaches and
which SHALL be no branch of the twin. A constructor that does its own work (the primary one, or an
explicit one that does not chain with `: this(…)`) SHALL bind its parameters, start the class's
instance members, call its base's constructor with its own arguments and run its body. One that chains
with `: this(…)` SHALL evaluate the chain's arguments from the arguments that arrived, run the
constructor it chains to, and then its own body. Every argument of a chain, of a `: base(…)` and of a
base clause SHALL land in its parameter's place and be evaluated in the order it is written, a base
clause's where the primary constructor's parameters are in scope. eqc SHALL refuse with EQ1009, naming
it, a constructor that takes a count of arguments another constructor of the class takes too, and one
that chains to a constructor that chains in turn.

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

#### Scenario: Visual Studio's exception template

- **WHEN** `class TemplatedException : Exception` declares `()`, `(string)`, `(string, Exception)` and the
  protected `(SerializationInfo, StreamingContext)`, and
  `new TemplatedException("outer", new InvalidOperationException("inner"))`
- **THEN** it builds, and its `Message` is `outer` and its `InnerException`'s is `inner`, as in .NET,
  where the build refused the class with EQ1009
