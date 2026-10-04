# transpiler-bcl Specification

## Purpose
What a BCL member answers in the browser, which is what it answers in .NET.

## Requirements

### Requirement: List.Remove answers as .NET does

`list.Remove(item)` SHALL remove the first item `EqualityComparer<T>.Default` finds equal to the value
and answer whether it found one, evaluating the list and the item once each, in that order. The
comparison SHALL be the one every search of the list takes (`IndexOf`, `LastIndexOf`, `Contains`):
a value tuple, a record and a struct by value, and so a nullable one of those and an anonymous type, a
tuple's or an anonymous type's array member by reference. Through `ICollection<T>`, a HashSet or a
LinkedList held when the call runs SHALL remove as it does when called directly, and so SHALL a
dictionary through `ICollection<KeyValuePair<K, V>>`: the pair leaves only when its key is there with
an equal value.

#### Scenario: A present item and a missing one

- **WHEN** `var list = new List<int> { 0, 1, 2 };` runs `list.Remove(1)` and then `list.Remove(5)`
- **THEN** the calls answer true and false, and the list is `0, 2`

#### Scenario: Equality by the type's rule

- **WHEN** a `List<Point>` of records removes `new Point(3, 4)`, and a `List<double>` removes `double.NaN`
- **THEN** each removes its equal item and answers true

#### Scenario: A tuple element by element

- **WHEN** a `List<(int, string)>` holding `(1, "a")` and `(2, "b")` removes `(2, "b")`
- **THEN** it answers true, and one item is left

#### Scenario: A tuple's array by reference

- **WHEN** a `List<(int[], int)>` holding `(a, 1)` removes `(new[] { 1 }, 1)`
- **THEN** it answers false, and the item stays, as in .NET

#### Scenario: A HashSet through ICollection

- **WHEN** `ICollection<int> c = new HashSet<int> { 1, 2 };` runs `c.Remove(1)` and then `c.Remove(5)`
- **THEN** the calls answer true and false, and one item is left

### Requirement: Convert.ToBoolean answers by the overload C# binds

`Convert.ToBoolean` SHALL answer a bool as itself; a number of any width, a decimal included, as
whether it is not zero, a NaN being not zero and a negative zero being zero; and SHALL throw .NET's
InvalidCastException for a char and a DateTime, after evaluating the argument. A provider SHALL be
evaluated in the order the arguments are written, though it is not consulted, except the invariant
and the current culture and a null, which are reads with no effect; another `CultureInfo`, which has
no twin to evaluate it, SHALL be refused with EQ2108.

#### Scenario: A false bool and a zero long

- **WHEN** `Convert.ToBoolean(false)` and `Convert.ToBoolean(0L)` run
- **THEN** both answer false

#### Scenario: A char

- **WHEN** `Convert.ToBoolean('a')` runs
- **THEN** it throws with the message `Invalid cast from 'Char' to 'Boolean'.`

### Requirement: A bool reads from text as .NET reads it

`bool.Parse`, `bool.TryParse` and `Convert.ToBoolean(string)` SHALL read "True" or "False" in any
case, trimmed of white space and NUL characters at both ends, and nothing else. Parse SHALL throw
.NET's FormatException for other text and ArgumentNullException for a null; TryParse SHALL answer
false and write false to its `out`; `Convert.ToBoolean` SHALL answer false for a null.

#### Scenario: Trimmed text

- **WHEN** `bool.Parse` reads `" True "`, `"true\0"` and `" true\u0085"`
- **THEN** each answers true

#### Scenario: Text that is not a bool

- **WHEN** `bool.Parse` reads `"abc"`
- **THEN** it throws with the message `String 'abc' was not recognized as a valid Boolean.`

#### Scenario: TryParse fails

- **WHEN** `bool v = true; var ok = bool.TryParse("x", out v);`
- **THEN** `ok` is false and `v` is false

### Requirement: A number prints through a format specifier as .NET prints it

A number formatted with a standard specifier (`C`, `D`, `E`, `F`, `G`, `N`, `P`, `R`, `X`, `B`) or a
custom picture SHALL print the text .NET prints for it, in the culture in force: from the number's exact
value, a double's binary value included, with an exact half rounded to even for a double and a float
and away from zero for a decimal and an integer, at any precision .NET takes. `E` SHALL write the
mantissa's digits, the exponent's sign and an exponent of at least three digits. `X` and `B` SHALL write
a negative integer as its two's complement at its type's width. A custom picture SHALL be drawn as
.NET draws it: its sections, its text, its percent and per mille, its exponent and its scaling commas.
A specifier the value's type does not take SHALL throw .NET's `FormatException` message.

