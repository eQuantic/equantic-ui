## ADDED Requirements

### Requirement: Object.Equals and an inherited Equals answer as .NET's

`object.Equals(a, b)`, `Object.Equals(a, b)`, `System.Object.Equals(a, b)` and bare `Equals(a, b)` SHALL
answer what .NET's `object.Equals(object, object)` answers: true when both are null or the same
reference, false when one is null, and otherwise the first parameter's own `Equals(object)`, whatever the
second operand is and in whichever order the call names the two, evaluating both arguments as written. A
value whose type refuses every other (a number, a char, a bool, an enum, a tuple, a struct that does not
override `Equals`, a string, an anonymous object) SHALL be unequal to one whose type is known to be
another, a byte to an int and a char to a string included. An instance `Equals(object)` inherited from
`object` SHALL answer as .NET's: by reference for an array, a list, a `StringBuilder` and a class that
does not override `Equals`, through the override where one exists, and on an `object` receiver by what
the value turns out to be, a tuple held as `object` compared by its elements. Where either value's type
before C# boxed it is an array, a list or a class that does not override `Equals`, the comparison SHALL be
by reference or through the other value's own override. A list, an array, a tuple or an enum held as
`object` on both sides has lost the type .NET reads, since the browser holds the first three as one
JavaScript array and an enum as its member's key, so two lists or two arrays held so compare by their
elements and a boxed enum equals its key's string. Calling `Equals` or `ToString` on a null reference
receiver (a record, a plain class, an interface, a `string` or an `object`, a method group included)
SHALL throw `NullReferenceException` after evaluating any argument, while an interpolation hole and a
concatenation of null write nothing.

#### Scenario: Static object.Equals across values, nulls and types

- **WHEN** `object.Equals(1, 1)`, `Object.Equals(null, null)`, `System.Object.Equals("a", null)`,
  `object.Equals(new P(1), new P(1))`, `object.Equals((byte)1, 1)`, `object.Equals('a', "a")`,
  `object.Equals((1, 2), new[] { 1, 2 })` and bare `Equals("x", "x")` run
- **THEN** they answer true, true, false, true, false, false, false and true, as in .NET; the browser
  threw `object is not defined` or `Object.equals is not a function`

#### Scenario: The first parameter's Equals decides

- **WHEN** `object.Equals(objB: "x", objA: m)` and `object.Equals(any, (1, 2))` run over an instance `m`
  of a class whose `Equals(object)` accepts `"x"` and an instance `any` of one whose `Equals(object)`
  answers true
- **THEN** they answer true and true, as in .NET, `"x"` evaluated before `m`; the browser asked the
  string's equality and the tuple's

#### Scenario: Inherited Equals on a class, a StringBuilder and a list

- **WHEN** `c1.Equals(c1)`, `c1.Equals(c2)`, `sb.Equals(sb)`, `l1.Equals((object)l2)`,
  `object.Equals(l1, l2)` and `o.Equals(l2)` run over `var c1 = new C(); var c2 = new C(); var sb = new StringBuilder("a");`
  and `var l1 = new List<int> { 1 }; var l2 = new List<int> { 1 }; object o = l1;`
- **THEN** they answer true, false, true, false, false and false, as in .NET; the browser refused
  `StringBuilder.Equals`, threw `c1.equals is not a function`, and compared the two lists by their items

#### Scenario: A tuple held as object

- **WHEN** `new HashSet<object> { (1, 2) }.Contains((1, 2))`, `new object[] { (1, 2), (1, 2) }.Distinct().Count()`
  and `a.Equals(b)` run over `object a = (1, 2), b = (1, 2);`
- **THEN** they answer true, 1 and true, as in .NET

#### Scenario: Equals and ToString on a null record or class instance

- **WHEN** `p.Equals(null)`, `p.Equals(Step())`, `p.ToString()`, `c.ToString()`, `s.ToString()` and
  `o.ToString()` run over `P? p = null; C? c = null; string? s = null; object? o = null;`
