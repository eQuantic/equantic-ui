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

### Requirement: A value hashes as it equals

`GetHashCode()` SHALL answer in the browser by .NET's contract: two values `Equals` finds equal SHALL
hash equal, whatever their type, a string, a number, a long, a bool, a value tuple, a record, a
struct, a decimal of any scale, a date, a vocabulary value type (`Point`, `TypeStyle`) and a value
under `object` included. A type that overrides `GetHashCode` SHALL answer its own, an instance of a
class that does not SHALL hash by its identity, and so SHALL an array, however its items change.
`base.GetHashCode()` SHALL call the base's override where the app wrote one or a record's is
synthesized, and `HashCode.Combine(…)` SHALL combine its values' hashes in its parameters' order,
evaluating its arguments as written. An instance call on a null reference SHALL be refused, as .NET
throws `NullReferenceException`, while an empty `Nullable<T>` answers 0, and the method group
`value.GetHashCode` SHALL take its receiver once, where the delegate is made, refusing a null one
there.

#### Scenario: Equal values hash equal

- **WHEN** browser-side code compares `new P(1, 2).GetHashCode()` with another `new P(1, 2)`'s, for
  `record P(int X, int Y)`, and `1.0m.GetHashCode()` with `1.00m.GetHashCode()`
- **THEN** both comparisons are true, as in .NET

#### Scenario: An override answers its own

- **WHEN** browser-side code calls `new Weighted(3).GetHashCode()` for
  `record Weighted(int X) { public override int GetHashCode() => X * 7; }`
- **THEN** it answers `21`, as in .NET

#### Scenario: An array is its identity, and a null receiver is refused

- **WHEN** browser-side code hashes `var a = new[] { 1 }`, sets `a[0] = 2` and hashes it again, and
  calls `GetHashCode()` on a null `object`
- **THEN** the two hashes are equal and the call on null throws, as in .NET

### Requirement: A Guid is its canonical text

A Guid made from text by `Guid.Parse`, `Guid.TryParse` or `new Guid(text)` SHALL be the text .NET
writes for it, the lowercase `D` format, read from any format .NET reads (`N`, `D`, `B`, `P`, `X`, in
either case, surrounded by white space), so two spellings of one Guid SHALL be one value to `==`, a
dictionary's key, a set and its hash. A text .NET refuses SHALL be refused, and a failed `TryParse`
SHALL leave `Guid.Empty`.

#### Scenario: Two spellings, one Guid

- **WHEN** browser-side code compares `Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950A")` with
  `Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950a")`, and adds an uppercase and a parenthesized
  spelling of one Guid to a `HashSet<Guid>`
- **THEN** the comparison is true and the set holds one Guid, as in .NET

### Requirement: A date leaving the calendar is refused as .NET refuses it

A `DateTime`'s and a `DateTimeOffset`'s `Add`, `Subtract`, `AddMonths`, `AddYears` and the `+` and `-`
operators with a `TimeSpan`, their compound forms included, SHALL refuse a result outside the calendar
with .NET's message, naming the parameter of the member that refuses it: `value` for `Add`, `Subtract`
and `AddYears`, `months` for `AddMonths`, and `t` for the operators. `AddMonths` SHALL refuse a count
past 120000 either way, and `AddYears` one past 10000, in .NET's words. A `DateTimeOffset`'s
`AddMonths` and `AddYears` SHALL then refuse a UTC time outside the calendar, and a `DateOnly`'s
`AddDays`, `AddMonths` and `AddYears` SHALL refuse a day outside it, each in .NET's words.

#### Scenario: The operator names its own parameter

- **WHEN** browser-side code evaluates `DateTime.MaxValue + TimeSpan.FromTicks(1)` and
  `DateTime.MaxValue.Add(TimeSpan.FromTicks(1))`, catching each message
- **THEN** both are "The added or subtracted value results in an un-representable DateTime.", the
  first with `(Parameter 't')` and the second with `(Parameter 'value')`, as in .NET

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

### Requirement: A string's own search compares by the comparison it is given

`StartsWith`, `EndsWith`, `Contains`, `IndexOf` and `LastIndexOf` given a `StringComparison`, and
`Equals(string, StringComparison)`, SHALL answer what .NET answers by that comparison, whether it is
written as a constant or held in a variable. An ordinal comparison that ignores case SHALL compare
each code point's simple upper case as .NET's ordinal casing does: the Kelvin sign, the long s and
the dotless i SHALL NOT match k, s and I, a Greek letter with an iota subscript SHALL match its title
case, a surrogate pair SHALL match by its code point, and a lone half SHALL be found inside a pair. A
comparison .NET does not define SHALL throw .NET's message.

#### Scenario: The Kelvin sign is not a k