#### Scenario: The E specifier

- **WHEN** `(12345.0).ToString("E2")` and `string.Format("{0:E2}", 12345.678)` run
- **THEN** they print `1.23E+004` and `1.23E+004`

#### Scenario: A double's exact value

- **WHEN** `(0.1).ToString("F20")` runs
- **THEN** it prints `0.10000000000000000555`

#### Scenario: A half by the type

- **WHEN** `(1.25).ToString("E1")` and `125.ToString("E1")` run
- **THEN** they print `1.2E+000` and `1.3E+002`

#### Scenario: A negative integer at its width

- **WHEN** `((short)-1).ToString("X")` and `(-1).ToString("X")` run
- **THEN** they print `FFFF` and `FFFFFFFF`

#### Scenario: A picture's sections and percent

- **WHEN** `(-1.0).ToString("0.00;(0.00)")` and `(0.25).ToString("0%")` run
- **THEN** they print `(1.00)` and `25%`

#### Scenario: A precision past 100 digits

- **WHEN** `(0.1).ToString("F101")` runs
- **THEN** it prints the 55 digits of 0.1's exact value after the point, then zeros to 101 digits

#### Scenario: The culture's symbols

- **WHEN** `(1234.0).ToString("N0")` runs in es-ES and `(-42).ToString("D")` in sv-SE
- **THEN** they print `1.234` and `−42`, with sv-SE's minus sign

#### Scenario: A specifier the type does not take

- **WHEN** `(2.5).ToString("D")` runs
- **THEN** it throws `Format specifier was invalid.`

### Requirement: A sorted collection orders as its element type does

A `SortedSet`, a `SortedDictionary` and a `SortedList` built in the browser SHALL order their
elements or keys as .NET's `Comparer<T>.Default` orders the element type: a number by its value, a
double with its NaN first and equal to another NaN, a string in the current culture, an enum by its
value, and a decimal, a date and a type of the app's own that implements `IComparable` by its
`CompareTo`. A `StringComparer.Ordinal` handed to a sorted set SHALL order by code unit.

#### Scenario: Strings in the culture

- **WHEN** browser-side code adds `b`, `B`, `a`, `A` and `_x` to a `SortedSet<string>` and enumerates it
- **THEN** it enumerates `_x`, `a`, `A`, `b`, `B`, as in .NET

#### Scenario: Decimals by their value

- **WHEN** browser-side code adds `10m`, `9m`, `1.0m` and `1.00m` to a `SortedSet<decimal>`
- **THEN** it holds three elements and enumerates `1.0`, `9`, `10`, as in .NET

#### Scenario: An enum by its value

- **WHEN** browser-side code adds `Mid`, `Alpha` and `Zeta` of `enum Rank { Zeta, Alpha, Mid = 5 }` to a
  `SortedSet<Rank>`, and asks `Max()` of the three
- **THEN** it enumerates `Zeta`, `Alpha`, `Mid`, and `Max()` is `Mid`, as in .NET

#### Scenario: An ordinal comparer

- **WHEN** browser-side code builds `new SortedSet<string>(StringComparer.Ordinal)` and adds `b`, `B`,
  `a` and `A`
- **THEN** it enumerates `A`, `B`, `a`, `b`, as in .NET

### Requirement: A queue and a stack find a member by its value

`Queue<T>.Contains` and `Stack<T>.Contains` SHALL answer as .NET's, by `EqualityComparer<T>.Default`:
a decimal, a date, a record and a type that overrides `Equals` by its value.

#### Scenario: A decimal in a queue

- **WHEN** browser-side code enqueues `1m` on a `Queue<decimal>` and asks `Contains(1.00m)`
- **THEN** the answer is `True`, as in .NET

### Requirement: A member reached through using static answers as its qualified spelling

A .NET member reached by its simple name through `using static` SHALL answer in the browser what
its qualified spelling answers where the bare spelling reaches that translation: a `Math` and `MathF`
member, a `string` static, an inlined constant and an enum's member. Any other platform member reached
bare SHALL fail the build with EQ2004 rather than be emitted as a member of its class, even where its
qualified spelling translates (`DateTime.Now`), until the bare spelling reaches that translation too.

#### Scenario: Constants and methods

- **WHEN** browser-side code writes, under `using static System.Double;`, `using static System.Math;`
  and `using static System.String;`, `IsNaN(NaN)`, `Round(PI * 100) / 100` and
  `Join(",", parts) + Empty`
