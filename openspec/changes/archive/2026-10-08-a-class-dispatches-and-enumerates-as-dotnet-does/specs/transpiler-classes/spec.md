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
