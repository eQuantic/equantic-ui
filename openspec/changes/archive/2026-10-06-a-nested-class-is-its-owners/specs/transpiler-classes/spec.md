## ADDED Requirements

### Requirement: A nested type is a module of its own, named by its owner

Every class, record and struct declared inside another type SHALL have a twin of its own, in a module
of its own, named by the chain of the types that contain it and its own name joined by `$`
(`Cart$Item`), which no C# type can take. Every reference to it, inside its owner or out, SHALL name
that twin: a construction, a type test, a static member, a default and an annotation alike. A nested
type of an owner that never crosses (`[ServerOnly]`, an exception, an attribute) SHALL have none. A
record's text SHALL print its C# name, as .NET's does. The server SHALL name the page it serves by the
same rule, so a page declared inside a class loads the module the build wrote for it.

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
- **THEN** the build writes no module for `Key`, as it writes none for `Vault`

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
