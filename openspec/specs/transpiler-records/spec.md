# transpiler-records Specification

## Purpose
How eqc builds the twin of a record, a struct or a class: its constructor, and what each member
starts as.

## Requirements

### Requirement: A member starts as its declaration says

A twin's constructor SHALL give each member the default its declaration gives it (a positional
parameter's default, or a property's or a field's initializer), converted as the expression it is,
and its type's default when it declares none.

#### Scenario: A field initializer

- **WHEN** `record Fields { public string Log = "x"; }` is built with `new Fields()`
- **THEN** its `Log` is `"x"`, as in .NET

#### Scenario: A decimal and a long property

- **WHEN** `record Props { public decimal Price { get; init; } = 1.5m; public long Count { get; init; } = 5; }`
- **THEN** `(new Props().Price + 1m).ToString()` answers `2.5` and `(new Props().Count + 1L).ToString()` answers `6`

#### Scenario: A fresh collection per construction

- **WHEN** a property is initialized with `= new()` and two instances are built
- **THEN** adding to one instance's collection leaves the other's empty

### Requirement: A construction that skips a member leaves it to the constructor

A construction site SHALL pass nothing of its own for a member it does not set, so the
constructor's default applies.

#### Scenario: An object initializer sets one member

- **WHEN** `new Fields { N = 3 }` is built
- **THEN** its `Log` is `"x"` and its `N` is 3

### Requirement: A default and a base clause read the constructor's parameters

A member default and a base clause's arguments SHALL read a primary-constructor parameter as that
parameter, before any member of the instance is set.

#### Scenario: An initializer reads a positional parameter

- **WHEN** `record Tagged(int Id) { public string Tag = "#" + Id; }`
- **THEN** `new Tagged(4).Tag` answers `#4`

#### Scenario: A base clause computes from a parameter

- **WHEN** `record Offset(int X) : Measure(X + 1)` with `record Measure(int Value)`
- **THEN** `new Offset(3).Value` answers 4, and building it throws nothing

### Requirement: A record's and a struct's members lower as a class's do

A record's and a struct's method, operator, conversion and computed property SHALL be lowered
through the same lowering as a class's members, their own and a default an interface supplies: an
async method SHALL run as an async function, an iterator SHALL return its sequence, an `out` or
`ref` parameter SHALL carry its value back, and a variable an expression body's pattern binds SHALL
be declared before its use. A parameter SHALL be declared by the name every reference to it uses.
Whether a method is async SHALL be decided by its `async` modifier or its return type's symbol, never
by the type's name, and a getter that yields SHALL return its sequence.

#### Scenario: An async method and an iterator

- **WHEN** `record R(int V)` declares `async Task<int> Own() => await Task.FromResult(V);` and
  `IEnumerable<int> Mine() { yield return V; yield return V + 1; }`
- **THEN** `await new R(3).Own()` answers 3 and `string.Join(",", new R(3).Mine())` answers `3,4`, as in .NET

#### Scenario: A pattern variable in an expression body

- **WHEN** `struct SE` declares `public override bool Equals(object o) => o is SE m && m.V == V;`
- **THEN** `new SE(1).Equals(new SE(1))` answers true, and `List<SE>.Remove(new SE(1))` removes it

#### Scenario: A return type named like a task

- **WHEN** `record R(int V)` declares `TaskItem First() => new TaskItem(V);` with `record TaskItem(int N)`
- **THEN** `new R(4).First().N` answers 4, the method not being async

#### Scenario: A reserved parameter name

- **WHEN** a record's method takes `int package` or `int @class`
- **THEN** it runs, the parameter declared as `package$` or `class$`, the name every use of it has

### Requirement: A record's twin says what C# declares

A record's twin SHALL annotate a nullable delegate as a function that may be missing, and SHALL
import every runtime name its annotations use, `Decimal` included.

#### Scenario: A record with a fold label and an amount

- **WHEN** a record has a `decimal Amount`, a method returning `(decimal Net, decimal Tax)`, and a
  parameter `Func<int, string>? label`
- **THEN** its module imports `Decimal`, the method is annotated `[Decimal, Decimal]`, and the
  parameter `((value: number) => string) | null`

#### Scenario: A tuple holding an enum