- **THEN** each answers what .NET answers: `true`, `3.14` and `a,b` for `parts` of `a` and `b`

#### Scenario: An enum's member

- **WHEN** browser-side code writes, under `using static System.DayOfWeek;`, `Monday == DayOfWeek.Monday`
- **THEN** the answer is `true`, as in .NET

#### Scenario: A member nothing translates

- **WHEN** a component calls `WriteLine("built")` under `using static System.Console;`
- **THEN** the build fails with EQ2004 naming `System.Console.WriteLine`

### Requirement: An enum's text is .NET's

An enum's `ToString()`, its interpolation and its concatenation SHALL write what .NET writes: a
member's declared name, a `[Flags]` combination's set flags joined by `, ` in ascending order, a value
no member names as its digits, and nothing for a null nullable enum. A format SHALL write what .NET
writes for it: `D` the number, `X` its hex in the underlying type's width, `F` the set flags of any
enum, and `G` or none the text above, and an unknown format SHALL throw. An alignment SHALL pad that
text.

#### Scenario: A flags combination

- **WHEN** browser-side code prints `Perm.Read | Perm.Write` of `[Flags] enum Perm { None = 0, Read = 1, Write = 2, Exec = 4 }`
- **THEN** it writes `Read, Write`, as in .NET

#### Scenario: A format and an alignment

- **WHEN** browser-side code writes `$"{r:D}|{r,6}|{r,-6:X}|"` for `Rank.Mid` of `enum Rank { Zeta, Alpha, Mid = 5 }`
- **THEN** it writes `5|   Mid|00000005|`, as in .NET

#### Scenario: A nullable enum

- **WHEN** browser-side code writes `$"[{s,10}][{n,3}]"` for `Status? s = Status.Pending` and `Status? n = null`
- **THEN** it writes `[   Pending][   ]`, as in .NET

### Requirement: The statics of Enum read the enum's shape

`Enum.Parse`, `TryParse`, `GetName`, `GetNames`, `GetValues` and `IsDefined` SHALL answer in the
browser what they answer in .NET, for the enum their type argument or their `typeof` names: `Parse` reads a name, names
joined by commas and a number, keeps the case unless told not to, and throws where .NET throws;
`TryParse` leaves the enum's default on a failure, and the overload that takes a `Type` leaves null;
`GetName` answers the name of the member with the value, or null where none has it;
`GetNames` and `GetValues` follow the values; `IsDefined` reads its argument as the enum, a number, a
declared name, or an `object` holding any of them.

#### Scenario: Parse and TryParse

- **WHEN** browser-side code calls `Enum.TryParse<Status>("nope", out var s)` and `Enum.Parse<Status>("pending", true)`
- **THEN** the first answers false with `s` the first member, and the second answers `Pending`, as in .NET

#### Scenario: An object holding the enum

- **WHEN** browser-side code casts `Enum.Parse(typeof(Status), "Pending")` to `Status` and asks
  `Enum.IsDefined(typeof(Status), o)` for `object o = Status.Pending`
- **THEN** the cast is `Pending` and `IsDefined` answers true, as in .NET

### Requirement: An enum crosses as a dictionary key

A dictionary keyed by an enum SHALL cross between the server and the browser in both directions, its
keys as the browser holds them: a member's camelCase name, and a `[Flags]` enum's number. A name no
member has SHALL be refused, as a value and as a key.

#### Scenario: Keys both ways

- **WHEN** the server serializes a `Dictionary<Shelf, int>` and a `Dictionary<Channels, int>` keyed by a flags enum
- **THEN** it writes `{"dataAccess":1,"core":2}` and `{"1":1,"3":3}`, and reads both back to the same dictionaries

#### Scenario: A key no member has

- **WHEN** the server reads `{"gone":1,"old":2}` as a `Dictionary<Shelf, int>`
- **THEN** it throws a `JsonException` naming `'gone'`

### Requirement: A search compares as the default comparer

A list's and an array's `IndexOf`, `LastIndexOf` and `Contains`, `Array.IndexOf` and
`Array.LastIndexOf`, and LINQ's `Contains` SHALL compare as `EqualityComparer<T>.Default` does for
the element type, by the one comparison a set's elements and a dictionary's keys take: a NaN equal to
a NaN, a record, a struct, a tuple, a decimal and a date by value, a class by its own `Equals` where it
has one and by reference otherwise, an element typed `object` by what it holds. `Contains`, `IndexOf`
and `Remove` SHALL agree about one list. The ranged overloads SHALL search their range and refuse one
that leaves the list with .NET's exception and words.

