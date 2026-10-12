# Spec Delta

## ADDED Requirements

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
