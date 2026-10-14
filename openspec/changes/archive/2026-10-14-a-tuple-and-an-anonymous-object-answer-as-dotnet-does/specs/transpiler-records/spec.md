## ADDED Requirements

### Requirement: A value tuple prints as .NET does

A value tuple's text SHALL be the text `ValueTuple.ToString` writes: its elements between parentheses,
separated by `, `, each as its type's `ToString` writes it in the culture in force (a float in its own
digits, an enum by its member's name, a bool as `True`, a nested tuple and an anonymous object by their
own text), a null element as nothing, and a tuple of eight elements or more flat, as .NET writes the
elements it nests in `Rest`. It SHALL be that text wherever C# writes the value into text: `ToString()`
and its method group, an interpolation hole, one with an alignment or a format included, which a tuple
ignores as it is not `IFormattable`, a concatenation, `string.Concat`, `string.Join`, `string.Format`, a
`StringBuilder`, `Convert.ToString`, and a record's, a tuple's or an anonymous object's member. An empty
`Nullable` tuple SHALL write nothing. A tuple's `Equals` method group SHALL compare as its call does, and
a `Nullable` tuple's `Equals` SHALL be its value's, an empty one equal only to null.

#### Scenario: A pair, wherever it is written

- **WHEN** `var t = (1, 2);` is written by `t.ToString()`, `$"{t}"`, `"" + t` and `string.Concat(t, "!")`
- **THEN** they write `(1, 2)`, `(1, 2)`, `(1, 2)` and `(1, 2)!`, as in .NET; the browser wrote `1,2`

#### Scenario: A null element and a nested tuple

- **WHEN** `("a", none, 1).ToString()` runs with `string? none = null`, and `(1, (2, (3, "x"))).ToString()`
- **THEN** they write `(a, , 1)` and `(1, (2, (3, x)))`, as in .NET

#### Scenario: Each element in the culture

- **WHEN** `(1.5, 2.50m, new DateTime(2024, 1, 2, 3, 4, 5), -3, 0.1f, -5L).ToString()` runs on a pt-BR page
- **THEN** it writes `(1,5, 2,50, 02/01/2024 03:04:05, -3, 0,1, -5)`, as in .NET; the browser wrote the
  invariant numbers, the float in the double's digits

#### Scenario: Eight elements and more

- **WHEN** `(1, 2, 3, 4, 5, 6, 7, 8, 9).ToString()` runs
- **THEN** it writes `(1, 2, 3, 4, 5, 6, 7, 8, 9)`, as in .NET

#### Scenario: The pairs Zip answers

- **WHEN** `string.Join(";", new[] { 1, 2 }.Zip(new[] { "a", "b" }))` runs
- **THEN** it writes `(1, a);(2, b)`, as in .NET

#### Scenario: An aligned and a formatted hole

- **WHEN** `$"[{t,10}]|[{t:N2}]"` runs over `var t = (1.5, "a");`
- **THEN** it writes `[  (1.5, a)]|[(1.5, a)]`, as in .NET

#### Scenario: A record's member

- **WHEN** `new Segment((1, 2), "s").ToString()` runs for `record Segment((int, int) Ends, string Name);`
- **THEN** it writes `Segment { Ends = (1, 2), Name = s }`, as in .NET

#### Scenario: The Equals method group and a Nullable tuple

- **WHEN** `Func<object?, bool> eq = t.Equals;` is made over `var t = (xs, 1);` with `var xs = new[] { 1 };`,
  and `n.Equals((1, 2))` and `none.Equals(null)` run over `(int, int)? n = (1, 2), none = null;`
- **THEN** `eq((xs, 1))` is true, `eq((new[] { 1 }, 1))` false, and both `Equals` calls true, as in .NET;
  the browser threw making the delegate and calling the `Nullable` tuple's `Equals`

### Requirement: An anonymous object compares and prints as .NET does

An anonymous object's `Equals`, a call or a method group, SHALL compare as the `Equals` C# writes for an
anonymous type: each member by its type's default comparer, and only another anonymous object of the
same members in the same order can be equal to it, so an anonymous object of other members or of the
same members in another order, a record with the same members, a tuple and null SHALL be unequal. Its
`==` SHALL stay reference equality, and its `GetHashCode` SHALL hash equal objects equal. As a key of a
set or a dictionary, and to `Distinct` and `GroupBy`, equal anonymous objects SHALL be one key. Its text SHALL
be the text C# writes for an anonymous type: `{ R = 255, Name = a }`, its members under their C# names
in the order they are declared, each as its type writes it in the culture in force, a null one as
nothing, and `{ }` for none, wherever C# writes the value into text, as a tuple's text is.

#### Scenario: The reported row

- **WHEN** `a.Equals((object)new { R = (byte)255 })` and `a.Equals((object)new { R = (byte)254 })` run over
  `var a = new { R = (byte)255 };`
- **THEN** they answer true and false, as in .NET; the browser threw `equals is not a function`

#### Scenario: Another member set

- **WHEN** `new { R = 1 }.Equals(new { G = 1 })`, `new { R = 1 }.Equals(new { R = 1, G = 2 })` and
  `new { R = 1, G = 2 }.Equals(new { G = 2, R = 1 })` run
- **THEN** each answers false, as in .NET

#### Scenario: A member compared by its type

- **WHEN** `new { A = xs }.Equals(new { A = xs })` and `new { A = new[] { 1 } }.Equals(new { A = new[] { 1 } })`
  run with `var xs = new[] { 1 };`
- **THEN** they answer true and false, as in .NET, which compares an array by reference

#### Scenario: A record with the same members

- **WHEN** `new { X = 1, Y = 2 }.Equals(new Point(1, 2))` runs for `record Point(int X, int Y);`
- **THEN** it answers false, as in .NET

#### Scenario: Reference equality and the hash

- **WHEN** `a == b`, `a == a` and `a.GetHashCode() == b.GetHashCode()` run over `var a = new { R = 1 };`
  and `var b = new { R = 1 };`
- **THEN** they answer false, true and true, as in .NET

#### Scenario: As a key

- **WHEN** a `HashSet` holding `new { R = 1 }` twice, `Distinct()` over the same two and `GroupBy` by an
  anonymous key run
- **THEN** the two are one element, one distinct value and one group, as in .NET; `Distinct` kept both in
  the browser, through a JavaScript `Set` that compares by reference

#### Scenario: Its text

- **WHEN** `new { R = 255, Name = "a" }.ToString()`, `new { }.ToString()` and `new { x, p.Name }.ToString()`
  run with `var x = 1;` and `var p = new { Name = "n" };`
- **THEN** they write `{ R = 255, Name = a }`, `{ }` and `{ x = 1, Name = n }`, as in .NET; the browser
  wrote `[object Object]`

#### Scenario: Its members in the culture

- **WHEN** `new { D = -1.5, M = 2.50m }.ToString()` runs on a pt-BR page
- **THEN** it writes `{ D = -1,5, M = 2,50 }`, as in .NET
