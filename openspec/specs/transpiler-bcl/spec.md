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
a culture comparison as a constant, and an overload that takes a `CultureInfo`, SHALL fail the build
with EQ1004. One given a culture comparison in a variable SHALL throw at run time with a message that
names the method and the comparison, after every check .NET makes before it searches. `Equals` by a
culture comparison SHALL compare the two whole strings by the culture's collator.

#### Scenario: A constant culture comparison

- **WHEN** a component calls `a.StartsWith(b, StringComparison.CurrentCulture)`
- **THEN** the build fails with EQ1004 at that call

#### Scenario: A whole-string culture comparison

- **WHEN** `"\u00ADab".Equals("ab", StringComparison.InvariantCulture)` runs
- **THEN** it answers true
