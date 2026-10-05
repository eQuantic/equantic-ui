# transpiler-bcl Specification

## Purpose
What a BCL member answers in the browser, which is what it answers in .NET.

## Requirements

### Requirement: List.Remove answers as .NET does

`list.Remove(item)` SHALL remove the first item `EqualityComparer<T>.Default` finds equal to the value
and answer whether it found one, evaluating the list and the item once each, in that order. A value
tuple, a record and a struct SHALL compare by value, through the same equality `Contains` uses, and so
SHALL a nullable one of those and an anonymous type. Through
`ICollection<T>`, a HashSet or a LinkedList held when the call runs SHALL remove as it does when
called directly, and so SHALL a dictionary through `ICollection<KeyValuePair<K, V>>`: the pair
leaves only when its key is there with an equal value.

#### Scenario: A present item and a missing one

- **WHEN** `var list = new List<int> { 0, 1, 2 };` runs `list.Remove(1)` and then `list.Remove(5)`
- **THEN** the calls answer true and false, and the list is `0, 2`

#### Scenario: Equality by the type's rule

- **WHEN** a `List<Point>` of records removes `new Point(3, 4)`, and a `List<double>` removes `double.NaN`
- **THEN** each removes its equal item and answers true

#### Scenario: A tuple element by element

- **WHEN** a `List<(int, string)>` holding `(1, "a")` and `(2, "b")` removes `(2, "b")`
- **THEN** it answers true, and one item is left

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

`IndexOf` and `LastIndexOf` given a start, or a start and a count, with a `StringComparison` SHALL
search only that range, and SHALL throw .NET's `ArgumentOutOfRangeException`, with its message, for a
start or a count outside the string. `LastIndexOf` SHALL read a start one past the end as the last
unit. A null value SHALL throw `ArgumentNullException`.

#### Scenario: The count ends the search

- **WHEN** `"abcb".IndexOf("B", 2, 1, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers -1

#### Scenario: A start past the end

- **WHEN** `"abc".IndexOf("b", 4, StringComparison.Ordinal)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')`

#### Scenario: One past the end steps back

- **WHEN** `"abc".LastIndexOf("b", 3, StringComparison.OrdinalIgnoreCase)` runs
- **THEN** it answers 1

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