- **WHEN** `("x" + "\u212A").Contains("k", StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers false

#### Scenario: A comparison held in a variable

- **WHEN** `var c = StringComparison.OrdinalIgnoreCase; return "aB".StartsWith("ab", c);` runs
- **THEN** it answers true

#### Scenario: A surrogate pair by its code point

- **WHEN** `"x\uD801\uDC28y".IndexOf("\uD801\uDC00", StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers 1

### Requirement: A string's search checks its start and its count as .NET does

`IndexOf` and `LastIndexOf` of a string given a start, or a start and a count, with a `StringComparison`
or without one, SHALL search only that range, and SHALL throw .NET's `ArgumentOutOfRangeException`,
with its message, for a start or a count outside the string. `LastIndexOf` SHALL read its start as
the last unit a match may cover, and a start one past the end as the last unit. A null value SHALL
throw `ArgumentNullException`. Without a comparison the search SHALL be ordinal, as the search without
a start is in the browser, where .NET searches by the current culture (#532).

#### Scenario: The count ends the search

- **WHEN** `"abcb".IndexOf("B", 2, 1, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers -1

#### Scenario: A start past the end

- **WHEN** `"abc".IndexOf("b", 4, StringComparison.Ordinal)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')`

#### Scenario: One past the end steps back

- **WHEN** `"abc".LastIndexOf("b", 3, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers 1

#### Scenario: A match must end by the start

- **WHEN** `"abcabc".LastIndexOf("bc", 4)` runs
- **THEN** it answers 1, as in .NET, where JavaScript's `lastIndexOf` answered the match that begins at 4

#### Scenario: The count ends a search with no comparison

- **WHEN** `"abcabc".LastIndexOf("a", 5, 2)` runs
- **THEN** it answers -1, as in .NET, where the count was dropped and the search answered 3

### Requirement: Replace keeps its comparison and writes its replacement as text

`Replace(string, string)` SHALL replace by the ordinal comparison, and `Replace(string, string,
StringComparison)` by the one it is given, every match from left to right without overlap. The
replacement SHALL be written as text, a null replacement SHALL remove the matches, and a null or
empty old value SHALL throw .NET's exception.

#### Scenario: Ignoring case

- **WHEN** `"aBc".Replace("b", "x", StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers `axc`

#### Scenario: A replacement that looks like a pattern

- **WHEN** `"abc".Replace("b", "$&$&")` runs
- **THEN** it answers `a$&$&c`

### Requirement: CompareTo orders by the current culture

`string.CompareTo(string)` SHALL compare as `string.Compare(a, b)` does, by the current culture, a
null ordering first. `CompareTo(object)` SHALL fail the build with EQ1004.

#### Scenario: A sort written with CompareTo

- **WHEN** a `List<string>` holding `b`, `B`, `a` and `A` runs `names.Sort((x, y) => x.CompareTo(y))`
- **THEN** the list is `a,A,b,B`

### Requirement: A search by a culture comparison is refused

A string's search (`StartsWith`, `EndsWith`, `Contains`, `IndexOf`, `LastIndexOf` or `Replace`) given
a culture comparison as a constant, written in its place or named, and an overload that takes a
`CultureInfo`, SHALL fail the build with EQ1004. A comparison that is not a constant, held in a
variable or chosen by a conditional, SHALL reach the run time, where a culture one SHALL throw with a
message that names the method and the comparison, after every check .NET makes before it searches.
`Equals` by a culture comparison SHALL compare the two whole strings by the culture's collator.

#### Scenario: A constant culture comparison

- **WHEN** a component calls `a.StartsWith(b, StringComparison.CurrentCulture)`, or
  `a.StartsWith(comparisonType: StringComparison.CurrentCulture, value: b)`
- **THEN** the build fails with EQ1004 at that call

#### Scenario: A comparison chosen at run time

- **WHEN** `var active = true; return "aB".StartsWith("ab", active ? StringComparison.OrdinalIgnoreCase : StringComparison.CurrentCulture);` runs
- **THEN** it builds and answers true

#### Scenario: A whole-string culture comparison

- **WHEN** `"\u00ADab".Equals("ab", StringComparison.InvariantCulture)` runs
- **THEN** it answers true

### Requirement: A number's text with no specifier is the culture's

A number written into text with no format specifier SHALL be its text in the culture in force, as
.NET writes it, wherever C# writes it: a concatenation, a plain or an aligned interpolation hole,
`ToString()`, `Convert.ToString`, `string.Format("{0}")`, `string.Concat`, `string.Join`,
`StringBuilder.Append`, `AppendLine` and `Insert` (the value written by position or by name, or an
interpolated string handed to one), and the text of a record, whose members are written as a
concatenation writes them. It SHALL carry the culture's decimal separator, minus sign and words for
NaN and the infinities, a float SHALL be written in its own digits, a decimal with its scale, and an
integer with no sign on a zero. The current culture or a null provider SHALL be the call with none, and
the invariant culture SHALL write the invariant text. An unsigned integer SHALL read the same in every
culture.

#### Scenario: A fraction in pt-BR

- **WHEN** `double n = -1.5;` is written as `$"{n}"`, `"v=" + n` and `n.ToString()` with pt-BR in force
- **THEN** each writes `-1,5`, as .NET does

#### Scenario: A negative integer in sv-SE

- **WHEN** `int i = -5;` is written as `$"{i}"` with sv-SE in force
- **THEN** it writes `−5`, with the culture's U+2212 minus sign, as .NET does

#### Scenario: An integer JavaScript holds as a negative zero

- **WHEN** `int half = neg / 2;` with `neg` holding `-1` is written as `$"{half}"`, `"v=" + half` and
  `half.ToString()`
- **THEN** each writes `0`, as .NET does, where the division truncates to `-0` in JavaScript

#### Scenario: A builder and a record

- **WHEN** `sb.Append(-1.5)` and the text of `record Point(double X, string? Name, bool On)` built with
  `(-1.5, null, true)` run with pt-BR in force
- **THEN** they write `-1,5` and `Point { X = -1,5, Name = , On = True }`, as .NET does

#### Scenario: A builder's value by name, and an interpolated string handed to it

- **WHEN** `sb.Append(value: -1.5)`, `sb.Insert(value: V(), index: I())` and `sb.Append($"{d}|{on}")`
  with `double d = -1.5; bool on = true;` run with pt-BR in force
- **THEN** they write `-1,5`, evaluate V before I and insert V's value at I's index, and write
  `-1,5|True`, as .NET does

#### Scenario: An aligned hole

- **WHEN** `double d = -1234.5;` is written as `$"[{d,10}]"` with de-DE in force
- **THEN** it writes `[   -1234,5]`, the culture's text padded, as .NET does

#### Scenario: A value written for a machine

- **WHEN** `d.ToString(CultureInfo.InvariantCulture) + "px"` runs with pt-BR in force
- **THEN** it writes `-1.5px`, as it does in every culture

### Requirement: A number's kind decides what its format writes

A number's format SHALL be decided by its C# type where the compiler knows it: `D`, `X` and `B` SHALL
refuse a `double` with .NET's FormatException whatever it holds, a `nint` and a `nuint` SHALL be
formatted as integers of the platform's width, and a value typed `object` SHALL be formatted by what it
holds. The per mille sign, the signs of an exponent and the percent sign SHALL be the culture's own.

#### Scenario: A whole double and an integer specifier

- **WHEN** `(2.0).ToString("D")` runs
- **THEN** it throws FormatException, as .NET does, where it printed `2`

#### Scenario: A native integer

- **WHEN** `nint m = -1;` is written with `m.ToString("X")`
- **THEN** it writes `FFFFFFFFFFFFFFFF`, as .NET does on a 64-bit host

#### Scenario: An integer held as an object

- **WHEN** `object o = 2;` is written with `string.Format("{0:D3}", o)`
- **THEN** it writes `002`, as .NET does

### Requirement: The cancellation trio cancels as .NET does

`CancellationTokenSource`, `CancellationToken` and `CancellationTokenRegistration` SHALL cross as the
runtime's, which SHALL behave as .NET's: callbacks run once, the last registered first; one registered
after the cancellation runs at once, and its registration is the default one, whose token is `None`;
a method group of the three keeps its receiver, read once; the callbacks that throw are gathered into an
`AggregateException` after the others ran; `ThrowIfCancellationRequested` throws an
`OperationCanceledException`; a disposed source refuses to cancel and lets go of its callbacks, a
registration still unregistering once after it, and a linked one lets go of the tokens it follows;
a delay is taken up to 4294967294 ms, its fraction cut, refused past it, and waited out past what a
browser's timer holds; `default(CancellationTokenRegistration)` is the registration of nothing,
whose token is `None`; and `CancellationToken.None`,
`default` and `new CancellationToken(false)` are one token that never cancels. A member of the three
the runtime does not carry SHALL be refused at the build (EQ2004), a constructor among them: the
runtime's source takes a delay or nothing, and a `TimeProvider` beside the delay is refused.

#### Scenario: Three callbacks

- **WHEN** browser-side code registers callbacks writing `a`, `b` and `c` on a token and cancels its
  source twice
- **THEN** the log reads `cba`, as in .NET

#### Scenario: A source's Cancel as a callback

- **WHEN** browser-side code registers `inner.Cancel` on another source's token and cancels that source
- **THEN** `inner` is cancelled, as in .NET

#### Scenario: A registration after the cancellation

- **WHEN** a callback is registered on a token already cancelled
- **THEN** it runs at once, and its registration's token is `CancellationToken.None`, which cannot be
  cancelled, as in .NET

#### Scenario: A cancelled token asked to throw

- **WHEN** a cancelled token's `ThrowIfCancellationRequested` is caught as an
  `OperationCanceledException`
- **THEN** its message is `The operation was canceled.`, as in .NET

#### Scenario: A member with no twin

- **WHEN** client code calls `TryReset` on a source, or reads a token's `WaitHandle`
- **THEN** the build fails with EQ2004

### Requirement: A char's search checks its start and its count as .NET does

`IndexOf(char, int)`, `IndexOf(char, int, int)`, `LastIndexOf(char, int)` and
`LastIndexOf(char, int, int)` SHALL search the range their start and count give, ordinally, and SHALL
throw .NET's `ArgumentOutOfRangeException`, with its message, for a start or a count outside the
string, the start checked first. `IndexOf`'s start MAY stand at the end of the string.
`LastIndexOf`'s start SHALL stand on a char of the string, and an empty string's `LastIndexOf` SHALL
answer -1 for any start and count.

#### Scenario: The count ends the search

- **WHEN** `"abcabc".IndexOf('c', 0, 2)` runs
- **THEN** it answers -1

#### Scenario: A start at the end

- **WHEN** `"abcabc".IndexOf('c', 6)` runs
- **THEN** it answers -1

#### Scenario: A start past the end

- **WHEN** `"abc".IndexOf('a', 4)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')`

#### Scenario: A count past the end

- **WHEN** `"abcabc".IndexOf('c', 2, 5)` runs
- **THEN** it throws with the message `Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')`

#### Scenario: The count ends the search back

- **WHEN** `"abcabc".LastIndexOf('a', 5, 2)` runs
- **THEN** it answers -1

#### Scenario: LastIndexOf from one past the end

- **WHEN** `"abc".LastIndexOf('c', 3)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'startIndex')`

#### Scenario: An empty string

- **WHEN** `"".LastIndexOf('c', 5)` runs
- **THEN** it answers -1

### Requirement: A builder's counted and ranged overloads answer as .NET's do

A `StringBuilder`'s overloads that count or take a range SHALL write what .NET writes: `Append(char,
repeatCount)` the char that many times, `Append(value, startIndex, count)` and `Insert(index, char[],
startIndex, charCount)` that range of the value, `Insert(index, string, count)` the string that many
times, `Replace` over a range only the occurrences inside it, and `ToString(startIndex, length)` that
range of the text. A `char[]` SHALL be written as its characters and a null as nothing. Each refusal
SHALL be .NET's exception with .NET's message, its checks in .NET's order.

#### Scenario: A char repeated

- **WHEN** `new StringBuilder("12").Append('x', 3).ToString()` is read
- **THEN** it is "12xxx", as in .NET, where it was "12x"

#### Scenario: A range of an array

- **WHEN** `new StringBuilder("12").Append(new[] { 'a', 'b', 'c' }, 1, 2).ToString()` is read
- **THEN** it is "12bc", as in .NET, where it was "12a,b,c"

#### Scenario: A replacement inside a range

- **WHEN** `new StringBuilder("aaaa").Replace("a", "b", 0, 2).ToString()` is read
- **THEN** it is "bbaa", as in .NET, where every occurrence was replaced

#### Scenario: A null array's negative count

- **WHEN** `new StringBuilder().Append((char[])null, 0, -1)` runs
- **THEN** it throws the ArgumentOutOfRangeException .NET throws, naming `charCount`, where a null
  string's names `count`

### Requirement: A builder has the members a page reaches

Every `StringBuilder` member a page reaches SHALL answer as .NET's does: `AppendFormat` and
`AppendJoin` append what `string.Format` and `string.Join` write, `Capacity` follows the chunks .NET
allocates through every edit, `MaxCapacity` and `EnsureCapacity` answer, the `Chars` indexer reads and
writes a char, `Length` cuts the text or fills it with `\0`, `Equals(StringBuilder)` compares the text
and `CopyTo` copies it, each refusing what .NET refuses. A member that cannot cross SHALL fail the
build.

A member that .NET makes of several appends SHALL make them as .NET does, each growing the chunks and
refused on its own: an interpolated `Append` or `AppendLine` appends each part in turn, so a hole that
reads the builder sees the parts before it, and a hole prints as a plain interpolation's does;
`AppendLine` appends the line's end after the value; `Append(StringBuilder)` checks the maximum before
it copies; and a `Replace` past the maximum keeps the chunks it replaced first. A call that names its
arguments SHALL bind each to its parameter, evaluated in the order written, and a text longer than the
browser's string can hold SHALL be .NET's `OutOfMemoryException`, the builder unchanged.

#### Scenario: Capacity after appends and a Clear

- **WHEN** forty chars are appended one at a time to `new StringBuilder()` and it is cleared
- **THEN** its `Capacity` is 48, as in .NET, where `Capacity` read undefined

#### Scenario: AppendFormat

- **WHEN** `new StringBuilder("12").AppendFormat("{0}-{1}", 1, 2).ToString()` is read
- **THEN** it is "121-2", as in .NET, where `appendFormat` was a TypeError

#### Scenario: The indexer

- **WHEN** `b[0] = 'x'` runs on `new StringBuilder("12")` and `b[0]` is read
- **THEN** it is 'x', as in .NET, where the write set a property nobody read

#### Scenario: A culture an interpolation cannot format in

- **WHEN** `sb.Append(CultureInfo.InvariantCulture, $"x{n}")` is built
- **THEN** the build fails with EQ2108, where the browser was handed the provider as the value

#### Scenario: An interpolated append that reads the builder

- **WHEN** `b.Append($"{b.Length}{b.Length}-{b.Length}")` runs on `new StringBuilder()`
- **THEN** the builder holds "01-3", as in .NET, where every hole was read before the text was
  appended and it held "00-0"

#### Scenario: Named arguments out of their order

- **WHEN** `new StringBuilder("12").CopyTo(count: 2, destination: a, destinationIndex: 1, sourceIndex: 0)`
  runs on `a = { '-', '-', '-', '-' }`
- **THEN** `a` is "-12-", as in .NET, where the arguments reached the twin in the order written

#### Scenario: A text longer than the browser's string

- **WHEN** an append makes a text longer than the engine's longest string
- **THEN** it throws .NET's `OutOfMemoryException` and the builder is unchanged, where JavaScript's
  `RangeError` escaped every `catch (OutOfMemoryException)`

### Requirement: A list built with an argument and an initializer holds both

A `List<T>` constructed with an argument and a collection initializer SHALL hold what .NET's holds:
a copy of the source, never the source itself, or nothing for a capacity, then each element of the
initializer in order, in the explicit and the target-typed forms alike, the source evaluated before the
elements. A target-typed list built from a source with no initializer SHALL be a copy of the source.

#### Scenario: A capacity and an initializer

- **WHEN** browser-side code builds `new List<int>(10) { 1, 2 }` and joins it
- **THEN** the answer is `1,2|2` with its count, as in .NET

#### Scenario: A source and an initializer

- **WHEN** browser-side code builds `var b = new List<int>(source) { 3 }` over `source = new List<int> { 1 }`, then runs `b.Add(4)`
- **THEN** `b` holds `1,3,4` and `source` still holds `1`, as in .NET

#### Scenario: A target-typed copy

- **WHEN** browser-side code builds `List<int> c = new(source)` over `source = new List<int> { 1 }` and runs `c.Add(9)`
- **THEN** `c` holds `1,9` and `source` one element, as in .NET

#### Scenario: A capacity is evaluated first

- **WHEN** browser-side code builds `new List<int>(Cap()) { At("a", 1), At("b", 2) }`, each call logging its step, and `new List<int>(-1)`
- **THEN** the log is `cab` before the list holds `1,2`, and the second throws an ArgumentOutOfRangeException, "Non-negative number required. (Parameter 'capacity')", as in .NET

### Requirement: A list's face reads and writes the list behind it

An element read or written through the indexer of `IList<T>`, `IReadOnlyList<T>` or `IList`, and a
`Count` read through such a face, SHALL answer for whatever the face holds when it runs: an array's or
a list's elements, and a twin's own indexer and count. A read, a write, a compound, a step, a
null-conditional read, an object initializer's entry and a key from the end SHALL each reach it, the
receiver and the key evaluated once and in C#'s order, and a write SHALL answer the value it wrote.

#### Scenario: A twin behind IReadOnlyList

- **WHEN** browser-side code computes `r[0] + r[2] + r.Count` over `IReadOnlyList<int> r` holding a twin of `{ 7, 8, 9 }`
- **THEN** the answer is `19`, as in .NET

#### Scenario: A twin behind IList

- **WHEN** browser-side code runs `l[0] = 5; l[1] += 2; l[2]++;` over `IList<int> l` holding a twin of `{ 1, 2, 3 }`
- **THEN** `l[0]`, `l[1]` and `l[2]` are `5`, `4` and `4`, as in .NET

#### Scenario: A twin with a Length beside its Count

- **WHEN** browser-side code reads `p.Count` and `p[^1]` over `IReadOnlyList<int> p` holding a twin of `{ 3, 4 }` that also declares `Length => 12.5` and `Size => 7`
- **THEN** the answers are `2` and `4`, as in .NET

#### Scenario: A face that holds null

- **WHEN** browser-side code reads `r.Count`, `r[0]` and `r?.Count` over `IReadOnlyList<int> r = null`
- **THEN** the first two throw a NullReferenceException, "Object reference not set to an instance of an object.", and the null-conditional answers null, as in .NET

### Requirement: ICollection's Add and Clear answer for the collection behind it

`ICollection<T>.Add` and `ICollection<T>.Clear` SHALL add and empty as the collection the interface
holds when the call runs does in .NET: a list appends, a set adds only a value it does not hold, a
linked list adds last, a dictionary adds the pair and refuses a key already there with .NET's message,
and a twin of the app's own runs its own `Add` and `Clear`. A collection initializer that adds to a
member typed as the interface SHALL add the same way.

#### Scenario: A set behind ICollection

- **WHEN** browser-side code runs `c.Add(2); c.Add(1);` over `ICollection<int> c = new HashSet<int> { 1 }` and reads `c.Count`
- **THEN** the count is `2`, as in .NET, where the call threw `c.push is not a function`

#### Scenario: A dictionary's pairs behind ICollection

- **WHEN** browser-side code adds the pair `("a", 2)` to `ICollection<KeyValuePair<string, int>>` holding a dictionary with the key `a`
- **THEN** it throws `An item with the same key has already been added. Key: a`, as in .NET

### Requirement: A string splits as .NET splits

`string.Split` SHALL answer what the overload C# binds answers in .NET 10, each argument filling the
parameter it binds to, named or not, evaluated in the order written. A single separator, a char or a
string, SHALL split at each of its places, and a null or an empty one SHALL split nothing. A set of
separators, chars or strings, written as an array, a span's collection expression or one by one,
SHALL split at each place where one of them matches, the first in the set's order winning, a null or
an empty member skipped, and a null or an empty set SHALL split at .NET's white space. A count SHALL
cap the entries, the last one holding the rest of the text unsplit, and a count of zero SHALL answer
none. `TrimEntries` SHALL trim each entry of .NET's white space and `RemoveEmptyEntries` SHALL drop the
empty entries, the trimmed ones included. A negative count SHALL throw .NET's
`ArgumentOutOfRangeException` and an option .NET does not define its `ArgumentException`, in that
order, each with .NET's message.

#### Scenario: A set of separators

- **WHEN** `" abcd ef ".Split(new[] { 'a', 'd' })` runs
- **THEN** it answers ` `, `bc` and ` ef `, as in .NET, where the array was one text joined by commas

#### Scenario: The first separator in the set's order wins

- **WHEN** `"abcd".Split(new[] { "b", "bc" }, StringSplitOptions.None)` runs
- **THEN** it answers `a` and `cd`, as in .NET

#### Scenario: A count and options

- **WHEN** `"  a  b  c  ".Split(' ', 3, StringSplitOptions.RemoveEmptyEntries)` runs
- **THEN** it answers `a`, `b` and `c  `, as in .NET, where the count was dropped

#### Scenario: Options held in a variable

- **WHEN** `var o = StringSplitOptions.RemoveEmptyEntries; "a,,b".Split(',', o)` runs
- **THEN** it answers `a` and `b`, as in .NET, where the options were read from their spelling and the
  empty entry was kept

#### Scenario: An empty set is white space

- **WHEN** `" abcd ef ".Split(new string[0], StringSplitOptions.None)` runs
- **THEN** it answers an empty entry, `abcd`, `ef` and an empty entry, as in .NET, where the text was
  split into its characters

#### Scenario: A negative count

- **WHEN** `" abc ".Split(new[] { 'a' }, -1)` runs
- **THEN** it throws .NET's `ArgumentOutOfRangeException` with the message `count ('-1') must be a
  non-negative value. (Parameter 'count')`, the actual value on the next line

### Requirement: An array's encoding is .NET's

`Convert.ToBase64String`, `Convert.ToBase64CharArray`, `Convert.ToHexString` and
`Convert.ToHexStringLower` SHALL answer what the overload C# binds answers in .NET 10, each argument
filling the parameter it binds to, named or not, evaluated in the order written, for a `byte[]` C#
built and for one the platform built (`Encoding.UTF8.GetBytes`) alike. Base64 SHALL be padded with `=`
and, under `Base64FormattingOptions.InsertLineBreaks`, SHALL break its lines every 76 characters with
`\r\n` and none after the last line. `ToBase64CharArray` SHALL write the characters into the array it
is handed from its offset, leave the rest of it as it was, and answer their count. A null array, a
negative length or offset, a range past an array's end and an option .NET does not define SHALL throw
.NET's exception with its message, in the order .NET checks them. A span that is not a collection
expression SHALL fail the build.

#### Scenario: An array's base64 and hex

- **WHEN** `Convert.ToBase64String(new byte[] { 1, 2, 255 })` and
  `Convert.ToHexString(new byte[] { 1, 2, 255, 16 }, 1, 2)` run
- **THEN** they answer `AQL/` and `02FF`, as in .NET, where both were `String()` of the array

#### Scenario: Line breaks

- **WHEN** fifty-eight bytes are written with `Base64FormattingOptions.InsertLineBreaks`
- **THEN** the text is one line of 76 characters, `\r\n` and four more, as in .NET, and fifty-seven
  bytes are one line with no break

#### Scenario: A range past the end

- **WHEN** `Convert.ToBase64String(new byte[3], 2, 2)` runs
- **THEN** it throws .NET's `ArgumentOutOfRangeException` with the message `offset ('2') must be less
  than or equal to '1'. (Parameter 'offset')`, the actual value on the next line

### Requirement: A time type's TryParse answers as .NET's

`TryParse(string, out T)` of a `DateTime`, a `DateTimeOffset`, a `DateOnly`, a `TimeOnly` and a
`TimeSpan` SHALL answer true and write the value its `Parse` reads through the out argument, or answer
false and write the type's default there for a text its `Parse` refuses and for a null one, and SHALL
never throw. The out argument SHALL be any place C# can write, a discard, a variable declared before
and an array's element included, and the arguments SHALL be evaluated in the order written.
`TimeOnly.Parse` SHALL read the whole text in the culture the call names (`runtime-dates`): an hour
past 23, a minute past 59 and a second past 59 refused, an AM or PM designator before or after the
time in either case, `12` read as midnight under AM, an hour past 12 refused under AM and kept under
PM, an hour alone only beside a designator, and a fraction only after the seconds, rounded to the tick
as .NET rounds it. `TimeOnly.Parse` and `DateOnly.Parse` SHALL refuse a text in .NET's words. The
overloads of the five's `TryParse` with a provider, with styles or with both SHALL read as
`runtime-dates` says, and one over a span SHALL fail the build (EQ1004).

#### Scenario: A text that is a date

- **WHEN** `DateTime.TryParse("2024-01-15", out var d)` runs
- **THEN** it answers true with that date in `d`, as in .NET, where the page failed with a TypeError

#### Scenario: A text that is no date

- **WHEN** `DateOnly.TryParse("abc", out var d)` runs
- **THEN** it answers false with `DateOnly.MinValue` in `d`, as in .NET, where it answered null

#### Scenario: A clock that does not exist

- **WHEN** `TimeOnly.TryParse("25:00", out var t)` runs
- **THEN** it answers false, as in .NET, where `Parse` read a day and an hour

#### Scenario: A time with a designator

- **WHEN** `TimeOnly.Parse("9:30 PM")` and `TimeOnly.TryParse("13:00 AM", out var t)` run
- **THEN** the first answers 21:30 and the second false, as in .NET, where the first was half past
  nine in the morning

#### Scenario: An overload with a provider

- **WHEN** a page calls `DateTime.TryParse(text, null, out var when)`
- **THEN** it reads the text in the page's culture, as .NET reads the current one, where the build
  failed (EQ1004)

#### Scenario: An overload over a span

- **WHEN** a page calls `TimeSpan.TryParse(text.AsSpan(), out var span)`
- **THEN** the build fails (EQ1004)

### Requirement: An interpolation hole's alignment is the number it is

The alignment of an interpolation hole SHALL be written as the value of the constant expression C#
takes there, whatever the source spells it with: a literal, a constant by its name, a negated one, a
sum. The hole SHALL be padded to it as .NET pads it, in a plain interpolated string and in one handed
to `StringBuilder.Append` or `AppendLine`.

#### Scenario: A constant by its name

- **WHEN** `const int Name = 6;` and `string name = "ab";` are written as `$"{name,Widths.Name}|"`
- **THEN** it is `    ab|`, as in .NET

#### Scenario: A negated constant and a sum

- **WHEN** the same two are written as `$"{name,-Widths.Name}|"` and `$"{name,Widths.Name + 2}|"`
- **THEN** they are `ab    |` and `      ab|`, as in .NET

#### Scenario: An interpolated string handed to a builder

- **WHEN** `new StringBuilder().Append($"{name,Widths.Name}|").ToString()` is read
- **THEN** it is `    ab|`, as in .NET

### Requirement: A text changes case as .NET changes it

`ToUpper` and `ToLower` and their invariant pair, of a `string` and of a `char`, SHALL change each
UTF-16 code unit, or surrogate pair, to exactly one other, by .NET 10's simple case mappings, so a
text's length never changes: a letter whose full mapping lengthens SHALL keep its case, but for the
Greek letters with an iota subscript, which SHALL upper-case to the capital with a prosgegrammeni; a
capital sigma SHALL lower-case to the medial one wherever it stands; a lone surrogate SHALL stay as it
is; and a letter Unicode 16 or 17 cased SHALL change case as .NET 10 changes it over ICU 78 (Unicode 17),
Ubuntu 26.04's, and over macOS 26's ICU, on every engine, one whose Unicode is older included, where a
server whose ICU is older, Ubuntu 24.04's or Windows' own, leaves it as it is. The two
i's SHALL be the culture's: tr and az, by their language, SHALL upper-case `i` to `İ` and lower-case
`I` to `ı`, every other culture SHALL upper-case `ı` to `I` and lower-case `İ` to `i`, and the
invariant culture SHALL leave `İ` and `ı` as they are. No culture, `CultureInfo.CurrentCulture` and a
null SHALL read the page's culture, which is the current one, and `CultureInfo.InvariantCulture` and
the invariant pair the invariant one; any other culture SHALL fail the build (EQ2108). A char's culture
handed as null SHALL throw .NET's ArgumentNullException after the char is evaluated, and a char's
casing with a culture taken as a delegate SHALL fail the build as its call with a culture it cannot
name does (EQ2108).

#### Scenario: A letter whose full upper case lengthens

- **WHEN** `"straße".ToUpper()` runs
- **THEN** it answers `STRAßE`, as in .NET, where it answered `STRASSE`

#### Scenario: A letter Unicode 16 cased

- **WHEN** `"ƛ".ToUpper()` runs
- **THEN** it answers `Ƛ`, as .NET 10 does over ICU 78, where it answered `ƛ`, on an engine whose own
  Unicode leaves `ƛ` as it is too

#### Scenario: A final sigma

- **WHEN** `"ΟΔΟΣ".ToLower()` runs
- **THEN** it answers `οδοσ`, as in .NET, where it answered `οδος`

#### Scenario: The i of a Turkish page

- **WHEN** `"i".ToUpper()` runs in tr-TR
- **THEN** it answers `İ`, as in .NET, where it answered `I`

#### Scenario: The invariant culture keeps the dotless i

- **WHEN** `"ı".ToUpperInvariant()` runs
- **THEN** it answers `ı`, as in .NET, where it answered `I`

#### Scenario: A char's null culture

- **WHEN** `char.ToUpper('a', null)` runs
- **THEN** it throws ArgumentNullException for `culture`, as in .NET, where it answered `A`

#### Scenario: A culture the browser does not know

- **WHEN** `text.ToUpper(CultureInfo.GetCultureInfo("tr-TR"))` is built
- **THEN** the build fails with EQ2108, where the culture was dropped

#### Scenario: A char's casing with a culture, as a delegate

- **WHEN** `new Func<char, CultureInfo, char>(char.ToUpper)` is built
- **THEN** the build fails with EQ2108, as the call with a culture it cannot name does

### Requirement: A member a collection's twin does not carry fails the build

eqc SHALL lower a member of `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `LinkedListNode<T>` or
`SortedSet<T>` to the runtime twin's member only when the twin carries it, and SHALL refuse any other
member of the five with EQ2004, naming the member; it used to go out under its camel-cased name and
fail in the browser. `ToString`, `Equals` and `GetHashCode` SHALL be translated as every object's. A
member reached as a method group SHALL keep its receiver, and a take reached as one SHALL be refused.
A sorted set's `GetViewBetween` SHALL be the twin's.

#### Scenario: A sorted set's comparer

- **WHEN** a component reads `s.Comparer` on a `SortedSet<int>`
- **THEN** the build fails with EQ2004

#### Scenario: A view of a sorted set

- **WHEN** a component calls `s.GetViewBetween(1, 2)` on a `SortedSet<int>`
- **THEN** the lowering is `s.getViewBetween(1, 2)`, where the build failed with EQ2004

#### Scenario: A method group

- **WHEN** a component passes `q.Enqueue` to `List<int>.ForEach`
- **THEN** it is the delegate `q.Enqueue(item)` is, made over `q`, and each element is enqueued on `q`

### Requirement: A .NET property or field nothing carries fails the build

A read of a property or a field of a .NET type that no translation answers SHALL fail the build with
EQ2004, naming the member, unless a value the browser holds carries the member by its own name: a
string's, an array's and a span's `Length`, the members an exception's twin carries, a decimal's
`Scale`, and a `KeyValuePair`'s `Key` and `Value`. A member an interface declares SHALL NOT be
refused, since the value read through the interface may be a type of the app's own. A chain of such
reads SHALL be refused once, at its first member, whether it is read through `.` or through `?.`,
and a read made on a value a translation answered SHALL be asked as any other. A culture handed to a
call that reads it from how the call names it SHALL NOT be read as a value.

#### Scenario: A culture's property

- **WHEN** a component reads `CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator`
- **THEN** the build fails with one EQ2004 naming `System.Globalization.CultureInfo.CurrentCulture`;
  in the browser `CultureInfo` was not defined

#### Scenario: A chain through `?.`

- **WHEN** a component reads `error?.TargetSite?.Name`, where `error` is an `Exception`
- **THEN** the build fails with one EQ2004 naming `System.Exception.TargetSite`

#### Scenario: A member the runtime does not carry

- **WHEN** a component reads `Environment.NewLine`, `typeof(string).Name` or `new List<int>().Capacity`
- **THEN** the build fails with EQ2004 naming the member; each read undefined in the browser

#### Scenario: A member the browser carries

- **WHEN** a component reads `"ab".Length`, an exception's `ParamName`, a dictionary pair's `Key` or
  an `IGrouping`'s `Key`, or calls `char.ToUpper('i', CultureInfo.InvariantCulture)`
- **THEN** the build reports nothing

#### Scenario: A decimal's scale

- **WHEN** browser-side code reads `(1m / 3m).Scale` and `100.50m.Scale`
- **THEN** they are 28 and 2, as in .NET

### Requirement: Object.Equals and an inherited Equals answer as .NET's

`object.Equals(a, b)`, `Object.Equals(a, b)`, `System.Object.Equals(a, b)` and bare `Equals(a, b)` SHALL
answer what .NET's `object.Equals(object, object)` answers: true when both are null or the same
reference, false when one is null, and otherwise the first parameter's own `Equals(object)`, whatever the
second operand is and in whichever order the call names the two, evaluating both arguments as written, and
SHALL find a value whose type refuses every other (a number, a char, a bool, an enum, a tuple, a struct
that does not override `Equals`, a string, an anonymous object) unequal to one whose type is known to be
another, a byte and an int or a char and a string included. An instance `Equals(object)` inherited from
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

### Requirement: A package's type has no name in the browser

A static member of a type of an assembly the build has no source for, a package's, of any kind of type,
an interface's included, and whatever its name, `Count` included, read qualified, through an alias named
like any other type, read or called through `using static`, called qualified, or taken as a method group, and a construction of
such a type, SHALL fail the build with EQ2004 naming the member or the type and the assembly it comes
from, and what is read or called from one SHALL be refused with it, told once at its root. A value of
such a type SHALL keep reading its members by name, as the data the server sends, and a constant and an
enum's member SHALL be their values.

#### Scenario: A static through an alias named like a platform type

- **WHEN** a page reads `Int32.MaxValue` under `using Int32 = Vendor.Status`, `DateTimeOffset.Now` under
  `using DateTimeOffset = Vendor.DateTime`, or `String.Empty` under `using String = Vendor.Status`
- **THEN** the build fails with EQ2004 naming the package's member and `Vendor`, where a strategy that
  claims the platform type by its spelling wrote its member: `Int32.MaxValue` was 2147483647

#### Scenario: A static read of a package's class

- **WHEN** a page of an app that references the package `Vendor` writes `Text(Status.Label, …)`, where
  `Vendor.Status` is a class of that package
- **THEN** the build fails with EQ2004 naming `Vendor.Status.Label` and `Vendor`, and writes no module
  naming `Status`

#### Scenario: An eQuantic package the SDK does not ship

- **WHEN** a page reads `Clock.Zone`, calls `Clock.Now()` or constructs `new Clock("a")`, where
  `eQuantic.Core.Timing.Clock` is a class of the package `eQuantic.Core`
- **THEN** the build fails with EQ2004 naming the member and `eQuantic.Core`: the SDK ships twins of its
  own assemblies and of what the runtime provides, never of a namespace that merely starts like its
  own

#### Scenario: A construction of a package's class

- **WHEN** a page writes `new Gauge("a").Name`, `new Money(1m, "EUR")` or a target-typed `new("t")` of
  `Vendor.Gauge`
- **THEN** the build fails with EQ2004 naming the type and `Vendor`

#### Scenario: A chain from a package's static or construction

- **WHEN** a page writes `Status.Make().GetType().Name`, `Status.Culture().Name` or
  `new Gauge("a").GetType().Name`, where `Status.Culture()` answers a `CultureInfo`
- **THEN** the build fails with one EQ2004, naming `Vendor.Status.Make`, `Vendor.Status.Culture` or
  `Vendor.Gauge` and `Vendor`, where `object.GetType` and `CultureInfo.Name` were told again

#### Scenario: What names no class

- **WHEN** a page reads `Status.Answer`, a constant, `Level.High`, an enum's member, or `nameof(Gauge)`
- **THEN** the build succeeds and the module holds their values

### Requirement: A construction of a class the browser has no twin of fails the build

A construction of a .NET type that no translation builds SHALL fail the build with EQ2004 naming the
type, a target-typed one included, where the module would have constructed a class nothing defines, and
what is read or called from it SHALL be refused with it, told once at the construction. A collection the
browser holds as the array of its elements is built as one.

#### Scenario: A random source and a stopwatch

- **WHEN** a component writes `new Random()`, `Random r = new()`, `new Stopwatch()`, `new Lazy<int>(…)` or
  `new Tuple<int, int>(1, 2)`
- **THEN** the build fails with EQ2004 naming `System.Random`, `System.Diagnostics.Stopwatch`,
  `System.Lazy<int>` or `System.Tuple<int, int>`

#### Scenario: A chain from a construction

- **WHEN** a component writes `new Stopwatch().Elapsed` or `new Random().GetType()`
- **THEN** the build fails with one EQ2004, naming `System.Diagnostics.Stopwatch` or `System.Random`,
  where `Stopwatch.Elapsed` and `object.GetType` were told again

### Requirement: An interpolation is written in the culture a call names

`FormattableString.Invariant($"…")` and `string.Create(CultureInfo.InvariantCulture, $"…")` SHALL write
every hole of the interpolation in the invariant culture, and `FormattableString.CurrentCulture($"…")` and
`string.Create` with the current culture or a null provider in the page's, as .NET writes them, a hole's
format and alignment included. A hole that is itself text, an interpolation or a `ToString()`, SHALL be
the text it wrote in the page's culture, and a hole held as an object or as dynamic SHALL write the number
or the date it holds in the culture the call names. A `FormattableString` SHALL evaluate every hole
before it writes any, as it holds its arguments, where `string.Create` writes each hole as it is
evaluated. Another culture SHALL fail the build with EQ2108, and an interpolation made a
`FormattableString` or an `IFormattable` anywhere else SHALL fail the build with EQ2004.

#### Scenario: A hole whose evaluation changes an earlier one

- **WHEN** browser-side code writes `FormattableString.Invariant($"{sb}|{sb.Append("b")}")` and
  `string.Create(CultureInfo.InvariantCulture, $"{sb}|{sb.Append("b")}")` over a builder holding "a"
- **THEN** they are `ab|ab` and `a|ab`, as in .NET

#### Scenario: The invariant culture on a pt-BR page

- **WHEN** browser-side code in pt-BR writes `FormattableString.Invariant($"{1234.5}|{-3}|{d}|{x:N2}")`
- **THEN** it is `1234.5|-3|01/15/2024 21:30:05|1,234.50`, as in .NET

#### Scenario: A FormattableString made elsewhere

- **WHEN** a component writes `FormattableString text = $"{x}";`
- **THEN** the build fails with EQ2004 naming `System.FormattableString`

### Requirement: A nullable value's Equals and the default comparer answer as the lifted equality does

`Equals` called on a nullable struct, `Nullable.Equals(a, b)` and `EqualityComparer<T>.Default.Equals(x, y)`
SHALL build and SHALL answer as .NET's: two empty values equal, an empty value unequal to any value, and two
values by their type's own equality, its `Equals(object)` for `Equals` and its `IEquatable<T>` before that
for the other two, never its `==`. The default comparer's `Equals` SHALL answer so over any type, a number,
a string and a record included. A value's own equality that is the app's code, a struct's or a class's
`Equals(object)`, declared or inherited from the app's base, and an `IEquatable<T>` its type answers, SHALL be
asked every time two present values are compared, a value compared with itself included, as .NET asks it with
no identity check, whether the call names the value's type, a base class the value's class extends, `object`,
an interface or a type parameter of generic code, and `Equals` on a struct value that is not nullable SHALL ask
it with a null argument too. A record's synthesized `Equals` SHALL answer a record compared with itself at the
reference check C# writes into it, before it compares a member, wherever the comparer reaches it. A class with
neither SHALL compare by reference. Both values SHALL be evaluated once, in the order the call writes
them. The comparer's `Default` read on its own SHALL still fail
the build with EQ2004, since no value the browser holds carries it, and only `EqualityComparer<T>`'s own
`Default` SHALL be taken for the default comparer: a call made on a static property of the app's named
`Default` SHALL still fail the build with EQ2004.

#### Scenario: Two nullable record structs

- **WHEN** `a.Equals(b)`, `Nullable.Equals(a, b)` and `EqualityComparer<Spot?>.Default.Equals(a, b)` run for
  `Spot? a = new Spot(1, 2), b = new Spot(1, 2)`, for two empty values, and for `Spot? a = new Spot(1, 2)`
  beside an empty `b`, where `record struct Spot(int Row, int Col)`
- **THEN** they answer True for the first two pairs and False for the third, as in .NET, where the build
  refused all three with EQ2004

#### Scenario: A struct's Equals is not its ==

- **WHEN** `struct Reach` declares `==` over `Start` alone, and `a.Equals(b)` and `Nullable.Equals(a, b)` run
  for `Reach? a = new Reach(1, 5), b = new Reach(1, 9)`
- **THEN** both answer False, as in .NET, where `a == b` answers True

#### Scenario: A struct's own Equals(object)

- **WHEN** `struct Loose` overrides `Equals(object)` to take another `Loose` by its last digit and an `int` by
  its value, and `a.Equals(b)`, `a.Equals((object)3)` and `Nullable.Equals(a, b)` run for
  `Loose? a = new Loose(3), b = new Loose(13)`
- **THEN** each answers True, as in .NET

#### Scenario: In the order written

- **WHEN** `Nullable.Equals(n2: At(2), n1: At(1))` runs, where `At` logs its argument and answers a `Spot?`
- **THEN** the log reads `21`, as in .NET

#### Scenario: The default comparer over any type

- **WHEN** `EqualityComparer<int>.Default.Equals(1, 1)`, `EqualityComparer<string>.Default.Equals("a", "b")`
  and `EqualityComparer<double>.Default.Equals(double.NaN, double.NaN)` run
- **THEN** they answer True, False and True, as in .NET

#### Scenario: The comparer read alone

- **WHEN** a method reads `EqualityComparer<Spot?>.Default` into a local
- **THEN** the build refuses it with EQ2004, naming `EqualityComparer<Spot?>.Default`

#### Scenario: A struct's own Equals asked every time

- **WHEN** `struct Counted` overrides `Equals(object)` to count its calls, and `a.Equals(a)`,
  `Nullable.Equals(a, a)` and `EqualityComparer<Counted?>.Default.Equals(a, a)` run for
  `Counted? a = new Counted(1)`, and `v.Equals(v)`, `v.Equals(null)` and
  `EqualityComparer<Counted>.Default.Equals(v, v)` for `Counted v = new Counted(1)`
- **THEN** each counts one call, as in .NET, where the browser answered the same value at its identity check
  and the null without asking; beside an empty nullable value the count stays 0

#### Scenario: A class's own Equals asked by the default comparer

- **WHEN** `class Counter` overrides `Equals(object)` to count its calls, and `EqualityComparer<Counter>.Default.Equals(c, c)`
  runs for one `Counter c`, and so does the same call through `object`, an interface, a class that inherits the
  override and generic code, `EqualityComparer<T>.Default.Equals(a, b)` with `T` a `Counter`, built as an app's
  build builds it
- **THEN** each answers True and counts one call, as in .NET, and an override that refuses itself answers False,
  where the browser answered the same value at its identity check and counted none

#### Scenario: A class with no override

- **WHEN** a class declares neither `Equals(object)` nor `IEquatable<T>`, and
  `EqualityComparer<PlainClass>.Default.Equals(p, p)` and `EqualityComparer<PlainClass>.Default.Equals(p, new PlainClass(1))`
  run
- **THEN** they answer True and False, by reference, as in .NET

#### Scenario: A subclass's override behind its base

- **WHEN** `class Square : Shape` overrides `Equals(object)` to count its calls, where `Shape` overrides nothing,
  and `EqualityComparer<Shape>.Default.Equals(s, s)` runs for one `Shape s = new Square(1)`, and so do the same
  call behind an abstract base and `EqualityComparer<BaseRecord>.Default.Equals(e, e)` over a derived record whose
  own typed `Equals` counts its calls
- **THEN** each answers True and counts one call, as in .NET's virtual call, and a subclass that refuses itself
  answers False, where the browser answered the same value at its identity check and counted none

#### Scenario: A record's synthesized Equals checks the reference

- **WHEN** `record Wrapped(TypedClass T)` holds a member whose typed `Equals` counts its calls, and
  `EqualityComparer<Wrapped>.Default.Equals(w, w)` runs for one `Wrapped w`, and so does the same call through
  `object`, an interface the record implements and generic code
- **THEN** each answers True and counts no call, as C#'s synthesized `Equals` answers at its reference check,
  where through `object`, the interface and generic code the browser compared the record's member; two records
  holding one member count one call

#### Scenario: An app's own Default property

- **WHEN** a static class of the app's declares `EqualityComparer<int> Default`, whose getter throws, and a
  method calls `Custom.Default.Equals(1, 1)`
- **THEN** the build refuses it with EQ2004, as it did before, where the call was lowered and the getter
  dropped unread

### Requirement: Task.Delay waits as .NET waits

`Task.Delay` SHALL wait for a `TimeSpan` as for a number of milliseconds, a fraction of a millisecond
cut, `-1` and `Timeout.InfiniteTimeSpan` forever. It SHALL throw `ArgumentOutOfRangeException` before
anything else for a delay below -1, or past 4294967294 ms for a `TimeSpan`, with .NET's message and
parameter name (`millisecondsDelay`, `delay`). Given a token, the task SHALL end as cancelled, an
`await` of it throwing `TaskCanceledException`, as soon as the token is cancelled, and at once when it
already is.

#### Scenario: A TimeSpan

- **WHEN** `await Task.Delay(TimeSpan.FromMilliseconds(40))` is timed
- **THEN** it waits at least 40 ms, as in .NET

#### Scenario: A token cancelled during the wait

- **WHEN** `await Task.Delay(1000, source.Token)` runs and the source is cancelled after 30 ms
- **THEN** the await throws a `TaskCanceledException`, as in .NET

#### Scenario: A delay below -1

- **WHEN** `Task.Delay(-2)` is called
- **THEN** it throws an `ArgumentOutOfRangeException` for `millisecondsDelay`, with .NET's message

### Requirement: A number is read in the culture the call names

`Parse` and `TryParse` of a `decimal`, a `double`, a `float` and the eight integer types, and
`Convert.ToDecimal`, `Convert.ToDouble`, `Convert.ToSingle` and the integer conversions of a text or of
a value that may hold one, SHALL read the text as .NET 10 reads it in the culture the call names: with
no provider, with `CultureInfo.CurrentCulture` and with a null one, a null behind a cast included, the
page's culture, which is the current one; with `CultureInfo.InvariantCulture`, the invariant one. The
culture SHALL give the signs, the decimal separator and the thousands separator, and, where the style
allows a currency symbol, the currency's symbol and its own separators, the number's being read too
until the symbol is met; a no-break space in a culture's symbol SHALL also be read as a space, a culture
whose minus is one of the dashes CLDR names SHALL also read a hyphen as its minus, and a real's words
for infinity and for what is not a number SHALL be the culture's. Any other provider SHALL fail the
build (EQ2108). The build SHALL no longer warn about a number read with no culture.

#### Scenario: A decimal comma on a Portuguese page

- **WHEN** `decimal.TryParse("7,01", NumberStyles.Float, CultureInfo.CurrentCulture, out var d)` runs in
  pt-BR
- **THEN** it answers true and 7.01, as in .NET, where the build refused the current culture (EQ2108)

#### Scenario: No provider reads the page's culture

- **WHEN** `double.Parse("1,5")` runs in de-DE
- **THEN** it answers 1.5, as in .NET, where the browser read the invariant culture and answered 15

#### Scenario: A thousands separator typed as a space

- **WHEN** `decimal.Parse("1 000,5")` runs in fr-FR, whose separator is U+202F
- **THEN** it answers 1000.5, as in .NET

#### Scenario: A hyphen where the culture's minus is U+2212

- **WHEN** `int.Parse("-5")` and `int.Parse("−5")` run in sv-SE
- **THEN** both answer -5, as in .NET

#### Scenario: The invariant culture on any page

- **WHEN** `decimal.Parse("7.01", CultureInfo.InvariantCulture)` runs in pt-BR
- **THEN** it answers 7.01, as in .NET

#### Scenario: A null provider behind a cast

- **WHEN** `double.Parse("2.5", (IFormatProvider?)null)` is built
- **THEN** it builds and reads the page's culture, as a bare null does, where the build refused it
  (EQ2108)

#### Scenario: Another culture

- **WHEN** `decimal.Parse(text, CultureInfo.GetCultureInfo("pt-BR"))` is built
- **THEN** the build fails with EQ2108

### Requirement: A list adds and inserts a range of any size as .NET's does

`List<T>.AddRange(collection)` and `InsertRange(index, collection)` SHALL take a collection of any size,
adding its elements in the order it enumerates, a string's UTF-16 chars as its elements, and SHALL never
hand the elements to one call as its arguments, which every engine caps. A null collection SHALL be refused
with an ArgumentNullException for `collection`, before the index is checked, and an index below zero or past
the count SHALL be refused with an ArgumentOutOfRangeException for `index`. A list that is its own range SHALL
add or insert what it held before the call, and each argument SHALL be evaluated once, in the order it is
written.

#### Scenario: A million elements

- **WHEN** `list.AddRange(items)` runs for a list of one element and an array of a million, and
  `InsertRange(1, items)` for a list of two
- **THEN** the lists hold 1,000,001 and 1,000,002 elements in .NET's order; both threw a RangeError in the
  browser

#### Scenario: An index outside the list

- **WHEN** `new List<int> { 1 }.InsertRange(2, new[] { 9 })` runs
- **THEN** it throws "Index was out of range. Must be non-negative and less than or equal to the size of the
  collection. (Parameter 'index')", as in .NET; it inserted 9 at the end

#### Scenario: Named arguments out of order

- **WHEN** `L().InsertRange(collection: C(), index: I())` runs, each call recording its letter
- **THEN** the letters are "lci" and the collection is inserted at the index, as in .NET; the index was
  spread as the collection

### Requirement: A list inserts, removes and takes at a position as .NET's does

`List<T>.Insert(index, item)`, `RemoveAt(index)`, `RemoveRange(index, count)` and `GetRange(index, count)`,
and `IList<T>.Insert` and `RemoveAt`, SHALL take each argument in its parameter's place, evaluated once in
the order it is written, and SHALL refuse a position .NET refuses with its exception and its words: an
`Insert` index below zero or past the count with an ArgumentOutOfRangeException for `index` ("Index must
be within the bounds of the List."), a `RemoveAt` index below zero or at the count or past it with one for
`index`, and for `RemoveRange` and `GetRange` a negative index or count with one for that parameter and a
range past the end with an ArgumentException. A null list SHALL throw a NullReferenceException before its
arguments are checked.

#### Scenario: Named arguments

- **WHEN** `l.Insert(item: 9, index: 0)` runs over `{ 1, 2 }`
- **THEN** the list holds `9,1,2`, as in .NET; it held `1,2,0`

#### Scenario: A position past the end

- **WHEN** `l.RemoveAt(5)` runs over `{ 1, 2, 3 }`, or `l.GetRange(0, 5)` over `{ 1 }`
- **THEN** the first throws "Index was out of range. Must be non-negative and less than the size of the
  collection. (Parameter 'index')" and the second "Offset and length were out of bounds for the array or
  count is greater than the number of elements from index to the end of the source collection.", as in
  .NET; the first removed nothing and the second answered a list of one

#### Scenario: An index read once

- **WHEN** `l.GetRange(i++, 2)` runs over `{ 1, 2, 3 }` with `i` at 0
- **THEN** it answers `1,2` and `i` is 1, as in .NET; `i` was read twice

### Requirement: Convert to an integer rounds and checks as the overload C# bound does

`Convert.To<integer>` of a value that is not text SHALL answer what the overload C# bound answers: a double
or a float rounded half to even, a decimal by `decimal.Round`, a char as its code unit, a bool as 1 or 0, an
integer as itself, each inside the target's range or refused with .NET's OverflowException in its words, a
`long` or a `ulong` target held as a BigInt. An `object`, a nullable and an enum, which C# boxes for the
`object` overload, SHALL be read by what they hold when the call runs: a null as 0, a text as the type's
`Parse` reads it in the culture the call names, a number as the type it holds. `Convert.ToDouble`, `ToSingle`,
`ToBoolean` and `ToChar` of an `object` SHALL read it the same way. A char held in an `object` is read as a
text of one character, since a boxed value does not carry its type.

#### Scenario: Out of range

- **WHEN** `Convert.ToByte(300)` runs
- **THEN** it throws "Value was either too large or too small for an unsigned byte.", as .NET does; it
  answered 300

#### Scenario: A narrow width of a double

- **WHEN** `Convert.ToInt16(1e10)` runs
- **THEN** it throws "Value was either too large or too small for an Int32.", as .NET does, whose overload
  goes through `ToInt32(double)`

#### Scenario: A long

- **WHEN** `Convert.ToInt64(2.5) + 1L` runs
- **THEN** it answers 3, as .NET does; it threw a TypeError for mixing a number with a BigInt

#### Scenario: A decimal and a char

- **WHEN** `Convert.ToInt32(3.5m)` and `Convert.ToInt32('A')` run
- **THEN** they answer 4 and 65, as .NET does; they answered the Decimal 3.5 and the text "A"

#### Scenario: A text held in an object

- **WHEN** `object o = "1,5"; Convert.ToInt32(o, CultureInfo.InvariantCulture)` runs
- **THEN** it throws "The input string '1,5' was not in a correct format.", as .NET does

### Requirement: A conversion reads the type in sight

A `Convert` call whose value C# boxed for the `object` overload SHALL convert by the value's type where the call still shows it: an enum by its value into every target, and a `Nullable` by its value's type, a null being what the `object` overload answers. A checked cast into an enum SHALL throw past its underlying type's range.

#### Scenario: An enum into a real number

- **WHEN** a program evaluates `Convert.ToDouble(Tone.Warm)`, where `Warm` is 1
- **THEN** the answer is 1, as in .NET

#### Scenario: A nullable char

- **WHEN** a program evaluates `Convert.ToInt32(c)` over a `char?` holding `'5'`
- **THEN** the answer is 53, its code unit, and 0 for a null

#### Scenario: A checked cast into an enum

- **WHEN** a program evaluates `checked((Small)i)` over an int of 300, where `Small` is a byte enum
- **THEN** the cast throws `OverflowException`, as in .NET