- **THEN** each throws `NullReferenceException`, `Step()` running before `p.Equals(Step())` throws, while
  `$"{p}{c}{s}{o}"` and `"" + p + c` answer `""`, as in .NET; the browser answered false, `"null"` and `""`

### Requirement: An enum's Equals, hash and aliased members answer as .NET's

An enum's and a `Nullable` enum's `Equals(object)` and `Equals` method group SHALL answer true only for
an enum of the same type and numeric value (or two nulls for a `Nullable` enum), and false for a number,
a string, another enum or null, evaluating the argument, and the method group of an empty `Nullable` enum
SHALL be refused where it is made, as .NET refuses it. An enum's `GetHashCode()` and its method group
SHALL hash its underlying value as a value of its underlying type hashes, as `((int)e).GetHashCode()` does
for an `int` enum and `((long)e).GetHashCode()` for a `long` one, and a null `Nullable` enum SHALL answer
0. An enum member that shares its numeric value with an earlier member SHALL carry the same runtime value
and text as the member .NET's `Enum.GetName` names for that value, while `Enum.GetNames`, `Enum.Parse`
and `Enum.IsDefined` still read every declared member name, and `ToDictionary` keyed by an enum with
aliases SHALL build and merge keys by that value.

#### Scenario: Enum Equals and GetHashCode

- **WHEN** `Scale.Small.Equals(Scale.Small)`, `Scale.Small.Equals(Scale.Large)`,
  `Scale.Small.Equals((object)Scale.Small)`, `Scale.Small.Equals(0)`, `Scale.Small.Equals("small")`,
  `Scale.Small.Equals(null)` and `Scale.Small.GetHashCode() == 0.GetHashCode()` run over
  `enum Scale { Small = 0, Large = 1 }`
- **THEN** they answer true, false, true, false, false, false and true, as in .NET; the browser threw
  `equals is not a function` and hashed the string `"small"`

#### Scenario: A Nullable enum's group and a long enum's hash

- **WHEN** `eq(Scale.Small)` and `eq(Scale.Large)` run over `Scale? s = Scale.Small; Func<object?, bool> eq = s.Equals;`,
  `Func<object?, bool> f = none.Equals` is made over `Scale? none = null;`, and `Wide.High.GetHashCode()`
  runs over `enum Wide : long { High = 1099511627776L }`
- **THEN** they answer true and false, the delegate's making throws `NullReferenceException`, and the
  hash is 256, `1099511627776L.GetHashCode()`, as in .NET; the browser threw `s.equals is not a function`
  and hashed a double

#### Scenario: An enum member that aliases another

- **WHEN** `Alias.A == Alias.B`, `Alias.B.Equals(Alias.A)`, `Alias.B.ToString()`,
  `new HashSet<Alias> { Alias.A, Alias.B }.Count`, `string.Join(",", Enum.GetNames<Alias>())` and
  `Enum.Parse<Alias>("B") == Alias.A` run over `enum Alias { A = 0, B = 0, C = 1 }`
- **THEN** they answer true, true, `"A"`, 1, `"A,B,C"` and true, as in .NET; the browser held `Alias.B`
  as `"b"`

### Requirement: GetType fails the build once at the call

A call to `GetType()` and a `GetType` method group (`x.GetType` or bare `GetType`) SHALL fail the build
with EQ2004 naming `System.Object.GetType`, and a chain of member reads or calls rooted in a refused
platform member or call (`x.GetType().Name`, `typeof(string).Name.Trim()`) SHALL report EQ2004 once, at
the root.

#### Scenario: GetType and a chain from it

- **WHEN** a component calls `"a".GetType()`, reads `new P(1).GetType().Name` or takes
  `Func<Type> f = "a".GetType;`
- **THEN** each fails the build with one EQ2004 naming `System.Object.GetType`; the browser threw
  `getType is not a function`
