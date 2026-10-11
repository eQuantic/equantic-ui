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
member, which a call through the interface reaches by the hidden member's name. A method beside an
explicit interface implementation of its name SHALL hold a name of its own too, the explicit one
keeping the interface member's, and eqc SHALL refuse with EQ1007 one that also answers another
interface.

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

#### Scenario: A public method beside an explicit implementation of its name

- **WHEN** `class Labeled : INamedThing { public string Name() => "own"; string INamedThing.Name() => "interface"; }`
  and `var l = new Labeled(); INamedThing n = l; return l.Name() + "|" + n.Name();`
- **THEN** it answers `own|interface`, as .NET does, where both calls answered the one written last

## MODIFIED Requirements

### Requirement: Every constructor a class declares is reached or refused

A plain class's twin SHALL reach each constructor the class declares by the counts of arguments it
takes, its optional parameters counted, as a record's twin does, save the constructor only .NET's
serialization calls, `(SerializationInfo, StreamingContext)`, which SHALL be no branch of the twin, and
whose every call the browser would make, a `new`, or the `: base(…)`, `: this(…)` or base clause of a
constructor the twin writes, SHALL be refused with EQ1009. A constructor that does its own work (the primary one, or an
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

#### Scenario: A call of the serialization constructor

- **WHEN** `new Snap(info, context)` calls a public `Snap(SerializationInfo, StreamingContext)` beside
  `Snap(string, string)`, or a constructor of `class Passing : Base` calls a protected one with
  `: base(info, context)`
- **THEN** the build fails with EQ1009, naming the constructor called, where the twin ran
  `Snap(string, string)` and `Base(string, Exception)`

### Requirement: A nested type is a module of its own, named by its owner

Every class, record and struct declared inside another type SHALL have a twin of its own, in a module
of its own, named by the chain of the types that contain it and its own name joined by `$`
(`Cart$Item`), which no C# type can take. Every reference to it, inside its owner or out, SHALL name
that twin: a construction, a type test, a static member, a default and an annotation alike. A nested
type of an owner that never crosses (`[ServerOnly]`, an attribute) SHALL have none, and
client code that reaches its twin SHALL be refused: one that builds it, tests for it, reads its statics,
takes its zero or calls an operator or a conversion its value brings, however the code names the type
(an alias, `using static`, inference). A module that would import such a twin SHALL report it rather
than import it. A nested type of an owner the runtime provides SHALL be the runtime's,
imported from it under its twin name. A record's text SHALL print its C# name, as .NET's does. The server SHALL name the page it serves by the
same rule, so a page declared inside a class loads the module the build wrote for it, and SHALL refuse to
map a page declared inside an owner that never crosses. An exception class of the app's is a class, so
it SHALL be an owner as any class is, and one declared inside another type SHALL be a class too, its
twin named by its owner.

#### Scenario: A nested class beside a top-level class of its name

- **WHEN** `class Item { public int Qty = 3; }` and `class Cart { private class Item { public int Qty = 9; } public int Size() => new Item().Qty; }`
- **THEN** `new Cart().Size()` is `9`, as in .NET

#### Scenario: A private nested class built by its owner

- **WHEN** `class Roster { private sealed class Row { public string Name = ""; } public static string First() { var r = new Row { Name = "Ada" }; return r.Name; } }`
- **THEN** `Roster.First()` is `Ada`, as in .NET

#### Scenario: A nested record beside a top-level record of its name

- **WHEN** `class Shop { public record Line(int Qty); }` and `record Line(string Text);`
- **THEN** `new Shop.Line(2).Qty` is `2`, `new Line("x").Text` is `x`, and `new Shop.Line(2)` prints `Line { Qty = 2 }`, as in .NET

#### Scenario: A nested static class of a plain class

- **WHEN** `class Calc { static class Ops { public static int Twice(int x) => x * 2; } public int Run(int x) => Ops.Twice(x); }`
- **THEN** `new Calc().Run(4)` is `8`, as in .NET

#### Scenario: A public nested type from outside its owner

- **WHEN** `class Outer { public class Inner { public int N = 4; } }` and `object o = new Outer.Inner();`
- **THEN** `((Outer.Inner)o).N` is `4` and `o is Outer.Inner` is true, as in .NET

#### Scenario: Two levels of nesting

- **WHEN** `class A { public class B { public class C { public int V = 1; } } }`
- **THEN** `new A.B.C().V` is `1`, as in .NET

#### Scenario: A nested type of a server-only owner

- **WHEN** `[ServerOnly] class Vault { public class Key { } }`
- **THEN** the build writes no module for `Key`, as it writes none for `Vault`, client code that builds a
  `Vault.Key` or tests for one is refused with EQ2010, and a page declared inside `Vault` has no route,
  and mapping it with `MapPage` fails at startup

#### Scenario: A kept-out nested type the syntax never names

- **WHEN** `[ServerOnly] class Vault` holds `Key`, `Pair` and `Amount`, and client code reads `K.Count` through
  `using K = Vault.Key;`, reads `Count` or calls `Make()` through `using static Vault.Key;`, asks for
  `default(Vault.Pair)`, declares a field of type `Vault.Pair`, or adds or converts a `Vault.Amount` that
  another class's method returns
- **THEN** each is refused with EQ2010, and no module imports a twin of `Vault`'s

#### Scenario: A nested type of a runtime-provided owner

- **WHEN** `[RuntimeProvided] class Kit { public class Part { } }` and client code builds a `Kit.Part`
- **THEN** the module imports `Kit$Part` from the runtime, which carries it as it carries
  `CodeBlock$CodeMetrics`, and the build writes no module for it

#### Scenario: A page declared inside a class

- **WHEN** `public static class Admin { [Page("/admin/users")] public class Users : StatelessComponent { … } }` is served
- **THEN** the page's configuration and its route name `Admin$Users`, the module the build wrote

#### Scenario: A nested component, tested and built by its twin

- **WHEN** `class Host { public class Page : StatelessComponent { … } }`, `object o = new Host.Page();` and `Host.Page p = new();`
- **THEN** `o is Host.Page` tests `Host$Page`, and both constructions build `Host$Page`, imported where it is used

#### Scenario: A component beside a nested class of its name

- **WHEN** `class Item : StatelessComponent { … }` and `class Cart { public class Item { } }`
- **THEN** `Item` is the component's module and `Cart$Item` a plain class's, neither taken for the other

#### Scenario: A nested type the syntax never names

- **WHEN** `Factory.Make() + Factory.Make()`, `-Factory.Make()` and `total += Factory.Make()` bind the operators of a
  nested `Outer.Amount` that no expression names, and `default(Alias)` is the zero of a nested struct through
  `using Alias = Outer.Pair;`
- **THEN** each module imports `Outer$Amount` and `Outer$Pair`, as it imports a twin the syntax names

#### Scenario: A nested type inside an array or a generic

- **WHEN** a member is typed `A.Inner[]`, `List<A.Inner>` or `Func<A.Inner, B.Inner>`, in a class or a record
- **THEN** its annotation names `A$Inner` and `B$Inner`, each where its type stands, and a top-level `Inner`
  beside them keeps its own name

#### Scenario: An exception class and what it declares

- **WHEN** `class Outer { public class Inner : Exception { public int Code = 5; } }` and
  `class Rejected : Exception { public record Reason(string Code); }`
- **THEN** `new Outer.Inner().Code` is `5`, built as `Outer$Inner`, and `new Rejected.Reason("r").Code` is
  `r`, built as `Rejected$Reason`, where the first was an `Error` with none of its members and the second
  was refused with EQ2010
