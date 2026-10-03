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
