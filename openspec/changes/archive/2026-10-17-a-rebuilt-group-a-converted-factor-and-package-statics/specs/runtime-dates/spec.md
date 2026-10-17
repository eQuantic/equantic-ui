## ADDED Requirements

### Requirement: A date beside an instant is converted to one

An operator between a `DateTimeOffset` and a `DateTime`, on either side, SHALL convert the `DateTime`
to a `DateTimeOffset` as C#'s implicit conversion does, at the offset its kind gives, and SHALL answer
as the instant's operator: a difference SHALL be a span, and `==`, `!=`, `<`, `<=`, `>` and `>=`
SHALL compare the two instants. A nullable date SHALL be converted as C#'s lifted conversion does: a
null one SHALL stay null, the operand read once, and an instant's operator over it SHALL answer as the
lifted operator does.

#### Scenario: A difference and a comparison across the two types

- **WHEN** browser-side code holds `a = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)` and
  `b = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)` and writes `a - b`, `b - a`, `a < b`,
  `a == b`, `a > b` and `a == new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)`
- **THEN** each answers as in .NET, where the pair matched no operator and JavaScript's own ran

#### Scenario: A nullable date as an instant

- **WHEN** browser-side code assigns a null and a present `DateTime?` to a `DateTimeOffset?`, and writes
  `-`, `==` and `<` between a `DateTimeOffset?` and a `DateTime?` with a null on either side
- **THEN** each answers as in .NET: the null date is a null instant, a difference with a null is null,
  and a comparison answers the lifted operator's bool, where the null was handed to the conversion and
  threw a TypeError

## MODIFIED Requirements

### Requirement: A span scales and divides as .NET's operators do

`TimeSpan.Multiply`, `Divide` and the operators `*` and `/` SHALL round the ticks of a product or a
quotient by a number to the even tick, as .NET's `Math.Round` does; a factor or a divisor that is NaN
SHALL be an ArgumentException naming it, and a result past the range a span holds an
OverflowException. A span divided by a span SHALL be the ratio of their ticks. A sign in front of a
span SHALL negate it or leave it, and the least span's negation and duration, and a sum or a difference
past the range, SHALL throw the OverflowException .NET throws. A factor or a divisor SHALL be any value
C# converts to a double, a long, a ulong, a char and a type with a conversion of its own included, on
either side of the product, and a nullable one SHALL be the lifted operator, null for a null factor:
the operator C# bound decides, never the operand's own type. `*=` and `/=` SHALL be the same operators,
a nullable span lifted and the target read once.

#### Scenario: A factor of another type, and in place

- **WHEN** browser-side code scales `TimeSpan.FromTicks(3)` by a long 2 and a ulong 2 on either side and
  divides it by them, scales `TimeSpan.FromTicks(2)` by a null and a 1.5 `double?` and by the char 'a',
  and runs `t *= 2.5` and `t /= 4` over 10 ticks
- **THEN** they are 6, 6, 2, 6, 6, 2 ticks, null, 3 ticks, 194 ticks, 25 and 6 ticks, as in .NET, where
  each was JavaScript's own operator

#### Scenario: Rounding to the even tick

- **WHEN** browser-side code divides spans of 5, 15, 25 and 35 ticks by 2, 2, 10 and 10
- **THEN** they are 2, 8, 2 and 4 ticks, as in .NET

#### Scenario: A NaN factor and an overflow

- **WHEN** browser-side code runs `TimeSpan.FromSeconds(1).Multiply(double.NaN)` and
  `TimeSpan.MaxValue.Multiply(2)`
- **THEN** the first throws ArgumentException "TimeSpan does not accept floating point Not-a-Number
  values. (Parameter 'factor')" and the second OverflowException "TimeSpan overflowed because the
  duration is too long.", as in .NET
