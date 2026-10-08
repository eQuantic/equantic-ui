# Spec Delta

## ADDED Requirements

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
