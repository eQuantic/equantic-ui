## ADDED Requirements

### Requirement: Object.Equals and an inherited Equals answer as .NET's

`object.Equals(a, b)`, `Object.Equals(a, b)`, `System.Object.Equals(a, b)` and bare `Equals(a, b)` SHALL
answer what .NET's `object.Equals(object, object)` answers: true when both are null or the same
reference, false when one is null or their unboxed types cannot be equal, and `a.Equals(b)` otherwise,
evaluating both arguments in order. An instance `Equals(object)` inherited from `object` on a class, an
array, a collection, a `StringBuilder` or an `object` receiver SHALL answer as .NET's: reference
equality for a class that does not override `Equals`, an array, a list or a `StringBuilder`, and the
runtime value's own equality for an `object` receiver, a list or an array held as `object` compared by
reference. Calling `Equals` or `ToString` on a null reference receiver (a record, a plain class, an
interface, a `string` or an `object`, a method group included) SHALL throw `NullReferenceException`
after evaluating any argument, while an interpolation hole and a concatenation of null write nothing.

#### Scenario: Static object.Equals across values, nulls and types

- **WHEN** `object.Equals(1, 1)`, `Object.Equals(null, null)`, `System.Object.Equals("a", null)`,
  `object.Equals(new P(1), new P(1))`, `object.Equals((byte)1, 1)` and bare `Equals("x", "x")` run
- **THEN** they answer true, true, false, true, false and true, as in .NET; the browser threw
  `object is not defined` or `Object.equals is not a function`

#### Scenario: Inherited Equals on a class, a StringBuilder and a list held as object

- **WHEN** `c1.Equals(c1)`, `c1.Equals(c2)` and `sb.Equals(sb)` run over `var c1 = new C(); var c2 = new C(); var sb = new StringBuilder("a");`,
  and `a.Equals(b)`, `object.Equals(a, b)` and `new HashSet<object> { a, b }.Count` run over
  `object a = new List<int> { 1 }; object b = new List<int> { 1 };`
- **THEN** they answer true, false, true, false, false and 2, as in .NET; the browser refused
  `StringBuilder.Equals`, threw `c1.equals is not a function`, and compared the two lists by their items

#### Scenario: Equals and ToString on a null record or class instance

- **WHEN** `p.Equals(null)`, `p.Equals(Step())`, `p.ToString()`, `c.ToString()`, `s.ToString()` and
  `o.ToString()` run over `P? p = null; C? c = null; string? s = null; object? o = null;`
- **THEN** each throws `NullReferenceException`, `Step()` running before `p.Equals(Step())` throws, while
  `$"{p}{c}{s}{o}"` and `"" + p + c` answer `""`, as in .NET; the browser answered false, `"null"` and `""`

### Requirement: An enum's Equals, hash and aliased members answer as .NET's

An enum's and a `Nullable` enum's `Equals(object)` and `Equals` method group SHALL answer true only for
an enum of the same type and numeric value (or two nulls for a `Nullable` enum), and false for a number,
a string, another enum or null, evaluating the argument. An enum's `GetHashCode()` and its method group
SHALL hash its underlying numeric value, as `((int)e).GetHashCode()` does, and a null `Nullable` enum
SHALL answer 0. An enum member that shares its numeric value with an earlier member SHALL carry the same
runtime value and text as the member .NET's `Enum.GetName` names for that value, while `Enum.GetNames`,
`Enum.Parse` and `Enum.IsDefined` still read every declared member name, and `ToDictionary` keyed by an
enum with aliases SHALL build and merge keys by that value.

#### Scenario: Enum Equals and GetHashCode

- **WHEN** `Scale.Small.Equals(Scale.Small)`, `Scale.Small.Equals(Scale.Large)`,
  `Scale.Small.Equals((object)Scale.Small)`, `Scale.Small.Equals(0)`, `Scale.Small.Equals("small")`,
  `Scale.Small.Equals(null)` and `Scale.Small.GetHashCode() == 0.GetHashCode()` run over
  `enum Scale { Small = 0, Large = 1 }`
- **THEN** they answer true, false, true, false, false, false and true, as in .NET; the browser threw
  `equals is not a function` and hashed the string `"small"`

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
