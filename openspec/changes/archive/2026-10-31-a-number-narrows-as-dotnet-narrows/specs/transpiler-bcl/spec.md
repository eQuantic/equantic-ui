## ADDED Requirements

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
