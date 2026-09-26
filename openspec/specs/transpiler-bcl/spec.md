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

A number formatted with a standard specifier (`E`, `F`, `N`, `P`, `C`, `G`, `R`, `D`, `X`) or a custom
picture SHALL print the text .NET prints for it: from the number's exact value, a double's binary value
included, with an exact half rounded to even for a double and a float and away from zero for a
decimal and an integer. `E` SHALL write the mantissa's digits, the exponent's sign and an exponent of
at least three digits.

#### Scenario: The E specifier

- **WHEN** `(12345.0).ToString("E2")` and `string.Format("{0:E2}", 12345.678)` run
- **THEN** they print `1.23E+004` and `1.23E+004`

#### Scenario: A double's exact value

- **WHEN** `(0.1).ToString("F20")` runs
- **THEN** it prints `0.10000000000000000555`

#### Scenario: A half by the type

- **WHEN** `(1.25).ToString("E1")` and `125.ToString("E1")` run
- **THEN** they print `1.2E+000` and `1.3E+002`