#### Scenario: A NaN, a record and a tuple

- **WHEN** `new List<double> { double.NaN }.IndexOf(double.NaN)`, `new List<Point> { new Point(1, 2) }.IndexOf(new Point(1, 2))` and `new List<(int, int)> { (1, 2), (1, 2) }.LastIndexOf((1, 2))` run
- **THEN** they answer 0, 0 and 1, as in .NET

#### Scenario: A range that leaves the list

- **WHEN** `new List<int> { 5, 6, 5 }.IndexOf(5, 1, 3)` runs
- **THEN** it throws "Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')", as in .NET

### Requirement: A sort is .NET's introspective sort

`List<T>.Sort` and `Array.Sort` SHALL sort as .NET's introspective sort does, step for step, so that
equal elements land where .NET's unstable sort leaves them: by the element type's default comparer
(a number by value, a string in the current culture, an enum by its value, a null first, a double's
and a float's NaNs moved to the front before the sort), a `Comparison<T>`, a `StringComparer`'s six,
`Comparer<T>.Create` or a comparer of the app's own. A comparison that throws SHALL be wrapped in .NET's
InvalidOperationException, an inconsistent one that reads past the span SHALL be .NET's
ArgumentException naming the comparer, and a type .NET's default comparer cannot order SHALL throw
once two of its elements are compared. A comparer with no form here SHALL fail the build (EQ2007).

#### Scenario: Numbers by value

- **WHEN** `var l = new List<int> { 10, 9, 1 }; l.Sort();` runs
- **THEN** the list is "1,9,10", as in .NET

#### Scenario: Equal elements where .NET leaves them

- **WHEN** twenty records keyed `i % 3` are sorted by their key with a `Comparison`
- **THEN** their ids read "0 15 12 18 6 9 3 4 7 10 13 1 16 19 8 11 2 14 17 5", as in .NET

#### Scenario: A comparer that crosses

- **WHEN** `new List<string> { "b", "a" }` is sorted with `StringComparer.Ordinal`
- **THEN** the list is "a,b", as in .NET

### Requirement: A binary search answers as .NET's

`BinarySearch` SHALL search by .NET's midpoint, by the element type's default comparer or the comparer
it is handed, answer the index of an element it finds (among equal ones, the one .NET's midpoint meets
first) and the complement of the insertion point of one it does not, and check its range as .NET does.

#### Scenario: A hit and a miss

- **WHEN** `new List<int> { 1, 3, 5 }` searches for 3 and for 4
- **THEN** it answers 1 and -3, as in .NET

### Requirement: A list's finds, removals and copies answer as .NET's

`Find` and `FindLast` SHALL answer the element type's default where nothing matches; `FindIndex` and
`FindLastIndex` SHALL take their start index and count; `RemoveAll` SHALL remove in one pass, asking
the predicate once per element in order, and answer how many it removed; `CopyTo` SHALL write into the
array it is handed. Each SHALL check its arguments as .NET does, in its order and in its words.

#### Scenario: A find with no match

- **WHEN** `$"[{new List<int> { 1, 2 }.Find(x => x > 5)}]"` runs
- **THEN** it answers "[0]", as in .NET

#### Scenario: RemoveAll and CopyTo

- **WHEN** `new List<int> { 1, 2, 3, 4 }.RemoveAll(x => x % 2 == 0)` runs, and `new List<int> { 1, 2 }.CopyTo(a)` into `new int[3]`
- **THEN** the first answers 2 and leaves "1,3", and the array is "1,2,0", as in .NET

### Requirement: string.Join writes each value as .NET's ToString

`string.Join` SHALL read any sequence, an array, a list, a set, a linked list, a dictionary's keys and
a sequence behind an interface alike, and write each value as .NET's `ToString` writes it (a bool as
True or False, an enum by its member's name, a float by its own digits, a double in .NET's notation), a
null value as nothing, with the separator between them and a null separator as none. Values passed one
by one SHALL each be written by their own type. A null sequence SHALL be refused by the name of its
overload's parameter.

#### Scenario: A set and a linked list

- **WHEN** `string.Join(",", new HashSet<int> { 1, 2 })` and `string.Join(",", new LinkedList<int>(new[] { 1, 2 }))` run
- **THEN** each answers "1,2", as in .NET

#### Scenario: Values written as .NET writes them

- **WHEN** `string.Join(",", new[] { true, false })` and `string.Join(",", new[] { 1e21, 0.1 })` run
- **THEN** they answer "True,False" and "1E+21,0.1", as in .NET