- **WHEN** a record's method returns `(Side Side, int Line)`, `Side` an enum of the app
- **THEN** it is annotated `[string, number]`, the member string the enum crosses as

### Requirement: Every deconstruction reads a record or a struct through its Deconstruct

A deconstruction SHALL bind each part the way the bound tree says .NET does, in every shape it takes:
a declaration (`var (a, b) = p`, `(var a, var b) = p`), an assignment (`(a, b) = p`), a mixed one
(`(var a, b) = p`) and a `foreach (var (a, b) in ps)`. A tuple and a dictionary's pair SHALL bind by
position. Any other value SHALL bind through the `Deconstruct` each level calls: a record's own and a
BCL type's by the members its out parameters name, and one the app wrote by calling it. A nested
deconstruction SHALL bind each level by its own type, and a discard SHALL bind nothing.

#### Scenario: Existing locals

- **WHEN** browser-side code declares `int a, b;` and writes `(a, b) = new Point(1, 2)`
- **THEN** `a` is `1` and `b` is `2`, as in .NET

#### Scenario: A declaration written as a tuple, and a loop

- **WHEN** browser-side code writes `(var a, var b) = new Point(1, 2)`, or sums `a * 10 + b` over
  `foreach (var (a, b) in new[] { new Point(1, 2), new Point(3, 4) })`
- **THEN** it reads `12`, and the loop's sum is `46`, as in .NET

#### Scenario: A struct's own Deconstruct

- **WHEN** a struct's `Deconstruct(out double celsius, out double fahrenheit)` computes the second
  part, and browser-side code writes `var (c, f) = new Temperature(100)`
- **THEN** `c + f` is `312`, as in .NET

#### Scenario: A nested deconstruction

- **WHEN** browser-side code writes `var ((x1, y1), (x2, y2)) = new Line(new Point(1, 2), new Point(3, 4))`
- **THEN** the four names hold `1`, `2`, `3` and `4`, as in .NET

### Requirement: A static field store lives on the type

A static property whose accessors use `field` SHALL keep its store on the type, in a component, a
plain class and a record alike, and a record SHALL carry such a property with its accessors.

#### Scenario: A record's halving setter

- **WHEN** browser-side code sets `Shapes.Half = 9` on a record declaring
  `public static int Half { get; set => field = value / 2; }` and reads it back
- **THEN** it reads `4`, as in .NET

### Requirement: A static store starts as its declaration says

A static property's store SHALL start as its initializer, written into the store directly, or as its
type's default when it declares none, in a component, a plain class and a record alike, before any
code writes it.

#### Scenario: An initializer behind a doubling setter

- **WHEN** a class declares `public static int Total { get; set => field = value * 2; } = 5;`
- **THEN** reading `Total` before any write answers `5`, as in .NET

#### Scenario: No initializer

- **WHEN** a component declares `public static int Hits { get; set; }`
- **THEN** reading `Hits` before any write answers `0`, as in .NET

### Requirement: A deconstruction's part is converted to its target's type and written by what its target is

A deconstruction SHALL write each part as C# writes it: converted to its target's type through the
conversion the bound tree names for that part, and written by what the target is. A dictionary's
entry SHALL be written by its class, its receiver and key evaluated before the value, and a part a
declaration or a loop converts SHALL be declared in its own type. A deconstruction whose parts need
neither SHALL keep its destructuring.

#### Scenario: A dictionary's entries as targets

- **WHEN** browser-side code writes `(map["a"], map["b"]) = new Point(1, 2)` over a
  `Dictionary<string, int>`
- **THEN** `map["a"]` is `1` and `map["b"]` is `2`, as in .NET, where the module did not parse

#### Scenario: An int part into a long

- **WHEN** browser-side code writes `long total; int n; (total, n) = new Point(1, 2);` and then
  `(total + 1L).ToString()`
- **THEN** it answers `2`, as in .NET, where the long arithmetic threw

#### Scenario: A converted declaration and a converted loop

- **WHEN** browser-side code writes `(long t, int m) = pair` for an `(int, int)` pair, and sums
  `a * 10 + b` over `foreach ((long a, int b) in pairs)`
- **THEN** both compute in long, as in .NET
