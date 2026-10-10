# transpiler-vocabulary-values Specification

## Purpose
What a member of a vocabulary value type does in the browser, where the runtime ships the type's
twin and eqc only calls it: every member a component can use answers what it answers in .NET.

## Requirements

### Requirement: An instance member of a value the browser holds as data answers as in .NET

A call of a public instance method on a vocabulary value type the browser holds as plain data
SHALL answer in the browser what it answers in .NET, for every such method the type declares except
`GetHashCode`, which no type lowers yet (#519), and the build SHALL NOT emit the call as a method of
the value.

#### Scenario: WithOpacity

- **WHEN** a component's browser-side code evaluates `Color.FromRgb(0xF8, 0x71, 0x71).WithOpacity(0.8f)`
- **THEN** the result's channels are `R = 248, G = 113, B = 113, A = 204`, as in .NET

#### Scenario: MidpointWith

- **WHEN** it evaluates `Color.FromRgb(0, 0, 0).MidpointWith(Color.White)`
- **THEN** the result's channels are `R = 128, G = 128, B = 128, A = 255`, as in .NET

#### Scenario: Every method of every such type

- **WHEN** the coverage suite enumerates the public instance methods and properties of every
  vocabulary value type the browser holds as plain data, `GetHashCode` excepted
- **THEN** each one is executed on both sides, and the browser's answer equals .NET's

### Requirement: The text of a value the browser holds as data is .NET's

The text of such a value, from `ToString()`, an interpolation hole or a concatenation, SHALL be
the text .NET writes for it.

#### Scenario: ToString and an interpolation hole

- **WHEN** a component's browser-side code evaluates `Color.FromRgba(1, 2, 3, 4).ToString()` and
  `$"{Color.FromRgba(1, 2, 3, 4)}"`
- **THEN** both are `Color { R = 1, G = 2, B = 3, A = 4 }`

### Requirement: Only the vocabulary's type is the vocabulary's

A construction SHALL be lowered as a vocabulary value type's only when it constructs that type, by
symbol. An app's own type that shares the name SHALL be built as the app's type.

#### Scenario: The vocabulary's Color

- **WHEN** a component evaluates `new Color(1, 2, 3, 4)` with `eQuantic.UI.Primitives.Color` in scope
- **THEN** the value's channels are `R = 1, G = 2, B = 3, A = 4`, and its members answer as in the
  scenarios above

#### Scenario: An app's own Color

- **WHEN** an app declares `public sealed record Color(string Name, int Hue, int Light)` in its own
  namespace and a component evaluates `new Color("brand", 10, 50).Name`
- **THEN** the answer is `brand`, from the app's own record

### Requirement: A TypeStyle scales as .NET scales it

`TypeStyle.ScaledSize` and `TypeStyle.ScaledLineHeight` SHALL answer in the browser what they answer in
.NET: the factor clamped from 0.5 to the style's `MaxScale`, and the result snapped to the 0.5dp step,
half to even, in single precision.

#### Scenario: A style of 13 on 16.5, scaled

- **WHEN** a `TypeStyle` of size 13, line height 16.5 and `MaxScale` 1.3 is scaled by 0.2, 1.15,
  1.237 and 3
- **THEN** its size is 6.5, 15, 16 and 17, and its line height 8, 19, 20.5 and 21.5, as in .NET
